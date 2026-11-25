using UnityEngine;

namespace CodenameVelvet.Audio
{
    /// <summary>The volume setting as players see it (0-100, like the settings slider) and as audio uses it (0-1).</summary>
    public static class VolumeRules
    {
        public const float MaxPercent = 100f;
        public const float DefaultPercent = 100f;

        public static float ClampPercent(float percent) => Mathf.Clamp(percent, 0f, MaxPercent);

        public static float ToGain(float percent) => ClampPercent(percent) / MaxPercent;
    }
}
