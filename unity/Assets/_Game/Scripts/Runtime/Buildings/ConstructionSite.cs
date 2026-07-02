using System;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// A placed-but-unbuilt structure. Two sequential needs, in order:
    /// materials (units of the catalog cost list delivered one haul at a time via
    /// <see cref="DeliverResource"/>) and then work (builder seconds via
    /// <see cref="ApplyWork"/> — work is refused while materials are missing).
    /// Placement reserves NO resources: the cost is taken from the
    /// <see cref="ResourceLedger"/> at delivery time, one accepted batch at a time,
    /// so an abandoned site never strands more than what was already hauled.
    /// On completion the site swaps itself for the finished building
    /// (<see cref="Building.Construct"/>), logs a "build" record and deactivates.
    /// Static registry mirrors <see cref="Abbey.Economy.SalvageSite"/>; deterministic,
    /// integer units, no RNG. [ExecuteAlways] so EditMode tests get OnEnable/
    /// OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ConstructionSite : MonoBehaviour
    {
        [Tooltip("Optional transform scaled up as work progresses (placeholder visual).")]
        public Transform visualRoot;

        static readonly List<ConstructionSite> _active = new List<ConstructionSite>();

        /// <summary>Every enabled construction site (builder assignment, debug panel).</summary>
        public static IReadOnlyList<ConstructionSite> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        int[] _delivered;
        float _workApplied;

        /// <summary>Catalog entry being built (null until <see cref="Initialize"/>).</summary>
        public BuildingType Type { get; private set; }

        /// <summary>Raised once when the site finishes, with the spawned building.</summary>
        public event Action<ConstructionSite, Building> Completed;

        public bool IsComplete { get; private set; }

        /// <summary>The finished building spawned on completion (null before that).</summary>
        public Building CompletedBuilding { get; private set; }

        /// <summary>True while at least one cost unit is still undelivered.</summary>
        public bool NeedsMaterials
        {
            get
            {
                if (IsComplete || Type == null)
                {
                    return false;
                }
                for (int i = 0; i < ResourceTypes.Count; i++)
                {
                    if (RemainingNeed((ResourceType)i) > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>True when materials are complete but builder seconds remain.</summary>
        public bool NeedsWork => !IsComplete && Type != null && !NeedsMaterials
                                 && _workApplied < Type.buildWorkSeconds;

        /// <summary>Work progress 0..1 (0 until materials are complete, 1 when finished).</summary>
        public float Progress
        {
            get
            {
                if (IsComplete)
                {
                    return 1f;
                }
                if (Type == null || Type.buildWorkSeconds <= 0f)
                {
                    return 0f;
                }
                return Mathf.Clamp01(_workApplied / Type.buildWorkSeconds);
            }
        }

        /// <summary>Total units of one resource the catalog demands (duplicate stacks accumulate).</summary>
        public int Required(ResourceType type)
        {
            if (Type == null || Type.cost == null)
            {
                return 0;
            }
            int required = 0;
            for (int i = 0; i < Type.cost.Count; i++)
            {
                if (Type.cost[i].type == type)
                {
                    required += Mathf.Max(0, Type.cost[i].amount);
                }
            }
            return required;
        }

        public int Delivered(ResourceType type)
        {
            return _delivered != null ? _delivered[(int)type] : 0;
        }

        /// <summary>Units of one resource still to deliver.</summary>
        public int RemainingNeed(ResourceType type)
        {
            return Required(type) - Delivered(type);
        }

        /// <summary>Occupied ground rect on the XZ plane; zero-size until initialized.</summary>
        public Rect Footprint => Type != null
            ? Type.FootprintAt(transform.position)
            : new Rect(transform.position.x, transform.position.z, 0f, 0f);

        /// <summary>Binds the site to its catalog entry (called by <see cref="BuildingPlacer"/>).</summary>
        public void Initialize(BuildingType type)
        {
            Type = type;
            _delivered = new int[ResourceTypes.Count];
            _workApplied = 0f;
            IsComplete = false;
            UpdateVisual();
        }

        void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        void OnDisable()
        {
            _active.Remove(this);
        }

        /// <summary>
        /// Delivers up to <paramref name="amount"/> units of one resource, clamped
        /// by both the remaining need and the ledger's stock; the accepted units are
        /// consumed from the <see cref="ResourceLedger"/> at this moment (delivery
        /// pays, placement never does). Returns the units actually accepted — the
        /// villager-integration point: a Builder calls this per haul and keeps
        /// whatever was refused. When the last unit arrives and the catalog asks
        /// for zero work seconds, the site completes immediately.
        /// </summary>
        public int DeliverResource(ResourceType type, int amount)
        {
            if (IsComplete || Type == null || amount <= 0)
            {
                return 0;
            }

            int accepted = Mathf.Min(amount, RemainingNeed(type));
            accepted = Mathf.Min(accepted, ResourceLedger.Get(type));
            if (accepted <= 0)
            {
                return 0;
            }
            if (!ResourceLedger.TryConsume(type, accepted, $"build {Type.id}"))
            {
                return 0;
            }

            _delivered[(int)type] += accepted;
            if (!NeedsMaterials)
            {
                GameEventLog.Append("build", $"{Type.id} materials complete");
                if (Type.buildWorkSeconds <= 0f)
                {
                    Complete();
                }
            }
            return accepted;
        }

        /// <summary>
        /// Applies builder work. Refused (returns 0) while materials are missing —
        /// hammering an empty site does nothing. Returns the seconds actually
        /// counted (clamped to what was left), completing the building when the
        /// catalog's buildWorkSeconds is reached.
        /// </summary>
        public float ApplyWork(float seconds)
        {
            if (IsComplete || Type == null || seconds <= 0f || NeedsMaterials)
            {
                return 0f;
            }

            float applied = Mathf.Min(seconds, Type.buildWorkSeconds - _workApplied);
            if (applied <= 0f)
            {
                return 0f;
            }
            _workApplied += applied;
            UpdateVisual();
            if (_workApplied >= Type.buildWorkSeconds)
            {
                Complete();
            }
            return applied;
        }

        void Complete()
        {
            if (IsComplete)
            {
                return;
            }
            IsComplete = true;

            CompletedBuilding = Building.Construct(Type, transform.position);
            GameEventLog.Append("build",
                $"{Type.id} complete at ({transform.position.x:F1}, {transform.position.z:F1})");
            Completed?.Invoke(this, CompletedBuilding);

            // Swap: the finished building stands in this site's place; the site
            // deactivates (unregistering from Active) rather than destroying itself
            // so callers holding a reference can still read its final state.
            gameObject.SetActive(false);
        }

        void UpdateVisual()
        {
            if (visualRoot != null && Type != null && Type.buildWorkSeconds > 0f)
            {
                // Placeholder read of progress: the frame rises (visual only).
                visualRoot.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Progress);
            }
        }
    }
}
