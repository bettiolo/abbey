using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Morale;
using Abbey.Nightmares;
using Abbey.Villagers;
using Abbey.Rendering;
using UnityEngine;

namespace Abbey.Island
{
    /// <summary>One newcomer and what the settlement's trust made of them.</summary>
    public readonly struct Newcomer
    {
        public readonly ArrivalClass Class;
        public readonly ArrivalChannel Channel;
        public readonly IntegrationOutcome Outcome;
        public readonly VillagerAgent Villager; // null when they left or spawning is off

        public Newcomer(ArrivalClass cls, ArrivalChannel channel, IntegrationOutcome outcome,
            VillagerAgent villager)
        {
            Class = cls;
            Channel = channel;
            Outcome = outcome;
            Villager = villager;
        }
    }

    /// <summary>
    /// A recorded intent to sail away in spring (P3-14 end summary). Written when a newcomer
    /// would not stay under the current trust, or when a stayed newcomer later chooses to
    /// leave.
    /// </summary>
    public readonly struct DepartureIntent
    {
        public readonly ArrivalClass Class;
        public readonly ArrivalChannel Channel;
        public readonly string Reason;

        public DepartureIntent(ArrivalClass cls, ArrivalChannel channel, string reason)
        {
            Class = cls;
            Channel = channel;
            Reason = reason;
        }
    }

    /// <summary>The outcome of a storm-shipwreck event (debug / tests / P3-14).</summary>
    public readonly struct ShipwreckResult
    {
        public readonly int PeopleAshore;
        public readonly int Stayed;
        public readonly int SuppliesAdded;
        public readonly bool DrownedRiskArmed;

        public ShipwreckResult(int peopleAshore, int stayed, int suppliesAdded, bool drownedRiskArmed)
        {
            PeopleAshore = peopleAshore;
            Stayed = stayed;
            SuppliesAdded = suppliesAdded;
            DrownedRiskArmed = drownedRiskArmed;
        }
    }

