namespace Abbey.Morale
{
    /// <summary>
    /// The seven scalar moral pressures the settlement carries (ROADMAP Phase 3 item 3).
    /// They are a deterministic fold over the event log + active law tags (see
    /// <see cref="PressureSystem"/>); household + individual sanity (P3-03) and beast
    /// status (P3-07) feed the abbey transformation as separate inputs, not channels here.
    /// </summary>
    public enum PressureId
    {
        Trust,     // Trust in the Bellkeeper — gates volunteer behaviours
        Sanctity,  // Sanctity of the Abbey
        Mercy,     // how humanely the dead / weak are treated
        Fear,      // dread of the night, of harsh rule
        Reason,    // cool-headedness vs superstition
        Hunger,    // pressure of short rations
        OldFaith   // pull of the old pagan faith
    }

    /// <summary>
    /// Bellkeeper-trust bands (P3-10). Thresholds live in <see cref="PressuresConfig"/>;
    /// downstream volunteer behaviours gate on a minimum tier
    /// (<see cref="PressureSystem.IsVolunteerEligible"/>).
    /// </summary>
    public enum TrustTier
    {
        Broken,   // trust collapsed — no one volunteers
        Wary,
        Neutral,
        Trusting,
        Devoted   // the settlement follows the bell anywhere
    }

    /// <summary>
    /// The abbey's derived identity (ROADMAP Phase 3 item 6). <see cref="Balanced"/> is the
    /// neutral starting form before any transformation dominates; the five named forms are
    /// the transformation states, each applying settlement-wide modifiers through
    /// <see cref="Abbey.Buildings.AbbeyState"/>.
    /// </summary>
    public enum AbbeyForm
    {
        Balanced,
        Sanctuary, // high sanctity + mercy
        Fortress,  // high fear + reason, warrior investment
        Famine,    // hunger dominant
        Cult,      // old-faith dominant + tolerated / secret rites
        Broken     // trust collapsed / mass sanity loss
    }

    /// <summary>
    /// Volunteer behaviours whose availability is gated by the Bellkeeper trust tier
    /// (P3-10): the night watch, standing guards and later warrior recruitment volume.
    /// </summary>
    public enum VolunteerRole
    {
        NightWatch,
        Guard,
        WarriorRecruitment
    }
}
