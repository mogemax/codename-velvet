using System;
using CodenameVelvet.Multiplayer;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class CharacterLobbyTests
    {
        private const int Slots = 4;
        private const int Characters = 4;

        private static CharacterLobby NewLobby() => new CharacterLobby(Slots, Characters);

        private static CharacterLobby LobbyWithLocked(int slot, int character)
        {
            var lobby = NewLobby();
            lobby.Join(slot);
            while (lobby.GetCharacter(slot) != character)
                lobby.Cycle(slot, 1);
            lobby.Confirm(slot);
            return lobby;
        }

        [Test]
        public void NewLobby_IsEmptyWithoutHost()
        {
            var lobby = NewLobby();

            Assert.That(lobby.JoinedCount, Is.EqualTo(0));
            Assert.That(lobby.HostSlot, Is.EqualTo(CharacterLobby.NoHost));
            Assert.That(lobby.CanStart, Is.False);
            Assert.That(lobby.GetState(0), Is.EqualTo(LobbySlotState.Free));
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(CharacterLobby.NoCharacter));
        }

        [Test]
        public void Constructor_FewerCharactersThanSlots_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterLobby(4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterLobby(0, 4));
        }

        [Test]
        public void Join_StartsChoosingTheFirstCharacter()
        {
            var lobby = NewLobby();

            Assert.That(lobby.Join(2), Is.True);

            Assert.That(lobby.GetState(2), Is.EqualTo(LobbySlotState.Choosing));
            Assert.That(lobby.GetCharacter(2), Is.EqualTo(0));
        }

        [Test]
        public void Join_TakenSlot_Fails()
        {
            var lobby = NewLobby();
            lobby.Join(0);

            Assert.That(lobby.Join(0), Is.False);
            Assert.That(lobby.JoinedCount, Is.EqualTo(1));
        }

        [Test]
        public void Join_SkipsCharactersLockedByOthers()
        {
            var lobby = LobbyWithLocked(0, 0);

            lobby.Join(1);

            Assert.That(lobby.GetCharacter(1), Is.EqualTo(1));
        }

        [Test]
        public void Cycle_WrapsAroundBothWays()
        {
            var lobby = NewLobby();
            lobby.Join(0);

            lobby.Cycle(0, -1);
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(3));

            lobby.Cycle(0, 1);
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(0));
        }

        [Test]
        public void Cycle_SkipsCharactersLockedByOthers()
        {
            var lobby = LobbyWithLocked(0, 1);
            lobby.Join(1); // starts on 0

            lobby.Cycle(1, 1);
            Assert.That(lobby.GetCharacter(1), Is.EqualTo(2));

            lobby.Cycle(1, -1);
            Assert.That(lobby.GetCharacter(1), Is.EqualTo(0));
        }

        [Test]
        public void Cycle_WhenEveryOtherCharacterIsLocked_StaysPut()
        {
            var lobby = LobbyWithLocked(0, 0);
            Lock(lobby, 1);
            Lock(lobby, 2);
            lobby.Join(3);

            lobby.Cycle(3, 1);

            Assert.That(lobby.GetCharacter(3), Is.EqualTo(3));
        }

        [Test]
        public void Cycle_WhileLockedOrFree_Fails()
        {
            var lobby = LobbyWithLocked(0, 0);

            Assert.That(lobby.Cycle(0, 1), Is.False);
            Assert.That(lobby.Cycle(1, 1), Is.False);
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(0));
        }

        [Test]
        public void Cycle_ZeroDirection_Throws()
        {
            var lobby = NewLobby();
            lobby.Join(0);

            Assert.Throws<ArgumentException>(() => lobby.Cycle(0, 0));
        }

        [Test]
        public void Confirm_BumpsOthersBrowsingTheSameCharacter()
        {
            var lobby = NewLobby();
            lobby.Join(0);
            lobby.Join(1); // both on character 0

            Assert.That(lobby.Confirm(0), Is.True);

            Assert.That(lobby.GetState(0), Is.EqualTo(LobbySlotState.Locked));
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(0));
            Assert.That(lobby.GetState(1), Is.EqualTo(LobbySlotState.Choosing));
            Assert.That(lobby.GetCharacter(1), Is.EqualTo(1));
        }

        [Test]
        public void Confirm_WhenNotChoosing_Fails()
        {
            var lobby = LobbyWithLocked(0, 0);

            Assert.That(lobby.Confirm(0), Is.False);
            Assert.That(lobby.Confirm(1), Is.False);
        }

        [Test]
        public void Unlock_FreesTheCharacterForOthers()
        {
            var lobby = LobbyWithLocked(0, 0);
            lobby.Join(1); // starts on 1, since 0 is locked

            Assert.That(lobby.Unlock(0), Is.True);
            lobby.Cycle(1, -1);

            Assert.That(lobby.GetState(0), Is.EqualTo(LobbySlotState.Choosing));
            Assert.That(lobby.GetCharacter(1), Is.EqualTo(0));
        }

        [Test]
        public void Unlock_WhenNotLocked_Fails()
        {
            var lobby = NewLobby();
            lobby.Join(0);

            Assert.That(lobby.Unlock(0), Is.False);
            Assert.That(lobby.Unlock(1), Is.False);
        }

        [Test]
        public void Leave_FreesSlotAndCharacter()
        {
            var lobby = LobbyWithLocked(0, 0);

            Assert.That(lobby.Leave(0), Is.True);
            lobby.Join(1);

            Assert.That(lobby.GetState(0), Is.EqualTo(LobbySlotState.Free));
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(CharacterLobby.NoCharacter));
            Assert.That(lobby.GetCharacter(1), Is.EqualTo(0));
            Assert.That(lobby.Leave(0), Is.False);
        }

        [Test]
        public void Host_IsTheEarliestJoinedPlayerStillPresent()
        {
            var lobby = NewLobby();
            lobby.Join(2);
            lobby.Join(0);
            lobby.Join(1);

            Assert.That(lobby.HostSlot, Is.EqualTo(2));

            lobby.Leave(2);
            Assert.That(lobby.HostSlot, Is.EqualTo(0));

            lobby.Join(2); // rejoining goes to the back of the line
            lobby.Leave(0);
            Assert.That(lobby.HostSlot, Is.EqualTo(1));

            lobby.Leave(1);
            lobby.Leave(2);
            Assert.That(lobby.HostSlot, Is.EqualTo(CharacterLobby.NoHost));
        }

        [Test]
        public void CanStart_OnlyWhenEveryJoinedPlayerIsLocked()
        {
            var lobby = LobbyWithLocked(0, 0);
            Assert.That(lobby.CanStart, Is.True);

            lobby.Join(1);
            Assert.That(lobby.CanStart, Is.False);

            lobby.Confirm(1);
            Assert.That(lobby.CanStart, Is.True);

            lobby.Unlock(0);
            Assert.That(lobby.CanStart, Is.False);

            lobby.Leave(0);
            Assert.That(lobby.CanStart, Is.True);
        }

        [Test]
        public void Clear_ResetsEverySlot()
        {
            var lobby = LobbyWithLocked(0, 2);
            lobby.Join(1);

            lobby.Clear();

            Assert.That(lobby.JoinedCount, Is.EqualTo(0));
            Assert.That(lobby.HostSlot, Is.EqualTo(CharacterLobby.NoHost));
            Assert.That(lobby.GetState(0), Is.EqualTo(LobbySlotState.Free));
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(CharacterLobby.NoCharacter));
            Assert.That(lobby.Join(0), Is.True);
            Assert.That(lobby.GetCharacter(0), Is.EqualTo(0));
        }

        private static void Lock(CharacterLobby lobby, int slot)
        {
            lobby.Join(slot);
            lobby.Confirm(slot);
        }
    }
}
