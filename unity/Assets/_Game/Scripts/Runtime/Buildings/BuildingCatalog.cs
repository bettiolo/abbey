using System;
using System.Collections.Generic;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// What a completed building *is* to the simulation. Completion attaches the
    /// matching runtime component: <see cref="Abbey.Light.LightSource"/> for
    /// LightSource, <see cref="Abbey.Economy.StoragePile"/> for Storage, a sacred
    /// LightSource for Shrine, an <see cref="AsylumZone"/> for Asylum; Gate
    /// and BellTower flip their <see cref="AbbeyState"/> flags (abbey restoration,
    /// P2-04). Shelter, WorkHut and GuardPost are identified by
    /// <see cref="Building.Kind"/> alone. New kinds append at the end (serialized
    /// enum indices must stay stable — Asylum keeps index 6, the former sick-corner
    /// slot, renamed in P3-02).
    /// </summary>
    public enum FunctionKind
    {
        LightSource,
        Storage,
        Shelter,
        WorkHut,
        GuardPost,
        Shrine,
        Asylum,
        Gate,
        BellTower,
        Production,
        WarriorLodge,
        Watchtower,

        /// <summary>
        /// The spring-ship reconstruction (P3-14): the staged hull/rigging whose completion
        /// satisfies the manifest's hull part. Purely structural — no runtime component; the
        /// SpringShipScenario reads the site's completion.
        /// </summary>
        Ship
    }

    /// <summary>
    /// Which PrototypeConfig light values a completed LightSource building uses
    /// (balance stays in PrototypeConfig, never in the buildings code).
    /// </summary>
    public enum LightProfile
    {
        Campfire,
        Lantern
    }

    /// <summary>
    /// One buildable structure, per the GAME_DESIGN.md §15 Building spec shape:
    /// id, display name, footprint (world-units rect on the XZ plane), build cost,
    /// construction work time and the function it serves once complete.
    /// </summary>
    [Serializable]
    public class BuildingType
    {
        [Tooltip("Snake_case id used by placement, save data and the event log.")]
        public string id;

        public string displayName;

        [Tooltip("Occupied ground rect in world units (x = width along X, y = depth along Z), centered on the placement position.")]
        public Vector2 footprint = new Vector2(2f, 2f);

        [Tooltip("Resources that must be DELIVERED to the construction site (not paid upfront). Duplicate types accumulate.")]
        public List<ResourceStack> cost = new List<ResourceStack>();

        [Tooltip("Seconds of builder work needed after all materials are delivered.")]
        [Min(0f)] public float buildWorkSeconds = 5f;

        [Tooltip("What the finished building becomes.")]
        public FunctionKind function = FunctionKind.Shelter;

        [Tooltip("Only used when function is LightSource: which PrototypeConfig values configure the light.")]
        public LightProfile lightProfile = LightProfile.Lantern;

        [Tooltip("Optional finished-building visual (generated GLB prefab hook). Null = placeholder cube scaled to the footprint.")]
        public GameObject completedVisualPrefab;

        [Tooltip("A home settlers shelter in at night: destructible under the P3-05 night assault "
                 + "(monsters can raze it, killing the occupants and losing its light node). "
                 + "Structural property, not balance — the hit-point number lives in CombatConfig.")]
        public bool destructibleHome;

        [Tooltip("Per-type multiplier on CombatConfig.baseHomeHitPoints (sturdier homes take more to raze). Only used when destructibleHome.")]
        [Min(0f)] public float homeHitPointMultiplier = 1f;

        /// <summary>Occupied ground rect when centered at a world position (XZ plane).</summary>
        public Rect FootprintAt(Vector3 worldPosition)
        {
            return new Rect(
                worldPosition.x - footprint.x * 0.5f,
                worldPosition.z - footprint.y * 0.5f,
                footprint.x,
                footprint.y);
        }
    }

    /// <summary>
    /// Single ScriptableObject holding every Phase 2 buildable (AGENTS.md rule:
    /// no balance values inside MonoBehaviours — costs, footprints and work times
    /// live here). Systems fetch it via <see cref="LoadOrDefault"/> so tests and CI
    /// never need an asset file to exist. An optional asset at
    /// Resources/BuildingCatalog overrides the coded defaults. Mirrors
    /// <see cref="Abbey.Economy.EconomyConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingCatalog", menuName = "Abbey/Building Catalog")]
    public class BuildingCatalog : ScriptableObject
    {
        public const string ResourcePath = "BuildingCatalog";

        [Tooltip("Every buildable structure (VERTICAL_SLICE_SPEC §5 set).")]
        public List<BuildingType> buildings = CreateDefaultBuildings();

        /// <summary>Entry with the given id, or null. Linear scan (tiny list).</summary>
        public BuildingType Find(string id)
        {
            if (string.IsNullOrEmpty(id) || buildings == null)
            {
                return null;
            }
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].id == id)
                {
                    return buildings[i];
                }
            }
            return null;
        }

        static BuildingCatalog _cached;

        /// <summary>
        /// Returns the catalog asset from Resources/BuildingCatalog if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static BuildingCatalog LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<BuildingCatalog>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<BuildingCatalog>();
                _cached.name = "BuildingCatalog (defaults)";
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
        /// Coded default catalog: the buildable set of VERTICAL_SLICE_SPEC §5 with
        /// GAME_DESIGN.md §8 cost flavours (shrine = candles+relic, asylum corner =
        /// medicine+wood). An asset at Resources/BuildingCatalog overrides all of it.
        /// </summary>
        static List<BuildingType> CreateDefaultBuildings()
        {
            return new List<BuildingType>
            {
                new BuildingType
                {
                    id = "campfire_t1",
                    displayName = "Campfire",
                    footprint = new Vector2(1.5f, 1.5f),
                    cost = { new ResourceStack(ResourceType.Wood, 4) },
                    buildWorkSeconds = 6f,
                    function = FunctionKind.LightSource,
                    lightProfile = LightProfile.Campfire,
                },
                new BuildingType
                {
                    id = "storage_pile_t1",
                    displayName = "Storage Pile",
                    footprint = new Vector2(2f, 2f),
                    cost = { new ResourceStack(ResourceType.Wood, 6) },
                    buildWorkSeconds = 8f,
                    function = FunctionKind.Storage,
                },
                new BuildingType
                {
                    id = "shelter_t1",
                    displayName = "Shelter",
                    footprint = new Vector2(3f, 3f),
                    cost = { new ResourceStack(ResourceType.Wood, 10) },
                    buildWorkSeconds = 15f,
                    function = FunctionKind.Shelter,
                    destructibleHome = true,
                },
                new BuildingType
                {
                    id = "woodcutter_t1",
                    displayName = "Woodcutter Hut",
                    footprint = new Vector2(3f, 3f),
                    cost = { new ResourceStack(ResourceType.Wood, 8) },
                    buildWorkSeconds = 12f,
                    function = FunctionKind.WorkHut,
                },
                new BuildingType
                {
                    id = "lantern_post_t1",
                    displayName = "Lantern Post",
                    footprint = new Vector2(1f, 1f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 2),
                        new ResourceStack(ResourceType.Oil, 2),
                    },
                    buildWorkSeconds = 5f,
                    function = FunctionKind.LightSource,
                    lightProfile = LightProfile.Lantern,
                },
                new BuildingType
                {
                    id = "guard_post_t1",
                    displayName = "Guard Post",
                    footprint = new Vector2(2f, 2f),
                    cost = { new ResourceStack(ResourceType.Wood, 8) },
                    buildWorkSeconds = 10f,
                    function = FunctionKind.GuardPost,
                },
                new BuildingType
                {
                    id = "candle_shrine_t1",
                    displayName = "Candle Shrine",
                    footprint = new Vector2(2f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Candles, 4),
                        new ResourceStack(ResourceType.RelicFragments, 1),
                    },
                    buildWorkSeconds = 10f,
                    function = FunctionKind.Shrine,
                },
                new BuildingType
                {
                    id = "asylum_corner_t1",
                    displayName = "Asylum Corner",
                    footprint = new Vector2(3f, 3f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 6),
                        new ResourceStack(ResourceType.Medicine, 2),
                    },
                    buildWorkSeconds = 12f,
                    function = FunctionKind.Asylum,
                },
                // Abbey restoration nodes (GAME_DESIGN.md §8: gate = wood+stone,
                // bell tower = wood+iron). Fixed pre-placed sites; see RestorationNode.
                new BuildingType
                {
                    id = "abbey_gate_repair",
                    displayName = "Abbey Gate Repair",
                    footprint = new Vector2(4f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 8),
                        new ResourceStack(ResourceType.Stone, 6),
                    },
                    buildWorkSeconds = 20f,
                    function = FunctionKind.Gate,
                },
                new BuildingType
                {
                    id = "bell_tower_repair",
                    displayName = "Bell Tower Repair",
                    footprint = new Vector2(2f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 6),
                        new ResourceStack(ResourceType.ScrapIron, 4),
                    },
                    buildWorkSeconds = 20f,
                    function = FunctionKind.BellTower,
                },
                // Renewable production buildings (P3-04). Each carries a
                // ProductionBuilding once complete; recipes/yields live in
                // EconomyConfig (RecipeFor keyed by these ids). field_plot_t1 reuses
                // the A2-01 field asset; pasture/kiln/smithy get their own specs.
                new BuildingType
                {
                    id = "field_plot_t1",
                    displayName = "Grain Field",
                    footprint = new Vector2(2f, 2f),
                    cost = { new ResourceStack(ResourceType.Wood, 4) },
                    buildWorkSeconds = 8f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "pasture_t1",
                    displayName = "Pasture",
                    footprint = new Vector2(4f, 4f),
                    cost = { new ResourceStack(ResourceType.Wood, 6) },
                    buildWorkSeconds = 10f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "charcoal_kiln_t1",
                    displayName = "Charcoal Kiln",
                    footprint = new Vector2(2f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 4),
                        new ResourceStack(ResourceType.Stone, 4),
                    },
                    buildWorkSeconds = 12f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "smithy_t1",
                    displayName = "Smithy",
                    footprint = new Vector2(3f, 3f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 6),
                        new ResourceStack(ResourceType.ScrapIron, 3),
                    },
                    buildWorkSeconds = 14f,
                    function = FunctionKind.Production,
                },
                // Forest systems-test / Map 2 building vocabulary. These entries are
                // intentionally prototype-simple: their mechanical behaviour is expressed
                // through existing function kinds and EconomyConfig recipes so Map 1 can
                // exercise the whole option set before bespoke Map 2 art/UI arrives.
                new BuildingType
                {
                    id = "forester_hut_t1",
                    displayName = "Forester Hut",
                    footprint = new Vector2(3f, 3f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 6),
                        new ResourceStack(ResourceType.Tools, 1),
                    },
                    buildWorkSeconds = 12f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "herbalist_hut_t1",
                    displayName = "Herbalist Hut",
                    footprint = new Vector2(2.5f, 2.5f),
                    cost =
                    {
                        new ResourceStack(ResourceType.GreenWood, 4),
                        new ResourceStack(ResourceType.Stone, 2),
                    },
                    buildWorkSeconds = 10f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "orchard_plot_t1",
                    displayName = "Orchard Plot",
                    footprint = new Vector2(4f, 4f),
                    cost =
                    {
                        new ResourceStack(ResourceType.GreenWood, 4),
                        new ResourceStack(ResourceType.SacredSeeds, 1),
                    },
                    buildWorkSeconds = 10f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "hunter_blind_t1",
                    displayName = "Hunter Blind",
                    footprint = new Vector2(2f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.GreenWood, 3),
                        new ResourceStack(ResourceType.Resin, 1),
                    },
                    buildWorkSeconds = 8f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "grove_shrine_t1",
                    displayName = "Grove Shrine",
                    footprint = new Vector2(2f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.SacredSeeds, 2),
                        new ResourceStack(ResourceType.Candles, 2),
                    },
                    buildWorkSeconds = 12f,
                    function = FunctionKind.Shrine,
                },
                new BuildingType
                {
                    id = "root_bridge_t1",
                    displayName = "Root Bridge",
                    footprint = new Vector2(4f, 2f),
                    cost =
                    {
                        new ResourceStack(ResourceType.GreenWood, 6),
                        new ResourceStack(ResourceType.Resin, 2),
                    },
                    buildWorkSeconds = 14f,
                    function = FunctionKind.WorkHut,
                },
                new BuildingType
                {
                    id = "stag_garden_t1",
                    displayName = "Stag Garden",
                    footprint = new Vector2(4f, 4f),
                    cost =
                    {
                        new ResourceStack(ResourceType.GreenWood, 4),
                        new ResourceStack(ResourceType.Apples, 2),
                    },
                    buildWorkSeconds = 14f,
                    function = FunctionKind.Production,
                },
                new BuildingType
                {
                    id = "forest_watchpost_t1",
                    displayName = "Forest Watchpost",
                    footprint = new Vector2(2.5f, 2.5f),
                    cost =
                    {
                        new ResourceStack(ResourceType.GreenWood, 6),
                        new ResourceStack(ResourceType.Resin, 2),
                    },
                    buildWorkSeconds = 14f,
                    function = FunctionKind.Watchtower,
                },
                new BuildingType
                {
                    id = "abbey_cloister_repair",
                    displayName = "Abbey Cloister Repair",
                    footprint = new Vector2(4f, 3f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Stone, 6),
                        new ResourceStack(ResourceType.OldWood, 2),
                        new ResourceStack(ResourceType.SacredSeeds, 1),
                    },
                    buildWorkSeconds = 20f,
                    function = FunctionKind.Asylum,
                },
                // Warrior tier (P3-06). The lodge recruits/houses/upgrades warriors;
                // the watchtower adds ranged support and the vision that arms the
                // nightly dark objective. Warrior stats + upgrade costs live in
                // CombatConfig, never here — the build cost is the structure's own.
                new BuildingType
                {
                    id = "warrior_lodge_t1",
                    displayName = "Warrior Lodge",
                    footprint = new Vector2(4f, 4f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 10),
                        new ResourceStack(ResourceType.ScrapIron, 4),
                        new ResourceStack(ResourceType.Tools, 2),
                    },
                    buildWorkSeconds = 18f,
                    function = FunctionKind.WarriorLodge,
                },
                new BuildingType
                {
                    id = "watchtower_t1",
                    displayName = "Watchtower",
                    footprint = new Vector2(2.5f, 2.5f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 8),
                        new ResourceStack(ResourceType.Stone, 4),
                        new ResourceStack(ResourceType.ScrapIron, 2),
                    },
                    buildWorkSeconds = 16f,
                    function = FunctionKind.Watchtower,
                },
                // Spring-ship reconstruction (P3-14). The staged hull rebuilt at the wreck:
                // wood for the keel/hull, tools for the rigging, and WOOL woven into
                // sailcloth (the sailcloth acquisition route lives in the wool economy,
                // P3-04). Completion satisfies the manifest's hull/rigging part. The build
                // cost IS the "staging": each haul raises the ship's greybox visual.
                new BuildingType
                {
                    id = "spring_ship_t1",
                    displayName = "Spring Ship",
                    footprint = new Vector2(6f, 3f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 20),
                        new ResourceStack(ResourceType.Tools, 4),
                        new ResourceStack(ResourceType.Wool, 8),
                    },
                    buildWorkSeconds = 30f,
                    function = FunctionKind.Ship,
                },
            };
        }
    }
}
