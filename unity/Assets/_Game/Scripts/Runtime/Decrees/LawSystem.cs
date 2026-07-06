using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Sanity;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Decrees
{
    /// <summary>
    /// The standing-law system (P3-09): five policy groups (Food, Night labour, Burial,
    /// Hound, Old rites), each with exactly one active option. Changing a law is a
    /// <see cref="DecreeFood"/>/<see cref="DecreeNightLabour"/>/… decree — event-logged and
    /// gated by a per-group cooldown (<see cref="LawsConfig.decreeCooldownDays"/>) so choices
    /// have weight. Every option writes a durable tag (<see cref="LawTags"/>) that later
    /// systems read: P3-10 moral pressures off the standing tags, P3-11 nightmares off the
    /// per-death grave tags and the old-rite tags.
    ///
    /// The laws carry mechanical effects, all data-driven from <see cref="LawsConfig"/>:
    ///  * Food  → the daily ration pass (<see cref="IssueRations"/>) draws Food from the
    ///    <see cref="ResourceLedger"/> per the active table (workers vs idle, hound share,
    ///    fasting hunger + sanity pressure).
    ///  * Night labour → gates the P3-08 overdrive levers through
    ///    <see cref="OverdriveSystem.PermissionProvider"/> (<see cref="IsPermittedByLaw"/>).
    ///  * Burial → <see cref="ProcessBurial(string)"/> pays each option's costs/refunds and
    ///    stamps a grave tag when a villager dies (scanned from the log at dawn).
    ///  * Hound → writes <see cref="HoundEvolutionSystem.Doctrine"/>.
    ///  * Old rites → daily sanctity / old-faith pressure + rite tags.
    ///
    /// Deterministic (no RNG). Singleton + [ExecuteAlways] like the other Phase 3 systems so
    /// EditMode tests get the OnEnable/OnDisable lifecycle. The daily upkeep + burial pass
    /// runs off <see cref="EventBus.PhaseChanged"/> at Dawn; tests call the methods directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LawSystem : MonoBehaviour
    {
        public static LawSystem Instance { get; private set; }

        [Header("Active laws (defaults applied on enable)")]
        public FoodLaw food = FoodLaw.Equal;
        public NightLabourLaw nightLabour = NightLabourLaw.PaidRisk;
        public BurialLaw burial = BurialLaw.FullRites;
        public HoundLaw hound = HoundLaw.Family;
        public OldRitesLaw oldRites = OldRitesLaw.ForbidPaganRites;

        LawsConfig _config;
        bool _isDuplicate;
        int _burialCursor;

        readonly Dictionary<LawGroup, int> _lastDecreeDay = new Dictionary<LawGroup, int>();
        readonly List<VillagerAgent> _scratch = new List<VillagerAgent>();

        // ---- P3-10 pressure accumulators (read by moral pressures / end summary) ----

        /// <summary>Accumulated hunger pressure (rises under Fasting / short rations).</summary>
        public float Hunger { get; private set; }

        /// <summary>Mercy pressure: + for honoured dead, - for desecration.</summary>
        public float Mercy { get; private set; }

        /// <summary>Sanctity pressure: erodes when the dead are dishonoured / offerings tolerated.</summary>
        public float Sanctity { get; private set; }

        /// <summary>Old-faith pressure: vents when tolerated, builds when forbidden.</summary>
        public float OldFaithPressure { get; private set; }

        /// <summary>Fear pressure from harsh corpse handling.</summary>
        public float Fear { get; private set; }

        /// <summary>Nightmare pressure booked by desecration (P3-11 consequence nightmares).</summary>
        public float NightmarePressure { get; private set; }

        /// <summary>Food units issued in the most recent ration pass (debug overlay).</summary>
        public int FoodIssuedLastPass { get; private set; }

        /// <summary>Registered night-work volunteers this day (Paid Risk ration surcharge).</summary>
        public int NightWorkVolunteersToday { get; private set; }

        public FoodLaw ActiveFood => food;
        public NightLabourLaw ActiveNightLabour => nightLabour;
        public BurialLaw ActiveBurial => burial;
        public HoundLaw ActiveHound => hound;
        public OldRitesLaw ActiveOldRites => oldRites;

        public LawsConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = LawsConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[LawSystem] Duplicate instance ignored.", this);
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
            ApplyStandingLaws();
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

        /// <summary>Injects a config (tests) and clears cooldowns, pressures and the burial cursor.</summary>
        public void Configure(LawsConfig config)
        {
            _config = config;
            _lastDecreeDay.Clear();
            _burialCursor = GameEventLog.Count;
            Hunger = 0f;
            Mercy = 0f;
            Sanctity = 0f;
            OldFaithPressure = 0f;
            Fear = 0f;
            NightmarePressure = 0f;
            FoodIssuedLastPass = 0;
            NightWorkVolunteersToday = 0;
            ApplyStandingLaws();
        }

        int Day => GameClock.Instance != null ? GameClock.Instance.DayNumber : 1;

        // ------------------------------------------------------------------
        // Standing law side-effects (doctrine + overdrive gate)
        // ------------------------------------------------------------------

        /// <summary>Re-asserts the continuous side-effects of the active laws (doctrine, gate).</summary>
        public void ApplyStandingLaws()
        {
            var houndSystem = HoundEvolutionSystem.Instance;
            if (houndSystem != null)
            {
                houndSystem.Doctrine = LawTags.ToDoctrine(hound);
            }
            AttachOverdriveGate();
        }

        /// <summary>Points the overdrive system's permission gate at the Night labour law.</summary>
        public void AttachOverdriveGate()
        {
            var overdrive = OverdriveSystem.Instance;
            if (overdrive != null)
            {
                overdrive.PermissionProvider = IsPermittedByLaw;
            }
        }

        /// <summary>
        /// Whether a lever is permitted under the current Night labour law. Non-night-labour
        /// levers are always allowed; the gated levers obey the active option's
        /// <see cref="NightLabourEffect.nightWorkPermitted"/>.
        /// </summary>
        public bool IsPermittedByLaw(OverdriveActionId id)
        {
            var cfg = Config;
            if (!cfg.IsNightLabourAction(id))
            {
                return true;
            }
            return cfg.NightLabourEffectFor(nightLabour).nightWorkPermitted;
        }

        // ------------------------------------------------------------------
        // Decrees
        // ------------------------------------------------------------------

        bool CanDecree(LawGroup group)
        {
            int day = Day;
            if (_lastDecreeDay.TryGetValue(group, out int last)
                && day - last < Mathf.Max(0, Config.decreeCooldownDays))
            {
                GameEventLog.Append("decree_refused",
                    $"group={group} day={day} last={last} reason=cooldown");
                return false;
            }
            return true;
        }

        void CommitDecree(LawGroup group, string tag)
        {
            int day = Day;
            _lastDecreeDay[group] = day;
            GameEventLog.Append("decree", $"group={group} tag={tag} day={day}");
        }

        /// <summary>Enacts a Food law (event-logged; refused while the group is on cooldown).</summary>
        public bool DecreeFood(FoodLaw option)
        {
            if (_isDuplicate || !CanDecree(LawGroup.Food))
            {
                return false;
            }
            food = option;
            CommitDecree(LawGroup.Food, LawTags.For(option));
            return true;
        }

        /// <summary>Enacts a Night labour law and re-points the overdrive gate.</summary>
        public bool DecreeNightLabour(NightLabourLaw option)
        {
            if (_isDuplicate || !CanDecree(LawGroup.NightLabour))
            {
                return false;
            }
            nightLabour = option;
            AttachOverdriveGate();
            CommitDecree(LawGroup.NightLabour, LawTags.For(option));
            return true;
        }

        /// <summary>Enacts a Burial law (applied per death from then on).</summary>
        public bool DecreeBurial(BurialLaw option)
        {
            if (_isDuplicate || !CanDecree(LawGroup.Burial))
            {
                return false;
            }
            burial = option;
            CommitDecree(LawGroup.Burial, LawTags.For(option));
            return true;
        }

        /// <summary>Enacts a Hound law and writes the doctrine into the P3-07 evolution system.</summary>
        public bool DecreeHound(HoundLaw option)
        {
            if (_isDuplicate || !CanDecree(LawGroup.Hound))
            {
                return false;
            }
            hound = option;
            var houndSystem = HoundEvolutionSystem.Instance;
            if (houndSystem != null)
            {
                houndSystem.Doctrine = LawTags.ToDoctrine(option);
            }
            CommitDecree(LawGroup.Hound, LawTags.For(option));
            return true;
        }

        /// <summary>Enacts an Old rites law (drives the daily sanctity / old-faith pressure).</summary>
        public bool DecreeOldRites(OldRitesLaw option)
        {
            if (_isDuplicate || !CanDecree(LawGroup.OldRites))
            {
                return false;
            }
            oldRites = option;
            CommitDecree(LawGroup.OldRites, LawTags.For(option));
            return true;
        }

        // ------------------------------------------------------------------
        // Standing-tag queries (P3-10 reads these)
        // ------------------------------------------------------------------

        /// <summary>The durable standing tag for a group's active option.</summary>
        public string TagFor(LawGroup group)
        {
            switch (group)
            {
                case LawGroup.Food: return LawTags.For(food);
                case LawGroup.NightLabour: return LawTags.For(nightLabour);
                case LawGroup.Burial: return LawTags.For(burial);
                case LawGroup.Hound: return LawTags.For(hound);
                case LawGroup.OldRites: return LawTags.For(oldRites);
                default: return "";
            }
        }

        /// <summary>All five active standing tags (P3-10 moral pressures consume these).</summary>
        public string[] ActiveTags()
        {
            return new[]
            {
                LawTags.For(food),
                LawTags.For(nightLabour),
                LawTags.For(burial),
                LawTags.For(hound),
                LawTags.For(oldRites)
            };
        }

        // ------------------------------------------------------------------
        // Night-work volunteers (Paid Risk ration surcharge)
        // ------------------------------------------------------------------

        /// <summary>Registers a villager sent to night labour tonight (Paid Risk surcharge).</summary>
        public void RegisterNightWorkVolunteer()
        {
            NightWorkVolunteersToday++;
        }

        // ------------------------------------------------------------------
        // Dawn upkeep pass
        // ------------------------------------------------------------------

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            if (phase == DayPhase.Dawn)
            {
                RunDailyUpkeep();
            }
        }

        /// <summary>
        /// The daily upkeep pass, run at dawn: bury the night's dead under the active burial
        /// law, issue the day's rations under the active food law, and apply the old-rites
        /// daily pressure. Public so tests can drive it directly. Deterministic.
        /// </summary>
        public void RunDailyUpkeep()
        {
            ProcessNightBurials();
            IssueRations();
            ApplyOldRitesDaily();
            NightWorkVolunteersToday = 0;
        }

        /// <summary>
        /// Issues the day's rations from the ledger under the active Food law: the hound's
        /// keep (first, under Beast Share), then each living villager's ration (workers vs
        /// idle), plus the Paid Risk night-work surcharge and Fasting hunger + sanity
        /// pressure. Every draw is event-logged ("RationsIssued …"). Public for tests.
        /// </summary>
        public void IssueRations()
        {
            var cfg = Config;
            var effect = cfg.FoodEffectFor(food);
            var sanity = SanitySystem.Instance;
            int issued = 0;

            if (effect.feedHoundFirst)
            {
                issued += IssueHoundRation(effect);
            }

            CollectLivingVillagers(_scratch);
            int workerFood = 0, idleFood = 0, workers = 0, idle = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                var v = _scratch[i];
                var cls = Classify(v);
                int ration = cls == RationClass.Worker ? effect.workerRation : effect.idleRation;
                int got = ResourceLedger.IssueRation(ResourceType.Food, ration,
                    $"rations:{LawTags.For(food)}:{cls}");
                issued += got;
                if (cls == RationClass.Worker) { workers++; workerFood += got; }
                else { idle++; idleFood += got; }

                if (effect.sanityCostPerVillager > 0f && sanity != null)
                {
                    sanity.ApplySanityCost(v, effect.sanityCostPerVillager, "fasting");
                }
                Hunger += effect.hungerPerVillager;
            }

            // Paid Risk: night-work volunteers cost extra rations on top of their keep.
            if (nightLabour == NightLabourLaw.PaidRisk && NightWorkVolunteersToday > 0)
            {
                int surcharge = cfg.NightLabourEffectFor(nightLabour).extraRationPerNightWorker
                                * NightWorkVolunteersToday;
                if (surcharge > 0)
                {
                    issued += ResourceLedger.IssueRation(ResourceType.Food, surcharge,
                        "rations:night_risk");
                }
            }

            if (!effect.feedHoundFirst)
            {
                issued += IssueHoundRation(effect);
            }

            FoodIssuedLastPass = issued;
            GameEventLog.Append("RationsIssued",
                $"law={LawTags.For(food)} workers={workers}(+{workerFood}) " +
                $"idle={idle}(+{idleFood}) total={issued}");
        }

        int IssueHoundRation(FoodLawEffect effect)
        {
            if (effect.houndRation <= 0)
            {
                return 0;
            }
            int got = ResourceLedger.IssueRation(ResourceType.Food, effect.houndRation,
                $"rations:{LawTags.For(food)}:beast");
            GameEventLog.Append("RationsIssued", $"beast +{got}");
            return got;
        }

        /// <summary>Applies the active Old rites law's daily sanctity / old-faith pressure + tags.</summary>
        public void ApplyOldRitesDaily()
        {
            var effect = Config.OldRitesEffectFor(oldRites);
            Sanctity += effect.sanctityDeltaPerDay;
            OldFaithPressure += effect.oldFaithPressureDeltaPerDay;
            if (effect.emitsOfferingEvents)
            {
                GameEventLog.Append("old_rite", $"{LawTags.OfferingMade} ({LawTags.For(oldRites)})");
            }
            if (effect.emitsSecretRiteTags)
            {
                GameEventLog.Append("old_rite", $"{LawTags.SecretRite} ({LawTags.For(oldRites)})");
            }
        }

        // ------------------------------------------------------------------
        // Burial
        // ------------------------------------------------------------------

        /// <summary>Buries every villager whose death was logged since the last pass (dawn scan).</summary>
        void ProcessNightBurials()
        {
            var records = GameEventLog.Records;
            int end = records.Count;
            for (int i = _burialCursor; i < end; i++)
            {
                if (records[i].Type == "villager_died")
                {
                    ProcessBurial(records[i].Data);
                }
            }
            _burialCursor = GameEventLog.Count;
        }

        /// <summary>Buries a villager under the active Burial law. Convenience overload.</summary>
        public void ProcessBurial(VillagerAgent deceased)
        {
            ProcessBurial(deceased != null ? deceased.name : "<unknown>");
        }

        /// <summary>
        /// Buries one villager under the active Burial law: pays the option's costs (best
        /// effort — a shortfall still buries but is issued from what remains), banks any
        /// refund, applies the mercy / sanctity / fear / nightmare pressures + witness sanity
        /// cost, and stamps the grave tag P3-11 will read. Event-logged. Deterministic.
        /// </summary>
        public void ProcessBurial(string deceasedName)
        {
            if (_isDuplicate)
            {
                return;
            }
            var effect = Config.BurialEffectFor(burial);

            if (effect.cost != null)
            {
                for (int i = 0; i < effect.cost.Count; i++)
                {
                    var c = effect.cost[i];
                    ResourceLedger.IssueRation(c.type, c.amount, $"burial:{LawTags.For(burial)}");
                }
            }
            if (effect.refund != null)
            {
                for (int i = 0; i < effect.refund.Count; i++)
                {
                    var r = effect.refund[i];
                    ResourceLedger.Add(r.type, r.amount, $"burial:{LawTags.For(burial)}");
                }
            }

            Mercy += effect.mercyDelta;
            Sanctity += effect.sanctityDelta;
            Fear += effect.fearDelta;
            NightmarePressure += effect.nightmarePressure;

            if (effect.sanityCostPerVillager > 0f)
            {
                var sanity = SanitySystem.Instance;
                if (sanity != null)
                {
                    CollectLivingVillagers(_scratch);
                    for (int i = 0; i < _scratch.Count; i++)
                    {
                        sanity.ApplySanityCost(_scratch[i], effect.sanityCostPerVillager,
                            $"burial:{LawTags.For(burial)}");
                    }
                }
            }

            string graveTag = LawTags.GraveTagFor(burial);
            GameEventLog.Append("burial",
                $"law={LawTags.For(burial)} deceased={deceasedName} tag={graveTag}");
        }

        // ------------------------------------------------------------------
        // Villager classification
        // ------------------------------------------------------------------

        /// <summary>Which ration class a villager draws — working states get the worker ration.</summary>
        public static RationClass Classify(VillagerAgent villager)
        {
            if (villager == null)
            {
                return RationClass.Idle;
            }
            switch (villager.State)
            {
                case VillagerState.AssignedToWork:
                case VillagerState.WalkingToTask:
                case VillagerState.Working:
                case VillagerState.CarryingResource:
                case VillagerState.ReturningToStorage:
                case VillagerState.ReturningToLight:
                    return RationClass.Worker;
                default:
                    return RationClass.Idle;
            }
        }

        void CollectLivingVillagers(List<VillagerAgent> buffer)
        {
            buffer.Clear();
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State == VillagerState.Dead || v.State == VillagerState.Missing)
                {
                    continue;
                }
                buffer.Add(v);
            }
        }
    }
}
