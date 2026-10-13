namespace CodenameVelvet.Combat
{
    /// <summary>
    /// Anything that can receive damage (players, enemies, destructibles).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>True while the target can receive damage.</summary>
        bool IsAlive { get; }

        /// <summary>Applies <paramref name="amount"/> points of damage.</summary>
        void ApplyDamage(int amount);
    }
}
