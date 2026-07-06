using Abbey.Core;
using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// The world-space wear field that turns villager traffic into emergent roads
    /// (P3-12, ROADMAP Phase 3 item 15). Every path-wearing walker reports the
    /// segment it just travelled (via <see cref="PlanarMotion.StepWorn"/>, wired to
    /// <see cref="ReportTraversal"/>); the grid accumulates distance-proportional
    /// wear per cell, clamped to a saturation cap. Wear is a pure accumulator here —
    /// tier promotion, speed bonus and the lantern/light-debt scan live on
    /// <see cref="DesirePathSystem"/>, which also calls <see cref="DecayDay"/> each
    /// day marker so untrodden paths fade.
    ///
    /// The grid registers itself as <see cref="PlanarMotion.TrafficReporter"/> while
    /// enabled and clears it on disable, so movement participates without any
    /// per-agent wiring and existing tests (no grid) are byte-identical. Singleton
    /// like <see cref="SeedSlotSystem"/>; the field is instance state so tests build
    /// a grid without a global registry. [ExecuteAlways] so EditMode tests get the
    /// OnEnable/OnDisable lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TrafficGrid : MonoBehaviour
    {
        public static TrafficGrid Instance { get; private set; }

        /// <summary>When true the debug panel draws the wear heatmap gizmo.</summary>
        public static bool DrawHeatmap;

        PathsConfig _config;
        bool _isDuplicate;

        float[] _wear;
        int _columns;
        int _rows;
        float _cellSize;
        Vector2 _origin;

        public int Columns => _columns;
        public int Rows => _rows;
        public float CellSize => _cellSize;
        public Vector2 Origin => _origin;

        /// <summary>Raw wear field, row-major (index = row * columns + col). Read-only view for serialization/debug.</summary>
        public float[] WearField => _wear;

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
            set { _config = value; EnsureField(); }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[TrafficGrid] Duplicate instance ignored.", this);
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
            EnsureField();
            PlanarMotion.TrafficReporter = ReportTraversal;
        }

        void OnDisable()
        {
            if (PlanarMotion.TrafficReporter == (System.Action<Vector3, Vector3>)ReportTraversal)
            {
                PlanarMotion.TrafficReporter = null;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config and sizes the grid to it (tests / scene builder).</summary>
        public void Configure(PathsConfig config)
        {
            _config = config;
            _wear = null;
            EnsureField();
        }

        /// <summary>
        /// Sizes the grid to cover a world-space rectangle (scene builder: fit to map
        /// bounds). Keeps the config's cell size. Clears any accumulated wear.
        /// </summary>
        public void ConfigureBounds(Vector2 min, Vector2 max)
        {
            var cfg = Config;
            _cellSize = Mathf.Max(0.1f, cfg.cellSize);
            _origin = min;
            _columns = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / _cellSize));
            _rows = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / _cellSize));
            _wear = new float[_columns * _rows];
        }

        void EnsureField()
        {
            var cfg = Config;
            if (_wear != null && _columns == cfg.gridColumns && _rows == cfg.gridRows
                && Mathf.Approximately(_cellSize, cfg.cellSize) && _origin == cfg.gridOrigin)
            {
                return;
            }
            _cellSize = Mathf.Max(0.1f, cfg.cellSize);
            _origin = cfg.gridOrigin;
            _columns = Mathf.Max(1, cfg.gridColumns);
            _rows = Mathf.Max(1, cfg.gridRows);
            _wear = new float[_columns * _rows];
        }

        // ------------------------------------------------------------------
        // Cell addressing
        // ------------------------------------------------------------------

        /// <summary>Column/row for a world position; false when outside the grid bounds.</summary>
        public bool TryCell(Vector3 worldPos, out int col, out int row)
        {
            col = Mathf.FloorToInt((worldPos.x - _origin.x) / _cellSize);
            row = Mathf.FloorToInt((worldPos.z - _origin.y) / _cellSize);
            return col >= 0 && col < _columns && row >= 0 && row < _rows;
        }

        /// <summary>World center of a cell.</summary>
        public Vector3 CellCenter(int col, int row)
        {
            return new Vector3(
                _origin.x + (col + 0.5f) * _cellSize, 0f,
                _origin.y + (row + 0.5f) * _cellSize);
        }

        // ------------------------------------------------------------------
        // Wear
        // ------------------------------------------------------------------

        /// <summary>Adds wear to the cell containing a position (clamped to the cap). No-op off-grid.</summary>
        public void AddWear(Vector3 worldPos, float amount)
        {
            EnsureField();
            if (amount <= 0f || !TryCell(worldPos, out int col, out int row))
            {
                return;
            }
            int i = row * _columns + col;
            _wear[i] = Mathf.Min(Config.maxWearPerCell, _wear[i] + amount);
        }

        /// <summary>Wear at a world position (0 off-grid).</summary>
        public float WearAt(Vector3 worldPos)
        {
            EnsureField();
            return TryCell(worldPos, out int col, out int row) ? _wear[row * _columns + col] : 0f;
        }

        /// <summary>Path tier at a world position (0 = untrodden).</summary>
        public int TierAt(Vector3 worldPos)
        {
            return Config.TierForWear(WearAt(worldPos));
        }

        /// <summary>
        /// Deposits distance-proportional wear along the segment from -&gt; to,
        /// sampling at half-cell steps so every crossed cell is worn. Deterministic:
        /// a pure function of the segment and the config. Wired to
        /// <see cref="PlanarMotion.TrafficReporter"/>.
        /// </summary>
        public void ReportTraversal(Vector3 from, Vector3 to)
        {
            EnsureField();
            var cfg = Config;
            float length = PlanarMotion.Distance(from, to);
            if (length <= 0.0001f || cfg.wearPerDistanceUnit <= 0f)
            {
                return;
            }

            float step = Mathf.Max(0.1f, _cellSize * 0.5f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
            float segment = length / samples;
            float wearPerSample = cfg.wearPerDistanceUnit * segment;
            for (int s = 0; s < samples; s++)
            {
                // Sample the mid-point of each sub-segment so both endpoints' cells wear.
                float t = (s + 0.5f) / samples;
                Vector3 p = Vector3.Lerp(from, to, t);
                AddWear(p, wearPerSample);
            }
        }

        /// <summary>
        /// Sheds a fraction of every cell's wear (called at each day marker by
        /// <see cref="DesirePathSystem"/>). Wear that falls below the config floor is
        /// zeroed so faint scuffs heal fully rather than lingering forever.
        /// </summary>
        public void DecayDay()
        {
            EnsureField();
            var cfg = Config;
            float keep = 1f - Mathf.Clamp01(cfg.wearDecayPerDay);
            for (int i = 0; i < _wear.Length; i++)
            {
                float w = _wear[i] * keep;
                _wear[i] = w < cfg.wearDecayFloor ? 0f : w;
            }
        }

        /// <summary>Number of cells at or above a tier (debug / tests).</summary>
        public int CountCellsAtTier(int minTier)
        {
            EnsureField();
            var cfg = Config;
            int count = 0;
            for (int i = 0; i < _wear.Length; i++)
            {
                if (cfg.TierForWear(_wear[i]) >= minTier)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Total accumulated wear across the field (debug).</summary>
        public float TotalWear()
        {
            EnsureField();
            float total = 0f;
            for (int i = 0; i < _wear.Length; i++)
            {
                total += _wear[i];
            }
            return total;
        }

        // ------------------------------------------------------------------
        // Serializable state (save / load / tests)
        // ------------------------------------------------------------------

        [System.Serializable]
        public struct State
        {
            public int columns;
            public int rows;
            public float cellSize;
            public Vector2 origin;
            public float[] wear;
        }

        /// <summary>Snapshots the wear field (deep copy).</summary>
        public State SerializeState()
        {
            EnsureField();
            var copy = new float[_wear.Length];
            System.Array.Copy(_wear, copy, _wear.Length);
            return new State
            {
                columns = _columns,
                rows = _rows,
                cellSize = _cellSize,
                origin = _origin,
                wear = copy,
            };
        }

        /// <summary>Restores a previously serialized wear field.</summary>
        public void LoadState(State state)
        {
            if (state.wear == null || state.columns <= 0 || state.rows <= 0
                || state.wear.Length != state.columns * state.rows)
            {
                return;
            }
            _columns = state.columns;
            _rows = state.rows;
            _cellSize = Mathf.Max(0.1f, state.cellSize);
            _origin = state.origin;
            _wear = new float[state.wear.Length];
            System.Array.Copy(state.wear, _wear, state.wear.Length);
        }

        /// <summary>Zeroes the wear field (test isolation).</summary>
        public void ClearWear()
        {
            EnsureField();
            System.Array.Clear(_wear, 0, _wear.Length);
        }

        // ------------------------------------------------------------------
        // Heatmap gizmo
        // ------------------------------------------------------------------

        void OnDrawGizmos()
        {
            if (!DrawHeatmap || _wear == null)
            {
                return;
            }
            var cfg = Config;
            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _columns; col++)
                {
                    float w = _wear[row * _columns + col];
                    if (w <= 0f)
                    {
                        continue;
                    }
                    int tier = cfg.TierForWear(w);
                    // Cool (faint traffic) -> warm (worn road): green -> amber -> red.
                    float heat = Mathf.Clamp01(w / Mathf.Max(0.01f, cfg.maxWearPerCell));
                    Color c = tier >= cfg.importantTier
                        ? new Color(1f, 0.35f, 0.1f, 0.55f)
                        : new Color(Mathf.Lerp(0.2f, 1f, heat), Mathf.Lerp(1f, 0.6f, heat), 0.15f, 0.35f + 0.3f * heat);
                    Gizmos.color = c;
                    Gizmos.DrawCube(CellCenter(col, row) + new Vector3(0f, 0.02f, 0f),
                        new Vector3(_cellSize * 0.9f, 0.02f, _cellSize * 0.9f));
                }
            }
        }
    }
}
