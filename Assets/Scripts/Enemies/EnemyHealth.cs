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
    /// <see cref="Died"/> once, stops colliding and destroys the GameObject a moment later (so its death can be
    /// seen; the brains stop themselves on <see cref="Died"/>); damage before
    /// <see cref="Initialize"/> or after death is ignored.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable, IDamageEvents
    {
        private const string LogCategory = "Enemy";

        [Tooltip("Seconds the body stays after dying (for the death feedback) before it is destroyed.")]
        [SerializeField, Min(0f)] private float corpseSeconds = 0.25f;

        [Header("Debug")]
        [Tooltip("Logs damage taken and death.")]
        [SerializeField] private bool debugLogs = true;

        // PlayerHealthState is a generic HP state despite its name; reused to keep one copy of the math.
        private PlayerHealthState healthState;

        /// <summary>Raised once when HP reaches zero, right before the GameObject is destroyed.</summary>
        public event Action<EnemyHealth> Died;

        public event Action<DamageEvent> Damaged;

        public bool IsAlive => healthState != null && !healthState.IsDead;
        public int CurrentHp => healthState?.CurrentHp ?? 0;
        public int MaxHp => healthState?.MaxHp ?? 0;

        /// <summary>While true every hit is ignored (e.g. a boss whose fight has not started).</summary>
        public bool Invulnerable { get; set; }

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

            if (Invulnerable)
                return;

            healthState.ApplyDamage(amount);
            Damaged?.Invoke(new DamageEvent(amount, healthState.IsDead));

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Took {amount} damage, HP {healthState.CurrentHp}/{healthState.MaxHp}");

            if (healthState.IsDead)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, "Died");
                Died?.Invoke(this);
                StopColliding();
                Destroy(gameObject, corpseSeconds);
            }
        }
    
        /// <summary>The corpse no longer blocks, gets hit or falls through the ground while it fades.</summary>
        private void StopColliding()
        {
            foreach (Collider part in GetComponentsInChildren<Collider>())
                part.enabled = false;
            if (TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }
    }
}
