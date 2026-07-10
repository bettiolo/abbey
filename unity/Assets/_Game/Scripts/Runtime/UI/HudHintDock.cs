using UnityEngine;

namespace Abbey.UI
{
    /// <summary>
    /// Stable slots for the collapsed HUD/debug shortcuts. Every panel used to
    /// choose its own screen coordinate, so several labels landed on the same top
    /// pixels. The dock reserves one deterministic slot per surface and wraps into
    /// rows above the player vitals strip as the Game view narrows.
    /// </summary>
    public enum HudHintSlot
    {
        DebugOverlay,
        Economy,
        Buildings,
        Nightmare,
        MorningReport,
        Combat,
        Hud,
        Minimap,
        Sanity,
        Settlement,
        Season,
        Overdrive,
        Laws,
        Pressures,
        Island,
        Outcome,
    }

    public static class HudHintDock
    {
        public const int SlotCount = 16;
        public const float Margin = 8f;
        public const float RowHeight = 20f;
        public const float ColumnGap = 6f;
        public const float RowGap = 2f;
        public const float PreferredSlotWidth = 190f;
        public const float PlayerVitalsReserve = 46f;

        public static Rect RectFor(
            HudHintSlot slot, float screenWidth, float screenHeight)
        {
            float availableWidth = Mathf.Max(1f, screenWidth - Margin * 2f);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt(
                    (availableWidth + ColumnGap) / (PreferredSlotWidth + ColumnGap)),
                1,
                SlotCount);
            float slotWidth =
                (availableWidth - (columns - 1) * ColumnGap) / columns;
            int slotIndex = Mathf.Clamp((int)slot, 0, SlotCount - 1);
            int totalRows = Mathf.CeilToInt(SlotCount / (float)columns);
            int row = slotIndex / columns;
            int column = slotIndex % columns;
            float dockHeight = totalRows * RowHeight + (totalRows - 1) * RowGap;
            float dockY = Mathf.Max(
                Margin,
                screenHeight - PlayerVitalsReserve - dockHeight);

            return new Rect(
                Margin + column * (slotWidth + ColumnGap),
                dockY + row * (RowHeight + RowGap),
                slotWidth,
                RowHeight);
        }

        public static void Draw(HudHintSlot slot, string label)
        {
            GUI.Label(RectFor(slot, Screen.width, Screen.height), label);
        }
    }
}
