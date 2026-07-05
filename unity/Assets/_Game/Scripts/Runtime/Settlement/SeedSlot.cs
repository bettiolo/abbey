using System;
using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// How large a structure a seed slot can hold. Drives the slot's footprint
    /// (used for overlap, the hug rule and light-debt area) through
    /// <see cref="SettlementGrowthConfig.FootprintFor"/> — no dimensions live here.
    /// New sizes append at the end (serialized enum indices must stay stable).
    /// </summary>
    public enum SlotSizeClass
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// Life-cycle of a seed slot. Locked slots are authored-but-not-yet-available
    /// (reserved for later growth), Open slots accept one placement, Occupied slots
    /// already carry a construction site or building.
    /// </summary>
    public enum SlotState
    {
        Locked,
        Open,
        Occupied
    }

    /// <summary>
    /// One authored or grown settlement plot (P3-02 "clustered settlement growth").
    /// A plain serializable record held in the <see cref="SeedSlotSystem"/> registry
    /// — not a MonoBehaviour, so tests build a slot graph without scene objects.
    /// The player no longer free-places buildings anywhere valid: they pick an
    /// <see cref="SlotState.Open"/> slot, and completing a building opens child
    /// slots beside it (see <see cref="SeedSlotSystem.OpenChildSlots"/>). Balance
    /// (footprint per size, ring radius, hug rule) lives in
    /// <see cref="SettlementGrowthConfig"/>, never here.
    /// </summary>
    [Serializable]
    public class SeedSlot
    {
        [Tooltip("World-space ground position (XZ plane) the plot is centered on.")]
        public Vector3 position;

        [Tooltip("Size class the plot can hold (maps to a footprint via the config).")]
        public SlotSizeClass sizeClass = SlotSizeClass.Medium;

        [Tooltip("Current life-cycle state (Locked/Open/Occupied).")]
        public SlotState state = SlotState.Open;

        [Tooltip("Catalog id of the building whose completion opened this child slot; empty for authored seed slots.")]
        public string parentBuildingId;

        [Tooltip("Catalog id occupying the slot once placed (empty while Open/Locked).")]
        public string occupantBuildingId;

        public SeedSlot()
        {
        }

        public SeedSlot(Vector3 position, SlotSizeClass sizeClass,
            SlotState state = SlotState.Open, string parentBuildingId = null)
        {
            this.position = position;
            this.sizeClass = sizeClass;
            this.state = state;
            this.parentBuildingId = parentBuildingId;
        }

        /// <summary>True when the slot can accept a placement.</summary>
        public bool IsOpen => state == SlotState.Open;

        /// <summary>True for slots grown from another building's completion.</summary>
        public bool IsChild => !string.IsNullOrEmpty(parentBuildingId);

        /// <summary>Occupied ground rect for this slot given a config-driven footprint.</summary>
        public Rect FootprintAt(Vector2 footprint)
        {
            return new Rect(
                position.x - footprint.x * 0.5f,
                position.z - footprint.y * 0.5f,
                footprint.x,
                footprint.y);
        }
    }
}
