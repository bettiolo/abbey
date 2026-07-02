using Abbey.Core;
using Abbey.Nightmares;
using UnityEngine;

namespace Abbey.Beast
{
    /// <summary>Prototype 0.1 bond states: chained → wary → fed → following.</summary>
    public enum HoundState
    {
        Chained,
        Wary,
        Fed,
        Following
    }

    /// <summary>
    /// The Black Hound of the Bell Tower. Bond values (trust/hunger/pain/fear/
    /// attachment, all 0..1) with thresholds and rates in <see cref="PrototypeConfig"/>.
    /// Starts Chained in the tower, wounded and starving. Feeding lowers hunger and
    /// raises trust; past the fed threshold it answers the bell, and a monster inside
    /// engage range gets intercepted — the fed-hound-helps payoff. Low trust or
    /// starvation means the bell is ignored. Every transition is written to
    /// <see cref="GameEventLog"/>: the bond reads through behaviour, never a meter.
    /// [ExecuteAlways] so EditMode tests get the bell subscription.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HoundController : MonoBehaviour
    {
        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        PrototypeConfig _config;
        bool _initialized;
        float _trust;
        float _hunger;
        float _pain;
        float _fear;
        float _attachment;
        Vector3? _bellTarget;
        MonsterController _engageTarget;
        float _attackCooldown;

        public HoundState State { get; private set; } = HoundState.Chained;

        public float Trust
        {
            get { return _trust; }
            set { _trust = Mathf.Clamp01(value); }
        }

        public float Hunger
        {
            get { return _hunger; }
            set { _hunger = Mathf.Clamp01(value); }
        }

        public float Pain
        {
            get { return _pain; }
            set { _pain = Mathf.Clamp01(value); }
        }

        public float Fear
        {
            get { return _fear; }
            set { _fear = Mathf.Clamp01(value); }
        }

        public float Attachment
        {
            get { return _attachment; }
            set { _attachment = Mathf.Clamp01(value); }
        }

        public bool IsStarving => Hunger >= Config.hungerStarvingThreshold;

        public bool HasBellTarget => _bellTarget.HasValue;

        public MonsterController EngagedMonster => _engageTarget;

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
            EventBus.BellRang -= OnBellRang;
            EventBus.BellRang += OnBellRang;
        }

        void OnDisable()
        {
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
            var cfg = Config;
            Trust = cfg.houndStartTrust;
            Hunger = cfg.houndStartHunger;
            Pain = cfg.houndStartPain;
            Fear = cfg.houndStartFear;
            Attachment = cfg.houndStartAttachment;
            _initialized = true;
            GameEventLog.Append("HoundState", $"{name} start Chained trust={Trust:F2} hunger={Hunger:F2}");
        }

        /// <summary>Injects a config (tests) and resets the bond values and state to its start values.</summary>
        public void Configure(PrototypeConfig config)
        {
            _config = config;
            _initialized = false;
            State = HoundState.Chained;
            _bellTarget = null;
            _engageTarget = null;
            EnsureInit();
        }

        /// <summary>
        /// Feed the hound: hunger down, trust up (rates from config), raises
        /// <see cref="EventBus.HoundFed"/> and advances the bond state when trust
        /// crosses its thresholds.
        /// </summary>
        public void Feed()
        {
            EnsureInit();
            var cfg = Config;
            Hunger -= cfg.feedHungerRelief;
            Trust += cfg.feedTrustGain;
            Fear -= cfg.feedFearRelief;
            Attachment += cfg.feedAttachmentGain;
            GameEventLog.Append("hound_fed", $"trust={Trust:F2} hunger={Hunger:F2}");
            EventBus.RaiseHoundFed(Trust);

            if (Trust >= cfg.trustFollowThreshold)
            {
                SetState(HoundState.Following);
            }
            else if (State != HoundState.Following && Trust >= cfg.trustFedThreshold)
            {
                // Never demote: a hound already Following (e.g. promoted by
                // answering the bell) must not drop back to Fed because trust
                // sits between the two thresholds.
                SetState(HoundState.Fed);
            }
            else if (State == HoundState.Chained)
            {
                SetState(HoundState.Wary);
            }
        }

        void OnBellRang(Vector3 position, float radius)
        {
            EnsureInit();
            var cfg = Config;
            bool bondedEnough = (State == HoundState.Fed || State == HoundState.Following)
                                && Trust >= cfg.trustFedThreshold;
            if (!bondedEnough || IsStarving)
            {
                GameEventLog.Append("hound_ignored_bell",
                    $"{name} state={State} trust={Trust:F2} hunger={Hunger:F2}");
                return;
            }

            _bellTarget = position;
            GameEventLog.Append("hound_answered_bell", $"{name} pos={position}");
            SetState(HoundState.Following);
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (dt <= 0f)
            {
                return;
            }

            var cfg = Config;
            Hunger += cfg.houndHungerPerSecond * dt;
            _attackCooldown -= dt;

            // Chained/Wary hounds do not leave the tower.
            if (State != HoundState.Fed && State != HoundState.Following)
            {
                return;
            }

            AcquireMonsterTarget(cfg);
            if (_engageTarget != null && _engageTarget.IsAlive)
            {
                TickEngagement(cfg, dt);
                return;
            }
            _engageTarget = null;

            if (_bellTarget.HasValue)
            {
                transform.position = PlanarMotion.Step(
                    transform.position, _bellTarget.Value, cfg.houndMoveSpeed, dt,
                    cfg.arrivalRadius, out bool arrived);
                if (arrived)
                {
                    GameEventLog.Append("hound_reached_bell", name);
                    _bellTarget = null;
                }
            }
        }

        void AcquireMonsterTarget(PrototypeConfig cfg)
        {
            if (_engageTarget != null && _engageTarget.IsAlive)
            {
                return; // stay on the current quarry
            }

            MonsterController nearest = null;
            float bestDist = float.MaxValue;
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster == null || !monster.IsAlive)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, monster.transform.position);
                if (dist <= cfg.houndEngageRange && dist < bestDist)
                {
                    bestDist = dist;
                    nearest = monster;
                }
            }

            if (nearest != null)
            {
                _engageTarget = nearest;
                GameEventLog.Append("hound_engaged_monster", $"{name} -> {nearest.name}");
            }
        }

        void TickEngagement(PrototypeConfig cfg, float dt)
        {
            _engageTarget.FleeFrom(transform); // monsters break off when the hound presses

            float dist = PlanarMotion.Distance(transform.position, _engageTarget.transform.position);
            if (dist > cfg.houndAttackRange)
            {
                transform.position = PlanarMotion.Step(
                    transform.position, _engageTarget.transform.position,
                    cfg.houndMoveSpeed, dt, cfg.houndAttackRange * 0.5f, out _);
                return;
            }

            if (_attackCooldown <= 0f)
            {
                _attackCooldown = cfg.houndAttackCooldownSeconds;
                GameEventLog.Append("hound_attacked_monster", $"{name} -> {_engageTarget.name}");
                _engageTarget.TakeDamage(cfg.houndAttackDamage);
                if (!_engageTarget.IsAlive)
                {
                    GameEventLog.Append("hound_killed_monster", $"{name} -> {_engageTarget.name}");
                    _engageTarget = null;
                }
            }
        }

        void SetState(HoundState next)
        {
            if (State == next)
            {
                return;
            }
            GameEventLog.Append("HoundState", $"{name} {State}->{next}");
            State = next;
        }
    }
}
