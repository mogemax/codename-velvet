using System;

namespace CodenameVelvet.Combat
{
    /// <summary>One hit that landed: how much it took and whether it was the last one (death, depletion).</summary>
    public readonly struct DamageEvent
    {
        public int Amount { get; }
        public bool IsFatal { get; }

        public DamageEvent(int amount, bool isFatal)
        {
            Amount = amount;
            IsFatal = isFatal;
        }
    }

    /// <summary>
    /// Announces every hit that landed (blocked hits are not announced). Presentation (flash, particles, rumble,
    /// damage numbers) listens here, so gameplay code never knows about it.
    /// </summary>
    public interface IDamageEvents
    {
        event Action<DamageEvent> Damaged;
    }
}
