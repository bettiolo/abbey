using UnityEngine;

namespace Abbey.Core
{
    /// <summary>
    /// Single ScriptableObject holding ALL Prototype 0.1 tunables (AGENTS.md rule:
    /// no balance values inside MonoBehaviours). Systems fetch it via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset file to exist.
    /// An optional asset at Resources/PrototypeConfig overrides the coded defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "PrototypeConfig", menuName = "Abbey/Prototype Config")]
    public class PrototypeConfig : ScriptableObject
    {
        public const string ResourcePath = "PrototypeConfig";

        [Header("Day cycle (seconds, real time)")]
        [Min(0.01f)] public float dayDurationSeconds = 120f;
        [Min(0.01f)] public float duskDurationSeconds = 30f;
        [Min(0.01f)] public float nightDurationSeconds = 120f;
        [Min(0.01f)] public float dawnDurationSeconds = 30f;

        [Header("Light territory")]
        [Tooltip("Outer fraction of a light's effective radius that counts as Edge instead of Safe.")]
        [Range(0.01f, 0.99f)] public float edgeBandFraction = 0.3f;
        [Min(0f)] public float campfireRadius = 8f;
        [Range(0f, 1f)] public float campfireStrength = 1f;
        [Min(0f)] public float lanternRadius = 4f;
        [Range(0f, 1f)] public float lanternStrength = 0.8f;
        [Min(0f)] public float carriedFlameRadius = 3f;
        [Range(0f, 1f)] public float carriedFlameStrength = 0.6f;
        [Min(0f)] public float sacredFlameRadius = 10f;
        [Range(0f, 1f)] public float sacredFlameStrength = 1f;
        [Tooltip("Default fuel for a fresh non-sacred fire. Negative = infinite.")]
        public float defaultFuelSeconds = 180f;
        [Min(0f)] public float fuelConsumptionPerSecond = 1f;

        [Header("Villagers")]
        [Min(0f)] public float villagerWalkSpeed = 2f;
        [Min(0f)] public float villagerPanicSpeed = 3.5f;
        [Range(0f, 1f)] public float villagerBraveryMin = 0.2f;
        [Range(0f, 1f)] public float villagerBraveryMax = 0.9f;

        [Header("Black Hound thresholds (0..1 values)")]
        [Range(0f, 1f)] public float trustFedThreshold = 0.5f;
        [Range(0f, 1f)] public float trustFollowThreshold = 0.75f;
        [Range(0f, 1f)] public float hungerStarvingThreshold = 0.8f;
        [Range(0f, 1f)] public float feedTrustGain = 0.15f;
        [Range(0f, 1f)] public float feedHungerRelief = 0.4f;
        [Min(0f)] public float houndHungerPerSecond = 0.001f;

        [Header("Monsters")]
        [Min(0f)] public float monsterMoveSpeed = 2.5f;
        [Min(0f)] public float monsterFleeSpeed = 4f;
        [Tooltip("Maximum light intensity (0..1) a monster tolerates before retreating.")]
        [Range(0f, 1f)] public float monsterLightTolerance = 0.15f;

        [Header("Bell")]
        [Min(0f)] public float bellRadius = 15f;
        [Min(0f)] public float bellCooldownSeconds = 5f;

        [Header("Isometric camera")]
        [Min(0f)] public float cameraPanSpeed = 12f;
        [Min(0f)] public float cameraZoomSpeed = 10f;
        [Min(0.01f)] public float cameraMinOrthoSize = 4f;
        [Min(0.01f)] public float cameraMaxOrthoSize = 20f;
        [Min(0.01f)] public float cameraDefaultOrthoSize = 10f;

        static PrototypeConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/PrototypeConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static PrototypeConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<PrototypeConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<PrototypeConfig>();
                _cached.name = "PrototypeConfig (defaults)";
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