    /// <summary>
    /// Population-growth authority (P3-13, ROADMAP Phase 3 item 11). Three arrival channels
    /// feed one integration pipeline: passive survivors that walk toward the lit village at
    /// dawn (a deterministic per-day draw from <see cref="IslandConfig"/>), people an
    /// expedition finds (routed here by <see cref="ExplorationSystem"/>), and storm
    /// shipwreck crews thrown ashore with supplies and a drowned-nightmare risk tag. The
    /// settlement's Bellkeeper trust tier (P3-10) decides each newcomer's fate: below the
    /// stay tier they refuse and record a spring departure intent; at or above it they stay;
    /// at the volunteer tier they also offer for duty. Newcomers who stay are instantiated as
    /// villagers with a class tag (specialist = a job-speed bonus, warrior = a P3-06
    /// recruitment candidate).
    ///
    /// Singleton + [ExecuteAlways] like the other Phase 3 systems. Passive draws run off
    /// <see cref="EventBus.DayChanged"/>; tests call <see cref="OnDayChangedForTest"/> /
    /// <see cref="ReceiveArrivals"/> / <see cref="TriggerShipwreck"/> directly. All balance
    /// is in <see cref="IslandConfig"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ArrivalSystem : MonoBehaviour
    {
        public static ArrivalSystem Instance { get; private set; }

        [Tooltip("Instantiate a VillagerAgent for each newcomer who stays (tests may turn this off).")]
        public bool spawnVillagers = true;

        [Tooltip("Where newcomers walk in to (the lit camp). Defaults to this transform.")]
        public Transform arrivalAnchor;

        [Tooltip("The night director that arms the drowned-nightmare window on a wet rescue.")]
        public NightmareDirector director;

        readonly List<Newcomer> _newcomers = new List<Newcomer>();
        readonly List<DepartureIntent> _departures = new List<DepartureIntent>();
        IslandConfig _config;
        bool _isDuplicate;
        int _spawnCounter;

        public IReadOnlyList<Newcomer> Newcomers => _newcomers;
        public IReadOnlyList<DepartureIntent> DepartureIntents => _departures;

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

        /// <summary>The night director, from the field or the scene.</summary>
        public NightmareDirector Director
        {
            get
            {
                if (director == null)
                {
                    director = FindFirstObjectByType<NightmareDirector>();
                }
                return director;
            }
            set { director = value; }
        }

        Vector3 ArrivalPoint => arrivalAnchor != null ? arrivalAnchor.position : transform.position;

        /// <summary>The current Bellkeeper trust tier (Neutral when no pressure system).</summary>
        public TrustTier CurrentTrustTier =>
            PressureSystem.Instance != null ? PressureSystem.Instance.TrustTier : TrustTier.Neutral;

        // ---- Roll-ups (P3-14 end summary) --------------------------------

        public int StayedCount => CountOutcome(IntegrationOutcome.Stayed) + CountOutcome(IntegrationOutcome.Volunteered);
        public int VolunteeredCount => CountOutcome(IntegrationOutcome.Volunteered);
        public int LeftCount => CountOutcome(IntegrationOutcome.Left);

        int CountOutcome(IntegrationOutcome outcome)
        {
            int n = 0;
            for (int i = 0; i < _newcomers.Count; i++)
            {
                if (_newcomers[i].Outcome == outcome)
                {
                    n++;
                }
            }
            return n;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[ArrivalSystem] Duplicate instance ignored.", this);
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
            EventBus.DayChanged -= OnDayChanged;
            EventBus.DayChanged += OnDayChanged;
        }

        void OnDisable()
        {
            EventBus.DayChanged -= OnDayChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests).</summary>
        public void Configure(IslandConfig config)
        {
            _config = config;
        }

        void OnDayChanged(int day)
        {
            if (_isDuplicate)
            {
                return;
            }
            RunPassiveDraw(day);
        }

        /// <summary>Test entry point for the passive draw (autoTick-free).</summary>
        public void OnDayChangedForTest(int day) => RunPassiveDraw(day);

        void RunPassiveDraw(int day)
        {
            var cfg = Config;
            if (cfg.PassiveArrivalForDay(day))
            {
                GameEventLog.Append("arrival", $"passive_draw day={day} class={cfg.passiveArrivalClass}");
                ReceiveArrivals(cfg.passiveArrivalClass, 1, ArrivalChannel.Passive, ArrivalPoint);
            }
        }

        // ------------------------------------------------------------------
        // Integration
        // ------------------------------------------------------------------

        /// <summary>
        /// Brings <paramref name="count"/> people of a class in through a channel and
        /// resolves each by the current trust tier. Returns how many stayed (incl.
        /// volunteers).
        /// </summary>
        public int ReceiveArrivals(ArrivalClass cls, int count, ArrivalChannel channel, Vector3 at)
        {
            if (_isDuplicate || count <= 0)
            {
                return 0;
            }
            int stayed = 0;
            for (int i = 0; i < count; i++)
            {
                if (Integrate(cls, channel, at))
                {
                    stayed++;
                }
            }
            return stayed;
        }

        /// <summary>Resolves one newcomer. Returns true when they stayed.</summary>
        bool Integrate(ArrivalClass cls, ArrivalChannel channel, Vector3 at)
        {
            var cfg = Config;
            var tier = CurrentTrustTier;
            var outcome = ResolveOutcome(tier, cfg);

            if (outcome == IntegrationOutcome.Left)
            {
                _departures.Add(new DepartureIntent(cls, channel, "low_trust"));
                _newcomers.Add(new Newcomer(cls, channel, outcome, null));
                GameEventLog.Append("arrival",
                    $"refused class={cls} channel={channel} tier={tier} -> leaves in spring");
                return false;
            }

            VillagerAgent villager = spawnVillagers ? SpawnNewcomer(cls, at) : null;
            _newcomers.Add(new Newcomer(cls, channel, outcome, villager));
            GameEventLog.Append("arrival",
                $"joined class={cls} channel={channel} tier={tier} outcome={outcome}");

            if (outcome == IntegrationOutcome.Volunteered)
            {
                GameEventLog.Append("arrival", $"volunteer class={cls} tier={tier}");
            }
            if (cls == ArrivalClass.Warrior)
            {
                // A warrior newcomer is a P3-06 recruitment candidate.
                GameEventLog.Append("arrival", "warrior_recruitment_candidate");
            }
            return true;
        }

        /// <summary>Trust decides: below stay tier they leave; at/above volunteer tier they volunteer.</summary>
        IntegrationOutcome ResolveOutcome(TrustTier tier, IslandConfig cfg)
        {
            if (tier < cfg.stayMinTier)
            {
                return IntegrationOutcome.Left;
            }
            if (tier >= cfg.volunteerMinTier)
            {
                return IntegrationOutcome.Volunteered;
            }
            return IntegrationOutcome.Stayed;
        }

        VillagerAgent SpawnNewcomer(ArrivalClass cls, Vector3 at)
        {
            var go = new GameObject($"Newcomer_{cls}_{_spawnCounter}");
            _spawnCounter++;
            go.transform.position = at;
            var agent = go.AddComponent<VillagerAgent>();
            agent.seed = _spawnCounter * 101 + (int)cls;
            if (cls == ArrivalClass.Specialist)
            {
                // Specialist job-efficiency bonus (JobsConfig hook): a faster work loop.
                agent.WorkSpeedMultiplier = Mathf.Max(0f, Config.specialistWorkSpeedMultiplier);
            }
            SpriteProjectionBootstrap.RegisterGlobal(
                go, "villager_lowpoly", "actor.newcomer", $"newcomer:{cls}:{_spawnCounter}");
            return agent;
        }

        // ------------------------------------------------------------------
        // Storm shipwreck
        // ------------------------------------------------------------------

        /// <summary>
        /// A storm throws a crew ashore: the config supplies wash into the ledger, the crew
        /// composition is integrated by trust, and the drowned-nightmare risk window is armed
        /// on the director (a wet rescue). Returns a summary.
        /// </summary>
        public ShipwreckResult TriggerShipwreck(Vector3? ashore = null)
        {
            if (_isDuplicate)
            {
                return default;
            }
            var cfg = Config;
            Vector3 at = ashore ?? ArrivalPoint;

            int suppliesAdded = 0;
            if (cfg.shipwreckSupplies != null)
            {
                for (int i = 0; i < cfg.shipwreckSupplies.Count; i++)
                {
                    var stack = cfg.shipwreckSupplies[i];
                    if (stack.amount > 0)
                    {
                        suppliesAdded += ResourceLedger.Add(stack.type, stack.amount, "shipwreck");
                    }
                }
            }

            int people = 0, stayed = 0;
            if (cfg.shipwreckCrew != null)
            {
                for (int i = 0; i < cfg.shipwreckCrew.Count; i++)
                {
                    var entry = cfg.shipwreckCrew[i];
                    if (entry == null || entry.count <= 0)
                    {
                        continue;
                    }
                    people += entry.count;
                    stayed += ReceiveArrivals(entry.arrivalClass, entry.count, ArrivalChannel.Shipwreck, at);
                }
            }

            bool armed = ArmDrownedRisk(cfg.drownedRiskWindowNights);
            GameEventLog.Append("shipwreck",
                $"crew={people} stayed={stayed} supplies={suppliesAdded} drowned_window={cfg.drownedRiskWindowNights}");
            return new ShipwreckResult(people, stayed, suppliesAdded, armed);
        }

        /// <summary>Arms the drowned-nightmare window on the director. True when a director took it.</summary>
        bool ArmDrownedRisk(int nights)
        {
            var d = Director;
            if (d == null)
            {
                GameEventLog.Append("shipwreck", "drowned_risk_no_director");
                return false;
            }
            d.ArmDrownedRisk(nights);
            return true;
        }

        /// <summary>Clears the arrival history (test isolation; keeps the instance).</summary>
        public void ClearArrivals()
        {
            _newcomers.Clear();
            _departures.Clear();
            _spawnCounter = 0;
        }
    }
}
