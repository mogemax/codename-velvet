using System;
using System.Collections.Generic;
using System.Linq;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Who is playing this session and in which slot. Joins take the lowest free
    /// slot, so a player leaving the lobby frees their slot for the next joiner
    /// instead of producing duplicates. Once locked, nobody new can join, but
    /// disconnects can still remove players without re-indexing the others.
    /// </summary>
    public class SessionRoster
    {
        public const int DefaultMaxPlayers = 4;

        private readonly RosterEntry[] slots;

        public int MaxPlayers => slots.Length;
        public int Count { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsFull => Count >= MaxPlayers;

        /// <summary>Occupied slots in slot order.</summary>
        public IEnumerable<RosterEntry> Entries => slots.Where(entry => entry != null);

        public SessionRoster(int maxPlayers = DefaultMaxPlayers)
        {
            if (maxPlayers <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, "Roster needs at least one slot.");

            slots = new RosterEntry[maxPlayers];
        }

        /// <summary>
        /// Adds a player in the lowest free slot. Fails when the roster is locked,
        /// full, or when any of the devices already belongs to a joined player.
        /// </summary>
        public bool TryAdd(IReadOnlyList<int> deviceIds, string controlScheme, out int slotIndex)
        {
            if (deviceIds == null || deviceIds.Count == 0)
                throw new ArgumentException("A player needs at least one device.", nameof(deviceIds));

            slotIndex = -1;

            if (IsLocked || IsFull || deviceIds.Any(ContainsDevice))
                return false;

            slotIndex = Array.IndexOf(slots, null);
            slots[slotIndex] = new RosterEntry(slotIndex, deviceIds.ToArray(), controlScheme);
            Count++;
            return true;
        }

        public bool Remove(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || slots[slotIndex] == null)
                return false;

            slots[slotIndex] = null;
            Count--;
            return true;
        }

        public bool TryGetEntry(int slotIndex, out RosterEntry entry)
        {
            entry = IsValidSlot(slotIndex) ? slots[slotIndex] : null;
            return entry != null;
        }

        public bool ContainsDevice(int deviceId)
        {
            return Entries.Any(entry => entry.DeviceIds.Contains(deviceId));
        }

        /// <summary>One-way for the session; only <see cref="Clear"/> unlocks.</summary>
        public void Lock()
        {
            IsLocked = true;
        }

        /// <summary>Starts a fresh session: empties every slot and unlocks.</summary>
        public void Clear()
        {
            Array.Clear(slots, 0, slots.Length);
            Count = 0;
            IsLocked = false;
        }

        private bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < slots.Length;
    }
}
