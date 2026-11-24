using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Rattles the sprite child side to side for a moment (a struck tree, a hit enemy), then puts it back exactly
    /// where it was. Sleeps (disabled) when idle.
    /// </summary>
    public class Jolt : MonoBehaviour
    {
        private const float Frequency = 55f;

        [SerializeField] private Transform visual;

        private Vector3 restPosition;
        private float amplitude;
        private float duration;
        private float timeLeft;

        private void Awake()
        {
            if (visual == null)
                visual = FeedbackRig.VisualRoot(transform);
            enabled = false;
        }

        public void Shake(float shakeAmplitude, float seconds)
        {
            if (visual == null || shakeAmplitude <= 0f || seconds <= 0f)
                return;
            if (!enabled)
                restPosition = visual.localPosition;
            amplitude = shakeAmplitude;
            duration = timeLeft = seconds;
            enabled = true;
        }

        private void LateUpdate()
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                enabled = false;
                return;
            }
            float fade = timeLeft / duration;
            float offset = Mathf.Sin(timeLeft * Frequency) * amplitude * fade;
            visual.localPosition = restPosition + new Vector3(offset, 0f, 0f);
        }

        private void OnDisable()
        {
            if (visual != null && duration > 0f)
                visual.localPosition = restPosition;
        }
    }
}
