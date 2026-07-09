using System;
using System.Text;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Hero;
using Abbey.Island;
using Abbey.Nightmares;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Map2
{
    public enum Map2Result
    {
        Undecided,
        CovenantVictory,
        ExploitativeVictory,
        Loss,
    }

    public enum Map2LossReason
    {
        None,
        BellkeeperDead,
        VillageDead,
        CovenantBroken,
    }

    /// <summary>
    /// The Abbey-of-Antlers outcome authority.  It deliberately permits two wins:
    /// repair the covenant while restoring the forest, or take enough from the wood
    /// to secure the settlement without pushing the Stag into the Horned Accuser.
    /// Bellkeeper death, village death, and an actually broken covenant are terminal.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Map2Scenario : MonoBehaviour
    {
        public static Map2Scenario Instance { get; private set; }
        public static event Action<Map2Scenario> OutcomeDecided;

        [Tooltip("Map-2 Bellkeeper. Auto-found when omitted.")]
        public BellkeeperController bellkeeper;
        [Tooltip("The Stag covenant authority. Auto-found when omitted.")]
        public StagCovenantSystem stag;
        public bool autoEvaluate = true;

        Map2Config _config;
        bool _isDuplicate;
        readonly bool[] _dilemmasRaised = new bool[4];

        public Map2Result Result { get; private set; }
        public Map2LossReason LossReason { get; private set; }
        public int NightsSurvived { get; private set; }
        public string Chronicle { get; private set; } = string.Empty;
        public bool IsDecided => Result != Map2Result.Undecided;

        public Map2Config Config
        {
            get => _config != null ? _config : (_config = Map2Config.LoadOrDefault());
            set => _config = value;
        }

        public StagCovenantSystem Stag
        {
            get
            {
                if (stag == null) stag = FindFirstObjectByType<StagCovenantSystem>();
                return stag;
            }
        }

        public float ForestDebt
        {
            get
            {
                var threat = ThreatSourceSystem.Instance;
                return threat != null ? threat.PressureFor(ThreatSourceType.Forest) : 0f;
            }
        }

        public bool CovenantStockReady => ResourceLedger.CanAfford(Config.covenantRouteStock);
        public bool ExploitativeStockReady => ResourceLedger.CanAfford(Config.exploitativeRouteStock);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            if (_isDuplicate) return;
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.DayChanged -= OnDayChanged;
            EventBus.DayChanged += OnDayChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.DayChanged -= OnDayChanged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Application.isPlaying && autoEvaluate) Evaluate();
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dawn)
            {
                NightsSurvived++;
                Stag?.RecomputeFromLog();
                ThreatSourceSystem.Instance?.RecomputeFromLog();
                Evaluate();
            }
        }

        void OnDayChanged(int day)
        {
            var ids = Config.dilemmaIds;
            var days = Config.dilemmaDays;
            if (ids == null || days == null) return;
            int count = Mathf.Min(ids.Length, days.Length);
            for (int i = 0; i < count && i < _dilemmasRaised.Length; i++)
            {
                if (_dilemmasRaised[i] || day < days[i]) continue;
                _dilemmasRaised[i] = true;
                DilemmaSystem.Instance?.EnqueueCard(ids[i]);
            }
        }

        public Map2Result Evaluate()
        {
            if (_isDuplicate || IsDecided) return Result;
            if (bellkeeper == null) bellkeeper = FindFirstObjectByType<BellkeeperController>();

            if (bellkeeper != null && !bellkeeper.IsAlive)
                return DecideLoss(Map2LossReason.BellkeeperDead);

            CountVillagers(out int known, out int alive);
            if (known > 0 && alive == 0)
                return DecideLoss(Map2LossReason.VillageDead);

            var covenant = Stag;
            if (covenant != null)
            {
                covenant.RecomputeFromLog();
                if (covenant.CovenantBroken)
                    return DecideLoss(Map2LossReason.CovenantBroken);
            }

            if (NightsSurvived < Config.minimumNightsSurvived) return Result;

            var carry = CampaignCarryoverSystem.Instance;
            float grace = carry != null ? carry.ForestDebtGrace : 0f;
            float debt = ForestDebt;

            if (covenant != null && covenant.State == StagState.Allied
                && CovenantStockReady
                && debt <= Config.covenantRouteMaxForestDebt + grace)
            {
                return Decide(Map2Result.CovenantVictory);
            }

            if (ExploitativeStockReady
                && debt <= Config.exploitativeRouteMaxForestDebt + grace
                && (covenant == null || !covenant.CovenantBroken))
            {
                return Decide(Map2Result.ExploitativeVictory);
            }

            return Result;
        }

        Map2Result DecideLoss(Map2LossReason reason)
        {
            LossReason = reason;
            return Decide(Map2Result.Loss);
        }

        Map2Result Decide(Map2Result result)
        {
            Result = result;
            Chronicle = ComposeChronicle();
            GameEventLog.Append("map2_outcome",
                $"result={Result} reason={LossReason} nights={NightsSurvived} "
                + $"stag={(Stag != null ? Stag.State.ToString() : "none")} debt={ForestDebt:F2}");
            OutcomeDecided?.Invoke(this);
            return Result;
        }

        string ComposeChronicle()
        {
            var sb = new StringBuilder();
            var trait = CampaignCarryoverSystem.Instance != null
                ? CampaignCarryoverSystem.Instance.Trait : BellkeeperTrait.None;
            sb.Append($"The Bellkeeper came ashore carrying {trait}. ");
            if (Result == Map2Result.CovenantVictory)
                sb.Append("The marked trees stand, the hidden paths open, and the Stag permits the new abbey to endure.");
            else if (Result == Map2Result.ExploitativeVictory)
                sb.Append("Axes and kilns bought a hard survival; the Stag still watches, and every antler-shadow remembers the price.");
            else if (LossReason == Map2LossReason.CovenantBroken)
                sb.Append("The covenant snapped. The watcher beneath the abbey rose as the Horned Accuser.");
            else if (LossReason == Map2LossReason.BellkeeperDead)
                sb.Append("The true bell fell silent beneath the trees.");
            else
                sb.Append("No lit household remained to answer the bell.");
            return sb.ToString();
        }

        static void CountVillagers(out int known, out int alive)
        {
            known = 0;
            alive = 0;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null) continue;
                known++;
                if (v.State != VillagerState.Dead && v.State != VillagerState.Missing) alive++;
            }
        }

        public void SetNightsSurvivedForTests(int value) => NightsSurvived = Mathf.Max(0, value);

        public void Clear()
        {
            Result = Map2Result.Undecided;
            LossReason = Map2LossReason.None;
            NightsSurvived = 0;
            Chronicle = string.Empty;
            Array.Clear(_dilemmasRaised, 0, _dilemmasRaised.Length);
        }

        public static void ResetStaticEvents() => OutcomeDecided = null;
    }
}
