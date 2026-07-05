using System.Collections.Generic;
using System.Globalization;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Island
{
    /// <summary>
    /// The dilemma card system (P3-13, ROADMAP Phase 3 item 8). A FIFO queue of data-driven
    /// choice cards (Missing Salvager, Food Thief, Hound Bites a Child + any authored in
    /// <see cref="IslandConfig"/>) that interrupt play. The player reads the top card and
    /// picks one of its 2-3 options; the option's effect list is applied atomically —
    /// signed moral-pressure deltas (folded by <see cref="Abbey.Morale.PressureSystem"/> via
    /// a "pressure_delta" record), law-like tags, resource compensations/fines, and hound
    /// treatment/doctrine inputs to the P3-07 evolution. Every choice is event-logged.
    ///
    /// Singleton + [ExecuteAlways] like the other Phase 3 systems so EditMode tests get the
    /// lifecycle. Deterministic: cards are enqueued explicitly (by scripted triggers or the
    /// debug panel); effects are pure log/ledger writes. All card data lives in
    /// <see cref="IslandConfig"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DilemmaSystem : MonoBehaviour
    {
        public static DilemmaSystem Instance { get; private set; }

        readonly Queue<DilemmaCard> _queue = new Queue<DilemmaCard>();
        IslandConfig _config;
        bool _isDuplicate;

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

        /// <summary>The card awaiting a decision, or null when the queue is empty.</summary>
        public DilemmaCard PendingCard => _queue.Count > 0 ? _queue.Peek() : null;

        public int PendingCount => _queue.Count;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[DilemmaSystem] Duplicate instance ignored.", this);
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

        /// <summary>Injects a config (tests).</summary>
        public void Configure(IslandConfig config)
        {
            _config = config;
        }

        // ------------------------------------------------------------------
        // Queue
        // ------------------------------------------------------------------

        /// <summary>Enqueues a card by id (from the config deck). Returns false when unknown.</summary>
        public bool EnqueueCard(string id)
        {
            if (_isDuplicate)
            {
                return false;
            }
            var card = Config.CardFor(id);
            if (card == null)
            {
                GameEventLog.Append("dilemma", $"enqueue_failed id={id} reason=unknown");
                return false;
            }
            _queue.Enqueue(card);
            GameEventLog.Append("dilemma", $"raised id={card.id} options={CountOptions(card)}");
            return true;
        }

        /// <summary>Enqueues an explicit card instance (tests).</summary>
        public void EnqueueCard(DilemmaCard card)
        {
            if (_isDuplicate || card == null)
            {
                return;
            }
            _queue.Enqueue(card);
            GameEventLog.Append("dilemma", $"raised id={card.id} options={CountOptions(card)}");
        }

        static int CountOptions(DilemmaCard card) => card.options != null ? card.options.Count : 0;

        /// <summary>
        /// Resolves the pending card by option index: applies exactly that option's effects
        /// and dequeues it. Returns false when there is no pending card or the index is out
        /// of range.
        /// </summary>
        public bool Choose(int optionIndex)
        {
            if (_isDuplicate || _queue.Count == 0)
            {
                return false;
            }
            var card = _queue.Peek();
            if (card.options == null || optionIndex < 0 || optionIndex >= card.options.Count)
            {
                return false;
            }
            var option = card.options[optionIndex];
            _queue.Dequeue();
            GameEventLog.Append("dilemma_choice", $"card={card.id} option={option.id}");
            ApplyEffects(card, option);
            return true;
        }

        /// <summary>Resolves the pending card by option id. Returns false when not found.</summary>
        public bool ChooseById(string optionId)
        {
            var card = PendingCard;
            if (card == null || card.options == null)
            {
                return false;
            }
            for (int i = 0; i < card.options.Count; i++)
            {
                if (card.options[i].id == optionId)
                {
                    return Choose(i);
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Effect application
        // ------------------------------------------------------------------

        void ApplyEffects(DilemmaCard card, DilemmaOption option)
        {
            if (option.effects == null)
            {
                return;
            }
            for (int i = 0; i < option.effects.Count; i++)
            {
                ApplyEffect(card, option, option.effects[i]);
            }
        }

        void ApplyEffect(DilemmaCard card, DilemmaOption option, DilemmaEffect effect)
        {
            if (effect == null)
            {
                return;
            }
            switch (effect.kind)
            {
                case DilemmaEffectKind.Pressure:
                    // A signed pressure delta folded by PressureSystem's "pressure_delta" pass.
                    GameEventLog.Append("pressure_delta",
                        $"{effect.pressure}={effect.amount.ToString("0.###", CultureInfo.InvariantCulture)} " +
                        $"src=dilemma:{card.id}:{option.id}");
                    break;

                case DilemmaEffectKind.Tag:
                    GameEventLog.Append("dilemma_tag",
                        $"tag={effect.tag} card={card.id} option={option.id}");
                    break;

                case DilemmaEffectKind.Resource:
                    ApplyResource(card, effect);
                    break;

                case DilemmaEffectKind.HoundTreatment:
                    ApplyHoundTreatment(card, option, effect);
                    break;
            }
        }

        static void ApplyResource(DilemmaCard card, DilemmaEffect effect)
        {
            if (effect.resourceAmount > 0)
            {
                ResourceLedger.Add(effect.resource, effect.resourceAmount, $"dilemma:{card.id}");
            }
            else if (effect.resourceAmount < 0)
            {
                ResourceLedger.TryConsume(effect.resource, -effect.resourceAmount, $"dilemma:{card.id}");
            }
        }

        /// <summary>
        /// Writes a hound treatment / doctrine input for the P3-07 evolution. "punish" chains
        /// the hound (doctrine Chained + a treatment record the evolution folds); "rite"
        /// soothes it through the controller; "protect" is recorded without a doctrine shift.
        /// Always logs a "hound_treatment" entry so the choice is auditable even with no hound.
        /// </summary>
        void ApplyHoundTreatment(DilemmaCard card, DilemmaOption option, DilemmaEffect effect)
        {
            string kind = string.IsNullOrEmpty(effect.tag) ? "note" : effect.tag;
            var evolution = HoundEvolutionSystem.Instance;

            if (kind == "punish" && evolution != null)
            {
                evolution.Doctrine = HoundDoctrine.Chained;
            }
            else if (kind == "rite" && evolution != null && evolution.Hound != null)
            {
                evolution.Hound.ReceiveRite();
            }

            GameEventLog.Append("hound_treatment",
                $"kind={kind} card={card.id} option={option.id}");
        }

        /// <summary>Clears the queue (test isolation; keeps the instance).</summary>
        public void ClearQueue()
        {
            _queue.Clear();
        }
    }
}
