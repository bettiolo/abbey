using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// A night monster (pale hound in 0.1). It hunts the nearest villager exposed in
    /// Dark/Edge, will NOT enter Safe territory (any step into intolerable light is
    /// refused and it recoils back toward the dark), flees while the Black Hound
    /// presses it, and dies to hound attacks. The NightmareDirector spawns it at
    /// Night and despawns it at Dawn. [ExecuteAlways] so the static registry works
    /// in EditMode tests; Update only ticks in play mode with autoTick on.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class MonsterController : MonoBehaviour
    {
        static readonly List<MonsterController> _active = new List<MonsterController>();

        /// <summary>Every enabled, living monster (the hound scans this).</summary>
        public static IReadOnlyList<MonsterController> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        PrototypeConfig _config;
        bool _initialized;
        Transform _fleeFrom;
        float _attackCooldown;
        bool _wasRecoiling;

        public float Health { get; private set; }

        public bool IsAlive => _initialized ? Health > 0f : true;

        public bool IsFleeing => _fleeFrom != null;

        public PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
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
            Health = Config.monsterMaxHealth;
            _initialized = true;
        }

        /// <summary>
        /// Injects a config (director/tests) and resets health to its max value.
        /// Required because OnEnable already ran EnsureInit with the default config
        /// during AddComponent, before the caller could assign <see cref="Config"/>.
        /// </summary>
        public void Configure(PrototypeConfig config)
        {
            _config = config;
            _initialized = false;
            EnsureInit();
        }

        public void TakeDamage(float amount)
        {
            EnsureInit();
            if (Health <= 0f || amount <= 0f)
            {
                return;
            }
            Health -= amount;
            if (Health <= 0f)
            {
                Health = 0f;
                GameEventLog.Append("monster_killed", name);
                // Deactivate rather than Destroy: deterministic in the same tick for
                // manually driven tests; the director destroys carcasses at Dawn.
                gameObject.SetActive(false);
            }
        }

        /// <summary>Called by the hound while it presses this monster.</summary>
        public void FleeFrom(Transform threat)
        {
            if (_fleeFrom != threat)
            {
                _fleeFrom = threat;
                GameEventLog.Append("monster_fleeing", $"{name} from={(threat != null ? threat.name : "<null>")}");
            }
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (dt <= 0f || Health <= 0f)
            {
                return;
            }

            var cfg = Config;
            _attackCooldown -= dt;

            if (_fleeFrom != null)
            {
                float threatDist = PlanarMotion.Distance(transform.position, _fleeFrom.position);
                if (threatDist < cfg.monsterFleeDistance)
                {
                    Vector3 away = transform.position
                                   + PlanarMotion.Direction(_fleeFrom.position, transform.position)
                                   * cfg.monsterFleeSpeed * dt;
                    TryMoveTo(away, cfg);
                    return;
                }
                _fleeFrom = null;
            }

            var target = FindTargetVillager(cfg);
            if (target == null)
            {
                return;
            }

            Vector3 targetPos = target.transform.position;
            float dist = PlanarMotion.Distance(transform.position, targetPos);
            if (dist <= cfg.monsterAttackRange)
            {
                // Villagers standing in Safe light are untouchable.
                if (DarknessEvaluator.Classify(targetPos) != LightZone.Safe && _attackCooldown <= 0f)
                {
                    _attackCooldown = cfg.monsterAttackCooldownSeconds;
                    GameEventLog.Append("monster_attacked_villager", $"{name} -> {target.name}");
                    target.OnMonsterAttack();
                }
                return;
            }

            Vector3 next = PlanarMotion.Step(
                transform.position, targetPos, cfg.monsterMoveSpeed, dt, cfg.arrivalRadius, out _);
            TryMoveTo(next, cfg);
        }

        /// <summary>
        /// Applies a candidate move only if the destination stays tolerably dark;
        /// otherwise recoils away from the offending light back toward the Edge.
        /// </summary>
        void TryMoveTo(Vector3 candidate, PrototypeConfig cfg)
        {
            if (!IsTooBright(candidate, cfg))
            {
                transform.position = candidate;
                _wasRecoiling = false;
                return;
            }

            if (!_wasRecoiling)
            {
                _wasRecoiling = true;
                GameEventLog.Append("monster_recoiled_from_light", name);
            }

            var light = DarknessEvaluator.StrongestLightAt(candidate);
            if (light == null)
            {
                return; // blocked but no single source to recoil from: hold position
            }
            Vector3 away = transform.position
                           + PlanarMotion.Direction(light.transform.position, transform.position)
                           * cfg.monsterMoveSpeed * (1f / 60f); // small deterministic recoil step
            if (!IsTooBright(away, cfg))
            {
                transform.position = away;
            }
        }

        bool IsTooBright(Vector3 position, PrototypeConfig cfg)
        {
            return DarknessEvaluator.Classify(position) == LightZone.Safe
                   || DarknessEvaluator.LightIntensityAt(position) > cfg.monsterLightTolerance;
        }

        VillagerAgent FindTargetVillager(PrototypeConfig cfg)
        {
            VillagerAgent best = null;
            int bestTier = int.MaxValue; // Dark = 0, Edge = 1, Safe (stalked only) = 2
            float bestDist = float.MaxValue;

            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State == VillagerState.Missing || v.State == VillagerState.Dead)
                {
                    continue;
                }

                float dist = PlanarMotion.Distance(transform.position, v.transform.position);
                if (dist > cfg.monsterSightRange)
                {
                    continue;
                }

                var zone = DarknessEvaluator.Classify(v.transform.position);
                int tier = zone == LightZone.Dark ? 0 : zone == LightZone.Edge ? 1 : 2;
                if (tier < bestTier || (tier == bestTier && dist < bestDist))
                {
                    bestTier = tier;
                    bestDist = dist;
                    best = v;
                }
            }
            return best;
        }
    }
}
