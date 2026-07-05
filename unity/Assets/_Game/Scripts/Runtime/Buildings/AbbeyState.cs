using Abbey.Core;
using Abbey.Morale;
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

        /// <summary>True once an asylum corner stands (P3-03 sanity recovery hangs off this).</summary>
        public static bool AsylumBuilt { get; private set; }

        /// <summary>
        /// Bell pulse radius multiplier. 1 until the bell tower is repaired, then
        /// PrototypeConfig.bellTowerRangeMultiplier. Applied where the pulse is
        /// consumed (DuskRecallSystem), never inside BellkeeperController.
        /// </summary>
        public static float BellRangeMultiplier { get; private set; } = 1f;

        // ---- Transformation state (P3-10) ---------------------------------

        /// <summary>
        /// The abbey's derived identity (P3-10), written by
        /// <see cref="Abbey.Morale.AbbeyTransformationSystem"/>: Balanced until a
        /// transformation dominates, then one of Sanctuary / Fortress / Famine / Cult /
        /// Broken. Read-only for everyone else.
        /// </summary>
        public static AbbeyForm CurrentForm { get; private set; } = AbbeyForm.Balanced;

        /// <summary>
        /// The settlement-wide modifiers the current form applies (magnitudes from
        /// PressuresConfig — P3-10). Neutral under Balanced. Consumers read these instead of
        /// branching on <see cref="CurrentForm"/>: sacred-light radius (Sanctuary), window
        /// volley (Fortress), ration ceiling (Famine), recall penalty (Broken), offerings /
        /// sanctity decay (Cult). Never null.
        /// </summary>
        public static AbbeyFormModifiers Modifiers { get; private set; } = NeutralModifiers();

        static AbbeyFormModifiers NeutralModifiers()
        {
            return new AbbeyFormModifiers { rationCeilingMultiplier = 1f, note = "balanced" };
        }

        /// <summary>
        /// Adopts a transformation form and its modifiers (called by
        /// <see cref="Abbey.Morale.AbbeyTransformationSystem"/>). Idempotent; a null
        /// modifier set falls back to the neutral one.
        /// </summary>
        public static void SetTransformation(AbbeyForm form, AbbeyFormModifiers modifiers)
        {
            CurrentForm = form;
            Modifiers = modifiers ?? NeutralModifiers();
        }

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

        public static void MarkAsylumBuilt()
        {
            if (AsylumBuilt)
            {
                return;
            }
            AsylumBuilt = true;
            GameEventLog.Append("abbey", "asylum_built");
        }

        /// <summary>Test isolation.</summary>
        public static void Clear()
        {
            GateRepaired = false;
            BellTowerRepaired = false;
            ShrineLit = false;
            AsylumBuilt = false;
            BellRangeMultiplier = 1f;
            CurrentForm = AbbeyForm.Balanced;
            Modifiers = NeutralModifiers();
        }
    }
}
