using System;

namespace CodenameVelvet.Feedback
{
    /// <summary>Small rules behind the game feel, kept here so they are testable.</summary>
    public static class FeedbackRules
    {
        /// <summary>
        /// Low health: at or below <paramref name="fraction"/> of the max, and always on the last hit point (with 3
        /// HP, 30% would otherwise never show). Dead is not low: there is nothing left to warn about.
        /// </summary>
        public static bool IsLowHealth(int hp, int maxHp, float fraction)
        {
            if (hp <= 0 || maxHp <= 0)
                return false;
            int threshold = Math.Max(1, (int)Math.Floor(maxHp * fraction));
            return hp <= threshold;
        }

        /// <summary>
        /// Height of a dropped piece along its flight (t = 0..1): a high arc for the first 75%, then one small bounce.
        /// 0 at the start and on landing.
        /// </summary>
        public static float BounceHeight(float t, float height)
        {
            const float Split = 0.75f;
            const float BounceRatio = 0.22f;
            t = Math.Clamp(t, 0f, 1f);
            if (t < Split)
            {
                float u = t / Split;
                return 4f * height * u * (1f - u);
            }
            float v = (t - Split) / (1f - Split);
            return 4f * height * BounceRatio * v * (1f - v);
        }

        /// <summary>
        /// A heartbeat (0-1) at <paramref name="time"/> seconds: a quick double pulse, then a rest. Shared by the low
        /// health vignette and the last heart of the HUD, so both beat together.
        /// </summary>
        public static float Heartbeat(float time, float beatsPerSecond)
        {
            float t = time * beatsPerSecond;
            t -= (float)Math.Floor(t);
            return Math.Max(Bump(t, 0.08f), 0.7f * Bump(t, 0.28f));
        }

        private static float Bump(float t, float center) => Math.Clamp(1f - Math.Abs(t - center) / 0.1f, 0f, 1f);

        /// <summary>Gamepad rumble for taking <paramref name="damage"/>: stronger with the hit, capped at 1.</summary>
        public static float HurtIntensity(int damage, float baseIntensity, float perExtraPoint) =>
            Math.Clamp(baseIntensity + perExtraPoint * Math.Max(0, damage - 1), 0f, 1f);
    }
}
