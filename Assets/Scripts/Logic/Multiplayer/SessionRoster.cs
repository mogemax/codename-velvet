using System;
using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.Inventory;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Who is playing this session and in which slot. Joins take the lowest free
    /// slot, so a player leaving the lobby frees their slot for the next joiner
    /// instead of producing duplicates. Once locked, nobody new can join, but
    /// disconnects can still remove players without re-indexing the others.
    /// It also holds what travels with the players from one level to the next.
    /// </summary>
    public class SessionRoster
    {
        public const int DefaultMaxPlayers = 4;

        private readonly RosterEntry[] slots;
        private readonly ResourceInventory.Slot[][] carried;
        private readonly bool[] armed;

        /// <summary>Match time (day/night) when the players arrived at the current level.</summary>
        public float ClockSeconds { get; set; }

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
            carried = new ResourceInventory.Slot[maxPlayers][];
            armed = new bool[maxPlayers];
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

        /// <summary>Records the lobby's character pick; only before the roster is locked.</summary>
        public bool AssignCharacter(int slotIndex, int characterIndex)
        {
            if (characterIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(characterIndex), characterIndex, "Character index must be non-negative.");

            if (IsLocked || !TryGetEntry(slotIndex, out RosterEntry entry))
                return false;

            entry.CharacterIndex = characterIndex;
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

        /// <summary>Records the materials a slot takes to the next level (also without a roster entry, for test players).</summary>
        public void SetCarried(int slotIndex, IEnumerable<ResourceInventory.Slot> stacks) =>
            carried[slotIndex] = stacks.Where(stack => !stack.IsEmpty).ToArray();

        /// <summary>The materials a slot brought to this level. Kept, so restarting the level gives them again.</summary>
        public IReadOnlyList<ResourceInventory.Slot> GetCarried(int slotIndex) =>
            carried[slotIndex] ?? Array.Empty<ResourceInventory.Slot>();

        /// <summary>Records whether a slot takes a sword to the next level (also without a roster entry, for test players).</summary>
        public void SetArmed(int slotIndex, bool isArmed) => armed[slotIndex] = isArmed;

        /// <summary>The slot came to this level with a sword. Kept, so restarting the level gives it again.</summary>
        public bool IsArmed(int slotIndex) => armed[slotIndex];

        /// <summary>One-way for the session; only <see cref="Clear"/> unlocks.</summary>
        public void Lock()
        {
            IsLocked = true;
        }

        /// <summary>Starts a fresh session: empties every slot and unlocks.</summary>
        public void Clear()
        {
            Array.Clear(slots, 0, slots.Length);
            Array.Clear(carried, 0, carried.Length);
            Array.Clear(armed, 0, armed.Length);
            ClockSeconds = 0f;
            Count = 0;
            IsLocked = false;
        }

        private bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < slots.Length;
    }
}
