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

        [Header("Bellkeeper")]
        [Min(0f)] public float bellkeeperMoveSpeed = 4f;
        [Min(0f)] public float bellkeeperMaxHealth = 100f;
        [Min(0f)] public float bellkeeperMaxStamina = 100f;
        [Min(0f)] public float bellkeeperStaminaRegenPerSecond = 4f;
        [Tooltip("Stamina drained per second while the carried flame is lit.")]
        [Min(0f)] public float carriedFlameStaminaPerSecond = 2f;
        [Min(0f)] public float interactRange = 2f;
        [Min(0f)] public float rescueCooldownSeconds = 0.5f;
        [Min(0f)] public float feedCooldownSeconds = 0.5f;
        [Min(0f)] public float carryFlameCooldownSeconds = 0.25f;
        [Min(0)] public int startingCarriedFood = 3;

        [Header("Movement (shared kinematic steering)")]
        [Tooltip("Distance at which straight-line steering counts as arrived.")]
        [Min(0.01f)] public float arrivalRadius = 0.3f;

        [Header("Villagers")]
        [Min(0f)] public float villagerWalkSpeed = 2f;
        [Min(0f)] public float villagerPanicSpeed = 3.5f;
        [Range(0f, 1f)] public float villagerBraveryMin = 0.2f;
        [Range(0f, 1f)] public float villagerBraveryMax = 0.9f;
        [Tooltip("Fear gained per second standing in Dark at Dusk/Night (bravery scales it down).")]
        [Min(0f)] public float villagerFearPerSecondInDark = 0.15f;
        [Min(0f)] public float villagerFearPerSecondInEdge = 0.04f;
        [Min(0f)] public float villagerFearRecoveryPerSecond = 0.25f;
        [Tooltip("Fear rate multiplier at bravery 1 (1 = bravery has no effect).")]
        [Range(0f, 1f)] public float braveFearMultiplier = 0.4f;
        [Range(0f, 1f)] public float villagerPanicFearThreshold = 0.6f;
        [Tooltip("Panic breaks when fear falls below panicFearThreshold * this fraction.")]
        [Range(0f, 1f)] public float villagerPanicBreakFearFraction = 0.5f;
        [Tooltip("Continuous seconds in Dark (at Dusk/Night) before a villager is Injured.")]
        [Min(0f)] public float villagerInjuredDarkSeconds = 12f;
        [Tooltip("Continuous seconds in Dark before an Injured villager goes Missing.")]
        [Min(0f)] public float villagerMissingDarkSeconds = 30f;
        [Range(0.01f, 1f)] public float villagerInjuredSpeedMultiplier = 0.5f;
        [Min(0.01f)] public float villagerWorkDurationSeconds = 3f;
        [Min(0.01f)] public float villagerPickupDurationSeconds = 0.5f;
        [Min(0.01f)] public float villagerPanicDirectionChangeSeconds = 0.8f;
        [Min(0.01f)] public float villagerRestDurationSeconds = 8f;
        [Tooltip("Follow distance behind the hero while being rescued.")]
        [Min(0.1f)] public float rescueFollowDistance = 1.5f;
        [Tooltip("Villagers at least this brave finish their current work before recalling.")]
        [Range(0f, 1f)] public float braveryFinishWorkThreshold = 0.65f;

        [Header("Dusk recall")]
        [Tooltip("Distance from the nearest Safe point beyond which a villager is Endangered at dusk.")]
        [Min(0f)] public float duskRecallEndangeredDistance = 12f;
        [Tooltip("Recall delay for villagers NOT covered by a bell pulse (the drama beat).")]
        [Min(0f)] public float duskLateRecallDelaySeconds = 6f;
        [Tooltip("Speed multiplier for villagers recalled under a bell pulse.")]
        [Min(1f)] public float bellRecallSpeedMultiplier = 1.5f;
        [Tooltip("How long a bell pulse stays valid for the dusk evaluation.")]
        [Min(0f)] public float bellPulseMemorySeconds = 30f;
        [Tooltip("Fear removed from a villager covered by a bell pulse (the bell lowers panic).")]
        [Range(0f, 1f)] public float bellCalmAmount = 0.2f;

        [Header("Black Hound thresholds (0..1 values)")]
        [Range(0f, 1f)] public float trustFedThreshold = 0.5f;
        [Range(0f, 1f)] public float trustFollowThreshold = 0.75f;
        [Range(0f, 1f)] public float hungerStarvingThreshold = 0.8f;
        [Range(0f, 1f)] public float feedTrustGain = 0.15f;
        [Range(0f, 1f)] public float feedHungerRelief = 0.4f;
        [Range(0f, 1f)] public float feedFearRelief = 0.05f;
        [Range(0f, 1f)] public float feedAttachmentGain = 0.05f;
        [Min(0f)] public float houndHungerPerSecond = 0.001f;

        [Header("Black Hound start values (chained, wounded, starving)")]
        [Range(0f, 1f)] public float houndStartTrust = 0.1f;
        [Range(0f, 1f)] public float houndStartHunger = 0.9f;
        [Range(0f, 1f)] public float houndStartPain = 0.6f;
        [Range(0f, 1f)] public float houndStartFear = 0.5f;
        [Range(0f, 1f)] public float houndStartAttachment = 0f;

        [Header("Black Hound movement / combat")]
        [Tooltip("Must exceed monsterFleeSpeed or the hound can never catch its quarry.")]
        [Min(0f)] public float houndMoveSpeed = 4.5f;
        [Tooltip("A Fed/Following hound intercepts monsters inside this range.")]
        [Min(0f)] public float houndEngageRange = 12f;
        [Min(0f)] public float houndAttackRange = 1.5f;
        [Min(0f)] public float houndAttackDamage = 50f;
        [Min(0.01f)] public float houndAttackCooldownSeconds = 1f;

        [Header("Black Hound bond (P2-05 full state set)")]
        [Tooltip("Trust at/above this (plus attachment below) reads as Trusting, the top bond state.")]
        [Range(0f, 1f)] public float trustTrustingThreshold = 0.9f;
        [Range(0f, 1f)] public float trustingAttachmentThreshold = 0.4f;
        [Tooltip("An unchained, sated hound with at least this much trust settles into Guarding inside Safe light.")]
        [Range(0f, 1f)] public float guardTrustThreshold = 0.75f;
        [Tooltip("Trust gained the moment the chain comes off (free chain: trust up, control risk up).")]
        [Range(0f, 1f)] public float freeChainTrustGain = 0.05f;
        [Tooltip("Post-gain trust below this when freed: the hound turns Angry and flees to Missing.")]
        [Range(0f, 1f)] public float freeChainFollowThreshold = 0.35f;
        [Tooltip("A freed no-bond hound runs this far from its release point, then is Missing.")]
        [Min(0f)] public float houndFleeDistance = 25f;
        [Tooltip("Approach-slowly bites (deterministically) when fear + pain reach this sum.")]
        [Range(0f, 2f)] public float approachBiteThreshold = 1f;
        [Range(0f, 1f)] public float approachTrustGain = 0.05f;
        [Range(0f, 1f)] public float approachAttachmentGain = 0.08f;
        [Range(0f, 1f)] public float approachFearRelief = 0.05f;
        [Range(0f, 1f)] public float approachBiteTrustLoss = 0.1f;
        [Range(0f, 1f)] public float approachBiteFearGain = 0.1f;
        [Tooltip("Damage the bite deals to the approaching Bellkeeper.")]
        [Min(0f)] public float houndBiteDamage = 15f;
        [Tooltip("Fear removed from a still-chained hound when the bell rings within its radius.")]
        [Range(0f, 1f)] public float bellCalmFearRelief = 0.15f;
        [Range(0f, 1f)] public float bellCalmTrustGain = 0.05f;
        [Tooltip("Resentment: trust lost when the Bellkeeper explicitly walks away from the chain.")]
        [Range(0f, 1f)] public float leaveChainedTrustLoss = 0.05f;
        [Tooltip("Pain AND fear at/above these turn the hound Angry.")]
        [Range(0f, 1f)] public float houndAngryPainThreshold = 0.7f;
        [Range(0f, 1f)] public float houndAngryFearThreshold = 0.6f;
        [Tooltip("Pain alone at/above this reads as Wounded (no movement, no engagement).")]
        [Range(0f, 1f)] public float houndWoundedPainThreshold = 0.75f;
        [Range(0f, 1f)] public float houndHitFearGain = 0.1f;
        [Min(0f)] public float houndPainRecoveryPerSecond = 0.005f;
        [Tooltip("Minimum trust for the chained bonded hound to break its chain for the endangered hero.")]
        [Range(0f, 1f)] public float chainBreakTrustThreshold = 0.5f;
        [Tooltip("A live monster within this range of a hero standing in Dark triggers Protective.")]
        [Min(0f)] public float houndProtectMonsterRange = 10f;
        [Tooltip("How far a Hunting hound drags a fresh kill toward darkness before eating.")]
        [Min(0f)] public float houndDragDistance = 8f;
        [Tooltip("Hunger relieved when the Hunting hound eats its dragged kill alone.")]
        [Range(0f, 1f)] public float houndEatHungerRelief = 0.35f;

        [Header("Monsters")]
        [Min(0f)] public float monsterMoveSpeed = 2.5f;
        [Min(0f)] public float monsterFleeSpeed = 4f;
        [Tooltip("Maximum light intensity (0..1) a monster tolerates before retreating.")]
        [Range(0f, 1f)] public float monsterLightTolerance = 0.15f;
        [Min(1f)] public float monsterMaxHealth = 100f;
        [Min(0f)] public float monsterAttackRange = 1.2f;
        [Min(0.01f)] public float monsterAttackCooldownSeconds = 2f;
        [Tooltip("A fleeing monster keeps running until this far from the hound.")]
        [Min(0f)] public float monsterFleeDistance = 15f;
        [Min(0f)] public float monsterSightRange = 60f;

        [Header("Nightmare director")]
        [Min(0)] public int firstNightMonsterCount = 1;
        [Tooltip("Monsters spawn on a dark ring at least this far from the map center.")]
        [Min(0f)] public float monsterSpawnMinRadius = 20f;
        [Min(0f)] public float monsterSpawnMaxRadius = 30f;
        [Min(1)] public int monsterSpawnAttempts = 32;
        [Tooltip("Seed for every deterministic random draw in the simulation.")]
        public int simulationSeed = 12345;

        [Header("Bell")]
        [Min(0f)] public float bellRadius = 15f;
        [Min(0f)] public float bellCooldownSeconds = 5f;

        [Header("Isometric camera")]
        [Min(0f)] public float cameraPanSpeed = 12f;
        [Min(0f)] public float cameraZoomSpeed = 10f;
        [Min(0.01f)] public float cameraMinOrthoSize = 4f;
        [Min(0.01f)] public float cameraMaxOrthoSize = 20f;
        [Min(0.01f)] public float cameraDefaultOrthoSize = 10f;

        // ==================================================================
        // NIGHTMARE BLOCK (P2-06) — appended fields only. Owned by the
        // nightmare director task; other tasks append their own blocks below.
        // ==================================================================

        [Header("Nightmares — Phase 2 director (P2-06)")]
        [Tooltip("Opt-in: run the scripted Phase 2 night (schedule below) instead of the 0.1 single-spawn night. Off keeps the legacy one-pale-hound first night.")]
        public bool phase2NightsEnabled;

        [Tooltip("Scripted night events, each 'fraction:kind'. Fraction is 0..1 time into the night; kinds: pale_hound, drowned_sailor, lantern_moth, whisper, shadow, panic. Unparseable entries are skipped and logged.")]
        public string[] phase2NightSchedule =
        {
            "0.05:whisper",
            "0.10:pale_hound",
            "0.20:shadow",
            "0.30:lantern_moth",
            "0.40:whisper",
            "0.45:pale_hound",
            "0.55:drowned_sailor",
            "0.65:panic",
            "0.75:pale_hound",
            "0.90:whisper",
        };

        [Tooltip("A villager death within this XZ distance of the wreck anchor counts as died-by-water (gates the drowned sailor).")]
        [Min(0f)] public float waterDeathRadius = 8f;

        [Tooltip("The drowned sailor rises within this distance of the wreck anchor.")]
        [Min(0f)] public float drownedSailorSpawnRadius = 6f;

        [Tooltip("Slow, dripping dread-line speed toward the nearest lit zone.")]
        [Min(0f)] public float drownedSailorMoveSpeed = 1.2f;

        [Tooltip("Extinguish-resistant: light intensity tolerated by the sailor (well above the pale hound's monsterLightTolerance). Safe zones and sacred light still repel it.")]
        [Range(0f, 1f)] public float drownedSailorLightTolerance = 0.6f;

        [Min(0f)] public float lanternMothMoveSpeed = 5f;

        [Min(0f)] public float lanternMothFleeSpeed = 7f;

        [Tooltip("The moth abandons its light while the Bellkeeper is inside this range.")]
        [Min(0f)] public float lanternMothFleeRange = 5f;

        [Tooltip("The moth must be this close to a light to drain its fuel.")]
        [Min(0f)] public float lanternMothDrainRange = 1.5f;

        [Tooltip("Fuel seconds drained per second while the moth clings to a light (fast: it creates a darkness gap).")]
        [Min(0f)] public float lanternMothDrainPerSecond = 20f;

        [Tooltip("Bell = weak-nightmare stun (spec section 5): how long a bell pulse stuns weak nightmares inside its radius.")]
        [Min(0f)] public float bellNightmareStunSeconds = 5f;

        [Tooltip("Whispers rise from a dark point on this inner ring fraction of monsterSpawnMinRadius (the unlit road, nearer than the spawn ring).")]
        [Range(0f, 1f)] public float whisperRingFraction = 0.6f;

        // ============================== end nightmare block ===============

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
