using System.Collections.Generic;
using Abbey.World;
using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// One renewable production recipe (P3-04): what a completed production building
    /// turns a day of staffed work into. All values are data — never in the
    /// <see cref="ProductionBuilding"/> MonoBehaviour.
    ///
    /// <para><b>Seasonal (growth) recipes</b> — <see cref="seasonal"/> true — are
    /// scaled by <see cref="EconomyConfig.SeasonalYieldMultiplier"/>: they ripen more
    /// toward autumn and do not grow at all in winter (multiplier 0 halts them).
    /// Fields and pastures are seasonal.</para>
    ///
    /// <para><b>Conversion recipes</b> — <see cref="seasonal"/> false — run
    /// year-round at ×1 (winter never stops a kiln or a smithy). They consume
    /// <see cref="inputs"/> from the ledger to make <see cref="outputs"/>.</para>
    /// </summary>
    [System.Serializable]
    public class ProductionRecipe
    {
        [Tooltip("Catalog id of the building that runs this recipe (BuildingType.id).")]
        public string buildingId;

        [Tooltip("True = growth recipe (season-scaled yield, halted in winter). "
                 + "False = conversion recipe (year-round, ×1).")]
        public bool seasonal = true;

        [Tooltip("Staffed workers needed before the cycle advances at all.")]
        [Min(1)] public int workersRequired = 1;

        [Tooltip("Worker-days of staffed work to complete one production cycle. "
                 + "Progress accrues by (staffed workers) per growing day.")]
        [Min(0.01f)] public float cycleDays = 2f;

        [Tooltip("Resources consumed from the ledger each completed cycle (conversion inputs).")]
        public List<ResourceStack> inputs = new List<ResourceStack>();

        [Tooltip("Resources deposited to the ledger each completed cycle (before seasonal scaling).")]
        public List<ResourceStack> outputs = new List<ResourceStack>();
    }

    /// <summary>
    /// Single ScriptableObject holding ALL Phase 2 economy tunables (AGENTS.md rule:
    /// no balance values inside MonoBehaviours). Systems fetch it via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset file to exist.
    /// An optional asset at Resources/EconomyConfig overrides the coded defaults.
    /// Mirrors <see cref="Abbey.Core.PrototypeConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Abbey/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        public const string ResourcePath = "EconomyConfig";

        [Header("Starting stockpile (wreck crates, VERTICAL_SLICE_SPEC §4)")]
        [Tooltip("The wreck gave a temporary head start: some wood…")]
        [Min(0)] public int startingWood = 12;
        [Tooltip("…limited food…")]
        [Min(0)] public int startingFood = 8;
        [Tooltip("…a little lamp oil…")]
        [Min(0)] public int startingOil = 6;
        [Tooltip("…and a small medicine chest.")]
        [Min(0)] public int startingMedicine = 3;

        [Header("Storage")]
        [Tooltip("Total units the camp can hold with no storage piles (loose ground stacking).")]
        [Min(0)] public int baseStorageCapacity = 20;
        [Tooltip("Extra total capacity contributed by each built Storage Pile.")]
        [Min(0)] public int storagePileCapacity = 30;

        [Header("Salvage site pool (per shipwreck node)")]
        [Min(0)] public int salvageSiteWood = 30;
        [Min(0)] public int salvageSiteFood = 15;
        [Min(0)] public int salvageSiteOil = 10;
        [Min(0)] public int salvageSiteMedicine = 5;

        [Header("Salvage work")]
        [Tooltip("Units extracted by one completed work cycle at a salvage site.")]
        [Min(1)] public int salvageYieldPerCycle = 2;
        [Tooltip("Seconds of Working a salvage cycle takes (salvager role uses this).")]
        [Min(0.01f)] public float salvageWorkDurationSeconds = 4f;

        [Header("Salvage depletion stages (Intact / Picked / Stripped)")]
        [Tooltip("Remaining fraction at or below this looks Picked.")]
        [Range(0f, 1f)] public float salvagePickedFraction = 0.66f;
        [Tooltip("Remaining fraction at or below this looks Stripped.")]
        [Range(0f, 1f)] public float salvageStrippedFraction = 0.25f;

        [Header("Renewable economy — seasonal growth yield multipliers (P3-04)")]
        [Tooltip("Spring (hope): sowing and first growth — the baseline yield.")]
        [Min(0f)] public float springGrowthYield = 1f;
        [Tooltip("Summer (growth): fuller harvests.")]
        [Min(0f)] public float summerGrowthYield = 1.5f;
        [Tooltip("Autumn (warning): the great harvest — the year's peak yield.")]
        [Min(0f)] public float autumnGrowthYield = 2f;
        [Tooltip("Winter (judgment): nothing grows. 0 halts every growth recipe, forcing stockpiling.")]
        [Min(0f)] public float winterGrowthYield = 0f;

        [Tooltip("Food units one milled/cooked unit of grain yields (grain -> food, 1:N). "
                 + "Downstream conversion (P3-10 hunger, P3-14 manifest); not auto-applied here.")]
        [Min(1)] public int grainToFoodRatio = 2;

        [Tooltip("Renewable production recipes, one per production building (P3-04).")]
        public List<ProductionRecipe> productionRecipes = CreateDefaultRecipes();

        static EconomyConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/EconomyConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static EconomyConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<EconomyConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<EconomyConfig>();
                _cached.name = "EconomyConfig (defaults)";
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        /// <summary>Drops the cached instance (test isolation).</summary>
        public static void ClearCache()
        {
            _cached = null;
        }

        /// <summary>
        /// Growth yield multiplier for a season (P3-04): autumn peaks, winter is 0 so
        /// nothing grows. Conversion recipes ignore this and always run at ×1.
        /// </summary>
        public float SeasonalYieldMultiplier(Season season)
        {
            switch (season)
            {
                case Season.Spring: return springGrowthYield;
                case Season.Summer: return summerGrowthYield;
                case Season.Autumn: return autumnGrowthYield;
                case Season.Winter: return winterGrowthYield;
                default: return springGrowthYield;
            }
        }

        /// <summary>Production recipe for a building id, or null (linear scan; tiny list).</summary>
        public ProductionRecipe RecipeFor(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId) || productionRecipes == null)
            {
                return null;
            }
            for (int i = 0; i < productionRecipes.Count; i++)
            {
                if (productionRecipes[i] != null && productionRecipes[i].buildingId == buildingId)
                {
                    return productionRecipes[i];
                }
            }
            return null;
        }

        /// <summary>Food a quantity of grain mills into at the config ratio (grain -> food).</summary>
        public int GrainToFood(int grain)
        {
            return grain <= 0 ? 0 : grain * Mathf.Max(1, grainToFoodRatio);
        }

        /// <summary>
        /// Coded default renewable recipes (P3-04). Growth recipes (field, pasture)
        /// are season-scaled and winter-halted; conversion recipes (charcoal kiln,
        /// smithy) run year-round consuming ledger inputs. An asset at
        /// Resources/EconomyConfig overrides all of it.
        /// </summary>
        static List<ProductionRecipe> CreateDefaultRecipes()
        {
            return new List<ProductionRecipe>
            {
                new ProductionRecipe
                {
                    buildingId = "field_plot_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 2f,
                    outputs =
                    {
                        new ResourceStack(ResourceType.Grain, 3),
                        new ResourceStack(ResourceType.Herbs, 1),
                    },
                },
                new ProductionRecipe
                {
                    buildingId = "pasture_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 2f,
                    outputs =
                    {
                        new ResourceStack(ResourceType.Meat, 2),
                        new ResourceStack(ResourceType.Wool, 2),
                    },
                },
                new ProductionRecipe
                {
                    buildingId = "charcoal_kiln_t1",
                    seasonal = false,
                    workersRequired = 1,
                    cycleDays = 1f,
                    inputs = { new ResourceStack(ResourceType.Wood, 2) },
                    outputs = { new ResourceStack(ResourceType.Charcoal, 1) },
                },
                new ProductionRecipe
                {
                    buildingId = "smithy_t1",
                    seasonal = false,
                    workersRequired = 1,
                    cycleDays = 2f,
                    inputs =
                    {
                        new ResourceStack(ResourceType.ScrapIron, 1),
                        new ResourceStack(ResourceType.Charcoal, 1),
                    },
                    outputs = { new ResourceStack(ResourceType.Tools, 1) },
                },
                new ProductionRecipe
                {
                    buildingId = "forester_hut_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 2f,
                    outputs =
                    {
                        new ResourceStack(ResourceType.GreenWood, 3),
                        new ResourceStack(ResourceType.OldWood, 1),
                        new ResourceStack(ResourceType.Resin, 1),
                    },
                },
                new ProductionRecipe
                {
                    buildingId = "herbalist_hut_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 2f,
                    outputs =
                    {
                        new ResourceStack(ResourceType.Herbs, 2),
                        new ResourceStack(ResourceType.Resin, 1),
                    },
                },
                new ProductionRecipe
                {
                    buildingId = "orchard_plot_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 3f,
                    outputs = { new ResourceStack(ResourceType.Apples, 4) },
                },
                new ProductionRecipe
                {
                    buildingId = "hunter_blind_t1",
                    seasonal = false,
                    workersRequired = 1,
                    cycleDays = 2f,
                    outputs = { new ResourceStack(ResourceType.Venison, 2) },
                },
                new ProductionRecipe
                {
                    buildingId = "stag_garden_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 4f,
                    outputs =
                    {
                        new ResourceStack(ResourceType.SacredSeeds, 1),
                        new ResourceStack(ResourceType.Apples, 1),
                    },
                },
            };
        }
    }
}
