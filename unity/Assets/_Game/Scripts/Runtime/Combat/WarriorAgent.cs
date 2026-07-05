using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Sanity;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>The professional-tier night unit's state (P3-06).</summary>
    public enum WarriorState
    {
        /// <summary>By day, idle at the lodge.</summary>
        Garrison,

        /// <summary>Walking back to the lodge (dawn / threat cleared).</summary>
        Returning,

        /// <summary>Mustered at dusk, holding a patrol point out in the dark.</summary>
        Mustering,

        /// <summary>Closing on / striking a monster.</summary>
        Engaging,

        /// <summary>Walking out to solve tonight's dark objective.</summary>
        SolvingObjective
    }

    /// <summary>
    /// A warrior (ROADMAP Phase 3 item 17, warrior half): a Dark-capable professional
    /// defender promoted from a villager at the <see cref="WarriorStructure"/> lodge
    /// (population conservation). Unlike settlers it leaves the light on purpose —
    /// musters at dusk, patrols/engages out in the dark, solves the nightly dark
    /// objective, and returns at dawn. It is NOT band-exempt (only the beast is): it
    /// fights through <see cref="LightBandCombatResolver"/> as a Friendly, so its Dark
    /// strikes are debuffed and it suffers the Dark sanity drain — only reduced by its
    /// trained <see cref="WarriorStats.DarkSanityDrainFraction"/> (which upgrades lower
    /// further). Every stat comes from <see cref="CombatConfig"/> via
    /// <see cref="WarriorUpgrades"/>; nothing is hard-coded here. Deterministic: no
    /// RNG, nearest-target selection, manual <see cref="Tick"/> in tests.
    /// [ExecuteAlways] so the static registry works in EditMode.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WarriorAgent : MonoBehaviour
    {
        static readonly List<WarriorAgent> _active = new List<WarriorAgent>();

        /// <summary>Every enabled, living warrior (debug overlay, closest-solver query).</summary>
        public static IReadOnlyList<WarriorAgent> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        CombatConfig _combat;
        PrototypeConfig _proto;
        SanitySystem _sanity;
        bool _initialized;
        int _tier;
        WarriorStats _stats;
        Vector3 _garrisonPoint;
        Vector3 _patrolPoint;
        float _attackCooldown;
        bool _nightAnnounced;

        public WarriorState State { get; private set; } = WarriorState.Garrison;

        /// <summary>Structural health (upgradable); the warrior dies at 0.</summary>
        public float Health { get; private set; }

        /// <summary>Sanity 0..1: drains while the warrior fights in the Dark band.</summary>
        public float Sanity { get; private set; } = 1f;

        /// <summary>Applied upgrade tier (0 = base). Set by the lodge.</summary>
        public int UpgradeTier => _tier;

        /// <summary>Effective stats at the current tier (from config).</summary>
        public WarriorStats Stats => _stats;

        public bool IsAlive => !_initialized || Health > 0f;

        /// <summary>The villager this warrior was promoted from (population conservation), or null.</summary>
        public VillagerAgent SourceVillager { get; private set; }

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
            set { _combat = value; ApplyStats(); }
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

        /// <summary>The sanity system (optional): warriors carry their own sanity but mirror the cost here when a source villager record exists.</summary>
        public SanitySystem Sanity_System
        {
            get
            {
                if (_sanity == null)
                {
                    _sanity = SanitySystem.Instance;
                }
                return _sanity;
            }
            set { _sanity = value; }
        }

        /// <summary>Where the warrior garrisons and returns to (its lodge).</summary>
        public Vector3 GarrisonPoint
        {
            get { return _garrisonPoint; }
            set
            {
                _garrisonPoint = value;
                RecomputePatrolPoint();
            }
        }

        void OnEnable()
        {
            EnsureInit();
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

        void EnsureInit()
        {
            if (_initialized)
            {
                return;
            }
            _garrisonPoint = transform.position;
            ApplyStats();
            Health = _stats.MaxHealth;
            _initialized = true;
            RecomputePatrolPoint();
        }

        /// <summary>Injects configs (lodge/tests) and resets health/stats to the tier values.</summary>
        public void Configure(CombatConfig combat, PrototypeConfig proto)
        {
            _combat = combat;
            _proto = proto;
            _initialized = false;
            EnsureInit();
        }

        /// <summary>Records the villager this warrior was promoted from and seeds sanity from its record.</summary>
        public void SetSource(VillagerAgent source)
        {
            SourceVillager = source;
            var sys = Sanity_System;
            if (source != null && sys != null && sys.TryGetRecord(source, out var record))
            {
                Sanity = record.Sanity;
            }
        }

        /// <summary>Sets the applied upgrade tier and recomputes stats (lodge upgrade path).</summary>
        public void SetTier(int tier)
        {
            _tier = Mathf.Max(0, tier);
            ApplyStats();
        }

        void ApplyStats()
        {
            var previousMax = _stats.MaxHealth;
            _stats = WarriorUpgrades.StatsAtTier(Combat, _tier);
            if (!_initialized)
            {
                return;
            }
            // Grant the health headroom an upgrade adds (keep current damage taken).
            float gained = _stats.MaxHealth - previousMax;
            if (gained > 0f)
            {
                Health = Mathf.Min(_stats.MaxHealth, Health + gained);
            }
            Health = Mathf.Min(Health, _stats.MaxHealth);
        }

        void RecomputePatrolPoint()
        {
            var cfg = Combat;
            Vector3 outward = _garrisonPoint.sqrMagnitude > 0.01f
                ? _garrisonPoint.normalized
                : Vector3.forward;
            _patrolPoint = _garrisonPoint + outward * cfg.warriorPatrolRadius;
            _patrolPoint.y = _garrisonPoint.y;
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (dt <= 0f || Health <= 0f)
            {
                return;
            }

            _attackCooldown -= dt;

            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            bool nightTime = phase == DayPhase.Dusk || phase == DayPhase.Night;

            if (!nightTime)
            {
                _nightAnnounced = false;
                TickReturnToGarrison(dt);
                return;
            }

            if (!_nightAnnounced)
            {
                _nightAnnounced = true;
                GameEventLog.Append("warrior_mustered", name);
            }

            // Dark is the warrior's element but not free: it pays the Dark-band sanity
            // drain (reduced by training), same rule the resolver applies to any friendly.
            var band = DarknessEvaluator.Classify(transform.position);
            if (band == LightZone.Dark)
            {
                float drain = Combat.darkFriendlySanityDrainPerSecond
                              * _stats.DarkSanityDrainFraction * dt;
                if (drain > 0f)
                {
                    Sanity = Mathf.Clamp01(Sanity - drain);
                    var sys = Sanity_System;
                    if (SourceVillager != null && sys != null)
                    {
                        sys.ApplySanityCost(SourceVillager, drain, "warrior night watch");
                    }
                }
            }

            // Priority 1: the nightly dark objective (only a dark-capable unit can solve
            // it), pursued by the single closest warrior so the rest keep fighting.
            if (TrySolveObjective(dt))
            {
                return;
            }

            // Priority 2: engage the nearest monster out in the dark.
            var monster = NearestMonster();
            if (monster != null)
            {
                SetState(WarriorState.Engaging);
                float dist = PlanarMotion.Distance(transform.position, monster.transform.position);
                if (dist <= _stats.AttackRange)
                {
                    TryAttack(monster);
                }
                else
                {
                    transform.position = PlanarMotion.StepAroundBuildings(
                        transform.position, monster.transform.position, _stats.MoveSpeed, dt,
                        _stats.AttackRange * 0.5f, Config.movementObstaclePadding, out _);
                }
                return;
            }

            // Priority 3: hold the patrol line out in the dark (visibly outside the light).
            SetState(WarriorState.Mustering);
            transform.position = PlanarMotion.StepAroundBuildings(
                transform.position, _patrolPoint, _stats.MoveSpeed, dt, Config.arrivalRadius,
                Config.movementObstaclePadding, out _);
        }

        bool TrySolveObjective(float dt)
        {
            var system = NightEscalationSystem.Instance;
            var objective = system != null ? system.ActiveMarker : null;
            if (objective == null || objective.IsSolved || !objective.IsRevealed)
            {
                return false;
            }
            if (!IsClosestWarriorTo(objective.transform.position))
            {
                return false;
            }

            SetState(WarriorState.SolvingObjective);
            float dist = PlanarMotion.Distance(transform.position, objective.transform.position);
            if (dist <= Mathf.Max(Config.arrivalRadius, Combat.warriorObjectiveSolveRadius))
            {
                objective.Solve(this);
            }
            else
            {
                transform.position = PlanarMotion.StepAroundBuildings(
                    transform.position, objective.transform.position, _stats.MoveSpeed, dt,
                    Combat.warriorObjectiveSolveRadius * 0.5f,
                    Config.movementObstaclePadding, out _);
            }
            return true;
        }

        void TickReturnToGarrison(float dt)
        {
            float dist = PlanarMotion.Distance(transform.position, _garrisonPoint);
            if (dist <= Config.arrivalRadius)
            {
                if (State != WarriorState.Garrison)
                {
                    SetState(WarriorState.Garrison);
                    GameEventLog.Append("warrior_returned", name);
                }
                return;
            }
            SetState(WarriorState.Returning);
            transform.position = PlanarMotion.StepAroundBuildings(
                transform.position, _garrisonPoint, _stats.MoveSpeed, dt, Config.arrivalRadius,
                Config.movementObstaclePadding, out _);
        }

        void TryAttack(MonsterController monster)
        {
            if (monster == null || _attackCooldown > 0f || !monster.IsAlive)
            {
                return;
            }
            _attackCooldown = _stats.AttackCooldownSeconds;
            var band = DarknessEvaluator.Classify(transform.position);
            var treatment = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, band, Combat);
            float damage = _stats.AttackDamage * treatment.DamageMultiplier;
            monster.TakeDamage(damage);
            GameEventLog.Append("warrior_attacked_monster",
                $"{name} -> {monster.name} band={band} dmg={damage:F1}");
            if (!monster.IsAlive)
            {
                GameEventLog.Append("warrior_killed_monster", $"{name} -> {monster.name}");
            }
        }

        MonsterController NearestMonster()
        {
            MonsterController nearest = null;
            float bestDist = float.MaxValue;
            float sight = Combat.warriorSightRange;
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null || !m.IsAlive)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, m.transform.position);
                if (dist <= sight && dist < bestDist)
                {
                    bestDist = dist;
                    nearest = m;
                }
            }
            return nearest;
        }

        bool IsClosestWarriorTo(Vector3 position)
        {
            float myDist = PlanarMotion.Distance(transform.position, position);
            int myIndex = _active.IndexOf(this);
            for (int i = 0; i < _active.Count; i++)
            {
                var other = _active[i];
                if (other == null || other == this || !other.IsAlive)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(other.transform.position, position);
                if (dist < myDist || (Mathf.Approximately(dist, myDist)
                    && i < myIndex))
                {
                    return false;
                }
            }
            return true;
        }

        void SetState(WarriorState next)
        {
            if (State == next)
            {
                return;
            }
            GameEventLog.Append("WarriorState", $"{name} {State}->{next}");
            State = next;
        }
    }
}
