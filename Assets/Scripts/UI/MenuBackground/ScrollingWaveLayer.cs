using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// One layer of the menu background's sea: its texture scrolls sideways in a loop (the nearer the layer, the
    /// faster, so the boat seems to move) and the layer bobs up and down. The texture must wrap (Repeat) in U.
    /// Real time, so it keeps moving while the game is frozen.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ScrollingWaveLayer : MonoBehaviour
    {
        [Tooltip("Canvas units per second; positive moves the waves left (the boat sails right).")]
        [SerializeField] private float speed = 70f;
        [SerializeField, Min(0f)] private float bobHeight = 6f;
        [SerializeField, Min(0.01f)] private float bobSeconds = 4f;
        [Tooltip("Where in its scroll and bob this layer starts (0-1), so the layers do not move in step.")]
        [SerializeField, Range(0f, 1f)] private float phase;

        private RawImage image;
        private RectTransform rect;
        private float offset;
        private float bobPhase;
        private float baseY;
        private float speedScale = 1f;
        private float bobScale = 1f;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            rect = (RectTransform)transform;
            baseY = rect.anchoredPosition.y;
            offset = phase;
            bobPhase = phase;
        }

        /// <summary>Speeds up / raises the layer (rough sea) or slows it down (calm). 1 = as set.</summary>
        public void SetScale(float speed, float bob)
        {
            speedScale = speed;
            bobScale = bob;
        }

        private void Update()
        {
            if (image.texture == null) return;

            // One repeat of the texture is as wide as the layer is tall times the texture's aspect.
            Rect area = rect.rect;
            float repeatWidth = Mathf.Max(1f, area.height * image.texture.width / image.texture.height);
            float dt = Time.unscaledDeltaTime;
            offset = Mathf.Repeat(offset + speed * speedScale * dt / repeatWidth, 1f);
            image.uvRect = new Rect(offset, 0f, area.width / repeatWidth, 1f);

            // Accumulated, so speeding up does not make the layer jump.
            bobPhase = Mathf.Repeat(bobPhase + dt * speedScale / bobSeconds, 1f);
            float bob = Mathf.Sin(bobPhase * Mathf.PI * 2f) * bobHeight * bobScale;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, baseY + bob);
        }
    }
}
