using Abbey.Villagers;

namespace Abbey.Sanity
{
    /// <summary>
    /// A villager's mind on the persistent-damage track (health is the transient
    /// one). Bands run from <see cref="Stable"/> down through <see cref="Shaken"/>
    /// and <see cref="Breaking"/> to <see cref="Insane"/>. The thresholds live in
    /// <see cref="SanityConfig"/>; <see cref="SanitySystem"/> keeps a
    /// <see cref="SanityRecord"/> per villager and drives the transitions.
    /// </summary>
    public enum SanityState
    {
        Stable,
        Shaken,
        Breaking,
        Insane
    }

    /// <summary>
    /// Per-villager sanity record (the hidden state the debug overlay surfaces).
    /// <see cref="Sanity"/> is the persistent 0..1 track (1 = sound of mind);
    /// <see cref="Dread"/> is the transient 0..1 meter that fills in the dark at
    /// night and, once past the config's damage threshold, eats into sanity.
    ///
    /// A villager whose sanity falls below the insanity threshold enters
    /// <see cref="SanityState.Insane"/> and <see cref="Recovering"/> latches true:
    /// it holds the Insane band (with hysteresis) until sanity climbs back to the
    /// config release threshold, whether that recovery happens fast in the asylum
    /// (<see cref="HeldInAsylum"/>) or slowly at home. A plain reference type so the
    /// debug panel and downstream systems read the live values without copying.
    /// </summary>
    public class SanityRecord
    {
        public readonly VillagerAgent Villager;

        /// <summary>Persistent sanity 0..1 (1 = sound). Does not reset at morning.</summary>
        public float Sanity = 1f;

        /// <summary>Transient dread 0..1: rises in the dark at night, damages sanity when high.</summary>
        public float Dread;

        /// <summary>Current band, updated by <see cref="SanitySystem"/>.</summary>
        public SanityState State = SanityState.Stable;

        /// <summary>
        /// Latches true when the villager goes Insane and clears only when sanity
        /// recovers to the release threshold — the hysteresis that keeps a barely
        /// recovered mind from flickering in and out of insanity.
        /// </summary>
        public bool Recovering;

        /// <summary>True while the asylum holds this villager (parked, missing the night).</summary>
        public bool HeldInAsylum;

        /// <summary>Day number the asylum admitted this villager (for the cooldown).</summary>
        public int AdmitDay;

        public SanityRecord(VillagerAgent villager)
        {
            Villager = villager;
        }

        public bool IsInsane => State == SanityState.Insane;
    }
}
