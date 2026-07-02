using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>
    /// The job layer on top of <see cref="VillagerAgent"/>: it decides WHERE the
    /// day loop runs and WHAT a completed work cycle actually yields, while the
    /// villager state machine keeps owning all movement, fear, panic, dusk recall
    /// and injury. Salvagers and Woodcutters drive the existing
    /// task-site → storage loop through the agent's WorkCycleHandler /
    /// DepositHandler hooks and deposit their carried load into the
    /// <see cref="ResourceLedger"/>. Tenders and Guards run short errand legs of
    /// their own, but only while the villager is Idle and un-recalled, so every
    /// override (recall, bell, panic, injury) wins automatically. All tunables
    /// live in <see cref="JobsConfig"/> (salvage yield/duration stay in
    /// <see cref="EconomyConfig"/>). Carrying is visible: a small placeholder
    /// prop child is toggled while a load is held. [ExecuteAlways] so EditMode
    /// tests get OnEnable hook wiring; Update only ticks in play mode with
    /// autoTick on.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VillagerAgent))]
    public class VillagerJobAgent : MonoBehaviour
    {
        enum TenderPhase
        {
            Monitor,
            ToStorage,
            ToLight,
            Refueling
        }

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        [Tooltip("Explicit work point override (woodcutter tree / guard post). Null = nearest tagged JobWorkPoint.")]
        public Transform workPoint;

        [SerializeField] VillagerJob job = VillagerJob.None;

        JobsConfig _config;
        VillagerAgent _villager;
        SalvageSite _targetSite;
        LightSource _tenderTarget;
        TenderPhase _tenderPhase = TenderPhase.Monitor;
        float _tenderWorkTimer;
        bool _guardAtPost;
        GameObject _carriedProp;

        /// <summary>Resource type of the held load (meaningful while CarriedAmount &gt; 0).</summary>
        public ResourceType CarriedType { get; private set; }

        /// <summary>Units currently physically carried (not yet in the ledger).</summary>
        public int CarriedAmount { get; private set; }

        public bool IsCarrying => CarriedAmount > 0;

        /// <summary>Salvage site this salvager is working, null when idle/other job.</summary>
        public SalvageSite TargetSite => _targetSite;

        /// <summary>True while a guard is standing within post radius during Night.</summary>
        public bool IsAtGuardPost => _guardAtPost;

        /// <summary>The placeholder carried-prop child, null until first shown.</summary>
        public GameObject CarriedPropInstance => _carriedProp;

        public VillagerAgent Villager
        {
            get
            {
                if (_villager == null)
                {
                    _villager = GetComponent<VillagerAgent>();
                }
                return _villager;
            }
        }

        public JobsConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = JobsConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>Salvage tunables stay economy-owned; tests inject via ResourceLedger.Config.</summary>
        EconomyConfig Economy => ResourceLedger.Config;

        /// <summary>Current job. Setting it abandons the previous job's activity and logs.</summary>
        public VillagerJob Job
        {
            get { return job; }
            set { SetJob(value); }
        }

        /// <summary>Assigns a job, abandoning any in-flight activity of the old one.</summary>
        public void SetJob(VillagerJob next)
        {
            if (job == next)
            {
                return;
            }
            AbandonActivity();
            job = next;
            var v = Villager;
            if (v != null)
            {
                v.WorkSpeedMultiplier = Config.SpeedMultiplier(job);
                if (job == VillagerJob.Builder || job == VillagerJob.Tender
                    || job == VillagerJob.Guard)
                {
                    v.CancelWork(); // these jobs never use the generic day loop
                }
            }
            GameEventLog.Append("job", $"{name} assigned {VillagerJobs.Id(job)}");
        }

        void OnEnable()
        {
            var v = Villager;
            if (v != null)
            {
                v.WorkCycleHandler = HandleWorkCycle;
                v.DepositHandler = HandleDeposit;
                v.WorkSpeedMultiplier = Config.SpeedMultiplier(job);
            }
        }

        void OnDisable()
        {
            var v = _villager;
            if (v != null)
            {
                v.WorkCycleHandler = null;
                v.DepositHandler = null;
                v.WorkSpeedMultiplier = 1f;
                v.WorkDurationOverride = -1f;
            }
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            var v = Villager;
            if (v == null || dt <= 0f)
            {
                return;
            }

            UpdateInterrupts(v);
            UpdateCarriedProp();

            if (!IsJobWorkable(v))
            {
                return; // injured/panicking/recalled etc.: the job is suspended
            }

            switch (job)
            {
                case VillagerJob.Salvager:
                    TickSalvager(v);
                    break;
                case VillagerJob.Woodcutter:
                    TickWoodcutter(v);
                    break;
                case VillagerJob.Tender:
                    TickTender(v, dt);
                    break;
                case VillagerJob.Guard:
                    TickGuard(v, dt);
                    break;
                // None and Builder idle gracefully (Builder work sites arrive with
                // construction in P2-03).
            }
        }

        // ---- Salvager -------------------------------------------------------

        void TickSalvager(VillagerAgent v)
        {
            switch (v.State)
            {
                case VillagerState.Idle:
                    if (!IsWorkPhase())
                    {
                        return;
                    }
                    var site = NearestActiveSite();
                    if (site != null)
                    {
                        StartSalvageTrip(v, site);
                    }
                    break;

                case VillagerState.WalkingToTask:
                    // Mid-route depletion (someone stripped the site first): repath.
                    if (_targetSite == null || _targetSite.IsExhausted)
                    {
                        RepathOrIdle(v);
                    }
                    break;
            }
        }

        void StartSalvageTrip(VillagerAgent v, SalvageSite site)
        {
            _targetSite = site;
            v.WorkDurationOverride = Economy.salvageWorkDurationSeconds;
            Vector3 sitePos = site.transform.position;
            v.AssignWork(sitePos, NearestStoragePoint(sitePos));
        }

        /// <summary>
        /// Repaths to the next non-depleted site, or idles when the wreck is bare.
        /// Always changes the villager's state; returns false so the work-cycle
        /// hook never carries from a depleted site.
        /// </summary>
        bool RepathOrIdle(VillagerAgent v)
        {
            var next = NearestActiveSite();
            if (next == null)
            {
                _targetSite = null;
                v.CancelWork();
                GameEventLog.Append("job", $"{name} idle (no salvage left)");
                return false;
            }
            if (next != _targetSite)
            {
                GameEventLog.Append("job", $"{name} repath -> {next.name}");
            }
            StartSalvageTrip(v, next);
            return false;
        }

        SalvageSite NearestActiveSite()
        {
            // A 0-yield config means every cycle would extract nothing —
            // treat the whole wreck as depleted rather than loop forever
            // (SalvageSite.TryHarvestCycle can return true with amount == 0).
            if (Mathf.Min(Economy.salvageYieldPerCycle, Config.carryCapacity) <= 0)
            {
                return null;
            }

            var sites = SalvageSite.Active;
            SalvageSite best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || site.IsExhausted)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, site.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = site;
                }
            }
            return best;
        }

        // ---- Woodcutter -----------------------------------------------------

        void TickWoodcutter(VillagerAgent v)
        {
            if (v.State != VillagerState.Idle || !IsWorkPhase())
            {
                return;
            }
            var point = ResolveWorkPoint(VillagerJob.Woodcutter);
            if (point == null || Mathf.Min(Config.woodcutterYieldPerCycle, Config.carryCapacity) <= 0)
            {
                return; // no tree assigned (or nothing to gain): idle gracefully
            }
            v.WorkDurationOverride = Config.woodcutterWorkDurationSeconds;
            v.AssignWork(point.position, NearestStoragePoint(point.position));
        }

        // ---- Work-loop hooks (salvager + woodcutter) ------------------------

        bool HandleWorkCycle(VillagerAgent v)
        {
            switch (job)
            {
                case VillagerJob.Salvager:
                    if (_targetSite == null || _targetSite.IsExhausted)
                    {
                        return RepathOrIdle(v);
                    }
                    int want = Mathf.Min(Economy.salvageYieldPerCycle, Config.carryCapacity);
                    ResourceType type = default;
                    int amount = 0;
                    for (int i = 0; i < ResourceTypes.Count && amount <= 0; i++)
                    {
                        var candidate = (ResourceType)i;
                        if (_targetSite.Remaining(candidate) > 0)
                        {
                            type = candidate;
                            amount = _targetSite.Harvest(candidate, want);
                        }
                    }
                    if (amount <= 0)
                    {
                        // Covers exhaustion AND the 0-yield trap: nothing extracted
                        // means this site is done for us.
                        return RepathOrIdle(v);
                    }
                    CarriedType = type;
                    CarriedAmount = amount;
                    return true;

                case VillagerJob.Woodcutter:
                    int yield = Mathf.Min(Config.woodcutterYieldPerCycle, Config.carryCapacity);
                    if (yield <= 0)
                    {
                        v.CancelWork();
                        return false;
                    }
                    CarriedType = ResourceType.Wood;
                    CarriedAmount = yield;
                    return true;

                default:
                    return true; // not a job-driven loop: legacy behaviour
            }
        }

        void HandleDeposit(VillagerAgent v)
        {
            if (CarriedAmount > 0)
            {
                ResourceLedger.Add(CarriedType, CarriedAmount, VillagerJobs.Id(job));
                CarriedAmount = 0;
            }

            switch (job)
            {
                case VillagerJob.Salvager:
                    if (_targetSite == null || _targetSite.IsExhausted)
                    {
                        RepathOrIdle(v); // next trip goes somewhere useful
                    }
                    break;
                case VillagerJob.Woodcutter:
                    if (ResolveWorkPoint(VillagerJob.Woodcutter) == null)
                    {
                        v.CancelWork(); // tree gone: idle instead of hauling air
                    }
                    break;
            }
        }

        // ---- Tender ---------------------------------------------------------

        void TickTender(VillagerAgent v, float dt)
        {
            if (v.State != VillagerState.Idle)
            {
                return; // only errands from Idle; interrupts handled in UpdateInterrupts
            }

            switch (_tenderPhase)
            {
                case TenderPhase.Monitor:
                    var target = FindLightNeedingFuel();
                    if (target == null)
                    {
                        return;
                    }
                    if (!ResourceLedger.CanAfford(ResourceType.Wood, Config.tenderWoodCostPerRefuel))
                    {
                        return; // wait until the ledger has wood
                    }
                    _tenderTarget = target;
                    _tenderPhase = TenderPhase.ToStorage;
                    break;

                case TenderPhase.ToStorage:
                    if (_tenderTarget == null || !LightNeedsFuel(_tenderTarget))
                    {
                        ResetTender(); // someone else fed it meanwhile
                        break;
                    }
                    if (StepSelf(v, NearestStoragePoint(transform.position), dt))
                    {
                        if (ResourceLedger.TryConsume(
                                ResourceType.Wood, Config.tenderWoodCostPerRefuel, "tender refuel"))
                        {
                            CarriedType = ResourceType.Wood;
                            CarriedAmount = Config.tenderWoodCostPerRefuel;
                            _tenderPhase = TenderPhase.ToLight;
                        }
                        else
                        {
                            ResetTender(); // wood ran out under us
                        }
                    }
                    break;

                case TenderPhase.ToLight:
                    if (_tenderTarget == null)
                    {
                        AbortTenderErrand();
                        break;
                    }
                    if (StepSelf(v, _tenderTarget.transform.position, dt))
                    {
                        _tenderWorkTimer = Config.tenderRefuelWorkSeconds;
                        _tenderPhase = TenderPhase.Refueling;
                    }
                    break;

                case TenderPhase.Refueling:
                    _tenderWorkTimer -= dt;
                    if (_tenderWorkTimer <= 0f)
                    {
                        if (_tenderTarget != null)
                        {
                            _tenderTarget.Ignite(Config.tenderRefuelFuelSeconds);
                            GameEventLog.Append("job",
                                $"{name} refueled {_tenderTarget.name} (+{Config.tenderRefuelFuelSeconds:F0}s fuel)");
                        }
                        CarriedAmount = 0;
                        ResetTender();
                    }
                    break;
            }
        }

        LightSource FindLightNeedingFuel()
        {
            var sources = DarknessEvaluator.Sources;
            LightSource best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !LightNeedsFuel(source))
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, source.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = source;
                }
            }
            return best;
        }

        bool LightNeedsFuel(LightSource source)
        {
            if (source == null || source.HasInfiniteFuel)
            {
                return false;
            }
            // LightSource has no fuel capacity of its own, so the tender's idea of
            // "full" comes from config: fraction = fuelSeconds / tenderTargetFuelSeconds.
            float fraction = source.fuelSeconds / Mathf.Max(Config.tenderTargetFuelSeconds, 0.01f);
            return fraction < Config.tenderRefuelThresholdFraction;
        }

        void ResetTender()
        {
            _tenderTarget = null;
            _tenderPhase = TenderPhase.Monitor;
            _tenderWorkTimer = 0f;
        }

        /// <summary>Aborts a fetch mid-errand: the held wood goes back to the ledger.</summary>
        void AbortTenderErrand()
        {
            if (CarriedAmount > 0)
            {
                ResourceLedger.Add(CarriedType, CarriedAmount, "tender errand aborted");
                CarriedAmount = 0;
            }
            ResetTender();
        }

        // ---- Guard ----------------------------------------------------------

        void TickGuard(VillagerAgent v, float dt)
        {
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            var post = ResolveWorkPoint(VillagerJob.Guard);
            if (phase != DayPhase.Night || post == null)
            {
                _guardAtPost = false;
                return; // off duty: idles near camp like everyone else
            }
            if (v.State != VillagerState.Idle)
            {
                return;
            }

            float postRadius = Mathf.Max(Config.guardPostRadius, v.Config.arrivalRadius);
            if (PlanarMotion.Distance(transform.position, post.position) <= postRadius)
            {
                if (!_guardAtPost)
                {
                    _guardAtPost = true;
                    GameEventLog.Append("job", $"{name} guard_took_post");
                }
                return; // presence only; combat is a later task
            }
            _guardAtPost = false;
            StepSelf(v, post.position, dt);
        }

        // ---- Shared helpers -------------------------------------------------

        /// <summary>Recall, panic, injury, rescue and death all suspend the job.</summary>
        static bool IsJobWorkable(VillagerAgent v)
        {
            if (v.IsRecallOrdered)
            {
                return false;
            }
            switch (v.State)
            {
                case VillagerState.ReturningToLight:
                case VillagerState.Panicking:
                case VillagerState.Injured:
                case VillagerState.Resting:
                case VillagerState.Missing:
                case VillagerState.Dead:
                    return false;
                default:
                    return true;
            }
        }

        static bool IsWorkPhase()
        {
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            return phase == DayPhase.Day || phase == DayPhase.Dawn;
        }

        Transform ResolveWorkPoint(VillagerJob forJob)
        {
            if (workPoint != null)
            {
                return workPoint;
            }
            var registered = JobWorkPoint.Nearest(forJob, transform.position);
            return registered != null ? registered.transform : null;
        }

        Vector3 NearestStoragePoint(Vector3 from)
        {
            var piles = ResourceLedger.StoragePiles;
            Vector3 best = Vector3.zero;
            float bestDist = float.MaxValue;
            for (int i = 0; i < piles.Count; i++)
            {
                var pile = piles[i];
                if (pile == null)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(from, pile.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = pile.transform.position;
                }
            }
            if (bestDist < float.MaxValue)
            {
                return best;
            }
            // No pile built yet: deposit at the camp's nearest safe light — the
            // ledger's base capacity is loose ground stacking.
            return DarknessEvaluator.NearestSafePoint(from);
        }

        /// <summary>Moves the villager on a job errand leg. Returns true on arrival.</summary>
        bool StepSelf(VillagerAgent v, Vector3 target, float dt)
        {
            float speed = v.Config.villagerWalkSpeed * Config.SpeedMultiplier(job);
            transform.position = PlanarMotion.Step(
                transform.position, target, speed, dt, v.Config.arrivalRadius, out bool arrived);
            return arrived;
        }

        /// <summary>
        /// Watches for job interruptions each tick: a hauler yanked out of the work
        /// loop drops its load (fearful villagers abandon tools, spec §6); a tender
        /// interrupted mid-errand returns the fetched wood to the ledger so the
        /// accounting stays closed.
        /// </summary>
        void UpdateInterrupts(VillagerAgent v)
        {
            if (job == VillagerJob.Tender)
            {
                if (_tenderPhase != TenderPhase.Monitor && !IsJobWorkable(v))
                {
                    AbortTenderErrand();
                }
                return;
            }

            if (CarriedAmount <= 0)
            {
                return;
            }
            switch (v.State)
            {
                case VillagerState.Working:
                case VillagerState.CarryingResource:
                case VillagerState.ReturningToStorage:
                    break; // still hauling
                default:
                    GameEventLog.Append("job",
                        $"{name} dropped {ResourceTypes.Id(CarriedType)} x{CarriedAmount}");
                    CarriedAmount = 0;
                    break;
            }
        }

        /// <summary>Abandons whatever the current job was doing (job change / unassign).</summary>
        void AbandonActivity()
        {
            var v = Villager;
            if (job == VillagerJob.Tender)
            {
                AbortTenderErrand();
            }
            else if (CarriedAmount > 0)
            {
                GameEventLog.Append("job",
                    $"{name} dropped {ResourceTypes.Id(CarriedType)} x{CarriedAmount}");
                CarriedAmount = 0;
            }
            _targetSite = null;
            _guardAtPost = false;
            if (v != null)
            {
                v.WorkDurationOverride = -1f;
                if (job == VillagerJob.Salvager || job == VillagerJob.Woodcutter)
                {
                    v.CancelWork();
                }
            }
        }

        /// <summary>Placeholder carrying visual: a small cube child while a load is held.</summary>
        void UpdateCarriedProp()
        {
            bool show = CarriedAmount > 0;
            if (show && _carriedProp == null)
            {
                _carriedProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _carriedProp.name = "CarriedProp";
                var collider = _carriedProp.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(collider);
                    }
                    else
                    {
                        DestroyImmediate(collider);
                    }
                }
                _carriedProp.transform.SetParent(transform, false);
                _carriedProp.transform.localPosition = new Vector3(0f, 1.2f, 0.35f);
                _carriedProp.transform.localScale = Vector3.one * 0.3f;
            }
            if (_carriedProp != null && _carriedProp.activeSelf != show)
            {
                _carriedProp.SetActive(show);
            }
        }
    }
}
