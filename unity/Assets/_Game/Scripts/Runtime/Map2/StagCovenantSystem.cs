using Abbey.Core;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Map2
{
    public enum StagState
    {
        Hidden,
        Watching,
        Wary,
        Permitting,
        Allied,
        HornedAccuser,
    }

    /// <summary>
    /// The Stag's indirect bond.  It deterministically folds the shared event log,
    /// so the same extraction/restoration history always produces the same beast.
    /// The player never issues orders to it; interactions are observation, offerings,
    /// tending the wound, and following signs.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StagCovenantSystem : MonoBehaviour
    {
        public static StagCovenantSystem Instance { get; private set; }

        Map2Config _config;
        bool _isDuplicate;

        public float Trust { get; private set; }
        public float Patience { get; private set; }
        public float Wound { get; private set; }
        public float Wildness { get; private set; }
        public float Covenant { get; private set; }
        public int Encounters { get; private set; }
        public StagState State { get; private set; } = StagState.Hidden;
        public bool CovenantBroken => State == StagState.HornedAccuser;

        public Map2Config Config
        {
            get => _config != null ? _config : (_config = Map2Config.LoadOrDefault());
            set { _config = value; RecomputeFromLog(); }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Instance = this;
            RecomputeFromLog();
        }

        void OnEnable()
        {
            if (!_isDuplicate)
            {
                EventBus.PhaseChanged -= OnPhaseChanged;
                EventBus.PhaseChanged += OnPhaseChanged;
            }
        }

        void OnDisable() => EventBus.PhaseChanged -= OnPhaseChanged;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dawn || phase == DayPhase.Dusk) RecomputeFromLog();
        }

        public bool TryInteract(string id)
        {
            if (_isDuplicate || CovenantBroken) return false;
            var interaction = Config.InteractionFor(id);
            if (interaction == null || !ResourceLedger.CanAfford(interaction.costs)) return false;
            if (!ResourceLedger.TryConsume(interaction.costs, $"stag:{id}")) return false;
            GameEventLog.Append("stag_action", $"id={id} signal={interaction.signal}");
            RecomputeFromLog();
            return true;
        }

        public void RecordWorldChoice(string signal)
        {
            if (string.IsNullOrEmpty(signal)) return;
            GameEventLog.Append("stag_choice", $"signal={signal}");
            RecomputeFromLog();
        }

        public void RecomputeFromLog()
        {
            if (_isDuplicate) return;
            var cfg = Config;
            var carry = CampaignCarryoverSystem.Instance;
            Trust = cfg.startingTrust + (carry != null ? carry.StagTrustBonus : 0f);
            Patience = cfg.startingPatience;
            Wound = cfg.startingWound;
            Wildness = cfg.startingWildness;
            Covenant = cfg.startingCovenant + (carry != null ? carry.StagCovenantBonus : 0f);
            Encounters = 0;

            var records = GameEventLog.Records;
            for (int r = 0; r < records.Count; r++)
            {
                var record = records[r];
                if (cfg.reactions == null) continue;
                for (int i = 0; i < cfg.reactions.Count; i++)
                {
                    var reaction = cfg.reactions[i];
                    if (reaction == null || string.IsNullOrEmpty(reaction.signal)) continue;
                    if (!ContainsSignal(record.Data, reaction.signal)) continue;
                    Trust += reaction.trustDelta;
                    Patience += reaction.patienceDelta;
                    Wound += reaction.woundDelta;
                    Wildness += reaction.wildnessDelta;
                    Covenant += reaction.covenantDelta;
                    Encounters++;
                }
            }

            Trust = Mathf.Clamp01(Trust);
            Patience = Mathf.Clamp01(Patience);
            Wound = Mathf.Clamp01(Wound);
            Wildness = Mathf.Clamp01(Wildness);
            Covenant = Mathf.Clamp01(Covenant);
            State = DeriveState(cfg);
        }

        StagState DeriveState(Map2Config cfg)
        {
            if (Covenant <= cfg.brokenCovenantAt) return StagState.HornedAccuser;
            if (Encounters == 0) return StagState.Hidden;
            if (Trust >= cfg.alliedTrustAt && Covenant >= cfg.alliedCovenantAt)
                return StagState.Allied;
            if (Patience <= cfg.waryPatienceAt || Wildness >= cfg.waryWildnessAt)
                return StagState.Wary;
            if (Trust >= cfg.permittingTrustAt && Covenant >= cfg.permittingCovenantAt)
                return StagState.Permitting;
            return StagState.Watching;
        }

        static bool ContainsSignal(string data, string signal)
        {
            if (string.IsNullOrEmpty(data)) return false;
            int at = data.IndexOf(signal, System.StringComparison.Ordinal);
            while (at >= 0)
            {
                bool before = at == 0 || !IsId(data[at - 1]);
                int end = at + signal.Length;
                bool after = end >= data.Length || !IsId(data[end]);
                if (before && after) return true;
                at = data.IndexOf(signal, at + 1, System.StringComparison.Ordinal);
            }
            return false;
        }

        static bool IsId(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
