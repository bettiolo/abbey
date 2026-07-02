using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// Single ScriptableObject holding ALL Phase 2 economy tunables (AGENTS.md rule:
    /// no balance values inside MonoBehaviours). Systems fetch it via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset file to exist.
    /// An optional asset at Resources/EconomyConfig overrides the coded defaults.
    /// Mirrors <see cref="Abbey.Core.PrototypeConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Abbey/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        public const string ResourcePath = "EconomyConfig";

        [Header("Starting stockpile (wreck crates, VERTICAL_SLICE_SPEC §4)")]
        [Tooltip("The wreck gave a temporary head start: some wood…")]
        [Min(0)] public int startingWood = 12;
        [Tooltip("…limited food…")]
        [Min(0)] public int startingFood = 8;
        [Tooltip("…a little lamp oil…")]
        [Min(0)] public int startingOil = 6;
        [Tooltip("…and a small medicine chest.")]
        [Min(0)] public int startingMedicine = 3;

        [Header("Storage")]
        [Tooltip("Total units the camp can hold with no storage piles (loose ground stacking).")]
        [Min(0)] public int baseStorageCapacity = 20;
        [Tooltip("Extra total capacity contributed by each built Storage Pile.")]
        [Min(0)] public int storagePileCapacity = 30;

        [Header("Salvage site pool (per shipwreck node)")]
        [Min(0)] public int salvageSiteWood = 30;
        [Min(0)] public int salvageSiteFood = 15;
        [Min(0)] public int salvageSiteOil = 10;
        [Min(0)] public int salvageSiteMedicine = 5;

        [Header("Salvage work")]
        [Tooltip("Units extracted by one completed work cycle at a salvage site.")]
        [Min(1)] public int salvageYieldPerCycle = 2;
        [Tooltip("Seconds of Working a salvage cycle takes (salvager role uses this).")]
        [Min(0.01f)] public float salvageWorkDurationSeconds = 4f;

        [Header("Salvage depletion stages (Intact / Picked / Stripped)")]
        [Tooltip("Remaining fraction at or below this looks Picked.")]
        [Range(0f, 1f)] public float salvagePickedFraction = 0.66f;
        [Tooltip("Remaining fraction at or below this looks Stripped.")]
        [Range(0f, 1f)] public float salvageStrippedFraction = 0.25f;

        static EconomyConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/EconomyConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static EconomyConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<EconomyConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<EconomyConfig>();
                _cached.name = "EconomyConfig (defaults)";
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
