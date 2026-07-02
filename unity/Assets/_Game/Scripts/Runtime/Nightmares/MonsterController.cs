using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// Base night monster. The base behaviour IS the pale hound (kept concrete so
    /// the 0.1 director/tests that AddComponent this class directly stay valid):
    /// it hunts the nearest villager exposed in Dark/Edge, will NOT enter Safe
    /// territory (any step into intolerable light is refused and it recoils back
    /// toward the dark), flees while the Black Hound presses it, and dies to hound
    /// attacks. Phase 2 species (<see cref="DrownedSailorController"/>,
    /// <see cref="LanternMothController"/>) override <see cref="TickBehaviour"/>
    /// and reuse the shared movement/light/attack helpers. Weak nightmares
    /// (<see cref="IsStunnedByBell"/>) are stunned by a bell pulse inside its
    /// radius — spec: bell = weak-nightmare stun. The NightmareDirector spawns
    /// monsters at Night and despawns them at Dawn. [ExecuteAlways] so the static
    /// registry works in EditMode tests; Update only ticks in play mode with
    /// autoTick on.
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
        float _stunTimer;
        bool _wasRecoiling;

        public float Health { get; private set; }

        public bool IsAlive => _initialized ? Health > 0f : true;

        public bool IsFleeing => _fleeFrom != null;

        /// <summary>True while a bell pulse holds this (weak) nightmare frozen.</summary>
        public bool IsStunned => _stunTimer > 0f;

        /// <summary>Which nightmare species this is (base = the pale hound).</summary>
        public virtual NightmareType Type => NightmareType.PaleHound;

        /// <summary>Weak nightmares freeze when the bell rings over them.</summary>
        protected virtual bool IsStunnedByBell => false;

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

        protected virtual void OnEnable()
        {
            EnsureInit();
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
            EventBus.BellRang -= OnBellRang;
            EventBus.BellRang += OnBellRang;
        }

        protected virtual void OnDisable()
        {
            _active.Remove(this);
            EventBus.BellRang -= OnBellRang;
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

            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                return; // stunned nightmares neither move nor strike
            }

            TickBehaviour(cfg, dt);
        }

        /// <summary>
        /// Species behaviour, one deterministic step. The base implementation is
        /// the pale hound: break off from the Black Hound, then close on and
        /// strike the most exposed villager without ever entering Safe light.
        /// </summary>
        protected virtual void TickBehaviour(PrototypeConfig cfg, float dt)
        {
            if (TickFleeFromThreat(cfg, cfg.monsterFleeSpeed, dt))
            {
                return;
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
                TryAttack(target, cfg);
                return;
            }

            Vector3 next = PlanarMotion.Step(
                transform.position, targetPos, cfg.monsterMoveSpeed, dt, cfg.arrivalRadius, out _);
            TryMoveTo(next, cfg);
        }

        /// <summary>
        /// Shared flee handling: while the registered threat (the Black Hound) is
        /// inside monsterFleeDistance, run directly away from it. Returns true when
        /// a flee step consumed this tick.
        /// </summary>
        protected bool TickFleeFromThreat(PrototypeConfig cfg, float fleeSpeed, float dt)
        {
            if (_fleeFrom == null)
            {
                return false;
            }
            float threatDist = PlanarMotion.Distance(transform.position, _fleeFrom.position);
            if (threatDist < cfg.monsterFleeDistance)
            {
                Vector3 away = transform.position
                               + PlanarMotion.Direction(_fleeFrom.position, transform.position)
                               * fleeSpeed * dt;
                TryMoveTo(away, cfg);
                return true;
            }
            _fleeFrom = null;
            return false;
        }

        /// <summary>
        /// Strikes a villager already inside monsterAttackRange. Villagers standing
        /// in Safe light are untouchable; the cooldown gates repeat strikes.
        /// </summary>
        protected void TryAttack(VillagerAgent target, PrototypeConfig cfg)
        {
            if (target == null)
            {
                return;
            }
            if (DarknessEvaluator.Classify(target.transform.position) != LightZone.Safe
                && _attackCooldown <= 0f)
            {
                _attackCooldown = cfg.monsterAttackCooldownSeconds;
                GameEventLog.Append("monster_attacked_villager", $"{name} -> {target.name}");
                target.OnMonsterAttack();
            }
        }

        /// <summary>
        /// Applies a candidate move only if the destination stays tolerably dark;
        /// otherwise recoils away from the offending light back toward the Edge.
        /// </summary>
        protected void TryMoveTo(Vector3 candidate, PrototypeConfig cfg)
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

        /// <summary>
        /// Light a species refuses to stand in. Base (pale hound): any Safe zone,
        /// or intensity beyond monsterLightTolerance. Species override this to be
        /// braver (drowned sailor) or light-loving (lantern moth).
        /// </summary>
        protected virtual bool IsTooBright(Vector3 position, PrototypeConfig cfg)
        {
            return DarknessEvaluator.Classify(position) == LightZone.Safe
                   || DarknessEvaluator.LightIntensityAt(position) > cfg.monsterLightTolerance;
        }

        /// <summary>
        /// The most exposed reachable villager: Dark beats Edge beats Safe
        /// (stalked only), nearer beats farther inside a tier.
        /// </summary>
        protected VillagerAgent FindTargetVillager(PrototypeConfig cfg)
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

        void OnBellRang(Vector3 position, float radius)
        {
            if (!IsStunnedByBell || !IsAlive)
            {
                return;
            }
            if (PlanarMotion.Distance(transform.position, position) > radius)
            {
                return;
            }
            _stunTimer = Config.bellNightmareStunSeconds;
            GameEventLog.Append("nightmare",
                $"stun name={name} type={Type} seconds={_stunTimer:F1}");
        }
    }
}
