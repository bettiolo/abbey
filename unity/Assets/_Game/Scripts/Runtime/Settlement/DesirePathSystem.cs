using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// The desire-path authority (P3-12): it reads the <see cref="TrafficGrid"/>'s
    /// wear field and turns it into gameplay. Three jobs:
    ///
    /// 1. Speed bonus — <see cref="SpeedMultiplierAt"/> maps a position's path tier to
    ///    the config walk-speed multiplier; it is wired to
    ///    <see cref="PlanarMotion.PathSpeedProvider"/> so every path-wearing walker
    ///    speeds up on a worn road with no per-agent code.
    /// 2. Daily upkeep — at each day marker it decays the grid (untrodden paths fade)
    ///    and logs tier promotions/demotions.
    /// 3. Lit-path expectation — at Dusk it scans important paths (tier &gt;=
    ///    <see cref="PathsConfig.importantTier"/>): a lantern covering one is set to
    ///    burn fuel at the config multiplier through the night (fuel debt), while an
    ///    unlit important-path cell reads as danger and adds to the settlement light
    ///    debt (<see cref="ComputePathLightDebt"/>, surfaced beside P3-02's own debt).
    ///    At Dawn the fuel multipliers are cleared.
    ///
    /// Singleton, [ExecuteAlways] so EditMode tests get the lifecycle. Reads
    /// <see cref="PathsConfig"/>; needs a <see cref="TrafficGrid"/> in the scene.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DesirePathSystem : MonoBehaviour
    {
        public static DesirePathSystem Instance { get; private set; }

        PathsConfig _config;
        bool _isDuplicate;
        int _importantCellCount;
        int _unlitImportantCellCount;
        float _pathLightDebt;
        readonly List<LightSource> _boostedLanterns = new List<LightSource>();

        /// <summary>Important-path cells found by the last dusk scan.</summary>
        public int ImportantCellCount => _importantCellCount;

        /// <summary>Important-path cells left unlit by the last dusk scan.</summary>
        public int UnlitImportantCellCount => _unlitImportantCellCount;

        /// <summary>Light debt from unlit important paths at the last dusk scan.</summary>
        public float PathLightDebt => _pathLightDebt;

        /// <summary>Lanterns currently burning extra fuel because they cover an important path.</summary>
        public int BoostedLanternCount => _boostedLanterns.Count;

        public PathsConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PathsConfig.LoadOrDefault();
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
                Debug.LogWarning("[DesirePathSystem] Duplicate instance ignored.", this);
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
            EventBus.DayChanged -= OnDayChanged;
            EventBus.DayChanged += OnDayChanged;
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
            PlanarMotion.PathSpeedProvider = SpeedMultiplierAt;
        }

        void OnDisable()
        {
            EventBus.DayChanged -= OnDayChanged;
            EventBus.PhaseChanged -= OnPhaseChanged;
            ClearImportantPathCoverage();
            if (PlanarMotion.PathSpeedProvider == (System.Func<Vector3, float>)SpeedMultiplierAt)
            {
                PlanarMotion.PathSpeedProvider = null;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------
        // Speed bonus
        // ------------------------------------------------------------------

        /// <summary>Path-tier walk-speed multiplier at a position (1 with no grid / off a path).</summary>
        public float SpeedMultiplierAt(Vector3 worldPos)
        {
            var grid = TrafficGrid.Instance;
            if (grid == null)
            {
                return 1f;
            }
            return Config.SpeedMultiplierForTier(grid.TierAt(worldPos));
        }

        /// <summary>Path tier at a position (0 with no grid).</summary>
        public int TierAt(Vector3 worldPos)
        {
            var grid = TrafficGrid.Instance;
            return grid != null ? grid.TierAt(worldPos) : 0;
        }

        /// <summary>Whether a position sits on an important (lantern-expecting) path.</summary>
        public bool IsImportantPathAt(Vector3 worldPos)
        {
            return Config.IsImportantTier(TierAt(worldPos));
        }

        // ------------------------------------------------------------------
        // Daily upkeep
        // ------------------------------------------------------------------

        void OnDayChanged(int dayNumber)
        {
            var grid = TrafficGrid.Instance;
            if (grid == null)
            {
                return;
            }
            int importantBefore = grid.CountCellsAtTier(Config.importantTier);
            int tier1Before = grid.CountCellsAtTier(1);
            grid.DecayDay();
            int importantAfter = grid.CountCellsAtTier(Config.importantTier);
            int tier1After = grid.CountCellsAtTier(1);
            if (importantAfter != importantBefore || tier1After != tier1Before)
            {
                GameEventLog.Append("desire_path",
                    $"day={dayNumber} paths={tier1After} important={importantAfter} " +
                    $"(was {tier1Before}/{importantBefore})");
            }
        }

        // ------------------------------------------------------------------
        // Lit-path expectation (dusk fuel debt + light debt)
        // ------------------------------------------------------------------

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dusk)
            {
                ScanImportantPaths(applyFuelDebt: true);
            }
            else if (phase == DayPhase.Dawn || phase == DayPhase.Day)
            {
                ClearImportantPathCoverage();
            }
        }

        /// <summary>
        /// Walks every important-path cell: a lit lantern covering one is set to burn
        /// extra fuel for the night (when <paramref name="applyFuelDebt"/>); an unlit
        /// cell adds to the path light debt. Recomputes the cached counts either way,
        /// so debug/tests can poll it without a phase change.
        /// </summary>
        public void ScanImportantPaths(bool applyFuelDebt)
        {
            if (applyFuelDebt)
            {
                ClearImportantPathCoverage();
            }

            var grid = TrafficGrid.Instance;
            _importantCellCount = 0;
            _unlitImportantCellCount = 0;
            _pathLightDebt = 0f;
            if (grid == null)
            {
                return;
            }

            var cfg = Config;
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Columns; col++)
                {
                    Vector3 center = grid.CellCenter(col, row);
                    if (!cfg.IsImportantTier(grid.TierAt(center)))
                    {
                        continue;
                    }
                    _importantCellCount++;

                    var covering = DarknessEvaluator.StrongestLightAt(center);
                    if (covering != null)
                    {
                        if (applyFuelDebt)
                        {
                            BoostLantern(covering);
                        }
                    }
                    else
                    {
                        _unlitImportantCellCount++;
                        _pathLightDebt += cfg.unlitImportantPathLightDebtPerCell;
                    }
                }
            }

            if (applyFuelDebt)
            {
                GameEventLog.Append("desire_path",
                    $"dusk important={_importantCellCount} unlit={_unlitImportantCellCount} " +
                    $"lightDebt={_pathLightDebt:F1} boostedLanterns={_boostedLanterns.Count}");
            }
        }

        /// <summary>Light debt from unlit important paths (recomputes on demand; used by P3-02 surface + debug).</summary>
        public float ComputePathLightDebt()
        {
            ScanImportantPaths(applyFuelDebt: false);
            return _pathLightDebt;
        }

        void BoostLantern(LightSource lantern)
        {
            if (lantern == null || lantern.HasInfiniteFuel || _boostedLanterns.Contains(lantern))
            {
                return;
            }
            lantern.PathFuelMultiplier = Config.importantPathFuelMultiplier;
            _boostedLanterns.Add(lantern);
        }

        /// <summary>Restores every boosted lantern's normal fuel rate (dawn / disable).</summary>
        public void ClearImportantPathCoverage()
        {
            for (int i = 0; i < _boostedLanterns.Count; i++)
            {
                if (_boostedLanterns[i] != null)
                {
                    _boostedLanterns[i].PathFuelMultiplier = 1f;
                }
            }
            _boostedLanterns.Clear();
        }
    }
}
