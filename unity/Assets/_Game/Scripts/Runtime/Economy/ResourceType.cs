using System;
using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>Phase 2 resources, exactly per GAME_DESIGN.md §7.</summary>
    public enum ResourceType
    {
        Wood,
        Food,
        Oil,
        Candles,
        Stone,
        ScrapIron,
        Medicine,
        RelicFragments
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
