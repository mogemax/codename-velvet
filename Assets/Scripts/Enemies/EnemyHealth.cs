using System;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Enemy HP and death. Max HP comes from the <see cref="EnemyDefinition"/> through
    /// <see cref="Initialize"/> (called by <see cref="EnemyBrain"/>). On death it raises
    /// <see cref="Died"/> once and destroys the GameObject; damage before
    /// <see cref="Initialize"/> or after death is ignored.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        private const string LogCategory = "Enemy";

        [Header("Debug")]
        [Tooltip("Logs damage taken and death.")]
        [SerializeField] private bool debugLogs = true;

        // PlayerHealthState is a generic HP state despite its name; reused to keep one copy of the math.
        private PlayerHealthState healthState;

        /// <summary>Raised once when HP reaches zero, right before the GameObject is destroyed.</summary>
        public event Action<EnemyHealth> Died;

        public bool IsAlive => healthState != null && !healthState.IsDead;
        public int CurrentHp => healthState?.CurrentHp ?? 0;
        public int MaxHp => healthState?.MaxHp ?? 0;

        /// <summary>Sets max HP and refills to it. Values below 1 are clamped to 1.</summary>
        public void Initialize(int maxHp)
        {
            healthState = new PlayerHealthState(Mathf.Max(1, maxHp));
        }

        public void ApplyDamage(int amount)
        {
            if (!IsAlive)
            {
                if (debugLogs && healthState == null)
                    VelvetLog.Warn(LogCategory, this, $"Ignored {amount} damage: not initialized (no EnemyDefinition applied yet)");
                return;
            }

            healthState.ApplyDamage(amount);

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Took {amount} damage, HP {healthState.CurrentHp}/{healthState.MaxHp}");

            if (healthState.IsDead)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, "Died");
                Died?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
