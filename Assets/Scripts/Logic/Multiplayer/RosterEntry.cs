using System.Collections.Generic;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// One joined player as plain data: the slot it occupies, the input
    /// devices it joined with and the character it picked in the lobby. Device ids are <c>InputDevice.deviceId</c> values,
    /// kept as ints so this type has no Input System dependency.
    /// </summary>
    public sealed class RosterEntry
    {
        public int SlotIndex { get; }
        public IReadOnlyList<int> DeviceIds { get; }
        public string ControlScheme { get; }

        /// <summary>Index into the character catalog; -1 until the lobby assigns one.</summary>
        public int CharacterIndex { get; internal set; } = -1;

        public RosterEntry(int slotIndex, IReadOnlyList<int> deviceIds, string controlScheme)
        {
            SlotIndex = slotIndex;
            DeviceIds = deviceIds;
            ControlScheme = controlScheme;
        }
    }
}
