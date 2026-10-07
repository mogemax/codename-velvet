using System;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Which player slot this character belongs to. Assigned once at spawn by the
    /// join/spawn flow; every per-player system (health, camera, HUD) reads it from
    /// here instead of tracking its own slot.
    /// Rule: player identity is always instance data — no shared/global state,
    /// singletons or scene-wide lookups in this class.
    /// </summary>
    public class PlayerIdentity : MonoBehaviour
    {
        public const int UnassignedSlot = -1;

        public int SlotIndex { get; private set; } = UnassignedSlot;
        public Color PlayerColor { get; private set; } = Color.white;
        public bool IsInitialized => SlotIndex != UnassignedSlot;

        public void Initialize(int slotIndex, Color color)
        {
            if (IsInitialized)
                throw new InvalidOperationException($"{name} already belongs to slot {SlotIndex}.");
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Slot index must be non-negative.");

            SlotIndex = slotIndex;
            PlayerColor = color;
        }
    }
}
