using System;
using System.Collections.Generic;
using Abbey.Economy;
using Abbey.Morale;
using Abbey.Settlement;
using UnityEngine;

namespace Abbey.Island
{
    /// <summary>
    /// The class a newcomer arrives as (ROADMAP Phase 3 item 11). Survivors are plain
    /// settlers; specialists carry a job-efficiency bonus; warriors are P3-06 recruitment
    /// candidates. New values append at the end.
    /// </summary>
    public enum ArrivalClass
    {
        Survivor,
        Specialist,
        Warrior
    }

    /// <summary>How a newcomer reached the lit village.</summary>
    public enum ArrivalChannel
    {
        Passive,     // walked toward the light at dawn
        Expedition,  // an expedition found them
        Shipwreck    // a storm threw a crew ashore
    }

    /// <summary>
    /// What trust decided for one newcomer (P3-10 tiers). <see cref="Left"/> records a
    /// spring departure intent for the P3-14 end summary; <see cref="Stayed"/> integrates;
    /// <see cref="Volunteered"/> integrates and offers for volunteer duty.
    /// </summary>
    public enum IntegrationOutcome
    {
        Left,
        Stayed,
        Volunteered
    }

    /// <summary>What one dilemma option's effect writes.</summary>
    public enum DilemmaEffectKind
    {
        Pressure,        // a signed delta on one moral pressure (folded by PressureSystem)
        Tag,             // a law-like tag stamped into the log
        Resource,        // a signed resource ledger change (compensation / fine)
        HoundTreatment   // a treatment / doctrine input to the P3-07 hound evolution
    }

    /// <summary>
    /// The single ScriptableObject holding ALL island-exploration / arrival / dilemma
    /// balance (AGENTS.md: no magnitudes in MonoBehaviours). Mirrors
    /// <see cref="Abbey.Morale.PressuresConfig"/> / <see cref="Abbey.Nightmares.ThreatConfig"/>:
    /// fetched through <see cref="LoadOrDefault"/> so tests and CI never need an asset; an
    /// optional asset at Resources/IslandConfig overrides the coded defaults.
    ///
    /// Holds the expedition timings, the POI discovery-reward table, the passive-arrival
    /// cadence + trust integration gates, the storm-shipwreck crew/supplies and drowned
    /// risk window, and the three data-driven dilemma cards. Deterministic draws derive
    /// from <see cref="seed"/> + a day/POI index (the P2-06 director pattern).
    /// </summary>
    [CreateAssetMenu(fileName = "IslandConfig", menuName = "Abbey/Island Config")]
    public class IslandConfig : ScriptableObject
    {
        public const string ResourcePath = "IslandConfig";

        [Header("Determinism")]
        [Tooltip("Master seed for arrival draws (per-day seed = seed + day * salt).")]
        public int seed = 90211;

        [Header("Expeditions")]
        [Tooltip("Party walk speed out to a POI and back (world units / second).")]
        [Min(0.01f)] public float expeditionTravelSpeed = 4f;

        [Tooltip("Seconds spent surveying a POI once the party arrives before the reward lands.")]
        [Min(0f)] public float poiResolveSeconds = 3f;

        [Tooltip("How close (planar) the party must get to count as arrived.")]
        [Min(0.01f)] public float arrivalRadius = 0.6f;

        [Tooltip("Largest party an expedition may take (villagers pulled off jobs while away).")]
        [Min(1)] public int expeditionMaxParty = 3;

        [Header("POI discovery rewards (one rule per type)")]
        public List<PoiRewardRule> poiRewards = DefaultPoiRewards();

        [Header("Passive arrivals (survivors walk toward the light)")]
        [Tooltip("A passive-arrival draw happens on days that are a multiple of this (0 = never).")]
        [Min(0)] public int passiveArrivalIntervalDays = 2;

        [Tooltip("Chance (percent) that a scheduled draw actually produces an arrival.")]
        [Range(0, 100)] public int passiveArrivalChancePercent = 60;

        [Tooltip("The class of a passive walk-in arrival.")]
        public ArrivalClass passiveArrivalClass = ArrivalClass.Survivor;

        [Header("Trust integration gates (P3-10 tiers)")]
        [Tooltip("Minimum trust tier for a newcomer to stay at all (below it they leave).")]
        public TrustTier stayMinTier = TrustTier.Wary;

