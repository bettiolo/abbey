using System;
using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// Central stockpile accounting: the one place resources exist once deposited.
    /// Static registry mirroring <see cref="Abbey.Light.DarknessEvaluator"/>: storage
    /// piles register while enabled and raise the shared capacity ceiling; every
    /// completed transaction is appended to <see cref="GameEventLog"/> (type
    /// "resource", data like "wood +3 (salvage)") so downstream consumers — morning
    /// report, moral pressures — read one log. Deterministic: no RNG, integer units.
    /// Tests call <see cref="Clear"/> in [SetUp] for isolation.
    /// </summary>
    public static class ResourceLedger
    {
        static readonly int[] _stock = new int[ResourceTypes.Count];
        static readonly List<StoragePile> _piles = new List<StoragePile>();
        static EconomyConfig _config;

        /// <summary>Raised after a stock change: (type, delta, new amount of that type).</summary>
        public static event Action<ResourceType, int, int> StockChanged;

        /// <summary>Config override for tests; falls back to EconomyConfig.LoadOrDefault().</summary>
        public static EconomyConfig Config
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

        public static IReadOnlyList<StoragePile> StoragePiles => _piles;

        /// <summary>Total units the settlement can hold, across all resource types.</summary>
        public static int Capacity
        {
            get
            {
                var cfg = Config;
                int capacity = cfg.baseStorageCapacity;
                for (int i = 0; i < _piles.Count; i++)
                {
                    if (_piles[i] != null)
                    {
                        capacity += cfg.storagePileCapacity;
                    }
                }
                return capacity;
            }
        }

        /// <summary>Sum of every stored unit (compared against <see cref="Capacity"/>).</summary>
        public static int TotalStored
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _stock.Length; i++)
                {
                    total += _stock[i];
                }
                return total;
            }
        }

        public static int Get(ResourceType type)
        {
            return _stock[(int)type];
        }

        public static bool CanAfford(ResourceType type, int amount)
        {
            return amount <= 0 || _stock[(int)type] >= amount;
        }

        /// <summary>Affordability of a full cost list; duplicate types accumulate.</summary>
        public static bool CanAfford(IReadOnlyList<ResourceStack> cost)
        {
            if (cost == null)
            {
                return true;
            }
            var required = new int[ResourceTypes.Count];
            for (int i = 0; i < cost.Count; i++)
            {
                required[(int)cost[i].type] += Mathf.Max(0, cost[i].amount);
            }
            for (int i = 0; i < required.Length; i++)
            {
                if (_stock[i] < required[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Deposits up to <paramref name="amount"/> units, clamped by remaining
        /// capacity. Returns the units actually stored; the shortfall is logged as
        /// overflow (dropped at the pile — storage must cost something).
        /// </summary>
        public static int Add(ResourceType type, int amount, string reason)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int stored = Mathf.Min(amount, Mathf.Max(0, Capacity - TotalStored));
            if (stored > 0)
            {
                int index = (int)type;
                _stock[index] += stored;
                GameEventLog.Append("resource", $"{ResourceTypes.Id(type)} +{stored} ({reason})");
                StockChanged?.Invoke(type, stored, _stock[index]);
            }
            if (stored < amount)
            {
                GameEventLog.Append("resource",
                    $"{ResourceTypes.Id(type)} overflow {amount - stored} ({reason}, storage full)");
            }
            return stored;
        }

        /// <summary>Spends units of one resource. All-or-nothing; false leaves stock untouched.</summary>
        public static bool TryConsume(ResourceType type, int amount, string reason)
        {
            if (amount <= 0)
            {
                return true;
            }
            int index = (int)type;
            if (_stock[index] < amount)
            {
                return false;
            }
            _stock[index] -= amount;
            GameEventLog.Append("resource", $"{ResourceTypes.Id(type)} -{amount} ({reason})");
            StockChanged?.Invoke(type, -amount, _stock[index]);
            return true;
        }

        /// <summary>Spends a full cost list atomically (build costs). False changes nothing.</summary>
        public static bool TryConsume(IReadOnlyList<ResourceStack> cost, string reason)
        {
            if (cost == null)
            {
                return true;
            }
            if (!CanAfford(cost))
            {
                return false;
            }
            for (int i = 0; i < cost.Count; i++)
            {
                TryConsume(cost[i].type, cost[i].amount, reason);
            }
            return true;
        }

        public static void RegisterStorage(StoragePile pile)
        {
            if (pile != null && !_piles.Contains(pile))
            {
                _piles.Add(pile);
            }
        }

        public static void UnregisterStorage(StoragePile pile)
        {
            _piles.Remove(pile);
        }

        /// <summary>
        /// Grants the wreck-crate head start from config (VERTICAL_SLICE_SPEC §4).
        /// Call once at scene start, after storage exists.
        /// </summary>
        public static void GrantStartingStock()
        {
            var cfg = Config;
            Add(ResourceType.Wood, cfg.startingWood, "wreck crates");
            Add(ResourceType.Food, cfg.startingFood, "wreck crates");
            Add(ResourceType.Oil, cfg.startingOil, "wreck crates");
            Add(ResourceType.Medicine, cfg.startingMedicine, "wreck crates");
        }

        /// <summary>Drops stock, piles, config override and subscribers (test isolation).</summary>
        public static void Clear()
        {
            for (int i = 0; i < _stock.Length; i++)
            {
                _stock[i] = 0;
            }
            _piles.Clear();
            _config = null;
            StockChanged = null;
        }
    }
}
