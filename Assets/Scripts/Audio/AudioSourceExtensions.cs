using UnityEngine;

namespace CodenameVelvet.Audio
{
    public static class AudioSourceExtensions
    {
        /// <summary>Starts or stops a looping source; silent while the game is frozen (timeScale 0). Null is ignored.</summary>
        public static void SetLoopPlaying(this AudioSource source, bool playing)
        {
            if (source == null) return;

            playing &= Time.timeScale > 0f;
            if (playing == source.isPlaying) return;

            if (playing) source.Play();
            else source.Stop();
        }

        /// <summary>Plays one of the clips at random. A null source or no clips is silent.</summary>
        public static void PlayRandom(this AudioSource source, AudioClip[] clips)
        {
            if (source != null && clips != null && clips.Length > 0)
                source.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }
    }
}
