using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CodenameVelvet.Inventory
{
    /// <summary>
    /// Fixed-size resource inventory: each slot holds one <see cref="ResourceType"/>
    /// up to <see cref="StackCap"/>. Adding tops up existing stacks of that type first,
    /// then opens empty slots; whatever does not fit is not added.
    /// Non-positive amounts are a caller bug and throw <see cref="ArgumentOutOfRangeException"/>;
    /// "Try" only refers to running out of space or resources.
    /// </summary>
    public class ResourceInventory
    {
        /// <summary>One slot's contents. An empty slot has <see cref="Count"/> 0 and a meaningless <see cref="Type"/>.</summary>
        public readonly struct Slot
        {
            public ResourceType Type { get; }
            public int Count { get; }
            public bool IsEmpty => Count == 0;

            public Slot(ResourceType type, int count)
            {
                Type = type;
                Count = count;
            }
        }

        private readonly Slot[] slots;

        public int StackCap { get; }

        /// <summary>Read-only live view of every slot, empty ones included.</summary>
        public IReadOnlyList<Slot> Slots { get; }

        public ResourceInventory(int slotCount = 4, int stackCap = 20)
        {
            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "Slot count must be positive.");
            if (stackCap <= 0)
                throw new ArgumentOutOfRangeException(nameof(stackCap), stackCap, "Stack cap must be positive.");

            slots = new Slot[slotCount];
            StackCap = stackCap;
            Slots = new ReadOnlyCollection<Slot>(slots);
        }

        /// <summary>Adds as much of <paramref name="amount"/> as fits and returns how much was added (0..amount).</summary>
        public int TryAdd(ResourceType type, int amount)
        {
            RequirePositive(amount);

            int remaining = amount;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Type == type)
                    remaining = FillSlot(i, type, remaining);
            }

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                    remaining = FillSlot(i, type, remaining);
            }

            return amount - remaining;
        }

        /// <summary>
        /// Removes exactly <paramref name="amount"/> of <paramref name="type"/> across stacks,
        /// or nothing at all if there is not enough. Drains later slots first.
        /// </summary>
        public bool TryRemove(ResourceType type, int amount)
        {
            RequirePositive(amount);

            if (CountOf(type) < amount)
                return false;

            int remaining = amount;
            for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                if (slots[i].IsEmpty || slots[i].Type != type)
                    continue;

                int taken = Math.Min(slots[i].Count, remaining);
                slots[i] = new Slot(type, slots[i].Count - taken);
                remaining -= taken;
            }

            return true;
        }

        /// <summary>Total of <paramref name="type"/> across all slots.</summary>
        public int CountOf(ResourceType type)
        {
            int total = 0;
            foreach (Slot slot in slots)
            {
                if (!slot.IsEmpty && slot.Type == type)
                    total += slot.Count;
            }
            return total;
        }

        /// <summary>Empties every slot and returns the non-empty stacks it held (e.g. for a death drop).</summary>
        public List<Slot> Drain()
        {
            var drained = new List<Slot>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                    drained.Add(slots[i]);
                slots[i] = default;
            }
            return drained;
        }

        /// <summary>Puts up to the stack cap into slot <paramref name="index"/>; returns what is left over.</summary>
        private int FillSlot(int index, ResourceType type, int remaining)
        {
            int added = Math.Min(StackCap - slots[index].Count, remaining);
            slots[index] = new Slot(type, slots[index].Count + added);
            return remaining - added;
        }

        private static void RequirePositive(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");
        }
    }
}
