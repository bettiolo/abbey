using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using UnityEngine;

namespace Abbey.Beast
{
    /// <summary>
    /// Full bond state set (VERTICAL_SLICE_SPEC §8). The first four values are the
    /// prototype ladder and must keep their order; the rest are appended so nothing
    /// serialized shifts. Chained/Wary/Fed/Following/Trusting form the promote-only
    /// bond ladder; Guarding/Hunting/Protective/Angry/Wounded/Missing are
    /// value-driven overrides.
    /// </summary>
    public enum HoundState
    {
        Chained,
        Wary,
        Fed,
        Following,
        Guarding,
        Hunting,
        Protective,
        Angry,
        Missing,
        Wounded,
        Trusting
    }

    /// <summary>Outcome of the approach-slowly choice (deterministic, never random).</summary>
    public enum HoundApproachResult
    {
        /// <summary>The hound tolerated the approach: attachment and trust rose.</summary>
        Calmed,

        /// <summary>Fear + pain were past the bite threshold: the hound bit the hero.</summary>
        Bitten,

        /// <summary>No hound to approach (Missing).</summary>
        Unavailable
    }

    /// <summary>
    /// The Black Hound of the Bell Tower. Bond values (trust/hunger/pain/fear/
    /// attachment, all 0..1) with thresholds and rates in <see cref="PrototypeConfig"/>.
    /// Starts Chained in the tower, wounded and starving.
    ///
    /// First-encounter choices (never cosmetic): <see cref="Feed"/>,
    /// <see cref="FreeFromChain"/>, <see cref="ApproachSlowly"/>,
    /// <see cref="LeaveChained"/> and calm-with-bell (a bell rung inside its radius
    /// soothes a still-chained hound). Each writes a distinct "hound_choice" record.
    ///
    /// Behaviour branches (GAME_DESIGN §5): a fed, bonded hound breaks its chain to
    /// save a hero endangered in darkness near a monster (Protective); a starved,
    /// freed hound hunts on its own — kills a monster, drags the corpse toward
    /// darkness, eats alone and refuses the bell (Hunting); heavy pain reads as
    /// Wounded, pain + fear as Angry; a high-trust hound settles into Guarding
    /// inside Safe light; a hound freed with no bond flees to Missing. Every
    /// transition is written to <see cref="GameEventLog"/> ("hound_state" /
    /// "hound_choice" / "hound_intervention"): the bond reads through behaviour,
    /// never a meter. [ExecuteAlways] so EditMode tests get the bell subscription.
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
        bool _everInteracted;
        Vector3? _bellTarget;
        MonsterController _engageTarget;
        float _attackCooldown;
        BellkeeperController _hero;
        bool _fleeingToMissing;
        Vector3 _fleeOrigin;
        Vector3 _fleeDirection;
        Transform _dragCorpse;
        Vector3? _dragTarget;

        public HoundState State { get; private set; } = HoundState.Chained;

        /// <summary>True until the chain is removed (FreeFromChain) or broken (bond override).</summary>
        public bool IsChained { get; private set; } = true;

        public bool IsMissing => State == HoundState.Missing;

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

