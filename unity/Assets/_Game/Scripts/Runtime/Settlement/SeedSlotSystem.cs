using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// The settlement's seed-slot registry and growth authority (P3-02, ROADMAP
    /// Phase 3 item 14). Authored seed slots start Open; a placement occupies one
    /// (<see cref="BuildingPlacer"/> gates on <see cref="FindOpenSlotNear"/>), and
    /// completing a building opens child slots beside it — but only where they
    /// "hug" existing buildings or lit ground, so the village stays compact and
    /// overlapping windows/lanterns/abbey light compound safety. Overextension is
    /// surfaced as <see cref="ComputeLightDebt"/>: the summed area of slots and
    /// buildings sitting outside Safe light at dusk.
    ///
    /// Reacts to <see cref="Building.Constructed"/> (a static, GameEventLog-free
    /// signal so completions do not spam the log). Deterministic: child slots are
    /// laid on an evenly spaced ring, no RNG. Singleton like
    /// <see cref="Abbey.World.SeasonSystem"/>; the slot list is instance state so
    /// tests build a graph without touching global registries. [ExecuteAlways] so
    /// EditMode tests get the OnEnable/OnDisable lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SeedSlotSystem : MonoBehaviour
    {
        public static SeedSlotSystem Instance { get; private set; }

        readonly List<SeedSlot> _slots = new List<SeedSlot>();
        SettlementGrowthConfig _config;
        bool _isDuplicate;

        /// <summary>Every registered slot (authored + grown), in insertion order.</summary>
        public IReadOnlyList<SeedSlot> Slots => _slots;

        public SettlementGrowthConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = SettlementGrowthConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[SeedSlotSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnEnable()
        {
            if (_isDuplicate)
            {
                return;
            }
            Building.Constructed -= OnBuildingConstructed;
            Building.Constructed += OnBuildingConstructed;
        }

        void OnDisable()
        {
            Building.Constructed -= OnBuildingConstructed;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------
        // Authoring + queries
        // ------------------------------------------------------------------

        /// <summary>Registers an authored (parentless) seed slot and returns it.</summary>
        public SeedSlot AddAuthoredSlot(Vector3 position, SlotSizeClass size,
            SlotState state = SlotState.Open)
        {
            var slot = new SeedSlot(position, size, state);
            _slots.Add(slot);
            return slot;
        }

        /// <summary>Registers an already-built slot instance (tests / scene builder).</summary>
        public SeedSlot AddSlot(SeedSlot slot)
        {
            if (slot != null && !_slots.Contains(slot))
            {
                _slots.Add(slot);
            }
            return slot;
        }

        /// <summary>Number of slots in a given state.</summary>
        public int CountByState(SlotState state)
        {
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].state == state)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// The nearest Open slot whose center is within <paramref name="tolerance"/>
        /// planar distance of <paramref name="position"/>, or null. Used by
        /// <see cref="BuildingPlacer"/> to accept only on-slot placements.
        /// </summary>
        public SeedSlot FindOpenSlotNear(Vector3 position, float tolerance)
        {
            SeedSlot best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsOpen)
                {
                    continue;
                }
                float dist = PlanarDistance(position, slot.position);
                if (dist <= tolerance && dist < bestDist)
                {
                    bestDist = dist;
                    best = slot;
                }
            }
            return best;
        }

        /// <summary>
        /// Marks a slot Occupied by a building id and logs a "settlement" record.
        /// Returns false if the slot is null or not Open.
        /// </summary>
        public bool OccupySlot(SeedSlot slot, string buildingId)
        {
            if (slot == null || !slot.IsOpen)
            {
                return false;
            }
            slot.state = SlotState.Occupied;
            slot.occupantBuildingId = buildingId;
            GameEventLog.Append("settlement",
                $"slot_occupied {buildingId} at ({slot.position.x:F1}, {slot.position.z:F1})");
            return true;
        }

        // ------------------------------------------------------------------
        // Growth (child slots on completion)
        // ------------------------------------------------------------------

        void OnBuildingConstructed(Building building)
        {
            if (_isDuplicate || building == null)
            {
                return;
            }
            OpenChildSlots(building);
        }

        /// <summary>
        /// Opens up to <see cref="SettlementGrowthConfig.childSlotsPerBuilding"/>
        /// child slots on an evenly spaced ring around a completed building. A
        /// candidate is kept only when it does not overlap an existing building,
        /// site or slot AND (with the hug rule on) touches an existing building or
        /// sits on lit ground. Returns the number of slots actually opened.
        /// </summary>
        public int OpenChildSlots(Building building)
        {
            if (building == null)
            {
                return 0;
            }
            var cfg = Config;
            int count = Mathf.Max(0, cfg.childSlotsPerBuilding);
            if (count == 0 || cfg.childSlotRingRadius <= 0f)
            {
                return 0;
            }

            Vector3 center = building.transform.position;
            Vector2 childFootprint = cfg.FootprintFor(cfg.childSlotSize);
            int opened = 0;

            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f) * i / count;
                Vector3 candidate = center + new Vector3(
                    Mathf.Cos(angle) * cfg.childSlotRingRadius, 0f,
                    Mathf.Sin(angle) * cfg.childSlotRingRadius);

                if (!IsCandidateFree(candidate, childFootprint, cfg))
                {
                    continue;
                }
                if (cfg.requireHug && !Hugs(candidate, childFootprint, cfg))
                {
                    continue;
                }

                var slot = new SeedSlot(candidate, cfg.childSlotSize,
                    SlotState.Open, building.Id);
                _slots.Add(slot);
                opened++;
                GameEventLog.Append("settlement",
                    $"slot_opened parent={building.Id} at ({candidate.x:F1}, {candidate.z:F1})");
            }
            return opened;
        }

        /// <summary>A candidate does not overlap any building, site or existing slot.</summary>
        bool IsCandidateFree(Vector3 candidate, Vector2 footprint, SettlementGrowthConfig cfg)
        {
            var rect = RectAt(candidate, footprint);

            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && rect.Overlaps(buildings[i].Footprint))
                {
                    return false;
                }
            }
            var sites = ConstructionSite.Active;
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i] != null && rect.Overlaps(sites[i].Footprint))
                {
                    return false;
                }
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                if (PlanarDistance(candidate, _slots[i].position) < cfg.minSlotSeparation)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The hug rule: a candidate must touch an existing building (footprint
        /// expanded by the adjacency margin) OR sit on lit ground (Safe, or Edge
        /// when the config includes it).
        /// </summary>
        bool Hugs(Vector3 candidate, Vector2 footprint, SettlementGrowthConfig cfg)
        {
            var padded = RectAt(candidate, footprint);
            padded.xMin -= cfg.hugAdjacencyMargin;
            padded.xMax += cfg.hugAdjacencyMargin;
            padded.yMin -= cfg.hugAdjacencyMargin;
            padded.yMax += cfg.hugAdjacencyMargin;

            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && padded.Overlaps(buildings[i].Footprint))
                {
                    return true;
                }
            }

            var zone = DarknessEvaluator.Classify(candidate);
            if (zone == LightZone.Safe)
            {
                return true;
            }
            if (zone == LightZone.Edge && cfg.litGroundIncludesEdge)
            {
                return true;
            }
            // Child slots also hug existing desire paths (P3-12): the village grows
            // "beside existing buildings, paths, and lit ground".
            var paths = DesirePathSystem.Instance;
            return paths != null && paths.TierAt(candidate) >= 1;
        }

        // ------------------------------------------------------------------
        // Light debt
        // ------------------------------------------------------------------

        /// <summary>
        /// Overextension penalty at dusk: the summed ground area of every Open/
        /// Occupied slot and every completed building whose center sits outside
        /// Safe light, weighted per light zone by the config. Grows when a slot is
        /// opened in darkness and falls when a lantern brings it back into Safe.
        /// </summary>
        public float ComputeLightDebt()
        {
            var cfg = Config;
            float debt = 0f;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.state == SlotState.Locked)
                {
                    continue;
                }
                var zone = DarknessEvaluator.Classify(slot.position);
                debt += cfg.AreaFor(slot.sizeClass) * cfg.DebtWeightFor(zone);
            }

            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || building.Type == null)
                {
                    continue;
                }
                var zone = DarknessEvaluator.Classify(building.transform.position);
                float area = Mathf.Max(0f, building.Type.footprint.x)
                             * Mathf.Max(0f, building.Type.footprint.y);
                debt += area * cfg.DebtWeightFor(zone);
            }

            // Unlit important desire paths read as danger and add to the debt (P3-12).
            var paths = DesirePathSystem.Instance;
            if (paths != null)
            {
                debt += paths.ComputePathLightDebt();
            }

            return debt;
        }

        // ------------------------------------------------------------------
        // Helpers + test isolation
        // ------------------------------------------------------------------

        /// <summary>Clears the slot registry (test isolation; keeps the instance).</summary>
        public void ClearSlots()
        {
            _slots.Clear();
        }

        static Rect RectAt(Vector3 center, Vector2 footprint)
        {
            return new Rect(
                center.x - footprint.x * 0.5f,
                center.z - footprint.y * 0.5f,
                footprint.x,
                footprint.y);
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
