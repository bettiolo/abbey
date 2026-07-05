using Abbey.Buildings;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>
    /// Which side of a fight an attacker is on. Nightmares are <see cref="Monster"/>;
    /// settlers, warriors and the Bellkeeper are <see cref="Friendly"/>. The Black
    /// Hound is neither — it passes the beast-exempt flag and ignores band penalties
    /// entirely (see <see cref="LightBandCombatResolver"/>).
    /// </summary>
    public enum CombatSide
    {
        Friendly,
        Monster
    }

    /// <summary>
    /// Single ScriptableObject holding ALL Phase 3 combat tunables (AGENTS.md rule:
    /// no balance values inside MonoBehaviours). The light-band resolver
    /// (<see cref="LightBandCombatResolver"/>) and the two-tier
    /// <see cref="HomeDefenseSystem"/> read every multiplier, cost, radius and hit
    /// point from here. Systems fetch it via <see cref="LoadOrDefault"/> so tests and
    /// CI never need an asset file to exist; an optional asset at
    /// Resources/CombatConfig overrides the coded defaults. Mirrors
    /// <see cref="Abbey.Economy.EconomyConfig"/> / <see cref="Abbey.Sanity.SanityConfig"/>.
    ///
    /// <para>Band model (ROADMAP Phase 3 item 17): combat resolves on the light-band
    /// gradient — <b>Safe</b> debuffs monsters, <b>Edge</b> is even, <b>Dark</b>
    /// debuffs friendlies and drains their sanity. The beast is exempt everywhere.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "Abbey/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        public const string ResourcePath = "CombatConfig";

        [Header("Band combat multipliers — monster (nightmare) outgoing damage")]
        [Tooltip("Safe territory debuffs monster damage (< 1: the light weakens them).")]
        [Range(0f, 1f)] public float safeMonsterDamageMultiplier = 0.35f;
        [Tooltip("Edge is even for monsters (1 = no change).")]
        [Min(0f)] public float edgeMonsterDamageMultiplier = 1f;
        [Tooltip("Dark is the monster's element (1 = no penalty).")]
        [Min(0f)] public float darkMonsterDamageMultiplier = 1f;

        [Header("Band combat multipliers — friendly (settler/warrior/hero) outgoing damage")]
        [Tooltip("Safe is home ground for friendlies (1 = no change).")]
        [Min(0f)] public float safeFriendlyDamageMultiplier = 1f;
        [Tooltip("Edge is even for friendlies (1 = no change).")]
        [Min(0f)] public float edgeFriendlyDamageMultiplier = 1f;
        [Tooltip("Dark debuffs friendly damage (< 1: fighting blind in the dark).")]
        [Range(0f, 1f)] public float darkFriendlyDamageMultiplier = 0.5f;

        [Header("Dark-band sanity drain (friendlies only; the beast is exempt)")]
        [Tooltip("Sanity drained per second a friendly spends fighting in the Dark band.")]
        [Min(0f)] public float darkFriendlySanityDrainPerSecond = 0.05f;

        [Header("Two-tier home defense — wake + lit-window fire")]
        [Tooltip("A monster within this planar distance of an occupied home wakes the house.")]
        [Min(0f)] public float wakeRadius = 4f;
        [Tooltip("Radius of the interior light that flares when a house wakes (a small Safe zone at the door).")]
        [Min(0f)] public float flareLightRadius = 5f;
        [Tooltip("Strength (0..1) of the flared interior light.")]
        [Range(0f, 1f)] public float flareLightStrength = 1f;
        [Tooltip("Damage a single window volley deals to a monster (before band scaling).")]
        [Min(0f)] public float windowShotDamage = 5f;
        [Tooltip("Seconds between window volleys while a house is awake and a monster is in range.")]
        [Min(0.01f)] public float windowShotIntervalSeconds = 1f;
        [Tooltip("Sanity each occupant loses per defensive volley (the cost of being woken to fight).")]
        [Range(0f, 1f)] public float sanityCostPerVolley = 0.03f;
        [Tooltip("A monster within this distance of an awake home keeps drawing window fire.")]
        [Min(0f)] public float defenseEngageRange = 8f;

        [Header("Destructible homes (anti-turtle raze)")]
        [Tooltip("Base structural hit points of a home before its per-type multiplier.")]
        [Min(1f)] public float baseHomeHitPoints = 30f;
        [Tooltip("Damage one monster strike deals to a home (before band scaling).")]
        [Min(0f)] public float monsterHomeAttackDamage = 6f;

        /// <summary>
        /// Structural hit points for a home of the given type: the base value scaled
        /// by the type's per-building multiplier (sturdier homes take more to raze).
        /// Balance stays here; the multiplier is a structural property of the catalog
        /// entry. Non-home / null types fall back to the base value.
        /// </summary>
        public float HomeHitPointsFor(BuildingType type)
        {
            float mult = type != null ? Mathf.Max(0f, type.homeHitPointMultiplier) : 1f;
            return Mathf.Max(1f, baseHomeHitPoints * (mult <= 0f ? 1f : mult));
        }

        static CombatConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/CombatConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static CombatConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<CombatConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<CombatConfig>();
                _cached.name = "CombatConfig (defaults)";
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