        /// <summary>
        /// The Bellkeeper this hound watches over (Protective trigger). Lazily found
        /// in the scene; tests may inject one.
        /// </summary>
        public BellkeeperController Hero
        {
            get
            {
                if (_hero == null)
                {
                    _hero = FindFirstObjectByType<BellkeeperController>();
                }
                return _hero;
            }
            set { _hero = value; }
        }

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
            GameEventLog.Append("hound_state", $"{name} start Chained trust={Trust:F2} hunger={Hunger:F2}");
        }

        /// <summary>Injects a config (tests) and resets the bond values and state to its start values.</summary>
        public void Configure(PrototypeConfig config)
        {
            _config = config;
            _initialized = false;
            State = HoundState.Chained;
            IsChained = true;
            _everInteracted = false;
            _bellTarget = null;
            _engageTarget = null;
            _hero = null;
            _fleeingToMissing = false;
            _dragCorpse = null;
            _dragTarget = null;
            EnsureInit();
        }

        // ------------------------------------------------------------------
        // First-encounter choices (never cosmetic)
        // ------------------------------------------------------------------

        /// <summary>
        /// Feed the hound: hunger down, trust up (rates from config), raises
        /// <see cref="EventBus.HoundFed"/> and promotes the bond ladder when trust
        /// crosses its thresholds. Never demotes.
        /// </summary>
        public void Feed()
        {
            EnsureInit();
            if (IsMissing)
            {
                return;
            }
            var cfg = Config;
            _everInteracted = true;
            Hunger -= cfg.feedHungerRelief;
            Trust += cfg.feedTrustGain;
            Fear -= cfg.feedFearRelief;
            Attachment += cfg.feedAttachmentGain;
            GameEventLog.Append("hound_fed", $"trust={Trust:F2} hunger={Hunger:F2}");
            GameEventLog.Append("hound_choice",
                $"feed trust={Trust:F2} hunger={Hunger:F2} fear={Fear:F2}");
            EventBus.RaiseHoundFed(Trust);

            if (State == HoundState.Angry && !_fleeingToMissing && !IsAngry(cfg))
            {
                // A meal can settle an angry hound (feeding lowers fear).
                SetState(Pain >= cfg.houndWoundedPainThreshold
                    ? HoundState.Wounded : LadderState(cfg));
                return;
            }
            PromoteLadder(cfg);
        }

        /// <summary>
        /// Removes the chain. Trust rises a little (freeChainTrustGain), then the
        /// bond decides: below freeChainFollowThreshold the hound turns Angry and
        /// flees toward darkness until it is Missing; otherwise it stays — Wary,
        /// Fed or Following by the trust ladder.
        /// </summary>
        public bool FreeFromChain(Vector3 liberatorPosition)
        {
            EnsureInit();
            if (IsMissing || !IsChained)
            {
                return false;
            }
            var cfg = Config;
            _everInteracted = true;
            IsChained = false;
            Trust += cfg.freeChainTrustGain;

            if (Trust < cfg.freeChainFollowThreshold)
            {
                _fleeingToMissing = true;
                _fleeOrigin = transform.position;
                _fleeDirection = PlanarMotion.Direction(liberatorPosition, transform.position);
                GameEventLog.Append("hound_choice",
                    $"free_chain outcome=fled trust={Trust:F2}");
                SetState(HoundState.Angry);
                return true;
            }

            var next = LadderState(cfg);
            GameEventLog.Append("hound_choice",
                $"free_chain outcome={next} trust={Trust:F2}");
            SetState(next);
            return true;
        }

        /// <summary>
        /// The explicit walk-away: nothing physically changes, but the hound
        /// remembers (leaveChainedTrustLoss of resentment). Distinct log entry so
        /// the night and the morning report can branch on it.
        /// </summary>
        public bool LeaveChained()
        {
            EnsureInit();
            if (IsMissing || !IsChained)
            {
                return false;
            }
            Trust -= Config.leaveChainedTrustLoss;
            GameEventLog.Append("hound_choice", $"leave_chained trust={Trust:F2}");
            return true;
        }

        /// <summary>
        /// A slow, open-handed approach. Deterministic: when fear + pain reach
        /// approachBiteThreshold the hound bites (the caller injures the hero);
        /// otherwise attachment and trust rise and fear eases.
        /// </summary>
        public HoundApproachResult ApproachSlowly()
        {
            EnsureInit();
            if (IsMissing)
            {
                return HoundApproachResult.Unavailable;
            }
            var cfg = Config;
            _everInteracted = true;

            if (Fear + Pain >= cfg.approachBiteThreshold)
            {
                Trust -= cfg.approachBiteTrustLoss;
                Fear += cfg.approachBiteFearGain;
                GameEventLog.Append("hound_choice",
                    $"approach outcome=bite trust={Trust:F2} fear={Fear:F2} pain={Pain:F2}");
                EvaluateDistress(cfg);
                return HoundApproachResult.Bitten;
            }

            Trust += cfg.approachTrustGain;
            Attachment += cfg.approachAttachmentGain;
            Fear -= cfg.approachFearRelief;
            GameEventLog.Append("hound_choice",
                $"approach outcome=calm trust={Trust:F2} attach={Attachment:F2}");
            PromoteLadder(cfg);
            return HoundApproachResult.Calmed;
        }

        /// <summary>
        /// Damage taken (monster claws, stray strikes): pain and fear rise, and past
        /// the thresholds the hound reads Angry (pain + fear) or Wounded (pain).
        /// </summary>
        public void TakeHit(float painAmount)
        {
            EnsureInit();
            if (IsMissing || painAmount <= 0f)
            {
                return;
            }
            var cfg = Config;
            Pain += painAmount;
            Fear += cfg.houndHitFearGain;
            GameEventLog.Append("hound_took_hit",
                $"{name} pain={Pain:F2} fear={Fear:F2}");
            EvaluateDistress(cfg);
        }

        // ------------------------------------------------------------------
        // Bell
        // ------------------------------------------------------------------

        void OnBellRang(Vector3 position, float radius)
        {
            EnsureInit();
            if (IsMissing)
            {
                return; // there is no hound to hear it
            }
            var cfg = Config;

            if (State == HoundState.Hunting || _fleeingToMissing)
            {
                GameEventLog.Append("hound_ignored_bell",
                    $"{name} state={State} trust={Trust:F2} hunger={Hunger:F2}");
                return;
            }

            bool bondedState = State == HoundState.Fed || State == HoundState.Following
                               || State == HoundState.Guarding || State == HoundState.Trusting
                               || State == HoundState.Protective;
            bool bondedEnough = bondedState && Trust >= cfg.trustFedThreshold;
            if (bondedEnough && !IsStarving)
            {
                _bellTarget = position;
                GameEventLog.Append("hound_answered_bell", $"{name} pos={position}");
                if (State == HoundState.Fed || State == HoundState.Guarding)
                {
                    SetState(HoundState.Following);
                }
                return;
            }

            GameEventLog.Append("hound_ignored_bell",
                $"{name} state={State} trust={Trust:F2} hunger={Hunger:F2}");

            // Calm-with-bell: a bell rung inside its radius soothes a still-chained
            // hound even when it does not come (the first-encounter choice).
            if (IsChained && PlanarMotion.Distance(transform.position, position) <= radius)
            {
                _everInteracted = true;
                Fear -= cfg.bellCalmFearRelief;
                Trust += cfg.bellCalmTrustGain;
                GameEventLog.Append("hound_calmed_by_bell",
                    $"{name} fear={Fear:F2} trust={Trust:F2}");
                GameEventLog.Append("hound_choice",
                    $"calm_bell fear={Fear:F2} trust={Trust:F2}");
                if (State == HoundState.Angry && !_fleeingToMissing && !IsAngry(cfg))
                {
                    SetState(Pain >= cfg.houndWoundedPainThreshold
                        ? HoundState.Wounded : LadderState(cfg));
                }
            }
        }

        // ------------------------------------------------------------------
        // Simulation
        // ------------------------------------------------------------------

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (dt <= 0f || IsMissing)
            {
                return;
            }

            var cfg = Config;
            Hunger += cfg.houndHungerPerSecond * dt;
            Pain -= cfg.houndPainRecoveryPerSecond * dt;
            _attackCooldown -= dt;

            // A freed, no-bond hound runs for the dark until it is gone.
            if (_fleeingToMissing)
            {
                TickFlee(cfg, dt);
                return;
            }

            // Distress recovery / entry, purely from the values.
            if (State == HoundState.Angry)
            {
                if (!IsAngry(cfg))
                {
                    SetState(Pain >= cfg.houndWoundedPainThreshold
                        ? HoundState.Wounded : LadderState(cfg));
                }
                return; // an angry hound holds its ground and does nothing for anyone
            }
            if (State == HoundState.Wounded)
            {
                if (Pain < cfg.houndWoundedPainThreshold)
                {
                    SetState(LadderState(cfg));
                }
                return; // a wounded hound does not move or fight
            }
            if (IsAngry(cfg))
            {
                SetState(HoundState.Angry);
                return;
            }
            if (Pain >= cfg.houndWoundedPainThreshold)
            {
                SetState(HoundState.Wounded);
                return;
            }

            // Starved and freed: the hound hunts for itself (the chain stops a
            // chained one — matching the unfed first-night branch).
            if (!IsChained && IsStarving && State != HoundState.Hunting
                && State != HoundState.Protective)
            {
                var quarry = NearestMonsterTo(transform.position, cfg.houndEngageRange);
                if (quarry != null)
                {
                    _engageTarget = quarry;
                    GameEventLog.Append("hound_engaged_monster", $"{name} -> {quarry.name}");
                    GameEventLog.Append("hound_intervention",
                        $"hunting_starved {name} -> {quarry.name}");
                    SetState(HoundState.Hunting);
                }
            }

            if (State == HoundState.Hunting)
            {
                TickHunting(cfg, dt);
                return;
            }

            // Protective: a bonded, sated hound watches the hero. Endangered in
            // darkness with a monster closing in, it comes — chain or no chain.
            EvaluateProtective(cfg);
            if (State == HoundState.Protective)
            {
                if (_engageTarget != null && _engageTarget.IsAlive)
                {
                    BreakChainIfNeeded("save_hero");
                    TickEngagement(cfg, dt);
                    return;
                }
                _engageTarget = null;
                SetState(LadderState(cfg)); // the danger has passed
            }

            // Chained/Wary hounds do not leave the tower.
            bool active = State == HoundState.Fed || State == HoundState.Following
                          || State == HoundState.Guarding || State == HoundState.Trusting;
            if (!active)
            {
                return;
            }

            AcquireMonsterTarget(cfg);
            if (_engageTarget != null && _engageTarget.IsAlive)
            {
                if (State == HoundState.Guarding)
                {
                    SetState(HoundState.Following); // leaves its post to intercept
                }
                BreakChainIfNeeded("engage_monster");
                TickEngagement(cfg, dt);
                return;
            }
            _engageTarget = null;

            if (_bellTarget.HasValue)
            {
                BreakChainIfNeeded("answer_bell");
                transform.position = PlanarMotion.Step(
                    transform.position, _bellTarget.Value, cfg.houndMoveSpeed, dt,
                    cfg.arrivalRadius, out bool arrived);
                if (arrived)
                {
                    GameEventLog.Append("hound_reached_bell", name);
                    _bellTarget = null;
                }
                return;
            }

            // Settled: a free, trusted, sated hound inside Safe light guards it.
            if (State == HoundState.Following && !IsChained && !IsStarving
                && Trust >= cfg.guardTrustThreshold
                && DarknessEvaluator.Classify(transform.position) == LightZone.Safe)
            {
                SetState(HoundState.Guarding);
            }
            else if (State == HoundState.Guarding
                     && DarknessEvaluator.Classify(transform.position) != LightZone.Safe)
            {
                SetState(HoundState.Following);
            }
        }

        void TickFlee(PrototypeConfig cfg, float dt)
        {
            transform.position += _fleeDirection * cfg.houndMoveSpeed * dt;
            if (PlanarMotion.Distance(transform.position, _fleeOrigin) >= cfg.houndFleeDistance)
            {
                _fleeingToMissing = false;
                _bellTarget = null;
                _engageTarget = null;
                GameEventLog.Append("hound_intervention", $"went_missing {name}");
                SetState(HoundState.Missing);
            }
        }

        void TickHunting(PrototypeConfig cfg, float dt)
        {
            // Drag the kill toward darkness, then eat alone.
            if (_dragTarget.HasValue)
            {
                transform.position = PlanarMotion.Step(
                    transform.position, _dragTarget.Value, cfg.houndMoveSpeed, dt,
                    cfg.arrivalRadius, out bool arrived);
                if (_dragCorpse != null)
                {
                    _dragCorpse.position = transform.position;
                }
                if (arrived)
                {
                    GameEventLog.Append("hound_dragged_corpse",
                        $"{name} to={transform.position}");
                    Hunger -= cfg.houndEatHungerRelief;
                    GameEventLog.Append("hound_intervention",
                        $"ate_kill {name} hunger={Hunger:F2}");
                    _dragTarget = null;
                    _dragCorpse = null;
                    if (!IsStarving)
                    {
                        SetState(LadderState(cfg));
                    }
                }
                return;
            }

            if (_engageTarget != null && _engageTarget.IsAlive)
            {
                TickEngagement(cfg, dt);
                return;
            }

            _engageTarget = NearestMonsterTo(transform.position, cfg.houndEngageRange);
            if (_engageTarget == null && !IsStarving)
            {
                SetState(LadderState(cfg)); // sated and nothing left to hunt
            }
        }

        void EvaluateProtective(PrototypeConfig cfg)
        {
            if (IsStarving || Trust < cfg.chainBreakTrustThreshold)
            {
                return;
            }
            var hero = Hero;
            if (hero == null || !hero.IsAlive
                || DarknessEvaluator.Classify(hero.transform.position) != LightZone.Dark)
            {
                return;
            }
            var threat = NearestMonsterTo(hero.transform.position, cfg.houndProtectMonsterRange);
            if (threat == null)
            {
                return;
            }
            if (_engageTarget != threat)
            {
                _engageTarget = threat;
                GameEventLog.Append("hound_engaged_monster", $"{name} -> {threat.name}");
            }
            if (State != HoundState.Protective)
            {
                GameEventLog.Append("hound_intervention",
                    $"protect_hero {name} -> {threat.name}");
                SetState(HoundState.Protective);
            }
        }

        void BreakChainIfNeeded(string reason)
        {
            if (!IsChained)
            {
                return;
            }
            IsChained = false;
            GameEventLog.Append("hound_intervention", $"broke_chain reason={reason} {name}");
        }

        void AcquireMonsterTarget(PrototypeConfig cfg)
        {
            if (_engageTarget != null && _engageTarget.IsAlive)
            {
                return; // stay on the current quarry
            }
            var nearest = NearestMonsterTo(transform.position, cfg.houndEngageRange);
            if (nearest != null)
            {
                _engageTarget = nearest;
                GameEventLog.Append("hound_engaged_monster", $"{name} -> {nearest.name}");
            }
        }

        static MonsterController NearestMonsterTo(Vector3 position, float range)
        {
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
                float dist = PlanarMotion.Distance(position, monster.transform.position);
                if (dist <= range && dist < bestDist)
                {
                    bestDist = dist;
                    nearest = monster;
                }
            }
            return nearest;
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
                    if (State == HoundState.Hunting)
                    {
                        BeginCorpseDrag(cfg, _engageTarget.transform);
                    }
                    _engageTarget = null;
                }
            }
        }

        void BeginCorpseDrag(PrototypeConfig cfg, Transform corpse)
        {
            _dragCorpse = corpse;
            _dragTarget = corpse.position
                          + DarknessDirectionFrom(corpse.position) * cfg.houndDragDistance;
            GameEventLog.Append("hound_intervention",
                $"drag_corpse {name} corpse={corpse.name}");
        }

        /// <summary>Direction away from the nearest lit source — "toward darkness".</summary>
        static Vector3 DarknessDirectionFrom(Vector3 position)
        {
            LightSource nearest = null;
            float bestDist = float.MaxValue;
            var sources = DarknessEvaluator.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !source.isLit || source.EffectiveRadius <= 0f)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(position, source.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = source;
                }
            }
            return nearest != null
                ? PlanarMotion.Direction(nearest.transform.position, position)
                : Vector3.forward;
        }

        // ------------------------------------------------------------------
        // Bond ladder
        // ------------------------------------------------------------------

        bool IsAngry(PrototypeConfig cfg)
        {
            return Pain >= cfg.houndAngryPainThreshold && Fear >= cfg.houndAngryFearThreshold;
        }

        void EvaluateDistress(PrototypeConfig cfg)
        {
            if (IsMissing || _fleeingToMissing)
            {
                return;
            }
            if (IsAngry(cfg))
            {
                if (State != HoundState.Angry)
                {
                    _bellTarget = null;
                    SetState(HoundState.Angry);
                }
                return;
            }
            if (Pain >= cfg.houndWoundedPainThreshold && State != HoundState.Wounded)
            {
                _bellTarget = null;
                _engageTarget = null;
                SetState(HoundState.Wounded);
            }
        }

        /// <summary>The bond state the values alone justify (no behavioural overrides).</summary>
        HoundState LadderState(PrototypeConfig cfg)
        {
            if (Trust >= cfg.trustTrustingThreshold
                && Attachment >= cfg.trustingAttachmentThreshold)
            {
                return HoundState.Trusting;
            }
            if (Trust >= cfg.trustFollowThreshold)
            {
                return HoundState.Following;
            }
            if (Trust >= cfg.trustFedThreshold)
            {
                return HoundState.Fed;
            }
            return IsChained && !_everInteracted ? HoundState.Chained : HoundState.Wary;
        }

        static int LadderRank(HoundState state)
        {
            switch (state)
            {
                case HoundState.Chained: return 0;
                case HoundState.Wary: return 1;
                case HoundState.Fed: return 2;
                case HoundState.Following: return 3;
                case HoundState.Guarding: return 3; // Following-grade bond, settled
                case HoundState.Trusting: return 4;
                default: return -1; // behavioural overrides sit outside the ladder
            }
        }

        /// <summary>Promote-only: feeding or approaching never lowers the bond state.</summary>
        void PromoteLadder(PrototypeConfig cfg)
        {
            int currentRank = LadderRank(State);
            if (currentRank < 0)
            {
                return; // Angry/Wounded/Hunting/Protective/Missing resolve elsewhere
            }
            var ladder = LadderState(cfg);
            if (LadderRank(ladder) > currentRank)
            {
                SetState(ladder);
            }
        }

        void SetState(HoundState next)
        {
            if (State == next)
            {
                return;
            }
            GameEventLog.Append("hound_state", $"{name} {State}->{next}");
            State = next;
        }
    }
}
