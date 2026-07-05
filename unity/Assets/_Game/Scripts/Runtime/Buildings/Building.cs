using System;
using System.Collections.Generic;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Villagers;
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
    /// <see cref="AsylumZone"/>; Gate and BellTower completions flip their
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

        /// <summary>
        /// Raised once for every building finished through <see cref="Construct"/>,
        /// after its function component and visual are attached. A static,
        /// GameEventLog-free completion signal so listeners (e.g.
        /// <see cref="Abbey.Settlement.SeedSlotSystem"/>, which opens child slots)
        /// react without the per-instance <see cref="ConstructionSite.Completed"/>
        /// hook and without spamming the event log. Cleared by <see cref="ClearRegistry"/>.
        /// </summary>
        public static event Action<Building> Constructed;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
            Constructed = null;
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

        // ------------------------------------------------------------------
        // Destructible homes (P3-05): occupancy, interior-light flare, hit points
        // and the raze path. Balance (hit points) lives in CombatConfig, never here.
        // ------------------------------------------------------------------

        readonly List<VillagerAgent> _occupants = new List<VillagerAgent>();
        LightSource _flare;
        float _maxHitPoints = -1f;
        float _hitPoints;

        /// <summary>True when this building is a home settlers shelter in and defend at night.</summary>
        public bool IsDestructibleHome => Type != null && Type.destructibleHome;

        /// <summary>The settlers assigned to this home (source for the raze kill-list).</summary>
        public IReadOnlyList<VillagerAgent> Occupants => _occupants;

        /// <summary>The interior light that flares while the house is awake (null while asleep/razed).</summary>
        public LightSource FlareLight => _flare;

        /// <summary>True while the interior light is flared and lit (the door reads Safe).</summary>
        public bool IsFlaring => _flare != null && _flare.isLit;

        /// <summary>The home has been overwhelmed and razed (light node gone, occupants dead).</summary>
        public bool IsRazed { get; private set; }

        /// <summary>Structural hit points remaining (destructible homes only).</summary>
        public float HitPoints
        {
            get { EnsureDefenseInit(); return _hitPoints; }
        }

        /// <summary>Full structural hit points (destructible homes only).</summary>
        public float MaxHitPoints
        {
            get { EnsureDefenseInit(); return _maxHitPoints; }
        }

        void EnsureDefenseInit()
        {
            if (_maxHitPoints >= 0f || !IsDestructibleHome)
            {
                return;
            }
            InitializeDefense(CombatConfig.LoadOrDefault().HomeHitPointsFor(Type));
        }

        /// <summary>Sets this home's full/current hit points (HomeDefenseSystem injects its config's value).</summary>
        public void InitializeDefense(float maxHitPoints)
        {
            _maxHitPoints = Mathf.Max(1f, maxHitPoints);
            _hitPoints = _maxHitPoints;
        }

        /// <summary>Replaces this home's occupant list (HomeDefenseSystem syncs it from the shelter map).</summary>
        public void SetOccupants(IReadOnlyList<VillagerAgent> occupants)
        {
            _occupants.Clear();
            if (occupants == null)
            {
                return;
            }
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] != null && !_occupants.Contains(occupants[i]))
                {
                    _occupants.Add(occupants[i]);
                }
            }
        }

        /// <summary>Registers a single occupant (tests / manual assignment).</summary>
        public void AddOccupant(VillagerAgent villager)
        {
            if (villager != null && !_occupants.Contains(villager))
            {
                _occupants.Add(villager);
            }
        }

        /// <summary>
        /// Flares the interior light (wakeup): a small Safe zone at the door that
        /// debuffs the assaulting monsters. The flare IS a real
        /// <see cref="LightSource"/>, so <see cref="DarknessEvaluator"/> reclassifies
        /// the doorstep the moment it lights — and Dark again once it is razed.
        /// </summary>
        public void FlareOn(float radius, float strength)
        {
            if (IsRazed)
            {
                return;
            }
            if (_flare == null)
            {
                var go = new GameObject("InteriorFlare");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                _flare = go.AddComponent<LightSource>();
                _flare.fuelSeconds = -1f; // the defenders keep it burning while they fight
                _flare.autoTick = false;
            }
            _flare.radius = radius;
            _flare.strength = strength;
            _flare.Ignite();
            if (!_flare.isLit)
            {
                _flare.isLit = true;
            }
        }

        /// <summary>Extinguishes the interior flare (threat passed): the door goes dark again.</summary>
        public void FlareOff()
        {
            if (_flare != null)
            {
                _flare.Extinguish();
            }
        }

        /// <summary>
        /// Applies structural damage from a night assault. When hit points reach zero
        /// the home is razed (<see cref="Raze"/>). No effect on non-home buildings or
        /// an already-razed home.
        /// </summary>
        public void TakeStructuralDamage(float amount)
        {
            if (!IsDestructibleHome || IsRazed || amount <= 0f)
            {
                return;
            }
            EnsureDefenseInit();
            _hitPoints = Mathf.Max(0f, _hitPoints - amount);
            GameEventLog.Append("home_damaged", $"{name} hp={_hitPoints:F1}/{_maxHitPoints:F1} -{amount:F1}");
            if (_hitPoints <= 0f)
            {
                Raze();
            }
        }

        /// <summary>
        /// The outer line broke: the home collapses. Its interior light node is
        /// destroyed (the doorstep reclassifies Dark), every occupant inside is
        /// killed, and <see cref="EventBus.SettlersKilledInHome"/> /
        /// <see cref="EventBus.HomeRazed"/> fire so the morning report can mourn the
        /// loss. Idempotent.
        /// </summary>
        public void Raze()
        {
            if (IsRazed)
            {
                return;
            }
            IsRazed = true;
            _hitPoints = 0f;

            // Lose the light node: extinguish and destroy the flare (unregisters it
            // from DarknessEvaluator via LightSource.OnDisable).
            if (_flare != null)
            {
                _flare.Extinguish();
                if (Application.isPlaying)
                {
                    Destroy(_flare.gameObject);
                }
                else
                {
                    DestroyImmediate(_flare.gameObject);
                }
                _flare = null;
            }

            // Kill the settlers inside.
            int killed = 0;
            for (int i = 0; i < _occupants.Count; i++)
            {
                var v = _occupants[i];
                if (v == null || v.State == VillagerState.Dead)
                {
                    continue;
                }
                v.ForceState(VillagerState.Dead);
                killed++;
            }
            GameEventLog.Append("home_razed", $"{name} occupants_killed={killed}");
            if (killed > 0)
            {
                EventBus.RaiseSettlersKilledInHome(gameObject);
            }
            EventBus.RaiseHomeRazed(gameObject);
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
                case FunctionKind.Asylum:
                    // P3-02 shell: attach the care zone (radius + occupancy only).
                    // Sanity-recovery behaviour arrives in P3-03; no instant heal.
                    var zone = go.AddComponent<AsylumZone>();
                    zone.radius = cfg.asylumRadius;
                    AbbeyState.MarkAsylumBuilt();
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
                case FunctionKind.Production:
                    // Renewable production (P3-04): the component resolves its recipe
                    // from EconomyConfig by catalog id and runs the day-cycle once
                    // staffed. Balance stays in EconomyConfig, never here.
                    var production = go.AddComponent<ProductionBuilding>();
                    production.Initialize(type.id);
                    break;
                case FunctionKind.WarriorLodge:
                    // Warrior tier (P3-06): recruits/houses/upgrades warriors. Stats and
                    // upgrade costs live in CombatConfig.
                    var lodge = go.AddComponent<WarriorStructure>();
                    lodge.role = WarriorStructureRole.Lodge;
                    break;
                case FunctionKind.Watchtower:
                    var tower = go.AddComponent<WarriorStructure>();
                    tower.role = WarriorStructureRole.Watchtower;
                    break;
                // Shelter, WorkHut, GuardPost: identified by Building.Kind;
                // behaviour arrives with later tasks (villager housing, roles).
            }

            AttachVisual(type, go.transform);

            // Completion signal (e.g. SeedSlotSystem opens child slots beside it).
            Constructed?.Invoke(building);
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
