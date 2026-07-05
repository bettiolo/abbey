using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Nightmares;
using Abbey.Settlement;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Island
{
    /// <summary>Where an expedition is in its out-and-back day trip.</summary>
    public enum ExpeditionPhase
    {
        Outbound,   // walking to the POI
        Surveying,  // at the POI, resolving the reward
        Returning,  // walking home
        Done,       // back in the light
        CaughtOut   // dusk fell before it got home — stranded in the dark
    }

    /// <summary>
    /// One daytime expedition (P3-13). A party of villagers walks from
    /// <see cref="Home"/> out to a hidden <see cref="Target"/>, surveys it (which reveals
    /// the POI and applies its reward), then walks home — all before dusk, or the party is
    /// caught out in the dark. Movement routes through <see cref="Abbey.Core.PlanarMotion.StepWorn"/>
    /// so foot traffic wears desire paths out to the POIs (P3-12). Plain object; the
    /// <see cref="ExplorationSystem"/> drives <see cref="Tick"/> and owns the reward.
    /// </summary>
    public class Expedition
    {
        public PointOfInterest Target { get; }
        public Vector3 Home { get; }
        public IReadOnlyList<VillagerAgent> Party { get; }
        public ExpeditionPhase Phase { get; private set; } = ExpeditionPhase.Outbound;

        /// <summary>Straight-line one-way travel estimate at the config speed (debug/UI).</summary>
        public float OneWayTravelSeconds { get; }

        float _surveyTimer;
        readonly IslandConfig _config;

        public Expedition(PointOfInterest target, IReadOnlyList<VillagerAgent> party,
            Vector3 home, IslandConfig config)
        {
            Target = target;
            Party = party;
            Home = home;
            _config = config;
            float dist = PlanarMotion.Distance(home, target.position);
            OneWayTravelSeconds = dist / Mathf.Max(0.01f, config.expeditionTravelSpeed);
        }

        public bool IsActive => Phase != ExpeditionPhase.Done && Phase != ExpeditionPhase.CaughtOut;

        /// <summary>
        /// Advances the party. Returns the phase entered this tick when it changed to a
        /// terminal-ish milestone (Surveying start / Returning start / Done), else the
        /// current phase. Resolution of the POI is the caller's job (via a milestone).
        /// </summary>
        public ExpeditionPhase Tick(float dt)
        {
            if (dt <= 0f || !IsActive)
            {
                return Phase;
            }
            var cfg = _config;
            switch (Phase)
            {
                case ExpeditionPhase.Outbound:
                    if (StepPartyTo(Target.position, cfg, dt))
                    {
                        _surveyTimer = cfg.poiResolveSeconds;
                        Phase = ExpeditionPhase.Surveying;
                    }
                    break;

                case ExpeditionPhase.Surveying:
                    _surveyTimer -= dt;
                    if (_surveyTimer <= 0f)
                    {
                        Phase = ExpeditionPhase.Returning; // caller resolves on this transition
                    }
                    break;

                case ExpeditionPhase.Returning:
                    if (StepPartyTo(Home, cfg, dt))
                    {
                        Phase = ExpeditionPhase.Done;
                    }
                    break;
            }
            return Phase;
        }

        /// <summary>
        /// Steps every living party member toward a point; true when all have arrived.
        /// Movement routes through <see cref="Abbey.Core.PlanarMotion.StepWorn"/> so the
        /// party wears desire paths out to the POI (P3-12).
        /// </summary>
        bool StepPartyTo(Vector3 point, IslandConfig cfg, float dt)
        {
            bool allArrived = true;
            float speed = cfg.expeditionTravelSpeed;
            for (int i = 0; i < Party.Count; i++)
            {
                var v = Party[i];
                if (v == null || v.State == VillagerState.Dead || v.State == VillagerState.Missing)
                {
                    continue; // lost on the way; do not block the rest
                }
                v.transform.position = PlanarMotion.StepWorn(
                    v.transform.position, point, speed, dt, cfg.arrivalRadius, out bool arrived);
                if (!arrived)
                {
                    allArrived = false;
                }
            }
            return allArrived;
        }

        /// <summary>Marks the expedition stranded (dusk recall). Terminal.</summary>
        internal void MarkCaughtOut()
        {
            Phase = ExpeditionPhase.CaughtOut;
        }
    }

    /// <summary>
    /// Island exploration authority (P3-13, ROADMAP Phase 3 item 9). Holds the registry of
    /// authored points of interest (hidden until reached) and the live expeditions. The
    /// player sends a party to a fogged POI; on arrival the POI is revealed and its
    /// data-driven reward lands — resources to the <see cref="ResourceLedger"/>, new open
    /// seed slots through <see cref="SeedSlotSystem"/> (P3-02), people through the
    /// <see cref="ArrivalSystem"/>, and shrine/well threat sources into
    /// <see cref="ThreatSourceSystem"/> (P3-11). While away, party villagers are pulled off
    /// their jobs (<see cref="IsAway"/>, honoured by <see cref="Abbey.Villagers.JobAssigner"/>);
    /// an expedition that has not made it home by dusk is caught out in the dark (its
    /// villagers handed back to the normal dusk recall, exposed).
    ///
    /// Singleton + [ExecuteAlways] like the other Phase 3 systems so EditMode tests get the
    /// OnEnable/OnDisable lifecycle. The dusk recall runs off <see cref="EventBus.PhaseChanged"/>;
    /// tests call <see cref="RecallForDusk"/> directly. All balance is in
    /// <see cref="IslandConfig"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ExplorationSystem : MonoBehaviour
    {
        public static ExplorationSystem Instance { get; private set; }

        [Tooltip("Advance expeditions from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        [Tooltip("Where parties muster and return to (the lit camp). Defaults to this transform.")]
        public Transform homeAnchor;

        readonly List<PointOfInterest> _pois = new List<PointOfInterest>();
        readonly List<Expedition> _expeditions = new List<Expedition>();
        readonly HashSet<VillagerAgent> _away = new HashSet<VillagerAgent>();
        IslandConfig _config;
        bool _isDuplicate;

        public IReadOnlyList<PointOfInterest> Pois => _pois;
        public IReadOnlyList<Expedition> Expeditions => _expeditions;

        /// <summary>Number of villagers currently out on an expedition.</summary>
        public int AwayCount => _away.Count;

        public IslandConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = IslandConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        Vector3 HomePoint => homeAnchor != null ? homeAnchor.position : transform.position;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[ExplorationSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnEnable()
        {
            if (_isDuplicate)
            {
                return;
            }
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            if (_isDuplicate || !Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Injects a config (tests).</summary>
        public void Configure(IslandConfig config)
        {
            _config = config;
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            if (phase == DayPhase.Dusk)
            {
                RecallForDusk();
            }
        }

        // ------------------------------------------------------------------
        // POI registry
        // ------------------------------------------------------------------

        /// <summary>Registers an authored, hidden point of interest and returns it.</summary>
        public PointOfInterest AddPoi(PoiType type, Vector3 position)
        {
            var poi = new PointOfInterest(type, position);
            _pois.Add(poi);
            return poi;
        }

        /// <summary>The nearest still-hidden POI to a point, or null when all are found.</summary>
        public PointOfInterest NearestHiddenPoi(Vector3 from)
        {
            PointOfInterest best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _pois.Count; i++)
            {
                var poi = _pois[i];
                if (poi == null || poi.discovered)
                {
                    continue;
                }
                float d = PlanarMotion.Distance(from, poi.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = poi;
                }
            }
            return best;
        }

        public int CountDiscovered()
        {
            int n = 0;
            for (int i = 0; i < _pois.Count; i++)
            {
                if (_pois[i] != null && _pois[i].discovered)
                {
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------
        // Expeditions
        // ------------------------------------------------------------------

        /// <summary>True while a villager is out on an expedition (job accounting skips it).</summary>
        public bool IsAway(VillagerAgent villager) => villager != null && _away.Contains(villager);

        /// <summary>
        /// Sends a party to a hidden POI. Villagers are pulled off their day jobs for the
        /// duration; the party musters at <paramref name="home"/> (defaults to the home
        /// anchor). Returns the created expedition, or null when the target/party is empty
        /// or the target is already discovered. The party is capped at the config max.
        /// </summary>
        public Expedition LaunchExpedition(PointOfInterest target,
            IReadOnlyList<VillagerAgent> party, Vector3? home = null)
        {
            if (_isDuplicate || target == null || target.discovered || party == null || party.Count == 0)
            {
                return null;
            }
            var cfg = Config;
            Vector3 homePoint = home ?? HomePoint;

            var roster = new List<VillagerAgent>();
            int cap = Mathf.Max(1, cfg.expeditionMaxParty);
            for (int i = 0; i < party.Count && roster.Count < cap; i++)
            {
                var v = party[i];
                if (v == null || v.State == VillagerState.Dead || v.State == VillagerState.Missing
                    || _away.Contains(v))
                {
                    continue;
                }
                roster.Add(v);
            }
            if (roster.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                _away.Add(roster[i]);
                roster[i].CancelWork(); // drop the day job while away
            }

            var expedition = new Expedition(target, roster, homePoint, cfg);
            _expeditions.Add(expedition);
            GameEventLog.Append("expedition",
                $"depart poi={target.type} party={roster.Count} " +
                $"eta={expedition.OneWayTravelSeconds:F1}s at=({target.position.x:F0},{target.position.z:F0})");
            return expedition;
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            if (_isDuplicate || dt <= 0f)
            {
                return;
            }
            for (int i = _expeditions.Count - 1; i >= 0; i--)
            {
                var exp = _expeditions[i];
                var before = exp.Phase;
                var phase = exp.Tick(dt);

                // The moment surveying ends (Surveying -> Returning) the POI is resolved.
                if (before == ExpeditionPhase.Surveying && phase == ExpeditionPhase.Returning)
                {
                    ResolvePoi(exp);
                }
                if (phase == ExpeditionPhase.Done)
                {
                    FinishExpedition(exp);
                    _expeditions.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Dusk recall: any expedition not yet home is caught out. Its villagers are handed
        /// back to the ordinary dusk recall (still standing wherever the field left them, so
        /// they are exposed in the dark — the reused dusk danger). Public for tests.
        /// </summary>
        public void RecallForDusk()
        {
            for (int i = _expeditions.Count - 1; i >= 0; i--)
            {
                var exp = _expeditions[i];
                exp.MarkCaughtOut();
                for (int p = 0; p < exp.Party.Count; p++)
                {
                    _away.Remove(exp.Party[p]);
                }
                GameEventLog.Append("expedition",
                    $"caught_out poi={exp.Target.type} discovered={exp.Target.discovered} " +
                    $"party={exp.Party.Count}");
                _expeditions.RemoveAt(i);
            }
        }

        void FinishExpedition(Expedition exp)
        {
            for (int p = 0; p < exp.Party.Count; p++)
            {
                var v = exp.Party[p];
                if (v == null)
                {
                    continue;
                }
                _away.Remove(v);
            }
            GameEventLog.Append("expedition", $"return poi={exp.Target.type} party={exp.Party.Count}");
        }

        // ------------------------------------------------------------------
        // Reward resolution
        // ------------------------------------------------------------------

        /// <summary>
        /// Reveals a POI and applies its data-driven reward: ledger deposits, unlocked seed
        /// slots, people found, and any threat-source addition. Idempotent per POI (a
        /// second survey of an already-discovered POI yields nothing).
        /// </summary>
        public void ResolvePoi(Expedition exp)
        {
            var poi = exp.Target;
            if (poi == null || poi.discovered)
            {
                return;
            }
            poi.discovered = true;
            var cfg = Config;
            var rule = cfg.RewardFor(poi.type);
            GameEventLog.Append("poi_discovered",
                $"type={poi.type} at=({poi.position.x:F0},{poi.position.z:F0})");
            if (rule == null)
            {
                return;
            }

            // Resources.
            if (rule.resourceYields != null)
            {
                for (int i = 0; i < rule.resourceYields.Count; i++)
                {
                    var stack = rule.resourceYields[i];
                    if (stack.amount > 0)
                    {
                        ResourceLedger.Add(stack.type, stack.amount, $"expedition {poi.type}");
                    }
                }
            }

            // Unlocked seed slots (P3-02).
            if (rule.seedSlotsUnlocked > 0 && SeedSlotSystem.Instance != null)
            {
                int opened = SeedSlotSystem.Instance.UnlockSlotsNear(
                    poi.position, rule.seedSlotsUnlocked, rule.unlockedSlotSize);
                GameEventLog.Append("poi_reward", $"seed_slots={opened} at={poi.type}");
            }

            // People found (integrated by trust through the arrival channel).
            if (rule.arrivalCount > 0 && ArrivalSystem.Instance != null)
            {
                ArrivalSystem.Instance.ReceiveArrivals(
                    rule.arrivalClass, rule.arrivalCount, ArrivalChannel.Expedition, poi.position);
            }

            // Shrine / well threat source on the P3-11 map.
            if (rule.addsThreatSource && ThreatSourceSystem.Instance != null)
            {
                var srcType = ThreatTypeFor(poi.type);
                ThreatSourceSystem.Instance.RegisterSource(srcType, poi.position);
                GameEventLog.Append("poi_reward", $"threat_source={srcType} at={poi.type}");
            }
        }

        static ThreatSourceType ThreatTypeFor(PoiType poi)
        {
            switch (poi)
            {
                case PoiType.Well: return ThreatSourceType.Well;
                case PoiType.Shrine: return ThreatSourceType.Crypt;
                case PoiType.OldRoad: return ThreatSourceType.OldRoad;
                default: return ThreatSourceType.Forest;
            }
        }

        /// <summary>Clears the registries (test isolation; keeps the instance).</summary>
        public void ClearIsland()
        {
            _pois.Clear();
            _expeditions.Clear();
            _away.Clear();
        }
    }
}
