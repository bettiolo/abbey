using System;
using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// Settlement resources. Indices 0..7 are the Phase 2 salvage economy (exactly
    /// per GAME_DESIGN.md §7); 8..13 are the Phase 3 renewable economy (P3-04). New
    /// values append at the end — serialized indices must stay stable.
    ///
    /// Grain vs Food: <see cref="Food"/> stays the eat-ledger (what hunger consumes,
    /// P3-10). <see cref="Grain"/> is the raw renewable harvest that mills/cooks into
    /// Food at the 1:N ratio in <see cref="EconomyConfig.grainToFoodRatio"/> — that
    /// conversion is a downstream concern (P3-10 hunger / P3-14 manifest), not an
    /// automatic ledger effect, so the two stocks are tracked separately here.
    /// </summary>
    public enum ResourceType
    {
        Wood,
        Food,
        Oil,
        Candles,
        Stone,
        ScrapIron,
        Medicine,
        RelicFragments,
        // Phase 3 renewable economy (P3-04).
        Grain,
        Meat,
        Wool,
        Herbs,
        Tools,
        Coal
    }

    /// <summary>Enum helpers shared by ledger, sites and the event log.</summary>
    public static class ResourceTypes
    {
        /// <summary>Number of resource types (array sizing).</summary>
        public static readonly int Count = Enum.GetValues(typeof(ResourceType)).Length;

        /// <summary>Snake_case id matching the GAME_DESIGN.md vocabulary (log-friendly).</summary>
        public static string Id(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return "wood";
                case ResourceType.Food: return "food";
                case ResourceType.Oil: return "oil";
                case ResourceType.Candles: return "candles";
                case ResourceType.Stone: return "stone";
                case ResourceType.ScrapIron: return "scrap_iron";
                case ResourceType.Medicine: return "medicine";
                case ResourceType.RelicFragments: return "relic_fragment";
                case ResourceType.Grain: return "grain";
                case ResourceType.Meat: return "meat";
                case ResourceType.Wool: return "wool";
                case ResourceType.Herbs: return "herbs";
                case ResourceType.Tools: return "tools";
                case ResourceType.Coal: return "coal";
                default: return type.ToString().ToLowerInvariant();
            }
        }
    }

    /// <summary>An amount of one resource — cost lists, yields, carried loads.</summary>
    [Serializable]
    public struct ResourceStack
    {
        public ResourceType type;
        [Min(0)] public int amount;

        public ResourceStack(ResourceType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }

        public override string ToString()
        {
            return $"{ResourceTypes.Id(type)} x{amount}";
        }
    }
}
