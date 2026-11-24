using CodenameVelvet.Cameras;
using CodenameVelvet.Combat;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// What getting hurt feels like for this player, on their own screen and gamepad only: rumble and camera shake
    /// that grow with the damage, a red flash at the edges, blinking during the post-hit invulnerability, and a red
    /// vignette that beats while health is low. (The flash and squash of the sprite come from <see cref="HitReaction"/>.)
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerHurtFeedback : MonoBehaviour
    {
        [Header("Rumble")]
        [Tooltip("Strength for 1 damage (0-1); each extra point adds Per Extra Point.")]
        [SerializeField, Range(0f, 1f)] private float rumble = 0.55f;
        [SerializeField, Range(0f, 1f)] private float rumblePerExtraPoint = 0.2f;
        [SerializeField, Min(0f)] private float rumbleSeconds = 0.25f;

        [Header("Camera")]
        [Tooltip("Shake of this player's camera for 1 damage; grows like the rumble.")]
        [SerializeField, Range(0f, 1f)] private float cameraShake = 0.4f;

        [Header("Blink")]
        [Tooltip("Blinks per second while invulnerable after a hit.")]
        [SerializeField, Min(1f)] private float blinkRate = 12f;

        [Header("Screen")]
        [SerializeField] private Color vignetteColor = new Color(0.7f, 0.03f, 0.03f);
        [Tooltip("Edge flash on every hit.")]
        [SerializeField, Range(0f, 1f)] private float hurtFlash = 0.6f;
        [SerializeField, Min(0.01f)] private float hurtFlashSeconds = 0.35f;
        [Tooltip("Low health: at or below this fraction of max HP (and always on the last point).")]
        [SerializeField, Range(0f, 1f)] private float lowHealthFraction = 0.3f;
        [SerializeField, Range(0f, 1f)] private float lowHealthOpacity = 0.4f;
        [Tooltip("Heartbeats per second while health is low.")]
        [SerializeField, Min(0.1f)] private float heartbeatRate = 1.3f;

        private PlayerHealth health;
        private PlayerRumble playerRumble;
        private PlayerInput input;
        private SpriteRenderer[] sprites;
        private ScreenVignette vignette;
        private CameraShake shake;
        private float hurtFlashLeft;
        private bool isBlinking;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            input = GetComponent<PlayerInput>();
            playerRumble = FeedbackRig.GetOrAdd<PlayerRumble>(gameObject);
        }

        private void Start()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            // The spawner gives the player its camera right after creating it, before Start.
            Camera cam = input != null ? input.camera : null;
            if (cam == null)
                return;
            vignette = ScreenVignette.Create(cam, vignetteColor, name);
            cam.TryGetComponent(out shake);
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += StopBlinking;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= StopBlinking;
            StopBlinking();
        }

        private void OnDestroy()
        {
            if (vignette != null)
                Destroy(vignette.gameObject);
        }

        private void OnDamaged(DamageEvent hit)
        {
            float strength = FeedbackRules.HurtIntensity(hit.Amount, rumble, rumblePerExtraPoint);
            playerRumble.Pulse(strength, strength * 0.6f, hit.IsFatal ? rumbleSeconds * 2f : rumbleSeconds);

            float scale = strength / Mathf.Max(rumble, 0.01f);
            if (shake != null)
                shake.AddTrauma(cameraShake * scale);

            hurtFlashLeft = hurtFlashSeconds;
        }

        private void Update()
        {
            UpdateBlink();
            UpdateVignette();
        }

        private void UpdateBlink()
        {
            if (health.IsAlive && health.IsRecovering)
            {
                isBlinking = true;
                SetSpritesVisible(Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f);
            }
            else if (isBlinking)
            {
                StopBlinking();
            }
        }

        private void StopBlinking()
        {
            if (!isBlinking)
                return;
            isBlinking = false;
            SetSpritesVisible(true);
        }

        private void SetSpritesVisible(bool visible)
        {
            if (sprites == null)
                return;
            foreach (SpriteRenderer sprite in sprites)
                if (sprite != null)
                    sprite.enabled = visible;
        }

        private void UpdateVignette()
        {
            if (vignette == null)
                return;

            hurtFlashLeft = Mathf.Max(0f, hurtFlashLeft - Time.deltaTime);
            float flash = hurtFlash * (hurtFlashLeft / hurtFlashSeconds);

            float low = 0f;
            if (FeedbackRules.IsLowHealth(health.CurrentHp, health.MaxHp, lowHealthFraction))
            {
                // A heartbeat: a quick double pulse, then a rest.
                float beat = Mathf.Repeat(Time.time * heartbeatRate, 1f);
                float pulse = Mathf.Max(Bump(beat, 0.08f), 0.7f * Bump(beat, 0.28f));
                low = lowHealthOpacity * (0.45f + 0.55f * pulse);
            }

            vignette.Opacity = Mathf.Max(flash, low);
        }

        private static float Bump(float t, float center) => Mathf.Clamp01(1f - Mathf.Abs(t - center) / 0.1f);
    }
}
