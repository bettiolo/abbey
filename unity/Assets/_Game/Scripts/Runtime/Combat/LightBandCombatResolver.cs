using Abbey.Light;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>
    /// The outcome of resolving one attacker against the light band it fights in:
    /// the multiplier to apply to its outgoing damage, and the sanity drained per
    /// second while it fights there (friendlies in the Dark only). Pure data.
    /// </summary>
    public readonly struct CombatTreatment
    {
        /// <summary>Multiplier applied to the attacker's outgoing damage (1 = no change).</summary>
        public readonly float DamageMultiplier;

        /// <summary>Sanity drained per second while fighting here (0 unless a friendly in the Dark).</summary>
        public readonly float SanityDrainPerSecond;

        /// <summary>The band the fight resolved in.</summary>
        public readonly LightZone Band;

        /// <summary>True when the attacker is the beast (immune to all band penalties).</summary>
        public readonly bool BeastExempt;

        public CombatTreatment(float damageMultiplier, float sanityDrainPerSecond,
            LightZone band, bool beastExempt)
        {
            DamageMultiplier = damageMultiplier;
            SanityDrainPerSecond = sanityDrainPerSecond;
            Band = band;
            BeastExempt = beastExempt;
        }
    }

    /// <summary>
    /// Pure, deterministic combat resolution on the light-band gradient (ROADMAP
    /// Phase 3 item 17): given an attacker's side, whether it is the beast, the band
    /// the fight happens in and the <see cref="CombatConfig"/>, it returns the damage
    /// multiplier and sanity-drain rate. No RNG, no side effects — the single source
    /// of truth every fighter (window volleys, door assaults, warriors, the hero)
    /// routes its damage through, so the "light is territory" rule is applied
    /// identically everywhere and can be unit-tested exhaustively.
    ///
    /// <list type="bullet">
    /// <item><b>Safe</b> — monsters debuffed (the light weakens them); friendlies even.</item>
    /// <item><b>Edge</b> — even for both sides.</item>
    /// <item><b>Dark</b> — friendlies debuffed and their sanity drains; monsters even.</item>
    /// <item><b>Beast</b> — exempt in every band: multiplier 1, no drain.</item>
    /// </list>
    /// </summary>
    public static class LightBandCombatResolver
    {
        /// <summary>
        /// Resolves the treatment for an attacker fighting in <paramref name="band"/>.
        /// A null config falls back to <see cref="CombatConfig.LoadOrDefault"/>.
        /// </summary>
        public static CombatTreatment Resolve(CombatSide side, bool isBeast, LightZone band,
            CombatConfig config)
        {
            if (config == null)
            {
                config = CombatConfig.LoadOrDefault();
            }

            // The beast bypasses the band gradient entirely (exempt everywhere).
            if (isBeast)
            {
                return new CombatTreatment(1f, 0f, band, true);
            }

            if (side == CombatSide.Monster)
            {
                float mult;
                switch (band)
                {
                    case LightZone.Safe: mult = config.safeMonsterDamageMultiplier; break;
                    case LightZone.Edge: mult = config.edgeMonsterDamageMultiplier; break;
                    default: mult = config.darkMonsterDamageMultiplier; break;
                }
                return new CombatTreatment(Mathf.Max(0f, mult), 0f, band, false);
            }

            // Friendly.
            switch (band)
            {
                case LightZone.Safe:
                    return new CombatTreatment(
                        Mathf.Max(0f, config.safeFriendlyDamageMultiplier), 0f, band, false);
                case LightZone.Edge:
                    return new CombatTreatment(
                        Mathf.Max(0f, config.edgeFriendlyDamageMultiplier), 0f, band, false);
                default: // Dark: debuffed damage AND a sanity drain.
                    return new CombatTreatment(
                        Mathf.Max(0f, config.darkFriendlyDamageMultiplier),
                        Mathf.Max(0f, config.darkFriendlySanityDrainPerSecond), band, false);
            }
        }

        /// <summary>
        /// Convenience overload that classifies the band at a world position through
        /// <see cref="DarknessEvaluator"/> (which already sees the P3-01 weather
        /// light-effectiveness multiplier) and resolves there.
        /// </summary>
        public static CombatTreatment ResolveAt(CombatSide side, bool isBeast, Vector3 position,
            CombatConfig config)
        {
            return Resolve(side, isBeast, DarknessEvaluator.Classify(position), config);
        }
    }
}
