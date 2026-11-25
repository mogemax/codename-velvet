using System;
using System.Collections;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Core.Events;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Per-player HP, death and respawn. Composes <see cref="PlayerHealthState"/>
    /// for the math; this class only decides what dying means in the scene.
    /// Death is announced scene-wide through <see cref="PlayerDiedEvent"/> (after the delay)
    /// and to this player's own components through <see cref="Died"/> (immediately).
    /// Rule: every reference here is to this GameObject's own components.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable, IDamageEvents
    {
        private const string LogCategory = "Player";

        [SerializeField] private int maxHp = 3;
        [SerializeField] private float deathDelaySeconds = 2f;
        [Tooltip("After a hit, every other hit is ignored this long (so several enemies can't land a combo).")]
        [SerializeField, Min(0f)] private float hitInvulnerabilitySeconds = 0.6f;
        [SerializeField] private PlayerDiedEvent onPlayerDied;

        [Tooltip("Child hidden while dead. If empty, child Renderers are toggled instead.")]
        [SerializeField] private GameObject spriteRoot;

        [Header("Debug")]
        [Tooltip("Logs damage taken, death and respawn.")]
        [SerializeField] private bool debugLogs = true;

        private PlayerHealthState healthState;
        private PlayerIdentity identity;
        private PlayerMovement movement;
        private Rigidbody body;
        private Renderer[] hiddenRenderers;
        private bool isDying;
        private float recoverUntil;
        private int hitFrame = -1;

        /// <summary>
        /// Raised once per death, at the start of the death sequence while the player
        /// is still at the death spot (before hiding and respawn).
        /// </summary>
        public event Action Died;

        public event Action<DamageEvent> Damaged;

        /// <summary>HP was restored (food): how much.</summary>
        public event Action<int> Healed;

        public int CurrentHp => healthState.CurrentHp;
        public int MaxHp => healthState.MaxHp;
        public bool IsAlive => !healthState.IsDead && !isDying;

        /// <summary>While true every hit is ignored (dev mode, temporary invulnerability).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Still in the short invulnerability after a hit (separate from <see cref="Invulnerable"/>).</summary>
        public bool IsRecovering => Time.time < recoverUntil;

        /// <summary>True on the frame a hit landed, so the same attack can still push the player.</summary>
        public bool WasHitThisFrame => hitFrame == Time.frameCount;

        /// <summary>Where the player comes back after dying: its start position unless changed (e.g. by a boss fight).</summary>
        public Vector3 RespawnPoint { get; set; }

        private void Awake()
        {
            healthState = new PlayerHealthState(maxHp);
            identity = GetComponent<PlayerIdentity>();
            movement = GetComponent<PlayerMovement>();
            body = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            RespawnPoint = transform.position;
        }

        public void ApplyDamage(int amount)
        {
            if (!IsAlive || Invulnerable || IsRecovering)
                return;

            healthState.ApplyDamage(amount);
            recoverUntil = Time.time + hitInvulnerabilitySeconds;
            hitFrame = Time.frameCount;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Took {amount} damage, HP {healthState.CurrentHp}/{healthState.MaxHp}");

            Damaged?.Invoke(new DamageEvent(amount, healthState.IsDead));

            if (healthState.IsDead)
                StartCoroutine(DeathSequence());
        }

        /// <summary>Restores up to <paramref name="amount"/> HP (never above max, never while dead) and returns how much.</summary>
        public int Heal(int amount)
        {
            if (!IsAlive)
                return 0;

            int healed = healthState.Heal(amount);
            if (healed > 0)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, $"Healed {healed}, HP {healthState.CurrentHp}/{healthState.MaxHp}");
                Healed?.Invoke(healed);
            }
            return healed;
        }

        private IEnumerator DeathSequence()
        {
            isDying = true;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Died, respawning in {deathDelaySeconds}s");

            Died?.Invoke();

            if (movement != null)
            {
                movement.CancelDash();
                movement.SetControlEnabled(false);
            }
            StopBody();
            SetVisible(false);

            yield return new WaitForSeconds(deathDelaySeconds);

            if (onPlayerDied != null)
                onPlayerDied.Raise(SlotIndexOrDefault());

            // With interpolation the Rigidbody ignores Transform-only moves, so it is moved too.
            transform.position = RespawnPoint;
            if (body != null) body.position = RespawnPoint;
            StopBody();
            SetVisible(true);
            healthState.ResetToFull();
            recoverUntil = 0f;
            isDying = false;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Respawned at {RespawnPoint}, HP {healthState.CurrentHp}/{healthState.MaxHp}");

            if (movement != null)
                movement.SetControlEnabled(true);
        }

        /// <summary>Single-player test scenes have no join flow, so fall back to slot 0.</summary>
        private int SlotIndexOrDefault()
        {
            return identity != null && identity.IsInitialized ? identity.SlotIndex : 0;
        }

        private void StopBody()
        {
            if (body != null)
                body.linearVelocity = Vector3.zero;
        }

        private void SetVisible(bool isVisible)
        {
            if (spriteRoot != null)
            {
                spriteRoot.SetActive(isVisible);
                return;
            }

            if (!isVisible)
                hiddenRenderers = GetComponentsInChildren<Renderer>();

            if (hiddenRenderers == null)
                return;

            foreach (Renderer hidden in hiddenRenderers)
            {
                if (hidden != null)
                    hidden.enabled = isVisible;
            }
        }
    }
}
