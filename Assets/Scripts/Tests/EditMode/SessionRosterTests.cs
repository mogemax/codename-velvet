using System;
using System.Linq;
using CodenameVelvet.Inventory;
using CodenameVelvet.Multiplayer;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class SessionRosterTests
    {
        private const string Gamepad = "Gamepad";
        private const string Keyboard = "Keyboard&Mouse";

        [Test]
        public void Carried_KeepsStacksPerSlot_UntilCleared()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 6);
            var roster = new SessionRoster { ClockSeconds = 90f };
            roster.SetCarried(2, inventory.Slots);

            Assert.That(roster.GetCarried(2).Count, Is.EqualTo(1));
            Assert.That(roster.GetCarried(2)[0].Count, Is.EqualTo(6));
            Assert.That(roster.GetCarried(0), Is.Empty);

            roster.Clear();
            Assert.That(roster.GetCarried(2), Is.Empty);
            Assert.That(roster.ClockSeconds, Is.Zero);
        }

        [Test]
        public void Armed_IsKeptPerSlot_UntilCleared()
        {
            var roster = new SessionRoster();
            roster.SetArmed(0, true); // no entry: the test player

            Assert.That(roster.IsArmed(0), Is.True);
            Assert.That(roster.IsArmed(1), Is.False);

            roster.Clear();
            Assert.That(roster.IsArmed(0), Is.False);
        }

        [Test]
        public void Bow_IsKeptPerSlot_ApartFromTheSword_UntilCleared()
        {
            var roster = new SessionRoster();
            roster.SetHasBow(1, true);

            Assert.That(roster.HasBow(1), Is.True);
            Assert.That(roster.HasBow(0), Is.False);
            Assert.That(roster.IsArmed(1), Is.False);

            roster.Clear();
            Assert.That(roster.HasBow(1), Is.False);
        }

        private static int AddDevice(SessionRoster roster, int deviceId)
        {
            Assert.That(roster.TryAdd(new[] { deviceId }, Gamepad, out int slot), Is.True);
            return slot;
        }

        [Test]
        public void NewRoster_IsEmptyUnlockedWithFourSlots()
        {
            var roster = new SessionRoster();

            Assert.That(roster.Count, Is.EqualTo(0));
            Assert.That(roster.IsLocked, Is.False);
            Assert.That(roster.MaxPlayers, Is.EqualTo(4));
            Assert.That(roster.Entries, Is.Empty);
        }

        [Test]
        public void TryAdd_AssignsSlotsInJoinOrder()
        {
            var roster = new SessionRoster();

            Assert.That(AddDevice(roster, 10), Is.EqualTo(0));
            Assert.That(AddDevice(roster, 11), Is.EqualTo(1));
            Assert.That(AddDevice(roster, 12), Is.EqualTo(2));
            Assert.That(roster.Count, Is.EqualTo(3));
        }

        [Test]
        public void TryAdd_AfterALeave_ReusesTheLowestFreeSlot()
        {
            var roster = new SessionRoster();
            AddDevice(roster, 10);
            AddDevice(roster, 11);
            AddDevice(roster, 12);

            roster.Remove(1);

            Assert.That(AddDevice(roster, 13), Is.EqualTo(1));
            Assert.That(roster.Entries.Select(e => e.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void TryAdd_SameDeviceTwice_Fails()
        {
            var roster = new SessionRoster();
            AddDevice(roster, 10);

            bool added = roster.TryAdd(new[] { 10 }, Gamepad, out int slot);

            Assert.That(added, Is.False);
            Assert.That(slot, Is.EqualTo(-1));
            Assert.That(roster.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryAdd_DeviceSharedWithAnotherPlayer_Fails()
        {
            var roster = new SessionRoster();
            Assert.That(roster.TryAdd(new[] { 1, 2 }, Keyboard, out _), Is.True);

            Assert.That(roster.TryAdd(new[] { 2 }, Keyboard, out _), Is.False);
        }

        [Test]
        public void TryAdd_WhenFull_Fails()
        {
            var roster = new SessionRoster();
            for (int device = 0; device < roster.MaxPlayers; device++)
                AddDevice(roster, device);

            Assert.That(roster.IsFull, Is.True);
            Assert.That(roster.TryAdd(new[] { 99 }, Gamepad, out _), Is.False);
        }

        [Test]
        public void TryAdd_WhenLocked_Fails()
        {
            var roster = new SessionRoster();
            AddDevice(roster, 10);
            roster.Lock();

            Assert.That(roster.TryAdd(new[] { 11 }, Gamepad, out _), Is.False);
        }

        [Test]
        public void TryAdd_WithoutDevices_Throws()
        {
            var roster = new SessionRoster();

            Assert.Throws<ArgumentException>(() => roster.TryAdd(Array.Empty<int>(), Gamepad, out _));
            Assert.Throws<ArgumentException>(() => roster.TryAdd(null, Gamepad, out _));
        }

        [Test]
        public void Remove_WhileLocked_KeepsOtherSlotsStable()
        {
            var roster = new SessionRoster();
            AddDevice(roster, 10);
            AddDevice(roster, 11);
            AddDevice(roster, 12);
            roster.Lock();

            Assert.That(roster.Remove(0), Is.True);

            Assert.That(roster.Entries.Select(e => e.SlotIndex), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(roster.IsLocked, Is.True);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(7)]
        public void Remove_EmptyOrInvalidSlot_ReturnsFalse(int slot)
        {
            var roster = new SessionRoster();
            AddDevice(roster, 10);

            Assert.That(roster.Remove(slot), Is.False);
            Assert.That(roster.Count, Is.EqualTo(1));
        }

        [Test]
        public void Clear_EmptiesAndUnlocks()
        {
            var roster = new SessionRoster();
            AddDevice(roster, 10);
            roster.Lock();

            roster.Clear();

            Assert.That(roster.Count, Is.EqualTo(0));
            Assert.That(roster.IsLocked, Is.False);
            Assert.That(AddDevice(roster, 10), Is.EqualTo(0));
        }

        [Test]
        public void AssignCharacter_StoresThePickOnTheEntry()
        {
            var roster = new SessionRoster();
            int slot = AddDevice(roster, 10);

            Assert.That(roster.TryGetEntry(slot, out RosterEntry entry), Is.True);
            Assert.That(entry.CharacterIndex, Is.EqualTo(-1));

            Assert.That(roster.AssignCharacter(slot, 2), Is.True);
            Assert.That(entry.CharacterIndex, Is.EqualTo(2));
        }

        [Test]
        public void AssignCharacter_EmptySlotOrLockedRoster_Fails()
        {
            var roster = new SessionRoster();
            int slot = AddDevice(roster, 10);

            Assert.That(roster.AssignCharacter(3, 0), Is.False);

            roster.Lock();
            Assert.That(roster.AssignCharacter(slot, 1), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => roster.AssignCharacter(slot, -1));
        }

        [Test]
        public void Entry_KeepsDevicesAndControlScheme()
        {
            var roster = new SessionRoster();
            int[] devices = { 1, 2 };
            roster.TryAdd(devices, Keyboard, out int slot);

            devices[0] = 99; // later changes to the caller's array must not leak in

            Assert.That(roster.TryGetEntry(slot, out RosterEntry entry), Is.True);
            Assert.That(entry.DeviceIds, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(entry.ControlScheme, Is.EqualTo(Keyboard));
            Assert.That(roster.ContainsDevice(2), Is.True);
            Assert.That(roster.ContainsDevice(99), Is.False);
        }
    }
}
