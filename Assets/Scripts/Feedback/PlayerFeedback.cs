using CodenameVelvet.Cameras;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// What this player feels for their own actions, on their own camera and gamepad:
    /// <list type="bullet">
    /// <item>Swing: a hit on an enemy freezes the swing for an instant (local hit stop), rumbles and shakes a little;
    /// a hit on a resource node, a lighter version; a miss, a stretch of the body.</item>
    /// <item>Pickup: a small flash, squash and sparkle; eating food, a green glow.</item>
    /// <item>Dust: a puff when dashing and small puffs while running.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerFeedback : MonoBehaviour
    {
        [Header("Hit on an enemy")]
        [SerializeField, Min(0f)] private float enemyHitStop = 0.06f;
        [SerializeField] private Vector2 enemyHitRumble = new Vector2(0.25f, 0.5f);
        [SerializeField, Min(0f)] private float enemyHitRumbleSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float enemyHitShake = 0.15f;

        [Header("Hit on a resource")]
        [SerializeField, Min(0f)] private float resourceHitStop = 0.035f;
        [SerializeField] private Vector2 resourceHitRumble = new Vector2(0.12f, 0.3f);
        [SerializeField, Min(0f)] private float resourceHitRumbleSeconds = 0.07f;
        [SerializeField, Range(0f, 1f)] private float resourceHitShake = 0.06f;

        [Header("Swing")]
        [Tooltip("Body deformation on a hit (x = width, y = height).")]
        [SerializeField] private Vector2 hitSquash = new Vector2(0.12f, -0.1f);
        [Tooltip("Body deformation on a miss: a lunge.")]
        [SerializeField] private Vector2 missStretch = new Vector2(0.16f, -0.08f);

        [Header("Pickup")]
        [SerializeField] private Color pickupFlash = new Color(1f, 0.95f, 0.75f, 0.45f);
        [SerializeField, Min(0f)] private float pickupFlashSeconds = 0.08f;
        [SerializeField] private Vector2 pickupSquash = new Vector2(0.08f, -0.08f);
        [SerializeField] private BurstStyle pickupBurst;
        [SerializeField] private Vector2 pickupRumble = new Vector2(0f, 0.12f);
        [SerializeField, Min(0f)] private float pickupRumbleSeconds = 0.04f;

        [Header("Food")]
        [SerializeField] private Color healFlash = new Color(0.55f, 1f, 0.5f, 0.6f);
        [SerializeField, Min(0f)] private float healFlashSeconds = 0.25f;
        [SerializeField] private BurstStyle healBurst;

        [Header("Dust")]
        [SerializeField] private BurstStyle dust;
        [Tooltip("Burst size of the dash puff relative to a step.")]
        [SerializeField, Min(0f)] private float dashDust = 2.5f;
        [Tooltip("Seconds between puffs while running.")]
        [SerializeField, Min(0.05f)] private float stepInterval = 0.3f;
        [Tooltip("Running faster than this (units/second) raises dust.")]
        [SerializeField, Min(0f)] private float stepMinSpeed = 3.5f;

        private PlayerMovement movement;
        private PlayerMeleeAttack attack;
        private PlayerInventory inventory;
        private PlayerHealth health;
        private Rigidbody body;
        private PlayerInput input;
        private PlayerRumble rumble;
        private HitStop hitStop;
        private SquashStretch squash;
        private HitFlash flash;
        private bool wasDashing;
        private float nextStepDust;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            attack = GetComponent<PlayerMeleeAttack>();
            inventory = GetComponent<PlayerInventory>();
            health = GetComponent<PlayerHealth>();
            body = GetComponent<Rigidbody>();
            input = GetComponent<PlayerInput>();
            rumble = FeedbackRig.GetOrAdd<PlayerRumble>(gameObject);
            hitStop = FeedbackRig.GetOrAdd<HitStop>(gameObject);
            squash = FeedbackRig.GetOrAdd<SquashStretch>(gameObject);
            flash = FeedbackRig.GetOrAdd<HitFlash>(gameObject);
        }

        private void OnEnable()
        {
            if (attack != null) attack.Swung += OnSwung;
            if (inventory != null) inventory.Added += OnPickedUp;
            if (health != null) health.Healed += OnHealed;
        }

        private void OnDisable()
        {
            if (attack != null) attack.Swung -= OnSwung;
            if (inventory != null) inventory.Added -= OnPickedUp;
            if (health != null) health.Healed -= OnHealed;
        }

        private void OnSwung(SwingResult swing)
        {
            if (swing.Missed)
            {
                squash.Kick(missStretch);
                return;
            }

            bool enemy = swing.EnemiesHit > 0;
            hitStop.Freeze(enemy ? enemyHitStop : resourceHitStop);
            squash.Kick(hitSquash);
            Vector2 strength = enemy ? enemyHitRumble : resourceHitRumble;
            rumble.Pulse(strength.x, strength.y, enemy ? enemyHitRumbleSeconds : resourceHitRumbleSeconds);
            ShakeOwnCamera(enemy ? enemyHitShake : resourceHitShake);
        }

        private void OnPickedUp(ResourceType type, int amount)
        {
            flash.Flash(pickupFlash, pickupFlashSeconds);
            squash.Kick(pickupSquash);
            rumble.Pulse(pickupRumble.x, pickupRumble.y, pickupRumbleSeconds);
            Bursts.Emit(pickupBurst, transform.position + Vector3.up * 1.5f);
        }

        private void OnHealed(int amount)
        {
            flash.Flash(healFlash, healFlashSeconds);
            squash.Kick(new Vector2(-0.08f, 0.12f));
            Bursts.Emit(healBurst, transform.position + Vector3.up * 2f, 1.5f);
        }

        private void Update()
        {
            bool dashing = movement.IsDashing;
            if (dashing && !wasDashing)
                Bursts.Emit(dust, transform.position, dashDust);
            wasDashing = dashing;

            if (dashing || movement.IsClimbing || movement.IsFlying || body == null || Time.time < nextStepDust)
                return;
            Vector3 velocity = body.linearVelocity;
            if (new Vector2(velocity.x, velocity.z).sqrMagnitude < stepMinSpeed * stepMinSpeed)
                return;
            nextStepDust = Time.time + stepInterval;
            Bursts.Emit(dust, transform.position, 0.5f);
        }

        private void ShakeOwnCamera(float trauma)
        {
            Camera cam = input != null ? input.camera : null;
            if (cam != null && cam.TryGetComponent(out CameraShake shake))
                shake.AddTrauma(trauma);
        }
    }
}
