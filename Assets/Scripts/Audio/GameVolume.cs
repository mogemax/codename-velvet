using UnityEngine;

namespace CodenameVelvet.Audio
{
    /// <summary>
    /// The game's overall volume (every sound: music, effects): the settings slider's value, kept between sessions
    /// and applied before the first scene loads.
    /// </summary>
    public static class GameVolume
    {
        private const string Key = "Velvet.Volume";

        /// <summary>0-100.</summary>
        public static float Percent => VolumeRules.ClampPercent(PlayerPrefs.GetFloat(Key, VolumeRules.DefaultPercent));

        public static void Set(float percent)
        {
            PlayerPrefs.SetFloat(Key, VolumeRules.ClampPercent(percent));
            PlayerPrefs.Save();
            Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply() => AudioListener.volume = VolumeRules.ToGain(Percent);
    }
}
