using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Elastic squash and stretch of the sprite child (see <see cref="FeedbackRig.VisualRoot"/>): a kick deforms it
    /// and a spring brings it back with a little wobble. Keeps the left/right flip other scripts put in scale.x.
    /// Sleeps (disabled) at rest.
    /// </summary>
    public class SquashStretch : MonoBehaviour
    {
        private const float Limit = 0.6f;

        [SerializeField] private Transform visual;
        [SerializeField, Min(1f)] private float stiffness = 260f;
        [SerializeField, Min(0f)] private float damping = 14f;

        private Vector3 baseScale;
        private Spring width;
        private Spring height;

        private void Awake()
        {
            if (visual == null)
                visual = FeedbackRig.VisualRoot(transform);
            if (visual != null)
            {
                Vector3 scale = visual.localScale;
                baseScale = new Vector3(Mathf.Abs(scale.x), scale.y, scale.z);
            }
            enabled = false;
        }

        /// <summary>x = extra width, y = extra height (0.2 = 20%). Squash: (+, -); stretch: (-, +).</summary>
        public void Kick(Vector2 amount)
        {
            if (visual == null || amount == Vector2.zero)
                return;
            width.Displace(amount.x, Limit);
            height.Displace(amount.y, Limit);
            enabled = true;
        }

        // LateUpdate: after the flip set in Update.
        private void LateUpdate()
        {
            width.Step(stiffness, damping, Time.deltaTime);
            height.Step(stiffness, damping, Time.deltaTime);

            float side = visual.localScale.x < 0f ? -1f : 1f;
            visual.localScale = new Vector3(side * baseScale.x * (1f + width.Value), baseScale.y * (1f + height.Value), baseScale.z);

            if (width.IsSettled && height.IsSettled)
                enabled = false;
        }
    }
}
