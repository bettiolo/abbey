using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// A completed, placed building: the identity component every finished
    /// structure carries (catalog id, function kind, occupied footprint). While
    /// enabled it registers in the static <see cref="Active"/> registry so
    /// <see cref="BuildingPlacer"/> can reject overlapping placements and later
    /// systems (villager roles, abbey nodes) can find structures by
    /// <see cref="Kind"/>. Function behaviour lives in sibling components attached
    /// by <see cref="Construct"/>: <see cref="Abbey.Light.LightSource"/> (plain or
    /// sacred for Shrine), <see cref="Abbey.Economy.StoragePile"/> or
    /// <see cref="InfirmaryZone"/>; Gate and BellTower completions flip their
    /// <see cref="AbbeyState"/> flags; the remaining kinds are identified by
    /// <see cref="Kind"/> alone.
    /// [ExecuteAlways] so EditMode tests get OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Building : MonoBehaviour
    {
        static readonly List<Building> _active = new List<Building>();

        /// <summary>Every enabled completed building (placement overlap, debug panel).</summary>
        public static IReadOnlyList<Building> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        /// <summary>Catalog entry this building was built from (null until Initialize).</summary>
        public BuildingType Type { get; private set; }

        public string Id => Type != null ? Type.id : null;

        public FunctionKind Kind => Type != null ? Type.function : default;

        /// <summary>Occupied ground rect on the XZ plane; zero-size until initialized.</summary>
        public Rect Footprint => Type != null
            ? Type.FootprintAt(transform.position)
            : new Rect(transform.position.x, transform.position.z, 0f, 0f);

        /// <summary>Binds this building to its catalog entry (called by <see cref="Construct"/>).</summary>
        public void Initialize(BuildingType type)
        {
            Type = type;
        }

        void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        void OnDisable()
        {
            _active.Remove(this);
        }

        /// <summary>
        /// Creates a finished building of the given type at a world position:
        /// the <see cref="Building"/> identity, the function component the catalog
        /// entry asks for (LightSource configured from <see cref="PrototypeConfig"/>
        /// per the entry's light profile, StoragePile for storage) and a visual —
        /// the catalog's completedVisualPrefab when set (generated GLB hook),
        /// otherwise a placeholder cube scaled to the footprint.
        /// </summary>
        public static Building Construct(BuildingType type, Vector3 position)
        {
            var go = new GameObject(type.id);
            go.transform.position = position;

            var building = go.AddComponent<Building>();
            building.Initialize(type);

            var cfg = PrototypeConfig.LoadOrDefault();
            switch (type.function)
            {
                case FunctionKind.LightSource:
                    var light = go.AddComponent<LightSource>();
                    bool campfire = type.lightProfile == LightProfile.Campfire;
                    light.radius = campfire ? cfg.campfireRadius : cfg.lanternRadius;
                    light.strength = campfire ? cfg.campfireStrength : cfg.lanternStrength;
                    light.fuelSeconds = cfg.defaultFuelSeconds;
                    light.fuelConsumptionPerSecond = cfg.fuelConsumptionPerSecond;
                    light.sacred = false;
                    light.isLit = true;
                    break;
                case FunctionKind.Storage:
                    go.AddComponent<StoragePile>();
                    break;
                case FunctionKind.Shrine:
                    // A candle shrine is a sacred flame: PrototypeConfig sacred
                    // values, infinite fuel (sacred lights never burn out).
                    var shrineLight = go.AddComponent<LightSource>();
                    shrineLight.radius = cfg.sacredFlameRadius;
                    shrineLight.strength = cfg.sacredFlameStrength;
                    shrineLight.fuelSeconds = -1f;
                    shrineLight.sacred = true;
                    shrineLight.isLit = true;
                    AbbeyState.MarkShrineLit();
                    break;
                case FunctionKind.Infirmary:
                    var zone = go.AddComponent<InfirmaryZone>();
                    zone.radius = cfg.infirmaryRadius;
                    zone.healSeconds = cfg.infirmaryHealSeconds;
                    AbbeyState.MarkInfirmaryBuilt();
                    break;
                case FunctionKind.Gate:
                    // The repaired gate blocks the dangerous night path. The
                    // queryable flag is AbbeyState.GateRepaired (the nightmare
                    // spawner consumes it in its own task); the placeholder visual
                    // cube below doubles as the physical blocker.
                    AbbeyState.MarkGateRepaired();
                    break;
                case FunctionKind.BellTower:
                    AbbeyState.MarkBellTowerRepaired(cfg.bellTowerRangeMultiplier);
                    break;
                // Shelter, WorkHut, GuardPost: identified by Building.Kind;
                // behaviour arrives with later tasks (villager housing, roles).
            }

            AttachVisual(type, go.transform);
            return building;
        }

        static void AttachVisual(BuildingType type, Transform parent)
        {
            GameObject visual;
            if (type.completedVisualPrefab != null)
            {
                visual = Instantiate(type.completedVisualPrefab, parent);
                visual.transform.localPosition = Vector3.zero;
            }
            else
            {
                // Placeholder box occupying the footprint (visual only, no balance).
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(parent, false);
                float height = Mathf.Max(1f, Mathf.Min(type.footprint.x, type.footprint.y));
                visual.transform.localScale = new Vector3(type.footprint.x, height, type.footprint.y);
                visual.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            }
            visual.name = "Visual";
        }
    }
}
