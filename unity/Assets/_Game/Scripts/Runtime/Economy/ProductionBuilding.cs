using System.Collections.Generic;
using Abbey.Core;
using Abbey.World;
using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// The renewable-production component on a completed production building (P3-04):
    /// a field plot, pasture, charcoal kiln or smithy. It turns staffed worker-days
    /// into ledger deposits by running its <see cref="ProductionRecipe"/> (resolved
    /// from <see cref="EconomyConfig"/> by building id — all balance is data, none is
    /// here). Mirrors <see cref="SalvageSite"/>: a static <see cref="Active"/>
    /// registry, deterministic integer accounting, no RNG, [ExecuteAlways] so EditMode
    /// tests get OnEnable/OnDisable registration.
    ///
    /// <para><b>Cycle.</b> Each in-game day, <see cref="AdvanceDay"/> adds one
    /// worker-day per staffed worker to the cycle progress once
    /// <see cref="ProductionRecipe.workersRequired"/> are present; an unstaffed
    /// building produces nothing. When progress reaches
    /// <see cref="ProductionRecipe.cycleDays"/> the recipe inputs are consumed (a
    /// conversion recipe stalls, holding progress, until the ledger can afford them)
    /// and the outputs are deposited and event-logged ("production" record).</para>
    ///
    /// <para><b>Season.</b> Growth recipes (<see cref="ProductionRecipe.seasonal"/>)
    /// scale their deposited yield by <see cref="EconomyConfig.SeasonalYieldMultiplier"/>
    /// for the season the cycle completes in — more toward autumn — and do not advance
    /// at all in winter (multiplier 0 halts growth, forcing the stockpiling that makes
    /// Winter judgment). Conversion recipes ignore the season and run year-round.</para>
    ///
    /// <para><b>Staffing.</b> Worker presence is supplied either directly (tests call
    /// <see cref="SetStaff"/>) or by villager production jobs
    /// (<see cref="AddWorker"/>/<see cref="RemoveWorker"/> as they arrive and leave) —
    /// the component itself has no dependency on the villager layer.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ProductionBuilding : MonoBehaviour
    {
        [Tooltip("Advance the cycle automatically on EventBus.DayChanged. Tests set "
                 + "false and call AdvanceDay() directly for determinism.")]
        public bool autoAdvanceOnDayChanged = true;

        static readonly List<ProductionBuilding> _active = new List<ProductionBuilding>();

        /// <summary>Every enabled production building (staffing, debug panel).</summary>
        public static IReadOnlyList<ProductionBuilding> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        EconomyConfig _config;
        ProductionRecipe _recipe;
        string _buildingId;
        float _progressWorkerDays;
        int _completedCycles;
        int _manualStaff;
        readonly List<Object> _workers = new List<Object>();

        /// <summary>Catalog id of this building (drives the recipe lookup).</summary>
        public string BuildingId => _buildingId;

        /// <summary>The recipe this building runs (null if none matches its id).</summary>
        public ProductionRecipe Recipe => _recipe;

        /// <summary>Completed production cycles since construction (debug / downstream).</summary>
        public int CompletedCycles => _completedCycles;

        /// <summary>Accumulated worker-days toward the current cycle.</summary>
        public float ProgressWorkerDays => _progressWorkerDays;

        public EconomyConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = EconomyConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>Staffed workers right now: the direct test count plus live workers.</summary>
        public int StaffedWorkers => _manualStaff + _workers.Count;

        /// <summary>True when at least workersRequired are staffing the building.</summary>
        public bool IsStaffed => _recipe != null && StaffedWorkers >= _recipe.workersRequired;

        /// <summary>Progress toward the next cycle, 0..1 (0 when no recipe/zero-length).</summary>
        public float CycleProgress01
        {
            get
            {
                if (_recipe == null || _recipe.cycleDays <= 0f)
                {
                    return 0f;
                }
                return Mathf.Clamp01(_progressWorkerDays / _recipe.cycleDays);
            }
        }

        /// <summary>
        /// Binds the building to its catalog id and resolves the recipe. Called by
        /// <see cref="Abbey.Buildings.Building.Construct"/>; tests may inject a config.
        /// </summary>
        public void Initialize(string buildingId, EconomyConfig config = null)
        {
            if (config != null)
            {
                _config = config;
            }
            _buildingId = buildingId;
            _recipe = Config.RecipeFor(buildingId);
            _progressWorkerDays = 0f;
            _completedCycles = 0;
        }

        void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
            EventBus.DayChanged -= OnDayChanged;
            EventBus.DayChanged += OnDayChanged;
        }

        void OnDisable()
        {
            EventBus.DayChanged -= OnDayChanged;
            _active.Remove(this);
        }

        void OnDayChanged(int dayNumber)
        {
            if (!autoAdvanceOnDayChanged)
            {
                return;
            }
            var season = SeasonSystem.Instance != null
                ? SeasonSystem.Instance.CurrentSeason
                : Season.Spring;
            AdvanceDay(season);
        }

        // ------------------------------------------------------------------
        // Staffing
        // ------------------------------------------------------------------

        /// <summary>Sets the direct staffed-worker count (authored buildings / tests).</summary>
        public void SetStaff(int workers)
        {
            _manualStaff = Mathf.Max(0, workers);
        }

        /// <summary>Registers a live worker (a villager production job on arrival).</summary>
        public void AddWorker(Object worker)
        {
            if (worker != null && !_workers.Contains(worker))
            {
                _workers.Add(worker);
            }
        }

        /// <summary>Unregisters a live worker (villager left / changed job / interrupted).</summary>
        public void RemoveWorker(Object worker)
        {
            _workers.Remove(worker);
        }

        // ------------------------------------------------------------------
        // Cycle
        // ------------------------------------------------------------------

        /// <summary>
        /// Advances one in-game day of production for the given season. Returns true
        /// if at least one cycle completed and deposited outputs this day. Deterministic:
        /// integer units, fixed enum order, no RNG.
        /// </summary>
        public bool AdvanceDay(Season season)
        {
            var recipe = _recipe;
            if (recipe == null || StaffedWorkers < recipe.workersRequired)
            {
                return false; // no recipe, or unstaffed: nothing is produced
            }

            float yieldMultiplier = recipe.seasonal ? Config.SeasonalYieldMultiplier(season) : 1f;
            if (recipe.seasonal && yieldMultiplier <= 0f)
            {
                return false; // winter: nothing grows — no progress, no yield
            }

            if (HasInputs(recipe) && !ResourceLedger.CanAfford(recipe.inputs))
            {
                // Conversion recipe with an empty larder: the crew idles today. No
                // progress banks while inputs are missing — held work never "catches
                // up" and over-produces once inputs return.
                return false;
            }

            _progressWorkerDays += StaffedWorkers;

            bool producedAny = false;
            int guard = 0;
            while (_progressWorkerDays >= recipe.cycleDays && guard++ < 64)
            {
                if (HasInputs(recipe) && !ResourceLedger.CanAfford(recipe.inputs))
                {
                    // Ran the larder dry mid-day (staff > input supply): hold the last
                    // cycle at the threshold for a future day rather than burning it.
                    _progressWorkerDays = recipe.cycleDays;
                    break;
                }

                if (HasInputs(recipe))
                {
                    ResourceLedger.TryConsume(recipe.inputs, $"production {_buildingId}");
                }

                _progressWorkerDays -= recipe.cycleDays;
                DepositOutputs(recipe, yieldMultiplier, season);
                _completedCycles++;
                producedAny = true;
                GameEventLog.Append("production",
                    $"{_buildingId} harvest cycle {_completedCycles} ({season})");
            }
            return producedAny;
        }

        static bool HasInputs(ProductionRecipe recipe)
        {
            return recipe.inputs != null && recipe.inputs.Count > 0;
        }

        void DepositOutputs(ProductionRecipe recipe, float yieldMultiplier, Season season)
        {
            if (recipe.outputs == null)
            {
                return;
            }
            for (int i = 0; i < recipe.outputs.Count; i++)
            {
                var output = recipe.outputs[i];
                int amount = recipe.seasonal
                    ? Mathf.Max(0, Mathf.RoundToInt(output.amount * yieldMultiplier))
                    : output.amount;
                if (amount > 0)
                {
                    ResourceLedger.Add(output.type, amount,
                        $"harvest {_buildingId} ({season})");
                }
            }
        }
    }
}
