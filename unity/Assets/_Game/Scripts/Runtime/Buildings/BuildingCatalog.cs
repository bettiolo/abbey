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
    /// LightSource for Shrine, an <see cref="InfirmaryZone"/> for Infirmary; Gate
    /// and BellTower flip their <see cref="AbbeyState"/> flags (abbey restoration,
    /// P2-04). Shelter, WorkHut and GuardPost are identified by
    /// <see cref="Building.Kind"/> alone. New kinds append at the end (serialized
    /// enum indices must stay stable).
    /// </summary>
    public enum FunctionKind
    {
        LightSource,
        Storage,
        Shelter,
        WorkHut,
        GuardPost,
        Shrine,
        Infirmary,
        Gate,
        BellTower
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
        /// GAME_DESIGN.md §8 cost flavours (shrine = candles+relic, infirmary =
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
                    id = "infirmary_corner_t1",
                    displayName = "Infirmary Corner",
                    footprint = new Vector2(3f, 3f),
                    cost =
                    {
                        new ResourceStack(ResourceType.Wood, 6),
                        new ResourceStack(ResourceType.Medicine, 2),
                    },
                    buildWorkSeconds = 12f,
                    function = FunctionKind.Infirmary,
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
            };
        }
    }
}
