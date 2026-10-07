using System;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Plain HP state for one player. Owns only the math (damage, death, reset);
    /// the MonoBehaviour wrapper decides what dying means for the scene.
    /// </summary>
    public class PlayerHealthState
    {
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public bool IsDead => CurrentHp <= 0;

        public PlayerHealthState(int maxHp)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "Max HP must be positive.");

            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        /// <summary>Non-positive amounts are ignored, so damage can never heal.</summary>
        public void ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDead)
                return;

            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        public void ResetToFull()
        {
            CurrentHp = MaxHp;
        }
    }
}
