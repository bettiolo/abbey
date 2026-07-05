using Abbey.Light;
using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// Single ScriptableObject holding every seed-slot growth tunable (AGENTS.md
    /// rule: no balance values inside MonoBehaviours). Systems fetch it via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset file to
    /// exist. An optional asset at Resources/SettlementGrowthConfig overrides the
    /// coded defaults. Mirrors <see cref="Abbey.Economy.EconomyConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "SettlementGrowthConfig", menuName = "Abbey/Settlement Growth Config")]
    public class SettlementGrowthConfig : ScriptableObject
    {
        public const string ResourcePath = "SettlementGrowthConfig";

        [Header("Slot footprints (world units, per size class)")]
        [Tooltip("Ground rect a Small slot occupies (overlap, hug and light-debt area).")]
        public Vector2 smallSlotFootprint = new Vector2(1.5f, 1.5f);
        [Tooltip("Ground rect a Medium slot occupies.")]
        public Vector2 mediumSlotFootprint = new Vector2(2f, 2f);
        [Tooltip("Ground rect a Large slot occupies.")]
        public Vector2 largeSlotFootprint = new Vector2(3f, 3f);

        [Header("Placement")]
        [Tooltip("A placement snaps to an Open slot whose center is within this planar distance; farther is refused (off-slot).")]
        [Min(0.01f)] public float slotPlacementTolerance = 1f;

        [Header("Child-slot growth (opened when a building completes)")]
        [Tooltip("How many child slots a completed building tries to open around itself.")]
        [Min(0)] public int childSlotsPerBuilding = 3;
        [Tooltip("Distance from the completed building's center to each candidate child slot (evenly spaced ring).")]
        [Min(0f)] public float childSlotRingRadius = 3.5f;
        [Tooltip("Size class of grown child slots.")]
        public SlotSizeClass childSlotSize = SlotSizeClass.Medium;
        [Tooltip("Minimum planar separation between two slot centers (candidates closer to an existing slot are dropped).")]
        [Min(0f)] public float minSlotSeparation = 2f;

        [Header("Hug rule (compact village)")]
        [Tooltip("Child slots must touch an existing building OR sit on lit ground; isolated dark plots are refused.")]
        public bool requireHug = true;
        [Tooltip("A slot 'touches a building' when its footprint expanded by this margin overlaps the building's footprint.")]
        [Min(0f)] public float hugAdjacencyMargin = 1f;
        [Tooltip("Whether Edge light (not just Safe) counts as 'lit ground' for the hug rule.")]
        public bool litGroundIncludesEdge = true;

        [Header("Light debt (overextension penalty, evaluated at dusk)")]
        [Tooltip("Debt weight per unit area for a slot/building whose center sits in Edge light.")]
        [Min(0f)] public float lightDebtEdgeWeight = 0.4f;
        [Tooltip("Debt weight per unit area for a slot/building whose center sits in the Dark.")]
        [Min(0f)] public float lightDebtDarkWeight = 1f;

        /// <summary>Footprint rect size for a slot size class.</summary>
        public Vector2 FootprintFor(SlotSizeClass sizeClass)
        {
            switch (sizeClass)
            {
                case SlotSizeClass.Small: return smallSlotFootprint;
                case SlotSizeClass.Large: return largeSlotFootprint;
                default: return mediumSlotFootprint;
            }
        }

        /// <summary>Ground area of a slot size class (light-debt area unit).</summary>
        public float AreaFor(SlotSizeClass sizeClass)
        {
            var f = FootprintFor(sizeClass);
            return Mathf.Max(0f, f.x) * Mathf.Max(0f, f.y);
        }

        /// <summary>Light-debt weight for a light zone (Safe contributes nothing).</summary>
        public float DebtWeightFor(LightZone zone)
        {
            switch (zone)
            {
                case LightZone.Dark: return lightDebtDarkWeight;
                case LightZone.Edge: return lightDebtEdgeWeight;
                default: return 0f;
            }
        }

        static SettlementGrowthConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/SettlementGrowthConfig if one
        /// exists, otherwise an in-memory instance with the coded defaults. Never
        /// returns null.
        /// </summary>
        public static SettlementGrowthConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<SettlementGrowthConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<SettlementGrowthConfig>();
                _cached.name = "SettlementGrowthConfig (defaults)";
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        /// <summary>Drops the cached instance (test isolation).</summary>
        public static void ClearCache()
        {
            _cached = null;
        }
    }
}
