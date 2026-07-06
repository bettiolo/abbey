using Abbey.Beast;

namespace Abbey.Decrees
{
    /// <summary>The five standing-law groups (P3-09). Exactly one option is active per group.</summary>
    public enum LawGroup
    {
        Food,
        NightLabour,
        Burial,
        Hound,
        OldRites
    }

    /// <summary>Food distribution policy applied in the daily ration pass.</summary>
    public enum FoodLaw
    {
        Equal,       // flat ration for everyone
        WorkersFirst, // workers full, idle/injured reduced
        BeastShare,  // hound fed from stores first
        Fasting      // all rations cut; hunger + sanity pressure
    }

    /// <summary>Night-labour policy gating the P3-08 overdrive levers.</summary>
    public enum NightLabourLaw
    {
        NoWorkAfterBell, // forbid forced night work / candle-line work
        PaidRisk,        // allowed, volunteers only, extra ration cost
        Forced           // always allowed, no consent
    }

    /// <summary>Corpse-handling policy applied when a villager dies.</summary>
    public enum BurialLaw
    {
        FullRites,  // costs candles + time, mercy boost
        MassGraves, // cheap, fear + sanctity hit, grave tags
        UseTheDead  // resources refunded, big mercy/sanctity hit, nightmare tags
    }

    /// <summary>Hound doctrine written into the P3-07 evolution system.</summary>
    public enum HoundLaw
    {
        Family,
        Weapon,
        Chained,
        Sacred
    }

    /// <summary>Old-faith policy driving sanctity / old-faith pressure and rite tags.</summary>
    public enum OldRitesLaw
    {
        TolerateOfferings, // old-faith pressure vents, sanctity slowly falls
        ForbidPaganRites   // sanctity holds, old-faith pressure builds, secret-rite tags
    }

    /// <summary>Which ration a villager draws in the daily pass.</summary>
    public enum RationClass
    {
        Worker,
        Idle
    }

    /// <summary>
    /// The durable tag vocabulary the laws write into the event log / law state. Later
    /// systems key off these strings rather than the enums so they never import the law
    /// module: P3-10 moral pressures read the standing tags, P3-11 consequence nightmares
    /// key off the per-death grave tags and the old-rite tags. Pure string mapping — no
    /// balance lives here.
    /// </summary>
    public static class LawTags
    {
        // ---- Food ------------------------------------------------------------
        public const string RationsEqual = "rations_equal";
        public const string RationsWorkersFirst = "rations_workers_first";
        public const string BeastShareActive = "beast_share_active";
        public const string FastingActive = "fasting_active";

        // ---- Night labour ----------------------------------------------------
        public const string NightWorkForbidden = "night_work_forbidden";
        public const string NightWorkPaidRisk = "night_work_paid_risk";
        public const string ForcedLabourNight = "forced_labour_night";

        // ---- Burial (standing policy) ---------------------------------------
        public const string FullRites = "full_rites";
        public const string MassGravesActive = "mass_graves_active";
        public const string UseTheDead = "use_the_dead";

        // ---- Burial (per-death grave tag, consumed by P3-11) ----------------
        public const string GraveFullRites = "grave_full_rites";
        public const string GraveMass = "grave_mass";
        public const string GraveUsed = "grave_used";

        // ---- Hound -----------------------------------------------------------
        public const string HoundFamily = "hound_family";
        public const string HoundWeapon = "hound_weapon";
        public const string HoundChained = "hound_chained";
        public const string HoundSacred = "hound_sacred";

        // ---- Old rites -------------------------------------------------------
        public const string OfferingsTolerated = "offerings_tolerated";
        public const string PaganRitesForbidden = "pagan_rites_forbidden";
        public const string OfferingMade = "offering_made";
        public const string SecretRite = "secret_rite";

        public static string For(FoodLaw law)
        {
            switch (law)
            {
                case FoodLaw.Equal: return RationsEqual;
                case FoodLaw.WorkersFirst: return RationsWorkersFirst;
                case FoodLaw.BeastShare: return BeastShareActive;
                case FoodLaw.Fasting: return FastingActive;
                default: return RationsEqual;
            }
        }

        public static string For(NightLabourLaw law)
        {
            switch (law)
            {
                case NightLabourLaw.NoWorkAfterBell: return NightWorkForbidden;
                case NightLabourLaw.PaidRisk: return NightWorkPaidRisk;
                case NightLabourLaw.Forced: return ForcedLabourNight;
                default: return NightWorkForbidden;
            }
        }

        public static string For(BurialLaw law)
        {
            switch (law)
            {
                case BurialLaw.FullRites: return FullRites;
                case BurialLaw.MassGraves: return MassGravesActive;
                case BurialLaw.UseTheDead: return UseTheDead;
                default: return FullRites;
            }
        }

        /// <summary>The tag stamped on an individual grave (per death), not the standing policy.</summary>
        public static string GraveTagFor(BurialLaw law)
        {
            switch (law)
            {
                case BurialLaw.FullRites: return GraveFullRites;
                case BurialLaw.MassGraves: return GraveMass;
                case BurialLaw.UseTheDead: return GraveUsed;
                default: return GraveFullRites;
            }
        }

        public static string For(HoundLaw law)
        {
            switch (law)
            {
                case HoundLaw.Family: return HoundFamily;
                case HoundLaw.Weapon: return HoundWeapon;
                case HoundLaw.Chained: return HoundChained;
                case HoundLaw.Sacred: return HoundSacred;
                default: return HoundFamily;
            }
        }

        public static string For(OldRitesLaw law)
        {
            switch (law)
            {
                case OldRitesLaw.TolerateOfferings: return OfferingsTolerated;
                case OldRitesLaw.ForbidPaganRites: return PaganRitesForbidden;
                default: return PaganRitesForbidden;
            }
        }

        /// <summary>Maps the Hound law onto the doctrine the P3-07 evolution system reads.</summary>
        public static HoundDoctrine ToDoctrine(HoundLaw law)
        {
            switch (law)
            {
                case HoundLaw.Family: return HoundDoctrine.Family;
                case HoundLaw.Weapon: return HoundDoctrine.Weapon;
                case HoundLaw.Chained: return HoundDoctrine.Chained;
                case HoundLaw.Sacred: return HoundDoctrine.Sacred;
                default: return HoundDoctrine.Neutral;
            }
        }
    }
}
