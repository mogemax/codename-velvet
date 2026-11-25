using System;
using System.Collections.Generic;
using System.Linq;

namespace CodenameVelvet.Inventory
{
    /// <summary>
    /// What has been delivered of a cost. Takes only what is still missing, in as many deliveries (and from as many
    /// inventories) as it needs; anything else stays in the inventory.
    /// </summary>
    public class BuildProgress
    {
        private readonly ResourceAmount[] cost;
        private readonly int[] delivered;
        private readonly int total;

        public BuildProgress(IEnumerable<ResourceAmount> cost)
        {
            this.cost = cost.ToArray();
            foreach (ResourceAmount item in this.cost)
                if (item.amount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(cost), item.amount, $"The cost in {item.type} must be positive.");

            delivered = new int[this.cost.Length];
            total = this.cost.Sum(item => item.amount);
        }

        public bool IsComplete => delivered.Sum() == total;

        /// <summary>Takes everything still missing that the inventory has. False when it took nothing.</summary>
        public bool TakeFrom(ResourceInventory inventory)
        {
            bool took = false;
            for (int i = 0; i < cost.Length; i++)
            {
                int amount = Math.Min(cost[i].amount - delivered[i], inventory.CountOf(cost[i].type));
                if (amount <= 0 || !inventory.TryRemove(cost[i].type, amount)) continue;
                delivered[i] += amount;
                took = true;
            }
            return took;
        }

        /// <summary>How many of <paramref name="steps"/> equal construction steps are done.</summary>
        public int StepsDone(int steps) => total == 0 ? steps : steps * delivered.Sum() / total;

        /// <summary>"Wood 5/24   Stone 0/12", in the order of the cost, three per line so it fits a split-screen view.</summary>
        public override string ToString() =>
            string.Concat(cost.Select((item, i) =>
                (i == 0 ? "" : i % 3 == 0 ? "\n" : "   ") + $"{item.type} {delivered[i]}/{item.amount}"));
    }
}
