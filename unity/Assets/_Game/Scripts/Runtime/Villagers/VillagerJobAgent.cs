using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Rendering;
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
    /// <see cref="ResourceLedger"/>. Builders, Tenders and Guards run short errand
    /// legs of their own, but only while the villager is Idle and un-recalled, so
    /// every override (recall, bell, panic, injury) wins automatically. The
    /// Builder serves <see cref="ConstructionSite"/>s: per haul it walks to
    /// storage then to the site, and only <see cref="ConstructionSite.DeliverResource"/>
    /// at the site consumes the ledger — the fetch leg reserves nothing, so an
    /// interrupted builder never strands or double-spends wood. All tunables
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

        enum BuilderPhase
        {
            Seek,
            ToStorage,
            ToSite,
            Working
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
        ConstructionSite _buildTarget;
        BuilderPhase _builderPhase = BuilderPhase.Seek;
        ResourceType _buildFetchType;
        bool _buildFetching;
        bool _guardAtPost;
        ProductionBuilding _productionTarget;
        bool _staffingProduction;
        GameObject _carriedProp;
        ResourceType _carriedPropType;

        static readonly Dictionary<ResourceType, Material> CarriedPropMaterials =
            new Dictionary<ResourceType, Material>();

        /// <summary>Resource type of the held load (meaningful while CarriedAmount &gt; 0).</summary>
        public ResourceType CarriedType { get; private set; }

        /// <summary>Units currently physically carried (not yet in the ledger).</summary>
        public int CarriedAmount { get; private set; }

        public bool IsCarrying => CarriedAmount > 0;

        /// <summary>Salvage site this salvager is working, null when idle/other job.</summary>
        public SalvageSite TargetSite => _targetSite;

        /// <summary>Construction site this builder is serving, null when idle/other job.</summary>
        public ConstructionSite BuildTarget => _buildTarget;

        /// <summary>True while a guard is standing within post radius during Night.</summary>
        public bool IsAtGuardPost => _guardAtPost;

        /// <summary>Production building this villager is staffing, null when idle/other job.</summary>
        public ProductionBuilding ProductionTarget => _productionTarget;

        /// <summary>True while this villager is on its production building's work slot.</summary>
        public bool IsStaffingProduction => _staffingProduction;

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
                    || job == VillagerJob.Guard || VillagerJobs.IsProduction(job))
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

            if (v.SanityWorkEfficiency <= 0f)
            {
                // Insane: down tools entirely until recovered (P3-03 stop-work state).
                UnstaffProduction();
                if (v.HasWorkAssignment)
                {
                    v.CancelWork();
                }
                return;
            }

            if (!IsJobWorkable(v))
            {
                UnstaffProduction(); // injured/panicking/recalled etc.: leave the work slot
                return; // the job is suspended
            }

            switch (job)
            {
                case VillagerJob.Salvager:
                    TickSalvager(v);
                    break;
                case VillagerJob.Builder:
                    TickBuilder(v, dt);
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
                case VillagerJob.Farmer:
                case VillagerJob.Herder:
                case VillagerJob.Charcoaler:
                case VillagerJob.Smith:
                    TickProduction(v, dt);
                    break;
                // None idles gracefully.
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

        // ---- Builder ---------------------------------------------------------

        /// <summary>
        /// The builder errand loop (mirrors the tender: legs only run while the
        /// villager is Idle, so recall/panic/injury overrides win automatically).
        /// One haul = walk to storage, walk to the site, then
        /// <see cref="ConstructionSite.DeliverResource"/> pays for the batch out
        /// of the ledger AT THE SITE — the storage leg reserves nothing, so an
        /// aborted trip has consumed nothing and there is no refund to manage and
        /// no way to double-spend. Sites needing only work get a direct walk and
        /// per-tick <see cref="ConstructionSite.ApplyWork"/>.
        /// </summary>
        void TickBuilder(VillagerAgent v, float dt)
        {
            if (v.State != VillagerState.Idle)
            {
                return; // only errands from Idle; interrupts handled in UpdateInterrupts
            }

            switch (_builderPhase)
            {
                case BuilderPhase.Seek:
                    if (!IsWorkPhase())
                    {
                        return;
                    }
                    var site = NearestServeableSite(out var fetchType, out bool needsFetch);
                    if (site == null)
                    {
                        return; // nothing to build (or nothing affordable): idle
                    }
                    _buildTarget = site;
                    _buildFetchType = fetchType;
                    _buildFetching = needsFetch;
                    _builderPhase = needsFetch ? BuilderPhase.ToStorage : BuilderPhase.ToSite;
                    break;

                case BuilderPhase.ToStorage:
                    if (_buildTarget == null || _buildTarget.IsComplete
                        || !_buildTarget.NeedsMaterials)
                    {
                        ResetBuilder(); // delivered/finished under us: re-seek
                        break;
                    }
                    if (StepSelf(v, NearestStoragePoint(transform.position), dt))
                    {
                        _builderPhase = BuilderPhase.ToSite; // picked up (paid at the site)
                    }
                    break;

                case BuilderPhase.ToSite:
                    if (_buildTarget == null || _buildTarget.IsComplete)
                    {
                        ResetBuilder();
                        break;
                    }
                    if (StepSelf(v, _buildTarget.transform.position, dt))
                    {
                        if (_buildFetching)
                        {
                            _buildFetching = false;
                            int accepted = _buildTarget.DeliverResource(
                                _buildFetchType, Config.carryCapacity);
                            if (accepted > 0)
                            {
                                GameEventLog.Append("job",
                                    $"{name} delivered {ResourceTypes.Id(_buildFetchType)} "
                                    + $"x{accepted} -> {_buildTarget.Type.id}");
                            }
                        }
                        if (_buildTarget != null && _buildTarget.NeedsWork
                            && Config.builderWorkPerSecond > 0f)
                        {
                            _builderPhase = BuilderPhase.Working;
                        }
                        else
                        {
                            ResetBuilder(); // next haul (or next site) via Seek
                        }
                    }
                    break;

                case BuilderPhase.Working:
                    if (_buildTarget == null || _buildTarget.IsComplete
                        || _buildTarget.NeedsMaterials)
                    {
                        ResetBuilder();
                        break;
                    }
                    float applied = _buildTarget.ApplyWork(dt * Config.builderWorkPerSecond);
                    if (_buildTarget.IsComplete)
                    {
                        GameEventLog.Append("job", $"{name} built {_buildTarget.Type.id}");
                        ResetBuilder();
                    }
                    else if (applied <= 0f)
                    {
                        ResetBuilder(); // 0-rate trap: never stand hammering nothing
                    }
                    break;
            }
        }

        /// <summary>
        /// Nearest construction site the builder can serve right now: one owed
        /// work, or one owed materials of a type the ledger has in stock (an
        /// unaffordable need is skipped — the builder never hauls air). Iterates
        /// the Active registry by index; the registry only mutates on completion,
        /// which never happens during this scan.
        /// </summary>
        ConstructionSite NearestServeableSite(out ResourceType fetchType, out bool needsFetch)
        {
            fetchType = default;
            needsFetch = false;
            var sites = ConstructionSite.Active;
            ConstructionSite best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || site.IsComplete)
                {
                    continue;
                }
                bool fetch = false;
                ResourceType type = default;
                if (site.NeedsMaterials)
                {
                    if (!TryPickFetchType(site, out type))
                    {
                        continue; // needs materials the ledger cannot supply yet
                    }
                    fetch = true;
                }
                else if (!site.NeedsWork || Config.builderWorkPerSecond <= 0f)
                {
                    continue; // nothing owed, or a 0-rate builder can't serve it
                }
                float dist = PlanarMotion.Distance(transform.position, site.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = site;
                    fetchType = type;
                    needsFetch = fetch;
                }
            }
            return best;
        }

        /// <summary>First still-needed resource type the ledger currently stocks.</summary>
        static bool TryPickFetchType(ConstructionSite site, out ResourceType type)
        {
            for (int i = 0; i < ResourceTypes.Count; i++)
            {
                var candidate = (ResourceType)i;
                if (site.RemainingNeed(candidate) > 0 && ResourceLedger.Get(candidate) > 0)
                {
                    type = candidate;
                    return true;
                }
            }
            type = default;
            return false;
        }

        /// <summary>
        /// Drops the builder errand. Nothing to refund by design: the ledger is
        /// only ever touched by DeliverResource at the site.
        /// </summary>
        void ResetBuilder()
        {
            _buildTarget = null;
            _buildFetching = false;
            _builderPhase = BuilderPhase.Seek;
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
                    int want = ScaleBySanity(
                        Mathf.Min(Economy.salvageYieldPerCycle, Config.carryCapacity), v);
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
                    int yield = ScaleBySanity(
                        Mathf.Min(Config.woodcutterYieldPerCycle, Config.carryCapacity), v);
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
                    if (_tenderTarget == null)
                    {
                        // Target destroyed mid-refuel: the fetched wood was never
                        // burned, so it goes back to the ledger, not into the void.
                        AbortTenderErrand();
                        break;
                    }
                    _tenderWorkTimer -= dt;
                    if (_tenderWorkTimer <= 0f)
                    {
                        _tenderTarget.Ignite(Config.tenderRefuelFuelSeconds);
                        GameEventLog.Append("job",
                            $"{name} refueled {_tenderTarget.name} (+{Config.tenderRefuelFuelSeconds:F0}s fuel)");
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

        // ---- Production (Farmer / Herder / Charcoaler / Smith) --------------

        /// <summary>
        /// The production errand (P3-04): during a work phase the villager walks to
        /// the nearest matching <see cref="ProductionBuilding"/> and staffs its work
        /// slot; while present it registers as a live worker so the building's daily
        /// <see cref="ProductionBuilding.AdvanceDay"/> counts it. Presence-only, like
        /// the guard — the cycle math and yields live on the building/economy, never
        /// here. Off-shift (night/dusk) or with no matching building it stands down.
        /// </summary>
        void TickProduction(VillagerAgent v, float dt)
        {
            if (!IsWorkPhase())
            {
                UnstaffProduction(); // off-shift: leave the field until morning
                return;
            }

            var building = NearestProductionBuilding(job);
            if (building == null)
            {
                UnstaffProduction();
                return; // no matching building built yet: idle gracefully
            }

            if (building != _productionTarget)
            {
                UnstaffProduction();
                _productionTarget = building;
            }

            if (v.State != VillagerState.Idle)
            {
                return; // an override owns the villager; resume when Idle again
            }

            float staffRadius = Mathf.Max(Config.productionStaffRadius, v.Config.arrivalRadius);
            if (PlanarMotion.Distance(transform.position, building.transform.position) <= staffRadius)
            {
                if (!_staffingProduction)
                {
                    _staffingProduction = true;
                    building.AddWorker(this);
                    GameEventLog.Append("job",
                        $"{name} staffs {building.BuildingId}");
                }
                return;
            }

            // Walking out to the building: not counted as a worker yet.
            if (_staffingProduction)
            {
                building.RemoveWorker(this);
                _staffingProduction = false;
            }
            StepSelf(v, building.transform.position, dt);
        }

        ProductionBuilding NearestProductionBuilding(VillagerJob forJob)
        {
            string wantedId = VillagerJobs.ProductionBuildingId(forJob);
            if (string.IsNullOrEmpty(wantedId))
            {
                return null;
            }
            var buildings = ProductionBuilding.Active;
            ProductionBuilding best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null || b.BuildingId != wantedId)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, b.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = b;
                }
            }
            return best;
        }

        /// <summary>Leaves the current production work slot (deregisters as a worker).</summary>
        void UnstaffProduction()
        {
            if (_productionTarget != null && _staffingProduction)
            {
                _productionTarget.RemoveWorker(this);
            }
            _staffingProduction = false;
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

        /// <summary>
        /// Scales a work-cycle yield by the villager's sanity work efficiency (P3-03).
        /// Efficiency 1 leaves it unchanged; a shaken mind produces less. Efficiency 0
        /// never reaches here — the Tick guard downs tools before a cycle completes.
        /// </summary>
        static int ScaleBySanity(int amount, VillagerAgent v)
        {
            if (amount <= 0)
            {
                return amount;
            }
            float eff = Mathf.Clamp01(v.SanityWorkEfficiency);
            if (eff >= 1f)
            {
                return amount;
            }
            return Mathf.Max(0, Mathf.RoundToInt(amount * eff));
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

        /// <summary>
        /// Moves the villager on a job errand leg. Returns true on arrival. Job errands
        /// route around active building/construction footprints (main) AND wear desire
        /// paths while reading their speed bonus (P3-12) via
        /// <see cref="PlanarMotion.StepWornAroundBuildings"/>; identical to
        /// <see cref="PlanarMotion.Step"/> when no footprints and no
        /// TrafficGrid/DesirePathSystem are present.
        /// </summary>
        bool StepSelf(VillagerAgent v, Vector3 target, float dt)
        {
            float speed = v.Config.villagerWalkSpeed * Config.SpeedMultiplier(job);
            transform.position = PlanarMotion.StepWornAroundBuildings(
                transform.position, target, speed, dt, v.Config.arrivalRadius,
                v.Config.movementObstaclePadding, out bool arrived);
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

            if (job == VillagerJob.Builder)
            {
                if (_builderPhase != BuilderPhase.Seek && !IsJobWorkable(v))
                {
                    // Nothing is held or reserved mid-errand (delivery pays at the
                    // site), so an interrupted builder just drops the plan.
                    ResetBuilder();
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
            ResetBuilder();
            UnstaffProduction();
            _productionTarget = null;
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

        /// <summary>
        /// Placeholder carrying visual: a small rounded bundle child while a load
        /// is held. The builder's site-bound leg also shows it — the materials are
        /// only paid for on arrival, but the haul should read as a haul.
        /// </summary>
        void UpdateCarriedProp()
        {
            bool show = CarriedAmount > 0
                        || (_buildFetching && _builderPhase == BuilderPhase.ToSite);
            if (show && _carriedProp == null)
            {
                _carriedProp = CreateCarriedProp(CarriedType);
                _carriedPropType = CarriedType;
            }
            else if (show && _carriedPropType != CarriedType)
            {
                DestroyCarriedProp();
                _carriedProp = CreateCarriedProp(CarriedType);
                _carriedPropType = CarriedType;
            }
            if (_carriedProp != null && _carriedProp.activeSelf != show)
            {
                _carriedProp.SetActive(show);
            }
        }

        GameObject CreateCarriedProp(ResourceType type)
        {
            var root = new GameObject("CarriedProp");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-0.24f, 1.04f, 0.26f);
            root.transform.localRotation = Quaternion.Euler(0f, -20f, -8f);
            root.transform.localScale = Vector3.one;

            switch (type)
            {
                case ResourceType.Wood:
                    AddCarriedPiece(root.transform, "Log_A", PrimitiveType.Capsule,
                        new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.26f, 0.08f),
                        Quaternion.Euler(0f, 0f, 90f), type);
                    AddCarriedPiece(root.transform, "Log_B", PrimitiveType.Capsule,
                        new Vector3(0f, -0.02f, 0.06f), new Vector3(0.07f, 0.24f, 0.07f),
                        Quaternion.Euler(0f, 8f, 90f), type);
                    AddCarriedPiece(root.transform, "Log_C", PrimitiveType.Capsule,
                        new Vector3(0f, -0.04f, -0.06f), new Vector3(0.07f, 0.22f, 0.07f),
                        Quaternion.Euler(0f, -8f, 90f), type);
                    break;
                case ResourceType.Stone:
                case ResourceType.ScrapIron:
                case ResourceType.RelicFragments:
                    AddCarriedPiece(root.transform, "Chunk_A", PrimitiveType.Sphere,
                        new Vector3(-0.08f, 0f, 0f), new Vector3(0.18f, 0.13f, 0.16f),
                        Quaternion.identity, type);
                    AddCarriedPiece(root.transform, "Chunk_B", PrimitiveType.Sphere,
                        new Vector3(0.08f, 0.02f, 0.04f), new Vector3(0.15f, 0.12f, 0.14f),
                        Quaternion.identity, type);
                    AddCarriedPiece(root.transform, "Chunk_C", PrimitiveType.Sphere,
                        new Vector3(0f, 0.08f, -0.05f), new Vector3(0.13f, 0.10f, 0.12f),
                        Quaternion.identity, type);
                    break;
                default:
                    AddCarriedPiece(root.transform, "Bundle", PrimitiveType.Sphere,
                        Vector3.zero, new Vector3(0.28f, 0.20f, 0.24f),
                        Quaternion.identity, type);
                    AddCarriedPiece(root.transform, "BundleTie", PrimitiveType.Capsule,
                        new Vector3(0f, 0.08f, 0f), new Vector3(0.035f, 0.16f, 0.035f),
                        Quaternion.Euler(0f, 0f, 90f), ResourceType.Wood);
                    break;
            }

            return root;
        }

        void AddCarriedPiece(
            Transform parent, string name, PrimitiveType primitive, Vector3 localPosition,
            Vector3 localScale, Quaternion localRotation, ResourceType materialType)
        {
            var piece = GameObject.CreatePrimitive(primitive);
            piece.name = name;
            RemoveCollider(piece);
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = localRotation;
            piece.transform.localScale = localScale;

            var renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CarriedMaterial(materialType);
            }
        }

        void DestroyCarriedProp()
        {
            if (_carriedProp == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(_carriedProp);
            }
            else
            {
                DestroyImmediate(_carriedProp);
            }
            _carriedProp = null;
        }

        static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        static Material CarriedMaterial(ResourceType type)
        {
            if (CarriedPropMaterials.TryGetValue(type, out var cached) && cached != null)
            {
                return cached;
            }

            var material = AbbeyMaterialFactory.CreateLit(
                $"Abbey_Carried_{ResourceTypes.Id(type)}", CarriedColor(type), smoothness: 0.08f);
            CarriedPropMaterials[type] = material;
            return material;
        }

        static Color CarriedColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:
                    return new Color(0.42f, 0.22f, 0.10f);
                case ResourceType.Food:
                    return new Color(0.60f, 0.46f, 0.22f);
                case ResourceType.Oil:
                    return new Color(0.12f, 0.10f, 0.08f);
                case ResourceType.Candles:
                    return new Color(0.88f, 0.80f, 0.58f);
                case ResourceType.Stone:
                    return new Color(0.47f, 0.48f, 0.44f);
                case ResourceType.ScrapIron:
                    return new Color(0.30f, 0.31f, 0.33f);
                case ResourceType.Medicine:
                    return new Color(0.28f, 0.47f, 0.34f);
                case ResourceType.RelicFragments:
                    return new Color(0.78f, 0.64f, 0.25f);
                default:
                    return Color.gray;
            }
        }
    }
}
