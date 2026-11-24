using System;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// A damped spring that settles back to 0: displace it (a squash, a wobble) and step it every frame. Underdamped
    /// values overshoot once or twice, which is what makes a squash feel elastic. Steps are split into small substeps,
    /// so a long frame never makes it explode.
    /// </summary>
    public struct Spring
    {
        private const float MaxSubstep = 1f / 120f;
        private const float RestValue = 0.0005f;
        private const float RestVelocity = 0.005f;

        public float Value;
        public float Velocity;

        public bool IsSettled => Math.Abs(Value) < RestValue && Math.Abs(Velocity) < RestVelocity;

        /// <summary>Moves it away from rest at once, clamped to ±<paramref name="limit"/>.</summary>
        public void Displace(float amount, float limit = 1f) => Value = Math.Clamp(Value + amount, -limit, limit);

        /// <param name="stiffness">Pull back to rest (higher = faster).</param>
        /// <param name="damping">Loss per second (lower = more wobble).</param>
        public void Step(float stiffness, float damping, float deltaTime)
        {
            while (deltaTime > 0f)
            {
                float h = Math.Min(deltaTime, MaxSubstep);
                Velocity += (-stiffness * Value - damping * Velocity) * h;
                Value += Velocity * h;
                deltaTime -= h;
            }

            if (IsSettled)
            {
                Value = 0f;
                Velocity = 0f;
            }
        }
    }
}
