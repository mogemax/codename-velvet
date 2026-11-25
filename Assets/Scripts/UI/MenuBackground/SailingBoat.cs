using UnityEngine;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// The menu background's boat: sails in from off screen to its place (once; the background lives through the
    /// menus, see <see cref="MenuBackground"/>), then keeps rocking on the waves. Its horizontal place is an anchor
    /// (0 = left edge, 1 = right edge), so it is the same on every screen size. Real time.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SailingBoat : MonoBehaviour
    {
        [Header("Setting sail")]
        [Tooltip("Where it starts (screen widths; negative = off screen to the left).")]
        [SerializeField] private float startX = -0.2f;
        [Tooltip("Where it stays sailing.")]
        [SerializeField] private float restX = 0.6f;
        [SerializeField, Min(0.01f)] private float sailInSeconds = 7f;
        [SerializeField, Min(0f)] private float delay = 0.4f;
        [Tooltip("Degrees the bow lifts as it sets off, settling down as it arrives.")]
        [SerializeField] private float launchTilt = 4f;

        [Header("Rocking")]
        [SerializeField] private float rockHeight = 12f;
        [SerializeField, Min(0.01f)] private float rockSeconds = 3.2f;
        [SerializeField] private float rockTilt = 3.5f;
        [Tooltip("Small surge back and forth, as if riding each wave.")]
        [SerializeField] private float surge = 10f;

        private RectTransform rect;
        private Vector2 basePosition;
        private float startTime;

        private void Awake()
        {
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
            startTime = Time.unscaledTime + delay;
            SetAnchorX(startX);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float sailed = Mathf.Clamp01((now - startTime) / sailInSeconds);
            float eased = 1f - Mathf.Pow(1f - sailed, 3f); // sets off hard, slows down gently
            SetAnchorX(Mathf.LerpUnclamped(startX, restX, eased));
            float lift = now < startTime ? 0f : launchTilt * Mathf.Pow(1f - sailed, 2f);

            float wave = Mathf.PI * 2f * now / rockSeconds;
            rect.anchoredPosition = basePosition + new Vector2(Mathf.Sin(wave * 0.5f) * surge, Mathf.Sin(wave) * rockHeight);
            // The tilt leads the height: the bow rises before the crest.
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Cos(wave) * rockTilt + lift);
        }

        private void SetAnchorX(float x)
        {
            rect.anchorMin = new Vector2(x, rect.anchorMin.y);
            rect.anchorMax = new Vector2(x, rect.anchorMax.y);
        }
    }
}
