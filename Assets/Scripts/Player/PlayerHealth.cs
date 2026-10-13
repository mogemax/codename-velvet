using System.Collections;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Events;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Per-player HP, death and respawn. Composes <see cref="PlayerHealthState"/>
    /// for the math; this class only decides what dying means in the scene.
    /// Death is announced outward only through <see cref="PlayerDiedEvent"/>.
    /// Rule: every reference here is to this GameObject's own components.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHp = 3;
        [SerializeField] private float deathDelaySeconds = 2f;
        [SerializeField] private PlayerDiedEvent onPlayerDied;

        [Tooltip("Child hidden while dead. If empty, child Renderers are toggled instead.")]
        [SerializeField] private GameObject spriteRoot;

        private PlayerHealthState healthState;
        private PlayerIdentity identity;
        private PlayerMovement movement;
        private Rigidbody body;
        private Renderer[] hiddenRenderers;
        private Vector3 spawnPoint;
        private bool isDying;

        public int CurrentHp => healthState.CurrentHp;
        public int MaxHp => healthState.MaxHp;
        public bool IsAlive => !healthState.IsDead && !isDying;

        private void Awake()
        {
            healthState = new PlayerHealthState(maxHp);
            identity = GetComponent<PlayerIdentity>();
            movement = GetComponent<PlayerMovement>();
            body = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            spawnPoint = transform.position;
        }

        public void ApplyDamage(int amount)
        {
            if (!IsAlive)
                return;

            healthState.ApplyDamage(amount);

            if (healthState.IsDead)
                StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            isDying = true;

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

            transform.position = spawnPoint;
            StopBody();
            SetVisible(true);
            healthState.ResetToFull();
            isDying = false;

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