        [Tooltip("Minimum trust tier for a staying newcomer to volunteer for duty.")]
        public TrustTier volunteerMinTier = TrustTier.Trusting;

        [Header("Storm shipwreck event")]
        public List<ArrivalCompositionEntry> shipwreckCrew = DefaultShipwreckCrew();

        [Tooltip("Supplies washed ashore with a shipwrecked crew.")]
        public List<ResourceStack> shipwreckSupplies = DefaultShipwreckSupplies();

        [Tooltip("Nights after a wet rescue during which the director may raise a drowned sailor.")]
        [Min(0)] public int drownedRiskWindowNights = 3;

        [Header("Class effects")]
        [Tooltip("Day-work speed multiplier a specialist newcomer brings (JobsConfig hook).")]
        [Min(0f)] public float specialistWorkSpeedMultiplier = 1.25f;

        [Header("Dilemma cards")]
        public List<DilemmaCard> dilemmas = DefaultDilemmas();

        // ------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------

        /// <summary>The reward rule for a POI type, or null when none is configured.</summary>
        public PoiRewardRule RewardFor(PoiType type)
        {
            if (poiRewards != null)
            {
                for (int i = 0; i < poiRewards.Count; i++)
                {
                    if (poiRewards[i] != null && poiRewards[i].type == type)
                    {
                        return poiRewards[i];
                    }
                }
            }
            return null;
        }

