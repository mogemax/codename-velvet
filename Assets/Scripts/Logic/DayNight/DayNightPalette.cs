using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>When the light turns warm: dusk at the end of the day, dawn at its start; never at night.</summary>
    public static class DayNightPalette
    {
        /// <summary>
        /// 0 = plain day, 1 = full dusk/dawn color. Ramps up over the last <paramref name="duskSeconds"/> of the day
        /// and fades over the first <paramref name="dawnSeconds"/> of it. 0 at night (the night colors take over).
        /// </summary>
        public static float Warmth(bool isNight, float elapsed, float remaining, float duskSeconds, float dawnSeconds)
        {
            if (isNight) return 0f;
            float dusk = duskSeconds > 0f ? 1f - remaining / duskSeconds : 0f;
            float dawn = dawnSeconds > 0f ? 1f - elapsed / dawnSeconds : 0f;
            return Mathf.Clamp01(Mathf.Max(dusk, dawn));
        }
    }
}
