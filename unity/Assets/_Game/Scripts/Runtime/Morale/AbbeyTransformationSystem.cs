using Abbey.Buildings;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Morale
{
    /// <summary>
    /// The abbey transformation system (P3-10, ROADMAP Phase 3 item 6). Once per dawn it
    /// reads a <see cref="PressureSnapshot"/> from the <see cref="PressureSystem"/>, scores
    /// each candidate form (<see cref="AbbeyForm.Sanctuary"/> / <see cref="AbbeyForm.Fortress"/>
    /// / <see cref="AbbeyForm.Famine"/> / <see cref="AbbeyForm.Cult"/> / <see cref="AbbeyForm.Broken"/>)
    /// through <see cref="PressuresConfig"/>, and adopts the highest form that clears its
    /// activation threshold — with <b>hysteresis</b> so a marginal swing does not flip the
    /// abbey's identity day to day. When none qualifies the abbey stays
    /// <see cref="AbbeyForm.Balanced"/>. Adopting a form pushes its data-driven modifiers onto
    /// <see cref="AbbeyState"/> (sacred-light radius, window volley, ration ceiling, recall
    /// penalty, offerings) and every transition is event-logged ("abbey_transformation").
    ///
    /// Deterministic: the form is a pure function of the pressures (themselves a pure fold of
    /// the log) plus the standing laws. Singleton + [ExecuteAlways] like the other Phase 3
    /// systems; the evaluation runs at dawn but tests call <see cref="EvaluateAtDawn"/> or
    /// <see cref="Evaluate"/> directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AbbeyTransformationSystem : MonoBehaviour
    {
        public static AbbeyTransformationSystem Instance { get; private set; }

        PressuresConfig _config;
        bool _isDuplicate;

        /// <summary>The abbey's current derived form (Balanced until a transformation dominates).</summary>
        public AbbeyForm CurrentForm { get; private set; } = AbbeyForm.Balanced;

        /// <summary>The winning form's score at the last evaluation (debug overlay).</summary>
        public float LastScore { get; private set; }

        public PressuresConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PressuresConfig.LoadOrDefault();
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
                Debug.LogWarning("[AbbeyTransformationSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config and resets the abbey to Balanced (tests).</summary>
        public void Configure(PressuresConfig config)
        {
            _config = config;
            CurrentForm = AbbeyForm.Balanced;
            LastScore = 0f;
        }

        /// <summary>Evaluates from the live <see cref="PressureSystem"/> snapshot (dawn hook).</summary>
        public void EvaluateAtDawn()
        {
            var pressures = PressureSystem.Instance;
            if (pressures == null)
            {
                return;
            }
            Evaluate(pressures.Snapshot());
        }

        /// <summary>
        /// Picks the abbey form for a snapshot, applies hysteresis against the current form,
        /// and — on a change — logs the transition and pushes the new modifiers onto
        /// <see cref="AbbeyState"/>. Public so tests can drive crafted snapshots. Deterministic.
        /// </summary>
        public AbbeyForm Evaluate(PressureSnapshot snapshot)
        {
            if (_isDuplicate)
            {
                return CurrentForm;
            }
            var cfg = Config;

            // Best candidate: the highest-scoring form that clears its activation threshold.
            AbbeyForm bestForm = AbbeyForm.Balanced;
            float bestScore = 0f;
            bool haveBest = false;

            if (cfg.formRules != null)
            {
                for (int i = 0; i < cfg.formRules.Count; i++)
                {
                    var rule = cfg.formRules[i];
                    if (rule == null)
                    {
                        continue;
                    }
                    float score = Score(rule, snapshot);
                    if (score < rule.activationThreshold)
                    {
                        continue;
                    }
                    if (!haveBest || score > bestScore)
                    {
                        haveBest = true;
                        bestForm = rule.form;
                        bestScore = score;
                    }
                }
            }

            // Hysteresis: while the current named form is still valid, a challenger must beat
            // it by the configured margin to take over.
            AbbeyForm next = bestForm;
            float nextScore = bestScore;
            if (CurrentForm != AbbeyForm.Balanced && CurrentForm != bestForm)
            {
                var currentRule = RuleFor(CurrentForm);
                if (currentRule != null)
                {
                    float currentScore = Score(currentRule, snapshot);
                    bool currentStillValid = currentScore >= currentRule.activationThreshold;
                    if (currentStillValid && bestScore <= currentScore + cfg.transformationHysteresisMargin)
                    {
                        next = CurrentForm;
                        nextScore = currentScore;
                    }
                }
            }

            LastScore = nextScore;
            if (next != CurrentForm)
            {
                var previous = CurrentForm;
                CurrentForm = next;
                ApplyModifiers(next);
                GameEventLog.Append("abbey_transformation",
                    $"{previous}->{next} (score={nextScore:F2})");
            }
            return CurrentForm;
        }

        /// <summary>The linear score for one form under a snapshot (weights + favoured tags).</summary>
        public static float Score(AbbeyFormRule rule, PressureSnapshot snap)
        {
            float score = rule.bias
                + rule.wTrust * snap.Trust
                + rule.wSanctity * snap.Sanctity
                + rule.wMercy * snap.Mercy
                + rule.wFear * snap.Fear
                + rule.wReason * snap.Reason
                + rule.wHunger * snap.Hunger
                + rule.wOldFaith * snap.OldFaith
                + rule.wBeastStatus * snap.BeastStatus
                + rule.wHouseholdSanity * snap.HouseholdSanity;

            if (rule.favouredTags != null && rule.tagBonus != 0f)
            {
                for (int i = 0; i < rule.favouredTags.Count; i++)
                {
                    if (snap.HasTag(rule.favouredTags[i]))
                    {
                        score += rule.tagBonus;
                    }
                }
            }
            return score;
        }

        AbbeyFormRule RuleFor(AbbeyForm form)
        {
            var cfg = Config;
            if (cfg.formRules != null)
            {
                for (int i = 0; i < cfg.formRules.Count; i++)
                {
                    if (cfg.formRules[i] != null && cfg.formRules[i].form == form)
                    {
                        return cfg.formRules[i];
                    }
                }
            }
            return null;
        }

        void ApplyModifiers(AbbeyForm form)
        {
            var rule = RuleFor(form);
            var mods = rule != null ? rule.modifiers : null;
            AbbeyState.SetTransformation(form, mods);
        }
    }
}