        /// <summary>The dilemma card with an id, or null.</summary>
        public DilemmaCard CardFor(string id)
        {
            if (dilemmas != null && !string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < dilemmas.Count; i++)
                {
                    if (dilemmas[i] != null && dilemmas[i].id == id)
                    {
                        return dilemmas[i];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Deterministic passive-arrival decision for a day: true only on interval days
        /// whose seeded roll clears the chance. Pure function of <see cref="seed"/> + day.
        /// </summary>
        public bool PassiveArrivalForDay(int day)
        {
            if (day <= 0 || passiveArrivalIntervalDays <= 0)
            {
                return false;
            }
            if (day % passiveArrivalIntervalDays != 0)
            {
                return false;
            }
            var rng = new System.Random(seed + day * 131);
            return rng.Next(100) < passiveArrivalChancePercent;
        }

        // ------------------------------------------------------------------
        // Coded defaults
        // ------------------------------------------------------------------

        static List<PoiRewardRule> DefaultPoiRewards()
        {
            return new List<PoiRewardRule>
            {
                // Old wreckage on the far shore: scrap + supplies, no new ground.
                new PoiRewardRule
                {
                    type = PoiType.Wreckage,
                    resourceYields = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.ScrapIron, 4),
                        new ResourceStack(ResourceType.Wood, 3),
                    },
                    note = "salvageable wreck",
                },
                // An old road end: opens buildable ground toward the interior.
                new PoiRewardRule
                {
                    type = PoiType.OldRoad,
                    seedSlotsUnlocked = 2,
                    unlockedSlotSize = SlotSizeClass.Medium,
                    note = "road reaches new plots",
                },
                // A forest shrine: relic fragments and a new threat source on the map.
                new PoiRewardRule
                {
                    type = PoiType.Shrine,
                    resourceYields = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.RelicFragments, 2),
                    },
                    addsThreatSource = true,
                    note = "shrine (crypt threat source)",
                },
                // A well: fresh water opens a plot AND registers a well threat source.
                new PoiRewardRule
                {
                    type = PoiType.Well,
                    seedSlotsUnlocked = 1,
                    unlockedSlotSize = SlotSizeClass.Small,
                    addsThreatSource = true,
                    note = "well (water + well threat source)",
                },
                // Boundary stone: marks safe ground — one new plot.
                new PoiRewardRule
                {
                    type = PoiType.BoundaryStone,
                    seedSlotsUnlocked = 1,
                    unlockedSlotSize = SlotSizeClass.Small,
                    note = "boundary stone plot",
                },
                // A survivor camp: people to bring home.
                new PoiRewardRule
                {
                    type = PoiType.SurvivorCamp,
                    arrivalClass = ArrivalClass.Survivor,
                    arrivalCount = 2,
                    note = "survivors found",
                },
                // A resource cache: grain + herbs.
                new PoiRewardRule
                {
                    type = PoiType.ResourceCache,
                    resourceYields = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.Grain, 5),
                        new ResourceStack(ResourceType.Herbs, 2),
                    },
                    note = "hidden cache",
                },
            };
        }

        static List<ArrivalCompositionEntry> DefaultShipwreckCrew()
        {
            return new List<ArrivalCompositionEntry>
            {
                new ArrivalCompositionEntry { arrivalClass = ArrivalClass.Survivor, count = 2 },
                new ArrivalCompositionEntry { arrivalClass = ArrivalClass.Specialist, count = 1 },
                new ArrivalCompositionEntry { arrivalClass = ArrivalClass.Warrior, count = 1 },
            };
        }

        static List<ResourceStack> DefaultShipwreckSupplies()
        {
            return new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Food, 6),
                new ResourceStack(ResourceType.Wood, 4),
                new ResourceStack(ResourceType.Oil, 2),
            };
        }

        static List<DilemmaCard> DefaultDilemmas()
        {
            return new List<DilemmaCard>
            {
                // Missing Salvager: a night search (risk / fear) vs writing them off (a
                // mercy failing that costs trust).
                new DilemmaCard
                {
                    id = "missing_salvager",
                    promptKey = "dilemma.missing_salvager.prompt",
                    options = new List<DilemmaOption>
                    {
                        new DilemmaOption
                        {
                            id = "search_at_night",
                            labelKey = "dilemma.missing_salvager.search",
                            effects = new List<DilemmaEffect>
                            {
                                Pressure(PressureId.Fear, 0.08f),
                                Pressure(PressureId.Mercy, 0.05f),
                                Pressure(PressureId.Trust, 0.04f),
                                Tag("night_search"),
                            },
                        },
                        new DilemmaOption
                        {
                            id = "write_off",
                            labelKey = "dilemma.missing_salvager.write_off",
                            effects = new List<DilemmaEffect>
                            {
                                Pressure(PressureId.Mercy, -0.06f),
                                Pressure(PressureId.Trust, -0.05f),
                                Tag("wrote_off_lost"),
                            },
                        },
                    },
                },
                // Food Thief: punish / forgive / exile.
                new DilemmaCard
                {
                    id = "food_thief",
                    promptKey = "dilemma.food_thief.prompt",
                    options = new List<DilemmaOption>
                    {
                        new DilemmaOption
                        {
                            id = "punish",
                            labelKey = "dilemma.food_thief.punish",
                            effects = new List<DilemmaEffect>
                            {
                                Pressure(PressureId.Fear, 0.07f),
                                Pressure(PressureId.Mercy, -0.05f),
                                Pressure(PressureId.Reason, 0.02f),
                                Tag("thief_punished"),
                            },
                        },
                        new DilemmaOption
                        {
                            id = "forgive",
                            labelKey = "dilemma.food_thief.forgive",
                            effects = new List<DilemmaEffect>
                            {
                                Pressure(PressureId.Mercy, 0.06f),
                                Pressure(PressureId.Trust, 0.04f),
                                Tag("thief_forgiven"),
                            },
                        },
                        new DilemmaOption
                        {
                            id = "exile",
                            labelKey = "dilemma.food_thief.exile",
                            effects = new List<DilemmaEffect>
                            {
                                Pressure(PressureId.Fear, 0.05f),
                                Pressure(PressureId.Trust, -0.06f),
                                Pressure(PressureId.Mercy, -0.04f),
                                Tag("thief_exiled"),
                            },
                        },
                    },
                },
                // Hound Bites a Child: punish the hound (doctrine/treatment input to P3-07),
                // protect the hound (a trust hit), or compensate the family (supplies + mercy).
                new DilemmaCard
                {
                    id = "hound_bites_child",
                    promptKey = "dilemma.hound_bites_child.prompt",
                    options = new List<DilemmaOption>
                    {
                        new DilemmaOption
                        {
                            id = "punish_hound",
                            labelKey = "dilemma.hound_bites_child.punish",
                            effects = new List<DilemmaEffect>
                            {
                                HoundTreatment("punish"),
                                Pressure(PressureId.Fear, 0.03f),
                                Pressure(PressureId.Mercy, -0.03f),
                                Tag("hound_punished"),
                            },
                        },
                        new DilemmaOption
                        {
                            id = "protect_hound",
                            labelKey = "dilemma.hound_bites_child.protect",
                            effects = new List<DilemmaEffect>
                            {
                                HoundTreatment("protect"),
                                Pressure(PressureId.Trust, -0.07f),
                                Tag("hound_protected"),
                            },
                        },
                        new DilemmaOption
                        {
                            id = "compensate_family",
                            labelKey = "dilemma.hound_bites_child.compensate",
                            effects = new List<DilemmaEffect>
                            {
                                Resource(ResourceType.Medicine, -1),
                                Resource(ResourceType.Food, -2),
                                Pressure(PressureId.Mercy, 0.05f),
                                Pressure(PressureId.Trust, 0.03f),
                                Tag("family_compensated"),
                            },
                        },
                    },
                },
            };
        }

        static DilemmaEffect Pressure(PressureId id, float amount)
        {
            return new DilemmaEffect { kind = DilemmaEffectKind.Pressure, pressure = id, amount = amount };
        }

        static DilemmaEffect Tag(string tag)
        {
            return new DilemmaEffect { kind = DilemmaEffectKind.Tag, tag = tag };
        }

        static DilemmaEffect Resource(ResourceType type, int amount)
        {
            return new DilemmaEffect { kind = DilemmaEffectKind.Resource, resource = type, resourceAmount = amount };
        }

        static DilemmaEffect HoundTreatment(string kind)
        {
            return new DilemmaEffect { kind = DilemmaEffectKind.HoundTreatment, tag = kind };
        }

        // ------------------------------------------------------------------
        // Cache
        // ------------------------------------------------------------------

        static IslandConfig _cached;

        /// <summary>Resources asset if present, otherwise a coded-default instance. Never null.</summary>
        public static IslandConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }
            _cached = Resources.Load<IslandConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<IslandConfig>();
                _cached.name = "IslandConfig (defaults)";
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

    /// <summary>The reward one POI type yields when discovered (all balance is here).</summary>
    [Serializable]
    public class PoiRewardRule
    {
        public PoiType type = PoiType.ResourceCache;

        [Tooltip("Resources deposited to the ledger on discovery.")]
        public List<ResourceStack> resourceYields = new List<ResourceStack>();

        [Tooltip("New open seed slots unlocked beside the POI (P3-02).")]
        [Min(0)] public int seedSlotsUnlocked;

        [Tooltip("Size class of the unlocked slots.")]
        public SlotSizeClass unlockedSlotSize = SlotSizeClass.Small;

        [Tooltip("Class of any people found here (arrivalCount 0 = nobody).")]
        public ArrivalClass arrivalClass = ArrivalClass.Survivor;

        [Tooltip("How many people are found (0 = none).")]
        [Min(0)] public int arrivalCount;

        [Tooltip("Register a P3-11 threat source at the POI (shrines / wells).")]
        public bool addsThreatSource;

        [Tooltip("Human-readable summary for the debug panel / log.")]
        public string note = "";
    }

    /// <summary>A count of one arrival class (shipwreck crew composition).</summary>
    [Serializable]
    public class ArrivalCompositionEntry
    {
        public ArrivalClass arrivalClass = ArrivalClass.Survivor;
        [Min(0)] public int count = 1;
    }

    /// <summary>One consequence a dilemma option writes. Balance/data only.</summary>
    [Serializable]
    public class DilemmaEffect
    {
        public DilemmaEffectKind kind = DilemmaEffectKind.Pressure;

        [Tooltip("Pressure: which channel the signed delta lands on.")]
        public PressureId pressure = PressureId.Trust;

        [Tooltip("Pressure: signed delta on the channel.")]
        public float amount;

        [Tooltip("Tag: the tag written to the log. HoundTreatment: the treatment kind (punish/protect/rite).")]
        public string tag = "";

        [Tooltip("Resource: which resource the signed change applies to.")]
        public ResourceType resource = ResourceType.Food;

        [Tooltip("Resource: signed units (positive adds, negative spends).")]
        public int resourceAmount;
    }

    /// <summary>One selectable option on a dilemma card and the effects it applies.</summary>
    [Serializable]
    public class DilemmaOption
    {
        public string id = "";
        public string labelKey = "";
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
    }

    /// <summary>A data-driven dilemma card: an id, a prompt text key and 2-3 options.</summary>
    [Serializable]
    public class DilemmaCard
    {
        public string id = "";
        public string promptKey = "";
        public List<DilemmaOption> options = new List<DilemmaOption>();
    }
}
