using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abbey.Morale
{
    /// <summary>
    /// The single ScriptableObject holding ALL moral-pressure and abbey-transformation
    /// balance (AGENTS.md: no magnitudes in MonoBehaviours). Mirrors
    /// <see cref="Abbey.Decrees.LawsConfig"/> / <see cref="Abbey.Economy.EconomyConfig"/>:
    /// fetched through <see cref="LoadOrDefault"/> so tests and CI never need an asset; an
    /// optional asset at Resources/PressuresConfig overrides the coded defaults.
    ///
    /// Holds the per-channel baselines / decay / clamps, the event→delta weight table the
    /// deterministic fold applies, the day-marker used to interleave decay, the trust-tier
    /// thresholds + volunteer gates, and the abbey-transformation derivation rules with each
    /// form's activation threshold, scoring weights, favoured law tags and modifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "PressuresConfig", menuName = "Abbey/Pressures Config")]
    public class PressuresConfig : ScriptableObject
    {
        public const string ResourcePath = "PressuresConfig";

        [Header("Pressure channels (baseline / decay-per-day / clamp)")]
        public List<PressureChannel> channels = DefaultChannels();

        [Header("Event → pressure weight table (deterministic fold)")]
        public List<PressureWeight> weights = DefaultWeights();

        [Header("Day marker (a decay step is applied each time this event appears in the log)")]
        [Tooltip("Event type counted as one elapsed day; decay pulls each channel toward baseline.")]
        public string dayMarkerEventType = "RationsIssued";

        [Tooltip("Only day-marker records whose data contains this fragment count (skip beast pass).")]
        public string dayMarkerDataContains = "law=";

        [Header("Trust tiers (ascending minimum Trust)")]
        public List<TrustTierBand> trustTiers = DefaultTrustTiers();

        [Header("Volunteer gates (minimum tier a behaviour needs)")]
        public List<VolunteerGate> volunteerGates = DefaultVolunteerGates();

        [Header("Abbey transformation")]
        public List<AbbeyFormRule> formRules = DefaultFormRules();

        [Tooltip("A challenger form must beat the current form's score by this margin to take over (anti-flap).")]
        [Min(0f)] public float transformationHysteresisMargin = 0.35f;

        // ------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------

        public PressureChannel ChannelFor(PressureId id)
        {
            if (channels != null)
            {
                for (int i = 0; i < channels.Count; i++)
                {
                    if (channels[i] != null && channels[i].id == id)
                    {
                        return channels[i];
                    }
                }
            }
            return new PressureChannel { id = id, baseline = 0f, min = -1f, max = 1f };
        }

        public TrustTier TierFor(float trust)
        {
            var tier = TrustTier.Broken;
            if (trustTiers == null)
            {
                return tier;
            }
            // Bands are ascending by minTrust; take the highest whose threshold is met.
            for (int i = 0; i < trustTiers.Count; i++)
            {
                if (trust >= trustTiers[i].minTrust)
                {
                    tier = trustTiers[i].tier;
                }
            }
            return tier;
        }

        public TrustTier MinTierFor(VolunteerRole role)
        {
            if (volunteerGates != null)
            {
                for (int i = 0; i < volunteerGates.Count; i++)
                {
                    if (volunteerGates[i].role == role)
                    {
                        return volunteerGates[i].minTier;
                    }
                }
            }
            return TrustTier.Neutral;
        }

        // ------------------------------------------------------------------
        // Coded defaults
        // ------------------------------------------------------------------

        static List<PressureChannel> DefaultChannels()
        {
            return new List<PressureChannel>
            {
                new PressureChannel { id = PressureId.Trust,    baseline = 0.5f, decayToBaselinePerDay = 0.01f, min = 0f, max = 1f },
                new PressureChannel { id = PressureId.Sanctity, baseline = 0.5f, decayToBaselinePerDay = 0.01f, min = 0f, max = 1f },
                new PressureChannel { id = PressureId.Mercy,    baseline = 0.5f, decayToBaselinePerDay = 0.02f, min = 0f, max = 1f },
                new PressureChannel { id = PressureId.Fear,     baseline = 0.1f, decayToBaselinePerDay = 0.03f, min = 0f, max = 1f },
                new PressureChannel { id = PressureId.Reason,   baseline = 0.5f, decayToBaselinePerDay = 0.02f, min = 0f, max = 1f },
                new PressureChannel { id = PressureId.Hunger,   baseline = 0.0f, decayToBaselinePerDay = 0.02f, min = 0f, max = 1f },
                new PressureChannel { id = PressureId.OldFaith, baseline = 0.0f, decayToBaselinePerDay = 0.0f,  min = -1f, max = 1f },
            };
        }

        static List<PressureWeight> DefaultWeights()
        {
            return new List<PressureWeight>
            {
                // A death is a wound to the settlement's soul: mercy + sanctity fall, fear +
                // grief rise, faith in the bell dips.
                Weight("villager_died", false, trust: -0.03f, sanctity: -0.02f, mercy: -0.05f, fear: 0.06f),
                // A home lost to the dark is terror and a broken promise.
                Weight("home_razed", false, trust: -0.08f, sanctity: -0.03f, fear: 0.10f),
                // A rescue rekindles faith and mercy.
                Weight("hero_rescue_released", false, trust: 0.06f, mercy: 0.04f, fear: -0.02f),
                // Every monster put down steadies nerves.
                Weight("monster_killed", false, reason: 0.01f, fear: -0.01f),
                // Corpse handling (per-death grave tags stamped by the Burial law, P3-09).
                Weight("grave_full_rites", true, mercy: 0.03f, sanctity: 0.02f),
                Weight("grave_mass", true, sanctity: -0.04f, mercy: -0.03f, fear: 0.03f),
                Weight("grave_used", true, sanctity: -0.06f, mercy: -0.06f, fear: 0.02f),
                // Old-rite daily records (P3-09 ApplyOldRitesDaily).
                Weight("offering_made", true, sanctity: -0.01f, oldFaith: -0.04f),
                Weight("secret_rite", true, oldFaith: 0.05f),
                // Fasting bites each day it is the standing food law (RationsIssued law=...).
                Weight("fasting_active", true, sanctity: -0.02f, reason: -0.01f, hunger: 0.06f),
                // A villager breaking down (sanity_state "...->Insane").
                Weight("->Insane", true, trust: -0.03f, reason: -0.03f, fear: 0.04f),
            };
        }

        static PressureWeight Weight(string signal, bool matchData,
            float trust = 0f, float sanctity = 0f, float mercy = 0f, float fear = 0f,
            float reason = 0f, float hunger = 0f, float oldFaith = 0f)
        {
            return new PressureWeight
            {
                signal = signal,
                matchData = matchData,
                trust = trust,
                sanctity = sanctity,
                mercy = mercy,
                fear = fear,
                reason = reason,
                hunger = hunger,
                oldFaith = oldFaith,
            };
        }

        static List<TrustTierBand> DefaultTrustTiers()
        {
            return new List<TrustTierBand>
            {
                new TrustTierBand { tier = TrustTier.Broken,   minTrust = 0.00f },
                new TrustTierBand { tier = TrustTier.Wary,     minTrust = 0.25f },
                new TrustTierBand { tier = TrustTier.Neutral,  minTrust = 0.45f },
                new TrustTierBand { tier = TrustTier.Trusting, minTrust = 0.65f },
                new TrustTierBand { tier = TrustTier.Devoted,  minTrust = 0.85f },
            };
        }

        static List<VolunteerGate> DefaultVolunteerGates()
        {
            return new List<VolunteerGate>
            {
                new VolunteerGate { role = VolunteerRole.NightWatch,          minTier = TrustTier.Wary },
                new VolunteerGate { role = VolunteerRole.Guard,               minTier = TrustTier.Neutral },
                new VolunteerGate { role = VolunteerRole.WarriorRecruitment,  minTier = TrustTier.Trusting },
            };
        }

        static List<AbbeyFormRule> DefaultFormRules()
        {
            return new List<AbbeyFormRule>
            {
                // Sanctuary — a holy, merciful refuge. Sacred light burns wider.
                new AbbeyFormRule
                {
                    form = AbbeyForm.Sanctuary,
                    activationThreshold = 1.4f,
                    wSanctity = 1.0f, wMercy = 1.0f, wTrust = 0.5f, wFear = -0.5f,
                    favouredTags = new List<string> { "full_rites" }, tagBonus = 0.15f,
                    modifiers = new AbbeyFormModifiers
                    {
                        sacredLightRadiusBonus = 0.25f,
                        note = "sacred light radius +25%"
                    }
                },
                // Fortress — hard, fearful, well-drilled. The windows fire harder.
                new AbbeyFormRule
                {
                    form = AbbeyForm.Fortress,
                    activationThreshold = 1.0f,
                    wFear = 1.0f, wReason = 0.8f, wTrust = 0.3f,
                    favouredTags = new List<string> { "forced_labour_night", "hound_weapon" }, tagBonus = 0.2f,
                    modifiers = new AbbeyFormModifiers
                    {
                        windowVolleyBonus = 0.25f,
                        note = "window volley +25%"
                    }
                },
                // Famine — hunger rules. Rations are capped.
                new AbbeyFormRule
                {
                    form = AbbeyForm.Famine,
                    activationThreshold = 0.6f,
                    wHunger = 2.0f, wReason = -0.2f,
                    favouredTags = new List<string> { "fasting_active" }, tagBonus = 0.2f,
                    modifiers = new AbbeyFormModifiers
                    {
                        rationCeilingMultiplier = 0.5f,
                        note = "ration ceiling x0.5"
                    }
                },
                // Cult — the old faith has taken hold. Offerings, and sanctity bleeds.
                new AbbeyFormRule
                {
                    form = AbbeyForm.Cult,
                    activationThreshold = 0.7f,
                    wOldFaith = 2.0f, wSanctity = -0.5f,
                    favouredTags = new List<string> { "offerings_tolerated", "hound_sacred" }, tagBonus = 0.4f,
                    modifiers = new AbbeyFormModifiers
                    {
                        offeringsEnabled = true,
                        sanctityDecayPerDay = 0.02f,
                        note = "offerings enabled, sanctity decays"
                    }
                },
                // Broken — trust collapsed and minds gone. Recall barely holds.
                new AbbeyFormRule
                {
                    form = AbbeyForm.Broken,
                    activationThreshold = 0.8f,
                    bias = 1.6f, wTrust = -2.0f, wHouseholdSanity = -2.0f, wFear = 0.5f,
                    modifiers = new AbbeyFormModifiers
                    {
                        recallCompliancePenalty = 0.25f,
                        note = "recall compliance -25%"
                    }
                },
            };
        }

        static PressuresConfig _cached;

        /// <summary>Resources asset if present, otherwise a coded-default instance. Never null.</summary>
        public static PressuresConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }
            _cached = Resources.Load<PressuresConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<PressuresConfig>();
                _cached.name = "PressuresConfig (defaults)";
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

    /// <summary>Baseline, decay and clamp for one pressure channel.</summary>
    [Serializable]
    public class PressureChannel
    {
        public PressureId id = PressureId.Trust;

        [Tooltip("Value the channel rests at with no events acting on it.")]
        public float baseline = 0.5f;

        [Tooltip("How far the channel is pulled back toward baseline each elapsed day.")]
        [Min(0f)] public float decayToBaselinePerDay;

        public float min = 0f;
        public float max = 1f;
    }

    /// <summary>
    /// One event→pressure weight. When <see cref="matchData"/> is false the fold matches the
    /// record TYPE against <see cref="signal"/>; when true it matches records whose DATA
    /// contains the signal (used for the law tags embedded in burial / old-rite / ration
    /// records). Each matched record adds these signed deltas to the channels.
    /// </summary>
    [Serializable]
    public class PressureWeight
    {
        public string signal = "";
        public bool matchData;
        public float trust;
        public float sanctity;
        public float mercy;
        public float fear;
        public float reason;
        public float hunger;
        public float oldFaith;

        public float For(PressureId id)
        {
            switch (id)
            {
                case PressureId.Trust: return trust;
                case PressureId.Sanctity: return sanctity;
                case PressureId.Mercy: return mercy;
                case PressureId.Fear: return fear;
                case PressureId.Reason: return reason;
                case PressureId.Hunger: return hunger;
                case PressureId.OldFaith: return oldFaith;
                default: return 0f;
            }
        }
    }

    /// <summary>One trust-tier band: the minimum Trust that reaches this tier.</summary>
    [Serializable]
    public class TrustTierBand
    {
        public TrustTier tier = TrustTier.Neutral;
        [Range(0f, 1f)] public float minTrust;
    }

    /// <summary>Minimum trust tier a volunteer behaviour needs to be available.</summary>
    [Serializable]
    public class VolunteerGate
    {
        public VolunteerRole role = VolunteerRole.NightWatch;
        public TrustTier minTier = TrustTier.Neutral;
    }

    /// <summary>
    /// The scoring rule for one abbey transformation form: a linear score over the pressures
    /// + beast status + household sanity, a bonus for favoured active law tags, and the
    /// activation threshold the score must clear. The highest-scoring form above its
    /// threshold (with hysteresis) becomes the abbey's identity and applies its modifiers.
    /// </summary>
    [Serializable]
    public class AbbeyFormRule
    {
        public AbbeyForm form = AbbeyForm.Sanctuary;

        [Tooltip("Score the form must reach to be a candidate (Balanced wins when none reach theirs).")]
        public float activationThreshold = 1f;

        public float bias;
        public float wTrust;
        public float wSanctity;
        public float wMercy;
        public float wFear;
        public float wReason;
        public float wHunger;
        public float wOldFaith;
        public float wBeastStatus;
        public float wHouseholdSanity;

        [Tooltip("Active standing law tags that push this form (e.g. Cult favours offerings_tolerated).")]
        public List<string> favouredTags = new List<string>();

        [Tooltip("Score added for each favoured tag that is active.")]
        public float tagBonus;

        public AbbeyFormModifiers modifiers = new AbbeyFormModifiers();
    }

    /// <summary>
    /// Settlement-wide modifiers a transformation form applies through
    /// <see cref="Abbey.Buildings.AbbeyState"/>. Magnitudes are data (this asset), never in
    /// MonoBehaviours. Consumers read <see cref="Abbey.Buildings.AbbeyState.Modifiers"/>.
    /// </summary>
    [Serializable]
    public class AbbeyFormModifiers
    {
        [Tooltip("Sanctuary: fractional bonus to sacred-light radius (0.25 = +25%).")]
        public float sacredLightRadiusBonus;

        [Tooltip("Fortress: fractional bonus to lit-window volley damage/rate.")]
        public float windowVolleyBonus;

        [Tooltip("Famine: multiplier applied as a ceiling on daily rations (0.5 = halved).")]
        public float rationCeilingMultiplier = 1f;

        [Tooltip("Broken: fractional reduction to dusk-recall compliance (0.25 = -25%).")]
        public float recallCompliancePenalty;

        [Tooltip("Cult: offering events become available.")]
        public bool offeringsEnabled;

        [Tooltip("Cult: sanctity bleeds by this much each day while the cult holds.")]
        public float sanctityDecayPerDay;

        [Tooltip("Human-readable summary for the debug panel.")]
        public string note = "";
    }
}
