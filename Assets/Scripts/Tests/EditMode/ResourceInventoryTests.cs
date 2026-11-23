using System;
using CodenameVelvet.Inventory;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class ResourceInventoryTests
    {
        [Test]
        public void NewInventory_HasFourEmptySlots()
        {
            var inventory = new ResourceInventory();

            Assert.That(inventory.Slots.Count, Is.EqualTo(4));
            Assert.That(inventory.StackCap, Is.EqualTo(20));
            foreach (ResourceInventory.Slot slot in inventory.Slots)
                Assert.That(slot.IsEmpty, Is.True);
        }

        [Test]
        public void TryAdd_SameType_FillsExistingStackBeforeNewSlot()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 5);

            int added = inventory.TryAdd(ResourceType.Wood, 7);

            Assert.That(added, Is.EqualTo(7));
            Assert.That(inventory.Slots[0].Count, Is.EqualTo(12));
            Assert.That(inventory.Slots[1].IsEmpty, Is.True);
        }

        [Test]
        public void TryAdd_DifferentType_UsesNewSlot()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 5);

            inventory.TryAdd(ResourceType.Stone, 3);

            Assert.That(inventory.Slots[0].Type, Is.EqualTo(ResourceType.Wood));
            Assert.That(inventory.Slots[1].Type, Is.EqualTo(ResourceType.Stone));
            Assert.That(inventory.Slots[1].Count, Is.EqualTo(3));
        }

        [Test]
        public void TryAdd_OverStackCap_SplitsAcrossStacks()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 15);

            int added = inventory.TryAdd(ResourceType.Wood, 30);

            Assert.That(added, Is.EqualTo(30));
            Assert.That(inventory.Slots[0].Count, Is.EqualTo(20));
            Assert.That(inventory.Slots[1].Count, Is.EqualTo(20));
            Assert.That(inventory.Slots[2].Count, Is.EqualTo(5));
            Assert.That(inventory.CountOf(ResourceType.Wood), Is.EqualTo(45));
        }

        [Test]
        public void TryAdd_TopsUpPartialStackEvenWhenItIsNotFirst()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Stone, 4);
            inventory.TryAdd(ResourceType.Wood, 18);

            inventory.TryAdd(ResourceType.Wood, 2);

            Assert.That(inventory.Slots[1].Count, Is.EqualTo(20));
            Assert.That(inventory.Slots[2].IsEmpty, Is.True);
        }

        [Test]
        public void TryAdd_NotEnoughSpace_AddsOnlyWhatFits()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 75);

            int added = inventory.TryAdd(ResourceType.Wood, 10);

            Assert.That(added, Is.EqualTo(5));
            Assert.That(inventory.CountOf(ResourceType.Wood), Is.EqualTo(80));
        }

        [Test]
        public void TryAdd_FullInventory_AddsNothing()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 80);

            Assert.That(inventory.TryAdd(ResourceType.Wood, 1), Is.EqualTo(0));
            Assert.That(inventory.TryAdd(ResourceType.Stone, 1), Is.EqualTo(0));
        }

        [Test]
        public void TryAdd_NoEmptySlot_OnlyTopsUpExistingStacks()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 1);
            inventory.TryAdd(ResourceType.Stone, 1);
            inventory.TryAdd(ResourceType.Mineral, 21); // Takes two slots.

            Assert.That(inventory.TryAdd(ResourceType.Wood, 5), Is.EqualTo(5));
            Assert.That(inventory.TryAdd(ResourceType.Stone, 25), Is.EqualTo(19));
            Assert.That(inventory.CountOf(ResourceType.Stone), Is.EqualTo(20));
        }

        [Test]
        public void TryAdd_EverySlotHoldsAnotherType_RejectsNewType()
        {
            var inventory = new ResourceInventory(2, 20);
            inventory.TryAdd(ResourceType.Wood, 1);
            inventory.TryAdd(ResourceType.Stone, 1);

            Assert.That(inventory.TryAdd(ResourceType.Mineral, 5), Is.EqualTo(0));
            Assert.That(inventory.CountOf(ResourceType.Mineral), Is.EqualTo(0));
        }

        [Test]
        public void TryAdd_NonPositiveAmount_Throws()
        {
            var inventory = new ResourceInventory();

            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryAdd(ResourceType.Wood, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryAdd(ResourceType.Wood, -3));
        }

        [Test]
        public void TryRemove_EnoughAcrossStacks_RemovesExactAmount()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 25);
            inventory.TryAdd(ResourceType.Stone, 4);

            bool removed = inventory.TryRemove(ResourceType.Wood, 22);

            Assert.That(removed, Is.True);
            Assert.That(inventory.CountOf(ResourceType.Wood), Is.EqualTo(3));
            Assert.That(inventory.CountOf(ResourceType.Stone), Is.EqualTo(4));
        }

        [Test]
        public void TryRemove_NotEnough_RemovesNothing()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 25);

            bool removed = inventory.TryRemove(ResourceType.Wood, 26);

            Assert.That(removed, Is.False);
            Assert.That(inventory.CountOf(ResourceType.Wood), Is.EqualTo(25));
        }

        [Test]
        public void TryRemove_EmptiedSlot_IsReusableByAnotherType()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 80);

            inventory.TryRemove(ResourceType.Wood, 20);

            Assert.That(inventory.TryAdd(ResourceType.Mineral, 3), Is.EqualTo(3));
        }

        [Test]
        public void TryRemove_NonPositiveAmount_Throws()
        {
            var inventory = new ResourceInventory();

            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryRemove(ResourceType.Wood, 0));
        }

        [Test]
        public void CountOf_MissingType_IsZero()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 5);

            Assert.That(inventory.CountOf(ResourceType.Mineral), Is.EqualTo(0));
        }

        [Test]
        public void Drain_ReturnsNonEmptyStacksAndEmptiesInventory()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 25);
            inventory.TryAdd(ResourceType.Stone, 3);

            var drained = inventory.Drain();

            Assert.That(drained.Count, Is.EqualTo(3));
            Assert.That(drained[0].Count + drained[1].Count, Is.EqualTo(25));
            Assert.That(drained[2].Type, Is.EqualTo(ResourceType.Stone));
            Assert.That(drained[2].Count, Is.EqualTo(3));
            Assert.That(inventory.CountOf(ResourceType.Wood), Is.EqualTo(0));
            foreach (ResourceInventory.Slot slot in inventory.Slots)
                Assert.That(slot.IsEmpty, Is.True);
        }

        [Test]
        public void Constructor_NonPositiveSizes_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceInventory(0, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceInventory(4, 0));
        }
    }
}
