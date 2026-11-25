using System;

namespace CodenameVelvet.Inventory
{
    /// <summary>How much of one resource something costs.</summary>
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType type;
        public int amount;

        public ResourceAmount(ResourceType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }
}
