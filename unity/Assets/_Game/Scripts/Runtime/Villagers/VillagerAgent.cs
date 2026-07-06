using Abbey.Core;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>Villager states, exactly per docs/VERTICAL_SLICE_SPEC.md section 6.</summary>
    public enum VillagerState
    {
        Idle,
        AssignedToWork,
        WalkingToTask,
        Working,
        CarryingResource,
        ReturningToStorage,
        ReturningToLight,
        Panicking,
        Injured,
        Resting,
        Missing,
        Dead
    }

    /// <summary>
    /// A villager: a state machine, not complex AI (GAME_DESIGN.md §6). By day it
    /// loops task-site → storage. In the Dark at Dusk/Night fear accumulates
    /// (bravery slows it), panic sends it running erratically, and staying too long
    /// in darkness ends in Injured then Missing. The hero can attach it in a
    /// rescued-follow mode (ReturningToLight with an escort transform).
    /// All movement is kinematic XZ steering; all thresholds live in
    /// <see cref="PrototypeConfig"/>. [ExecuteAlways] so EditMode tests get
    /// OnEnable registration; Update only ticks in play mode with autoTick on.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class VillagerAgent : MonoBehaviour
    {
        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        [Tooltip("Deterministic seed for bravery default and panic wandering.")]
        public int seed;

        [Tooltip("0..1 courage. Negative = derive deterministically from seed and config range.")]
        [SerializeField] float bravery = -1f;

        PrototypeConfig _config;
        System.Random _rng;

        // Work loop.
        Vector3 _taskSite;
        Vector3 _storagePoint;
        bool _hasAssignment;
        float _workTimer;
        float _pickupTimer;

        // Recall / rescue.
        bool _recallOrdered;
        bool _bellBoosted;
        float _recallDelay;
        Transform _escort;

        // Fear / darkness.
        float _timeInDark;
        float _panicDirTimer;
        Vector3 _panicDir = Vector3.forward;
        float _restTimer;

        public VillagerState State { get; private set; } = VillagerState.Idle;

        // ---- Job-layer hooks (P2-02). All default to "no job layer": null handlers,
        // no duration override, multiplier 1 — the original loop is untouched.

        /// <summary>
        /// Single job-layer handler invoked when the Working timer elapses (and no
        /// recall interrupts). Return true to proceed into CarryingResource as
        /// usual; return false when the handler took over (reassigned via
        /// <see cref="AssignWork"/> or stopped via <see cref="CancelWork"/>). A
        /// false return that leaves the state Working simply works another cycle.
        /// </summary>
        public System.Func<VillagerAgent, bool> WorkCycleHandler { get; set; }

        /// <summary>
        /// Single job-layer handler invoked on arrival at the storage point, after
        /// the deposit log record. May retarget (AssignWork) or stop (CancelWork);
        /// otherwise the loop walks back to the task site as before.
        /// </summary>
        public System.Action<VillagerAgent> DepositHandler { get; set; }

        /// <summary>Per-assignment Working duration; &lt;= 0 uses config villagerWorkDurationSeconds.</summary>
        public float WorkDurationOverride { get; set; } = -1f;

        /// <summary>Job walk-speed multiplier applied to the work loop legs only.</summary>
        public float WorkSpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// Daytime work-efficiency multiplier written by
        /// <see cref="Abbey.Sanity.SanitySystem"/> from the villager's sanity (1 when
        /// no sanity system exists). The job layer scales its output by this and stops
        /// working entirely at 0 — an insane villager downs tools until it recovers.
        /// </summary>
        public float SanityWorkEfficiency { get; set; } = 1f;

        /// <summary>True while the villager holds a day-loop assignment.</summary>
        public bool HasWorkAssignment => _hasAssignment;

        /// <summary>
        /// P3-08 overdrive: while true the villager is pressed into night service (Forced
        /// Night Work, a Candle Line carrier, Volunteer Watch) and ignores the dusk recall
        /// / bell — it keeps working out in the (overdriven) light. The
        /// <see cref="Abbey.Decrees.OverdriveSystem"/> sets it at activation and clears it
        /// at dawn, handing the villager back to the normal recall.
        /// </summary>
        public bool NightWorkExempt { get; set; }

        /// <summary>Fear 0..1. Panic threshold and rates come from config.</summary>
        public float Fear { get; private set; }

        /// <summary>Continuous seconds spent in the Dark at Dusk/Night.</summary>
        public float TimeInDark => _timeInDark;

        public bool IsEscorted => _escort != null;

        public bool IsRecallOrdered => _recallOrdered;

        public LightZone CurrentZone => DarknessEvaluator.Classify(transform.position);

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

        /// <summary>Per-villager courage in [braveryMin, braveryMax], deterministic from seed.</summary>
        public float Bravery
        {
            get
            {
                if (bravery < 0f)
                {
                    var cfg = Config;
                    bravery = Mathf.Lerp(cfg.villagerBraveryMin, cfg.villagerBraveryMax, Hash01(seed));
                }
                return bravery;
            }
            set { bravery = Mathf.Clamp01(value); }
        }

        void OnEnable()
        {
            DuskRecallSystem.Register(this);
        }

        void OnDisable()
        {
            DuskRecallSystem.Unregister(this);
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Gives the villager its day-loop assignment and starts it working.</summary>
        public void AssignWork(Vector3 taskSite, Vector3 storagePoint)
        {
            if (State == VillagerState.Missing || State == VillagerState.Dead)
            {
                return;
            }
            _taskSite = taskSite;
            _storagePoint = storagePoint;
            _hasAssignment = true;
            SetState(VillagerState.AssignedToWork);
        }

        /// <summary>
        /// Drops the day-loop assignment and idles. Only exits work-loop states;
        /// recall, panic, injury and rescue states are never touched.
        /// </summary>
        public void CancelWork()
        {
            _hasAssignment = false;
            switch (State)
            {
                case VillagerState.AssignedToWork:
                case VillagerState.WalkingToTask:
                case VillagerState.Working:
                case VillagerState.CarryingResource:
                case VillagerState.ReturningToStorage:
                    SetState(VillagerState.Idle);
                    break;
            }
        }

        /// <summary>
        /// Orders a recall to the nearest Safe light. Bell-covered villagers get the
        /// config speed multiplier, calm down a little, and leave immediately; the
        /// rest recall after <paramref name="delaySeconds"/> (the drama beat). Brave
        /// villagers finish their current work item first.
        /// </summary>
        public void OrderReturnToLight(bool bellBoosted, float delaySeconds = 0f)
        {
            if (NightWorkExempt)
            {
                return; // pressed into overdrive night service: it does not recall
            }
            switch (State)
            {
                case VillagerState.Missing:
                case VillagerState.Dead:
                case VillagerState.Injured:
                case VillagerState.Resting:
                case VillagerState.ReturningToLight:
                    return;
                case VillagerState.Panicking:
                    if (!bellBoosted)
                    {
                        return; // only the bell cuts through panic
                    }
                    Fear = Mathf.Max(0f, Fear - Config.bellCalmAmount);
                    _bellBoosted = true;
                    EnterReturningToLight();
                    return;
            }

            _bellBoosted = _bellBoosted || bellBoosted;
            if (bellBoosted)
            {
                Fear = Mathf.Max(0f, Fear - Config.bellCalmAmount);
                _recallDelay = 0f;
            }
            else if (!_recallOrdered)
            {
                _recallDelay = delaySeconds;
            }
            _recallOrdered = true;

            bool finishesWorkFirst = State == VillagerState.Working
                                     && Bravery >= Config.braveryFinishWorkThreshold;
            if (finishesWorkFirst)
            {
                GameEventLog.Append("villager_finishing_work", name);
                return; // Working completion routes into ReturningToLight
            }
            if (_recallDelay <= 0f)
            {
                EnterReturningToLight();
            }
        }

        /// <summary>
        /// Hero rescue: attach to an escort transform and follow it (rescued-follow
        /// behaviour, implemented as ReturningToLight with an escort).
        /// </summary>
        public bool BeginRescue(Transform escort)
        {
            if (escort == null || State == VillagerState.Missing || State == VillagerState.Dead)
            {
                return false;
            }
            _escort = escort;
            _recallOrdered = false;
            GameEventLog.Append("villager_rescue_started", name);
            SetState(VillagerState.ReturningToLight);
            return true;
        }

        /// <summary>
        /// Detaches from the escort. Released in a Safe zone the rescue completes
        /// (raises VillagerRescued); otherwise the villager keeps walking to the
        /// nearest light on its own.
        /// </summary>
        public bool ReleaseRescue()
        {
            if (_escort == null)
            {
                return false;
            }
            _escort = null;
            if (CurrentZone == LightZone.Safe)
            {
                EventBus.RaiseVillagerRescued(gameObject);
                Fear = 0f;
                SetState(VillagerState.Idle);
                return true;
            }
            if (State == VillagerState.ReturningToLight)
            {
                GameEventLog.Append("villager_released_in_dark", name);
            }
            return false;
        }

        /// <summary>A night monster strikes: healthy → Injured, Injured → Dead.</summary>
        public void OnMonsterAttack()
        {
            if (State == VillagerState.Missing || State == VillagerState.Dead)
            {
                return;
            }
            Fear = 1f;
            if (State == VillagerState.Injured)
            {
                GameEventLog.Append("villager_died", name);
                SetState(VillagerState.Dead);
                return;
            }
            SetState(VillagerState.Injured);
        }

        /// <summary>Test/API hook: some 0.1 states (Resting, Missing…) are reachable only here.</summary>
        public void ForceState(VillagerState state)
        {
            SetState(state);
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || State == VillagerState.Missing || State == VillagerState.Dead)
            {
                return;
            }

            var cfg = Config;
            var zone = CurrentZone;
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            bool fearTime = phase == DayPhase.Dusk || phase == DayPhase.Night;

            UpdateFearAndDarkness(cfg, zone, fearTime, dt);
            if (State == VillagerState.Missing)
            {
                return; // went missing this tick
            }

            UpdatePanicEntry(cfg, dt);
            UpdatePendingRecall(dt);

            switch (State)
            {
                case VillagerState.Idle:
                    break;

                case VillagerState.AssignedToWork:
                    if (_hasAssignment)
                    {
                        SetState(VillagerState.WalkingToTask);
                    }
                    break;

                case VillagerState.WalkingToTask:
                    if (StepTowards(_taskSite, cfg.villagerWalkSpeed * WorkSpeedMultiplier, dt))
                    {
                        _workTimer = WorkDuration(cfg);
                        SetState(VillagerState.Working);
                    }
                    break;

                case VillagerState.Working:
                    _workTimer -= dt;
                    if (_workTimer <= 0f)
                    {
                        if (_recallOrdered)
                        {
                            EnterReturningToLight();
                        }
                        else
                        {
                            bool proceed = WorkCycleHandler == null || WorkCycleHandler(this);
                            if (State != VillagerState.Working)
                            {
                                break; // handler reassigned or cancelled the loop
                            }
                            if (proceed)
                            {
                                _pickupTimer = cfg.villagerPickupDurationSeconds;
                                SetState(VillagerState.CarryingResource);
                            }
                            else
                            {
                                _workTimer = WorkDuration(cfg); // nothing to carry: work on
                            }
                        }
                    }
                    break;

                case VillagerState.CarryingResource:
                    _pickupTimer -= dt;
                    if (_pickupTimer <= 0f)
                    {
                        SetState(VillagerState.ReturningToStorage);
                    }
                    break;

                case VillagerState.ReturningToStorage:
                    if (StepTowards(_storagePoint, cfg.villagerWalkSpeed * WorkSpeedMultiplier, dt))
                    {
                        GameEventLog.Append("villager_deposited_resource", name);
                        DepositHandler?.Invoke(this);
                        if (State == VillagerState.ReturningToStorage)
                        {
                            SetState(VillagerState.WalkingToTask);
                        }
                    }
                    break;

                case VillagerState.ReturningToLight:
                    TickReturningToLight(cfg, dt);
                    break;

                case VillagerState.Panicking:
                    TickPanicking(cfg, dt);
                    break;

                case VillagerState.Injured:
                    // Crawl toward the nearest light; reaching Safe means Resting.
                    StepTowards(DarknessEvaluator.NearestSafePoint(transform.position),
                        cfg.villagerWalkSpeed * cfg.villagerInjuredSpeedMultiplier, dt);
                    if (CurrentZone == LightZone.Safe)
                    {
                        _restTimer = cfg.villagerRestDurationSeconds;
                        SetState(VillagerState.Resting);
                    }
                    break;

                case VillagerState.Resting:
                    _restTimer -= dt;
                    if (_restTimer <= 0f)
                    {
                        SetState(VillagerState.Idle);
                    }
                    break;
            }
        }

        void UpdateFearAndDarkness(PrototypeConfig cfg, LightZone zone, bool fearTime, float dt)
        {
            if (fearTime && zone == LightZone.Dark)
            {
                float braveryScale = Mathf.Lerp(1f, cfg.braveFearMultiplier, Bravery);
                Fear = Mathf.Min(1f, Fear + cfg.villagerFearPerSecondInDark * braveryScale * dt);
                _timeInDark += dt;
            }
            else if (fearTime && zone == LightZone.Edge)
            {
                float braveryScale = Mathf.Lerp(1f, cfg.braveFearMultiplier, Bravery);
                Fear = Mathf.Min(1f, Fear + cfg.villagerFearPerSecondInEdge * braveryScale * dt);
                _timeInDark = 0f;
            }
            else
            {
                Fear = Mathf.Max(0f, Fear - cfg.villagerFearRecoveryPerSecond * dt);
                _timeInDark = 0f;
            }

            if (_timeInDark >= cfg.villagerMissingDarkSeconds)
            {
                GameEventLog.Append("villager_missing", name);
                SetState(VillagerState.Missing);
            }
            else if (_timeInDark >= cfg.villagerInjuredDarkSeconds
                     && State != VillagerState.Injured)
            {
                GameEventLog.Append("villager_injured_by_darkness", name);
                SetState(VillagerState.Injured);
            }
        }

        void UpdatePanicEntry(PrototypeConfig cfg, float dt)
        {
            if (Fear < cfg.villagerPanicFearThreshold || IsEscorted)
            {
                return;
            }
            switch (State)
            {
                case VillagerState.Idle:
                case VillagerState.AssignedToWork:
                case VillagerState.WalkingToTask:
                case VillagerState.Working:
                case VillagerState.CarryingResource:
                case VillagerState.ReturningToStorage:
                case VillagerState.ReturningToLight:
                    _panicDirTimer = 0f;
                    SetState(VillagerState.Panicking);
                    break;
            }
        }

        void UpdatePendingRecall(float dt)
        {
            if (!_recallOrdered || State == VillagerState.ReturningToLight)
            {
                return;
            }
            switch (State)
            {
                case VillagerState.Idle:
                case VillagerState.AssignedToWork:
                case VillagerState.WalkingToTask:
                case VillagerState.CarryingResource:
                case VillagerState.ReturningToStorage:
                    _recallDelay -= dt;
                    if (_recallDelay <= 0f)
                    {
                        EnterReturningToLight();
                    }
                    break;
                case VillagerState.Working:
                    if (Bravery < Config.braveryFinishWorkThreshold)
                    {
                        _recallDelay -= dt;
                        if (_recallDelay <= 0f)
                        {
                            EnterReturningToLight();
                        }
                    }
                    break;
            }
        }

        void TickReturningToLight(PrototypeConfig cfg, float dt)
        {
            float speed = cfg.villagerWalkSpeed
                          * (_bellBoosted ? cfg.bellRecallSpeedMultiplier : 1f);

            if (_escort != null)
            {
                // Rescued-follow: trail the hero, stop at follow distance.
                float dist = PlanarMotion.Distance(transform.position, _escort.position);
                if (dist > cfg.rescueFollowDistance)
                {
                    transform.position = PlanarMotion.StepAroundBuildings(
                        transform.position, _escort.position, speed, dt,
                        cfg.rescueFollowDistance, cfg.movementObstaclePadding, out _);
                }
                return;
            }

            StepTowards(DarknessEvaluator.NearestSafePoint(transform.position), speed, dt);
            if (CurrentZone == LightZone.Safe)
            {
                GameEventLog.Append("villager_reached_light", name);
                _recallOrdered = false;
                _bellBoosted = false;
                SetState(VillagerState.Idle);
            }
        }

        void TickPanicking(PrototypeConfig cfg, float dt)
        {
            _panicDirTimer -= dt;
            if (_panicDirTimer <= 0f)
            {
                _panicDirTimer = cfg.villagerPanicDirectionChangeSeconds;
                if (_rng == null)
                {
                    _rng = new System.Random(seed * 7919 + 13);
                }
                float angle = (float)(_rng.NextDouble() * Mathf.PI * 2.0);
                _panicDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            transform.position = PlanarMotion.MoveAroundBuildings(
                transform.position, _panicDir * cfg.villagerPanicSpeed * dt,
                cfg.movementObstaclePadding);

            // Panic breaks when fear falls low enough (safe light drains it fastest).
            if (Fear < cfg.villagerPanicFearThreshold * cfg.villagerPanicBreakFearFraction)
            {
                if (CurrentZone == LightZone.Safe)
                {
                    _recallOrdered = false;
                    _bellBoosted = false;
                    SetState(VillagerState.Idle);
                }
                else
                {
                    EnterReturningToLight();
                }
            }
        }

        void EnterReturningToLight()
        {
            _recallOrdered = false;
            SetState(VillagerState.ReturningToLight);
        }

        float WorkDuration(PrototypeConfig cfg)
        {
            return WorkDurationOverride > 0f ? WorkDurationOverride : cfg.villagerWorkDurationSeconds;
        }

        /// <summary>
        /// Steps toward a target, returns true when arrived. Villager foot travel routes
        /// around active building/construction footprints (main) AND wears desire paths
        /// while reading their speed bonus (P3-12) via
        /// <see cref="PlanarMotion.StepWornAroundBuildings"/>; with no footprints and no
        /// TrafficGrid/DesirePathSystem in the scene this is identical to
        /// <see cref="PlanarMotion.Step"/>.
        /// </summary>
        bool StepTowards(Vector3 target, float speed, float dt)
        {
            transform.position = PlanarMotion.StepWornAroundBuildings(
                transform.position, target, speed, dt, Config.arrivalRadius,
                Config.movementObstaclePadding, out bool arrived);
            return arrived;
        }

        void SetState(VillagerState next)
        {
            if (State == next)
            {
                return;
            }
            GameEventLog.Append("VillagerState", $"{name} {State}->{next}");
            State = next;
        }

        static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed * 2654435761u + 0x9E3779B9u;
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
