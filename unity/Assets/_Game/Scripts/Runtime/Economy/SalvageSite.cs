using System;
using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>Visible depletion stages of a salvage node, in depletion order.</summary>
    public enum SalvageStage
    {
        Intact,
        Picked,
        Stripped
    }

    /// <summary>
    /// A finite salvage node on the shipwreck: a pool of wood/food/oil/medicine
    /// (sizes from <see cref="EconomyConfig"/>) that villagers and the hero harvest
    /// until it is stripped bare — the wreck head start runs out (GAME_DESIGN.md §7).
    /// Depletion is shown in three stages computed from the remaining fraction;
    /// stage transitions raise <see cref="StageChanged"/> and hit the event log.
    /// Visuals are optional: per-stage child objects are swapped and/or a visual
    /// root is scaled down — no asset is required. Deterministic: harvest order is
    /// fixed enum order, integer units, no RNG. [ExecuteAlways] so EditMode tests
    /// get OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SalvageSite : MonoBehaviour
    {
        [Tooltip("Optional per-stage visuals (index 0 Intact, 1 Picked, 2 Stripped); the active one is swapped in.")]
        public GameObject[] stageVisuals;

        [Tooltip("Optional transform scaled down as the site depletes (placeholder visual).")]
        public Transform visualRoot;

        static readonly List<SalvageSite> _active = new List<SalvageSite>();

        /// <summary>Every enabled salvage site (work assignment, debug panel).</summary>
        public static IReadOnlyList<SalvageSite> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        EconomyConfig _config;
        int[] _remaining;
        int _initialTotal;

        /// <summary>Raised after the visible depletion stage changes.</summary>
        public event Action<SalvageSite, SalvageStage> StageChanged;

        public SalvageStage Stage { get; private set; } = SalvageStage.Intact;

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

        /// <summary>Injects a config (tests) and refills the pool from it.</summary>
        public void Configure(EconomyConfig config)
        {
            _config = config;
            ResetPool();
        }

        public int Remaining(ResourceType type)
        {
            EnsurePool();
            return _remaining[(int)type];
        }

        public int TotalRemaining
        {
            get
            {
                EnsurePool();
                int total = 0;
                for (int i = 0; i < _remaining.Length; i++)
                {
                    total += _remaining[i];
                }
                return total;
            }
        }

        /// <summary>Fraction of the original pool still in the wreck, 1..0.</summary>
        public float RemainingFraction
        {
            get
            {
                EnsurePool();
                return _initialTotal <= 0 ? 0f : (float)TotalRemaining / _initialTotal;
            }
        }

        public bool IsExhausted => TotalRemaining <= 0;

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
        /// Takes up to <paramref name="amount"/> units of one resource out of the
        /// pool. Returns the units actually extracted (the caller carries them —
        /// depositing into the <see cref="ResourceLedger"/> is the hauler's job).
        /// </summary>
        public int Harvest(ResourceType type, int amount)
        {
            EnsurePool();
            if (amount <= 0)
            {
                return 0;
            }
            int index = (int)type;
            int taken = Mathf.Min(amount, _remaining[index]);
            if (taken <= 0)
            {
                return 0;
            }
            _remaining[index] -= taken;
            UpdateStage();
            return taken;
        }

        /// <summary>
        /// One completed work cycle: extracts up to salvageYieldPerCycle units of
        /// the first non-empty resource in enum order (deterministic). False when
        /// the site is exhausted.
        /// </summary>
        public bool TryHarvestCycle(out ResourceType type, out int amount)
        {
            EnsurePool();
            for (int i = 0; i < _remaining.Length; i++)
            {
                if (_remaining[i] > 0)
                {
                    type = (ResourceType)i;
                    amount = Harvest(type, Config.salvageYieldPerCycle);
                    return true;
                }
            }
            type = default;
            amount = 0;
            return false;
        }

        void EnsurePool()
        {
            if (_remaining == null)
            {
                ResetPool();
            }
        }

        void ResetPool()
        {
            var cfg = Config;
            _remaining = new int[ResourceTypes.Count];
            _remaining[(int)ResourceType.Wood] = cfg.salvageSiteWood;
            _remaining[(int)ResourceType.Food] = cfg.salvageSiteFood;
            _remaining[(int)ResourceType.Oil] = cfg.salvageSiteOil;
            _remaining[(int)ResourceType.Medicine] = cfg.salvageSiteMedicine;
            _initialTotal = cfg.salvageSiteWood + cfg.salvageSiteFood
                            + cfg.salvageSiteOil + cfg.salvageSiteMedicine;
            Stage = ComputeStage(RemainingFraction, cfg);
            ApplyStageVisuals();
        }

        void UpdateStage()
        {
            var next = ComputeStage(RemainingFraction, Config);
            if (next == Stage)
            {
                return;
            }
            GameEventLog.Append("salvage", $"{name} stage {Stage}->{next}");
            Stage = next;
            ApplyStageVisuals();
            StageChanged?.Invoke(this, next);
        }

        static SalvageStage ComputeStage(float fraction, EconomyConfig cfg)
        {
            if (fraction <= cfg.salvageStrippedFraction)
            {
                return SalvageStage.Stripped;
            }
            if (fraction <= cfg.salvagePickedFraction)
            {
                return SalvageStage.Picked;
            }
            return SalvageStage.Intact;
        }

        void ApplyStageVisuals()
        {
            if (stageVisuals != null && stageVisuals.Length > 0)
            {
                for (int i = 0; i < stageVisuals.Length; i++)
                {
                    if (stageVisuals[i] != null)
                    {
                        stageVisuals[i].SetActive(i == (int)Stage);
                    }
                }
            }
            if (visualRoot != null)
            {
                // Placeholder read of depletion: the heap shrinks (visual only).
                visualRoot.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, RemainingFraction);
            }
        }
    }
}
