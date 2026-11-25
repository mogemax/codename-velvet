using CodenameVelvet.Feedback;
using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// One heart of the health HUD: full (red) or lost (grey, arrow). Losing it jolts and shakes it; getting it back
    /// pops it in. The last heart also beats while health is low (<see cref="Beat"/>).
    /// </summary>
    public class HudHeart : MonoBehaviour
    {
        private const float ShakeSeconds = 0.3f;
        private const float ShakeDegrees = 18f;

        private Image image;
        private Sprite full;
        private Sprite lost;
        private Spring pop;
        private float shakeLeft;
        private float beat;

        public bool IsFull { get; private set; } = true;

        public void Setup(Image heartImage, Sprite fullSprite, Sprite lostSprite)
        {
            image = heartImage;
            full = fullSprite;
            lost = lostSprite;
            image.sprite = full;
        }

        public void SetFull(bool isFull, bool animate)
        {
            if (isFull == IsFull)
                return;
            IsFull = isFull;
            image.sprite = isFull ? full : lost;
            if (!animate)
                return;

            if (isFull)
            {
                pop.Displace(-0.5f, 0.6f); // grows back in with a little overshoot
            }
            else
            {
                pop.Displace(0.45f, 0.6f);
                shakeLeft = ShakeSeconds;
            }
        }

        /// <summary>Extra size this frame (0 = none), for the low-health heartbeat.</summary>
        public void Beat(float amount) => beat = amount;

        private void Update()
        {
            pop.Step(320f, 14f, Time.unscaledDeltaTime);
            shakeLeft = Mathf.Max(0f, shakeLeft - Time.unscaledDeltaTime);

            float scale = 1f + pop.Value + beat;
            transform.localScale = new Vector3(scale, scale, 1f);
            float angle = shakeLeft > 0f ? Mathf.Sin(shakeLeft * 70f) * ShakeDegrees * (shakeLeft / ShakeSeconds) : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
