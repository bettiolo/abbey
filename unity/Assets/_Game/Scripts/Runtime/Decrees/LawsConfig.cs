using System;
using System.Collections.Generic;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Decrees
{
    /// <summary>
    /// The single ScriptableObject holding ALL law balance (AGENTS.md: no magnitudes in
    /// MonoBehaviours). Mirrors <see cref="OverdriveConfig"/> / <see cref="EconomyConfig"/>:
    /// fetched through <see cref="LoadOrDefault"/> so tests and CI never need an asset; an
    /// optional asset at Resources/LawsConfig overrides the coded defaults. Holds the decree
    /// cooldown, the per-option food ration tables, the night-labour permission matrix, the
    /// per-option burial costs/refunds/pressures and the per-option old-rite pressures.
    /// </summary>
    [CreateAssetMenu(fileName = "LawsConfig", menuName = "Abbey/Laws Config")]
    public class LawsConfig : ScriptableObject
    {
        public const string ResourcePath = "LawsConfig";

        [Header("Decree lifecycle")]
        [Tooltip("Days that must pass before a group's law can be changed again (weight of a decree).")]
        [Min(0)] public int decreeCooldownDays = 3;

        [Header("Food ration tables (units of Food per villager per day)")]
        public List<FoodLawEffect> foodEffects = DefaultFood();

        [Header("Night-labour permission matrix")]
        [Tooltip("The overdrive levers considered night labour — gated by the Night labour law.")]
        public List<OverdriveActionId> nightLabourActions = new List<OverdriveActionId>
        {
            OverdriveActionId.ForcedNightWork,
            OverdriveActionId.CandleLine,
            OverdriveActionId.VolunteerWatch
        };

        public List<NightLabourEffect> nightLabourEffects = DefaultNightLabour();

        [Header("Burial costs / refunds / pressures")]
        public List<BurialLawEffect> burialEffects = DefaultBurial();

        [Header("Old-rites daily pressures")]
        public List<OldRitesEffect> oldRitesEffects = DefaultOldRites();

        // ------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------

        public FoodLawEffect FoodEffectFor(FoodLaw law)
        {
            if (foodEffects != null)
            {
                for (int i = 0; i < foodEffects.Count; i++)
                {
                    if (foodEffects[i] != null && foodEffects[i].law == law)
                    {
                        return foodEffects[i];
                    }
                }
            }
            return new FoodLawEffect { law = law };
        }

        public NightLabourEffect NightLabourEffectFor(NightLabourLaw law)
        {
            if (nightLabourEffects != null)
            {
                for (int i = 0; i < nightLabourEffects.Count; i++)
                {
                    if (nightLabourEffects[i] != null && nightLabourEffects[i].law == law)
                    {
                        return nightLabourEffects[i];
                    }
                }
            }
            return new NightLabourEffect { law = law };
        }

        public BurialLawEffect BurialEffectFor(BurialLaw law)
        {
            if (burialEffects != null)
            {
                for (int i = 0; i < burialEffects.Count; i++)
                {
                    if (burialEffects[i] != null && burialEffects[i].law == law)
                    {
                        return burialEffects[i];
                    }
                }
            }
            return new BurialLawEffect { law = law };
        }

        public OldRitesEffect OldRitesEffectFor(OldRitesLaw law)
        {
            if (oldRitesEffects != null)
            {
                for (int i = 0; i < oldRitesEffects.Count; i++)
                {
                    if (oldRitesEffects[i] != null && oldRitesEffects[i].law == law)
                    {
                        return oldRitesEffects[i];
                    }
                }
            }
            return new OldRitesEffect { law = law };
        }

        /// <summary>Whether a lever is one the Night labour law governs.</summary>
        public bool IsNightLabourAction(OverdriveActionId id)
        {
            if (nightLabourActions == null)
            {
                return false;
            }
            for (int i = 0; i < nightLabourActions.Count; i++)
            {
                if (nightLabourActions[i] == id)
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Coded defaults
        // ------------------------------------------------------------------

        static List<FoodLawEffect> DefaultFood()
        {
            return new List<FoodLawEffect>
            {
                // Everyone eats the same flat ration; the hound eats a modest keep.
                new FoodLawEffect
                {
                    law = FoodLaw.Equal,
                    workerRation = 2, idleRation = 2, houndRation = 1,
                    feedHoundFirst = false
                },
                // Workers keep a full ration; the idle / injured tighten their belts.
                new FoodLawEffect
                {
                    law = FoodLaw.WorkersFirst,
                    workerRation = 2, idleRation = 1, houndRation = 1,
                    feedHoundFirst = false
                },
                // The hound is fed from the stores first and fuller — feeds P3-07 treatment.
                new FoodLawEffect
                {
                    law = FoodLaw.BeastShare,
                    workerRation = 2, idleRation = 2, houndRation = 3,
                    feedHoundFirst = true
                },
                // A fast: everyone's ration is cut, and hunger + sanity press on the camp.
                new FoodLawEffect
                {
                    law = FoodLaw.Fasting,
                    workerRation = 1, idleRation = 1, houndRation = 1,
                    feedHoundFirst = false,
                    sanityCostPerVillager = 0.03f,
                    hungerPerVillager = 0.1f
                },
            };
        }

        static List<NightLabourEffect> DefaultNightLabour()
        {
            return new List<NightLabourEffect>
            {
                // The bell means rest: no forced night work, no candle-line labour.
                new NightLabourEffect { law = NightLabourLaw.NoWorkAfterBell, nightWorkPermitted = false },
                // Volunteers may work the night for extra rations.
                new NightLabourEffect
                {
                    law = NightLabourLaw.PaidRisk,
                    nightWorkPermitted = true,
                    extraRationPerNightWorker = 1
                },
                // The camp works the night whether it wills or not.
                new NightLabourEffect { law = NightLabourLaw.Forced, nightWorkPermitted = true },
            };
        }

        static List<BurialLawEffect> DefaultBurial()
        {
            return new List<BurialLawEffect>
            {
                // Full rites: candles + time, but a mercy boost to the living.
                new BurialLawEffect
                {
                    law = BurialLaw.FullRites,
                    cost = new List<ResourceStack> { new ResourceStack(ResourceType.Candles, 2) },
                    refund = new List<ResourceStack>(),
                    mercyDelta = 0.05f,
                    sanctityDelta = 0.02f
                },
                // Mass graves: cheap, but fear rises and sanctity falls.
                new BurialLawEffect
                {
                    law = BurialLaw.MassGraves,
                    cost = new List<ResourceStack>(),
                    refund = new List<ResourceStack>(),
                    mercyDelta = -0.05f,
                    sanctityDelta = -0.05f,
                    fearDelta = 0.05f,
                    sanityCostPerVillager = 0.02f
                },
                // Use the dead: resources refunded, but a heavy mercy/sanctity hit and a
                // strong nightmare tag P3-11 will read.
                new BurialLawEffect
                {
                    law = BurialLaw.UseTheDead,
                    cost = new List<ResourceStack>(),
                    refund = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.Meat, 2),
                        new ResourceStack(ResourceType.Herbs, 1)
                    },
                    mercyDelta = -0.15f,
                    sanctityDelta = -0.12f,
                    fearDelta = 0.03f,
                    nightmarePressure = 2f,
                    sanityCostPerVillager = 0.05f
                },
            };
        }

        static List<OldRitesEffect> DefaultOldRites()
        {
            return new List<OldRitesEffect>
            {
                // Tolerated: old-faith pressure vents daily, sanctity slowly erodes,
                // offering events surface.
                new OldRitesEffect
                {
                    law = OldRitesLaw.TolerateOfferings,
                    sanctityDeltaPerDay = -0.02f,
                    oldFaithPressureDeltaPerDay = -0.05f,
                    emitsOfferingEvents = true
                },
                // Forbidden: sanctity holds, but old-faith pressure builds behind closed
                // doors and secret-rite tags accrue.
                new OldRitesEffect
                {
                    law = OldRitesLaw.ForbidPaganRites,
                    sanctityDeltaPerDay = 0.01f,
                    oldFaithPressureDeltaPerDay = 0.06f,
                    emitsSecretRiteTags = true
                },
            };
        }

        static LawsConfig _cached;

        /// <summary>Resources asset if present, otherwise a coded-default instance. Never null.</summary>
        public static LawsConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }
            _cached = Resources.Load<LawsConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<LawsConfig>();
                _cached.name = "LawsConfig (defaults)";
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

    /// <summary>Ration table + pressure for one Food law option (units of Food per villager).</summary>
    [Serializable]
    public class FoodLawEffect
    {
        public FoodLaw law = FoodLaw.Equal;

        [Tooltip("Food units issued to each working villager.")]
        [Min(0)] public int workerRation = 2;

        [Tooltip("Food units issued to each idle/injured villager.")]
        [Min(0)] public int idleRation = 2;

        [Tooltip("Food units the hound is kept on.")]
        [Min(0)] public int houndRation = 1;

        [Tooltip("Feed the hound from the stores before the villagers (Beast Share).")]
        public bool feedHoundFirst;

        [Tooltip("Sanity cost applied to each villager on a cut ration (Fasting).")]
        [Range(0f, 1f)] public float sanityCostPerVillager;

        [Tooltip("Hunger pressure added per villager on a cut ration (Fasting).")]
        [Min(0f)] public float hungerPerVillager;
    }

    /// <summary>Night-labour permission + surcharge for one Night labour option.</summary>
    [Serializable]
    public class NightLabourEffect
    {
        public NightLabourLaw law = NightLabourLaw.NoWorkAfterBell;

        [Tooltip("Whether the gated night-labour levers may fire under this law.")]
        public bool nightWorkPermitted;

        [Tooltip("Extra Food units each registered night-work volunteer costs (Paid Risk).")]
        [Min(0)] public int extraRationPerNightWorker;
    }

    /// <summary>Costs, refunds and pressures for one Burial option (applied per death).</summary>
    [Serializable]
    public class BurialLawEffect
    {
        public BurialLaw law = BurialLaw.FullRites;

        [Tooltip("Resources spent to bury one villager (Full Rites: candles).")]
        public List<ResourceStack> cost = new List<ResourceStack>();

        [Tooltip("Resources recovered from one corpse (Use the Dead).")]
        public List<ResourceStack> refund = new List<ResourceStack>();

        [Tooltip("Mercy pressure delta (P3-10): + for rites, - for desecration.")]
        public float mercyDelta;

        [Tooltip("Sanctity pressure delta (P3-10): - as the dead are dishonoured.")]
        public float sanctityDelta;

        [Tooltip("Fear pressure delta (P3-10): + when the dead are handled harshly.")]
        public float fearDelta;

        [Tooltip("Nightmare pressure booked for P3-11 consequence nightmares.")]
        [Min(0f)] public float nightmarePressure;

        [Tooltip("Sanity cost applied to each living villager who witnesses the handling.")]
        [Range(0f, 1f)] public float sanityCostPerVillager;
    }

    /// <summary>Daily pressures + rite events for one Old rites option.</summary>
    [Serializable]
    public class OldRitesEffect
    {
        public OldRitesLaw law = OldRitesLaw.ForbidPaganRites;

        [Tooltip("Sanctity pressure change per day under this policy.")]
        public float sanctityDeltaPerDay;

        [Tooltip("Old-faith pressure change per day (vents when tolerated, builds when forbidden).")]
        public float oldFaithPressureDeltaPerDay;

        [Tooltip("Surface an offering event each day (Tolerate Offerings).")]
        public bool emitsOfferingEvents;

        [Tooltip("Accrue a secret-rite tag each day (Forbid Pagan Rites).")]
        public bool emitsSecretRiteTags;
    }
}
