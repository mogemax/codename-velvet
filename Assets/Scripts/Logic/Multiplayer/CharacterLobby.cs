using System;
using System.Collections.Generic;
using System.Linq;

namespace CodenameVelvet.Multiplayer
{
    public enum LobbySlotState
    {
        Free,
        Choosing,
        Locked,
    }

    /// <summary>
    /// Character-select rules for the join lobby, indexed by roster slot.
    /// A joined player browses characters and locks one in; a locked character
    /// belongs to that player only. The earliest joined player still present is
    /// the host, the only one who can start, and only once everybody has locked in.
    /// </summary>
    public class CharacterLobby
    {
        public const int NoCharacter = -1;
        public const int NoHost = -1;

        private readonly LobbySlotState[] states;
        private readonly int[] characters;
        private readonly List<int> joinOrder = new List<int>();

        public int SlotCount => states.Length;
        public int CharacterCount { get; }
        public int JoinedCount => joinOrder.Count;
        public int HostSlot => joinOrder.Count > 0 ? joinOrder[0] : NoHost;
        public bool CanStart => JoinedCount > 0 && joinOrder.All(slot => states[slot] == LobbySlotState.Locked);

        public CharacterLobby(int slotCount, int characterCount)
        {
            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "The lobby needs at least one slot.");
            if (characterCount < slotCount)
                throw new ArgumentOutOfRangeException(nameof(characterCount), characterCount,
                    "Characters are unique, so there must be at least one per slot.");

            states = new LobbySlotState[slotCount];
            characters = Enumerable.Repeat(NoCharacter, slotCount).ToArray();
            CharacterCount = characterCount;
        }

        public LobbySlotState GetState(int slot) => states[slot];

        /// <summary>The character the slot is browsing or has locked; <see cref="NoCharacter"/> when free.</summary>
        public int GetCharacter(int slot) => characters[slot];

        /// <summary>Joins a free slot, starting on the first character nobody has locked.</summary>
        public bool Join(int slot)
        {
            if (states[slot] != LobbySlotState.Free)
                return false;

            states[slot] = LobbySlotState.Choosing;
            characters[slot] = FindAvailable(slot, 0, 1);
            joinOrder.Add(slot);
            return true;
        }

        /// <summary>Frees the slot; the host role passes to the next player by join order.</summary>
        public bool Leave(int slot)
        {
            if (states[slot] == LobbySlotState.Free)
                return false;

            states[slot] = LobbySlotState.Free;
            characters[slot] = NoCharacter;
            joinOrder.Remove(slot);
            return true;
        }

        /// <summary>Moves to the next (direction &gt; 0) or previous character, skipping those locked by others.</summary>
        public bool Cycle(int slot, int direction)
        {
            if (direction == 0)
                throw new ArgumentException("Direction must be positive or negative.", nameof(direction));
            if (states[slot] != LobbySlotState.Choosing)
                return false;

            int step = Math.Sign(direction);
            characters[slot] = FindAvailable(slot, characters[slot] + step, step);
            return true;
        }

        /// <summary>
        /// Locks the browsed character in. Anyone else browsing it moves on to
        /// the next free one, so two players can never lock the same character.
        /// </summary>
        public bool Confirm(int slot)
        {
            if (states[slot] != LobbySlotState.Choosing)
                return false;

            states[slot] = LobbySlotState.Locked;

            for (int other = 0; other < SlotCount; other++)
            {
                if (states[other] == LobbySlotState.Choosing && characters[other] == characters[slot])
                    characters[other] = FindAvailable(other, characters[other] + 1, 1);
            }

            return true;
        }

        /// <summary>Back from locked to choosing; the character becomes free for others again.</summary>
        public bool Unlock(int slot)
        {
            if (states[slot] != LobbySlotState.Locked)
                return false;

            states[slot] = LobbySlotState.Choosing;
            return true;
        }

        public void Clear()
        {
            Array.Clear(states, 0, states.Length);
            for (int slot = 0; slot < SlotCount; slot++)
                characters[slot] = NoCharacter;
            joinOrder.Clear();
        }

        private int FindAvailable(int slot, int from, int step)
        {
            for (int i = 0; i < CharacterCount; i++)
            {
                int character = Wrap(from + i * step);
                if (!IsLockedByOther(character, slot))
                    return character;
            }

            // Unreachable: there are at least as many characters as slots.
            throw new InvalidOperationException("Every character is locked by another player.");
        }

        private bool IsLockedByOther(int character, int slot)
        {
            for (int other = 0; other < SlotCount; other++)
            {
                if (other != slot && states[other] == LobbySlotState.Locked && characters[other] == character)
                    return true;
            }

            return false;
        }

        private int Wrap(int character) => ((character % CharacterCount) + CharacterCount) % CharacterCount;
    }
}
