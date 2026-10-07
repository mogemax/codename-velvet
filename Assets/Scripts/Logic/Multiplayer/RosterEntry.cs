using System.Collections.Generic;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// One joined player as plain data: the slot it occupies and the input
    /// devices it joined with. Device ids are <c>InputDevice.deviceId</c> values,
    /// kept as ints so this type has no Input System dependency.
    /// </summary>
    public sealed class RosterEntry
    {
        public int SlotIndex { get; }
        public IReadOnlyList<int> DeviceIds { get; }
        public string ControlScheme { get; }

        public RosterEntry(int slotIndex, IReadOnlyList<int> deviceIds, string controlScheme)
        {
            SlotIndex = slotIndex;
            DeviceIds = deviceIds;
            ControlScheme = controlScheme;
        }
    }
}
