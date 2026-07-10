using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using Abbey.Rendering;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>What a buildable warrior structure does at night.</summary>
    public enum WarriorStructureRole
    {
        /// <summary>Recruits, houses and upgrades warriors (owns the upgrade tree).</summary>
        Lodge,

        /// <summary>Ranged support + vision that arms the dark-objective marker.</summary>
        Watchtower
    }

    /// <summary>
    /// A buildable warrior structure (P3-06): the <b>lodge</b> that recruits villagers
    /// into <see cref="WarriorAgent"/>s (population conservation — the villager is
    /// consumed) and owns the data-driven <see cref="WarriorUpgrades"/> tree (each
    /// purchased tier debits the Phase 3 ledger and raises every housed warrior's
    /// stats), and the <b>watchtower</b> that adds ranged support and the vision that
    /// arms tonight's <see cref="DarkObjectiveMarker"/>. All balance — capacity, stats,
    /// tier costs, tower range/damage — lives in <see cref="CombatConfig"/>; this
    /// component only orchestrates. Deterministic; manual <see cref="Tick"/> in tests.
    /// [ExecuteAlways] so the static registry works in EditMode.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WarriorStructure : MonoBehaviour
    {
        static readonly List<WarriorStructure> _active = new List<WarriorStructure>();

        /// <summary>Every enabled warrior structure (objective reveal, debug overlay).</summary>
        public static IReadOnlyList<WarriorStructure> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        [Tooltip("Lodge (recruit/house/upgrade) or Watchtower (ranged support + vision).")]
        public WarriorStructureRole role = WarriorStructureRole.Lodge;

        [Tooltip("Advance ranged support from Update (watchtower). Tests set false and call Tick().")]
        public bool autoTick = true;

        CombatConfig _combat;
        PrototypeConfig _proto;
        int _appliedTierCount;
        float _shotTimer;

        readonly List<WarriorAgent> _roster = new List<WarriorAgent>();

        public WarriorStructureRole Role => role;

        public CombatConfig Combat
        {
            get
            {
                if (_combat == null)
                {
                    _combat = CombatConfig.LoadOrDefault();
                }
                return _combat;
            }
            set { _combat = value; }
        }

        public PrototypeConfig Config
        {
            get
            {
                if (_proto == null)
                {
                    _proto = PrototypeConfig.LoadOrDefault();
                }
                return _proto;
            }
            set { _proto = value; }
        }

        /// <summary>Upgrades purchased so far (0 = base). Warriors here fight at this tier.</summary>
        public int AppliedTierCount => _appliedTierCount;

        /// <summary>Total tiers available in the tree.</summary>
        public int MaxTierCount => WarriorUpgrades.TierCount(Combat);

        /// <summary>The stats every warrior housed here currently fights with.</summary>
        public WarriorStats CurrentWarriorStats => WarriorUpgrades.StatsAtTier(Combat, _appliedTierCount);

        /// <summary>Warriors recruited and housed at this lodge.</summary>
        public IReadOnlyList<WarriorAgent> Roster => _roster;

        /// <summary>Warriors this lodge can house after the trust/arrivals multiplier.</summary>
        public int EffectiveCapacity
        {
            get
            {
                var cfg = Combat;
                return Mathf.Max(0,
                    Mathf.FloorToInt(cfg.warriorLodgeCapacity * Mathf.Clamp01(cfg.warriorRecruitTrustMultiplier)));
            }
        }

        void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        void OnDisable()
        {
            _active.Remove(this);
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Injects configs (scene/tests).</summary>
        public void Configure(CombatConfig combat, PrototypeConfig proto)
        {
            _combat = combat;
            _proto = proto;
        }

        /// <summary>
        /// Buys the next upgrade tier: debits its cost from the ledger and raises every
        /// housed warrior's stats. False (nothing spent) when the tree is maxed or the
        /// settlement cannot afford the next tier. Lodge only.
        /// </summary>
        public bool TryUpgrade()
        {
            if (role != WarriorStructureRole.Lodge || _appliedTierCount >= MaxTierCount)
            {
                return false;
            }
            if (!WarriorUpgrades.TryPurchaseTier(Combat, _appliedTierCount, null))
            {
                return false;
            }
            _appliedTierCount++;
            for (int i = 0; i < _roster.Count; i++)
            {
                if (_roster[i] != null)
                {
                    _roster[i].SetTier(_appliedTierCount);
                }
            }
            return true;
        }

        /// <summary>
        /// Promotes a villager into a warrior at this lodge (population conservation:
        /// the villager is consumed). Returns the new <see cref="WarriorAgent"/>, or
        /// null when the lodge is full / not a lodge. The warrior spawns at the lodge,
        /// garrisons here, and inherits the current upgrade tier.
        /// </summary>
        public WarriorAgent Recruit(VillagerAgent source)
        {
            if (role != WarriorStructureRole.Lodge || _roster.Count >= EffectiveCapacity)
            {
                return null;
            }

            var go = new GameObject($"Warrior_{_roster.Count}");
            go.transform.position = transform.position;
            var warrior = go.AddComponent<WarriorAgent>();
            warrior.autoTick = autoTick;
            warrior.Configure(Combat, Config);
            warrior.GarrisonPoint = transform.position;
            warrior.SetTier(_appliedTierCount);
            warrior.SetSource(source);
            _roster.Add(warrior);
            SpriteProjectionBootstrap.RegisterGlobal(
                go, "warrior", "actor.warrior", $"warrior:{name}:{_roster.Count - 1}");

            GameEventLog.Append("warrior_recruited",
                $"lodge={name} warrior={go.name} from={(source != null ? source.name : "levy")} tier={_appliedTierCount}");

            // Population conservation: the promoted villager leaves the settler pool.
            if (source != null)
            {
                GameEventLog.Append("warrior_promotion",
                    $"villager={source.name} -> {go.name}");
                source.gameObject.SetActive(false);
            }
            return warrior;
        }

        /// <summary>Deterministic simulation step (watchtower ranged support). autoTick = false in tests.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || role != WarriorStructureRole.Watchtower)
            {
                return;
            }
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            if (phase != DayPhase.Night && phase != DayPhase.Dusk)
            {
                _shotTimer = 0f;
                return;
            }

            _shotTimer -= dt;
            int guard = 0;
            while (_shotTimer <= 0f && guard++ < 16)
            {
                var target = NearestMonster(Combat.watchtowerRange);
                if (target == null)
                {
                    _shotTimer = 0f;
                    break;
                }
                FireSupport(target);
                _shotTimer += Combat.watchtowerShotIntervalSeconds;
            }
        }

        void FireSupport(MonsterController target)
        {
            var band = DarknessEvaluator.Classify(transform.position);
            var treatment = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, band, Combat);
            float damage = Combat.watchtowerShotDamage * treatment.DamageMultiplier;
            target.TakeDamage(damage);
            GameEventLog.Append("watchtower_shot",
                $"{name} -> {target.name} band={band} dmg={damage:F1}");
        }

        MonsterController NearestMonster(float range)
        {
            MonsterController nearest = null;
            float bestDist = float.MaxValue;
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null || !m.IsAlive)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, m.transform.position);
                if (dist <= range && dist < bestDist)
                {
                    bestDist = dist;
                    nearest = m;
                }
            }
            return nearest;
        }
    }
}
