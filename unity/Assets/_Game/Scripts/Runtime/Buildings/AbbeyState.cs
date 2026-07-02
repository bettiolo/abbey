using Abbey.Core;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// The abbey's restoration progress, as one static queryable surface
    /// (GAME_DESIGN.md §8 abbey restoration nodes). Completion effects write here
    /// (see <see cref="Building.Construct"/>); other systems only read:
    /// <see cref="GateRepaired"/> is the "dangerous night path is blocked" flag the
    /// nightmare spawner consumes (NightmareDirector reads it in its own task —
    /// nothing here touches Runtime/Nightmares), and
    /// <see cref="BellRangeMultiplier"/> scales every bell pulse radius (consumed
    /// by <see cref="Abbey.Villagers.DuskRecallSystem"/> where the BellRang pulse
    /// is evaluated, so BellkeeperController stays untouched). Every change lands
    /// in the event log as an "abbey" record. Static and mutation-idempotent;
    /// <see cref="Clear"/> resets for tests.
    /// </summary>
    public static class AbbeyState
    {
        /// <summary>True once the abbey gate node completed: the dangerous night path is blocked.</summary>
        public static bool GateRepaired { get; private set; }

        /// <summary>True once the bell tower node completed (the bell reaches further).</summary>
        public static bool BellTowerRepaired { get; private set; }

        /// <summary>True once a candle shrine stands (a sacred light burns).</summary>
        public static bool ShrineLit { get; private set; }

        /// <summary>True once an infirmary corner stands (injured villagers heal faster).</summary>
        public static bool InfirmaryBuilt { get; private set; }

        /// <summary>
        /// Bell pulse radius multiplier. 1 until the bell tower is repaired, then
        /// PrototypeConfig.bellTowerRangeMultiplier. Applied where the pulse is
        /// consumed (DuskRecallSystem), never inside BellkeeperController.
        /// </summary>
        public static float BellRangeMultiplier { get; private set; } = 1f;

        public static void MarkGateRepaired()
        {
            if (GateRepaired)
            {
                return;
            }
            GateRepaired = true;
            GameEventLog.Append("abbey", "gate_repaired (night path blocked)");
        }

        public static void MarkBellTowerRepaired(float rangeMultiplier)
        {
            if (BellTowerRepaired)
            {
                return;
            }
            BellTowerRepaired = true;
            BellRangeMultiplier = Mathf.Max(1f, rangeMultiplier);
            GameEventLog.Append("abbey",
                $"bell_tower_repaired (bell range x{BellRangeMultiplier:F2})");
        }

        public static void MarkShrineLit()
        {
            if (ShrineLit)
            {
                return;
            }
            ShrineLit = true;
            GameEventLog.Append("abbey", "shrine_lit (sacred flame burns)");
        }

        public static void MarkInfirmaryBuilt()
        {
            if (InfirmaryBuilt)
            {
                return;
            }
            InfirmaryBuilt = true;
            GameEventLog.Append("abbey", "infirmary_built");
        }

        /// <summary>Test isolation.</summary>
        public static void Clear()
        {
            GateRepaired = false;
            BellTowerRepaired = false;
            ShrineLit = false;
            InfirmaryBuilt = false;
            BellRangeMultiplier = 1f;
        }
    }
}
