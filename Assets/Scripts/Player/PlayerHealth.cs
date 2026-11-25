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
    /// A downed player stays down until they choose to come back (<see cref="Respawn"/>, from their death screen),
    /// which they can do once the wait is over (<see cref="RespawnSecondsLeft"/>). Someone else can instead bring
    /// them back on a timer (<see cref="RespawnIn"/>, e.g. a boss fight starting over). Either way they reappear at
    /// <see cref="RespawnPoint"/> with full health.
    /// Rule: every reference here is to this GameObject's own components.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable, IDamageEvents
    {
        private const string LogCategory = "Player";

        [SerializeField] private int maxHp = 3;
        [Tooltip("Seconds from dying to the scene-wide death event (time for the death to be seen).")]
        [SerializeField] private float deathDelaySeconds = 2f;
        [Tooltip("Seconds a downed player must wait before they can come back.")]
        [SerializeField, Min(0f)] private float respawnCooldownSeconds = 10f;
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
        private float respawnAt;
        private bool autoRespawn;
        private Coroutine deathSequence;
        private int hitFrame = -1;

        /// <summary>
        /// Raised once per death, at the start of the death sequence while the player
        /// is still at the death spot (before hiding and respawn).
        /// </summary>
        public event Action Died;

        public event Action<DamageEvent> Damaged;

        /// <summary>HP was restored (food): how much.</summary>
        public event Action<int> Healed;

        /// <summary>Came back after dying, at <see cref="RespawnPoint"/>, with full health.</summary>
        public event Action Respawned;

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

        /// <summary>Seconds down before the player can come back instead of the usual wait (boss fight). Read at death; null = usual.</summary>
        public float? DeathDelayOverride { get; set; }

        /// <summary>Seconds until the player can come back (or is brought back, see <see cref="RespawnIn"/>); 0 while alive.</summary>
        public float RespawnSecondsLeft => isDying ? Mathf.Max(0f, respawnAt - Time.time) : 0f;

        /// <summary>Down, the wait is over and nobody else is bringing them back: they may come back now.</summary>
        public bool CanRespawn => isDying && !autoRespawn && RespawnSecondsLeft <= 0f;

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
                deathSequence = StartCoroutine(DeathSequence());
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

        /// <summary>
        /// While down, brings the player back by itself this many seconds from now, without waiting for them to
        /// choose (e.g. everyone coming back together when a boss fight starts over).
        /// </summary>
        public void RespawnIn(float seconds)
        {
            if (!isDying) return;
            respawnAt = Time.time + seconds;
            autoRespawn = true;
        }

        /// <summary>The player chose to come back (their death screen). False while they still have to wait.</summary>
        public bool Respawn()
        {
            if (!CanRespawn)
                return false;
            Revive();
            return true;
        }

        private IEnumerator DeathSequence()
        {
            isDying = true;
            autoRespawn = false;
            float wait = DeathDelayOverride ?? respawnCooldownSeconds;
            respawnAt = Time.time + wait;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Died, can come back in {wait}s");

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

            // Down until they choose to come back (Respawn) or someone brings them back (RespawnIn).
            // Not WaitForSeconds: RespawnIn can change the time meanwhile.
            while (!autoRespawn || Time.time < respawnAt)
                yield return null;
            deathSequence = null;
            Revive();
        }

        private void Revive()
        {
            if (deathSequence != null)
            {
                StopCoroutine(deathSequence);
                deathSequence = null;
            }

            // With interpolation the Rigidbody ignores Transform-only moves, so it is moved too.
            transform.position = RespawnPoint;
            if (body != null) body.position = RespawnPoint;
            StopBody();
            SetVisible(true);
            healthState.ResetToFull();
            recoverUntil = 0f;
            isDying = false;
            autoRespawn = false;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Respawned at {RespawnPoint}, HP {healthState.CurrentHp}/{healthState.MaxHp}");

            if (movement != null)
                movement.SetControlEnabled(true);
            Respawned?.Invoke();
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
