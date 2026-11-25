using System.Collections;
using CodenameVelvet.UI;
using UnityEngine;

namespace CodenameVelvet.Audio
{
    /// <summary>
    /// The menus' music, one track through every menu scene: put one in each menu scene. The first one keeps playing
    /// across scene changes; the others, finding it there, remove themselves (so it never starts over). When a loading
    /// screen comes up (on the way to a level) it fades out and goes away; if the menu's boat is wrecked first
    /// (<see cref="Shipwreck"/>) it stops right at the impact. Real time, so a frozen game does not stop the fade.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusic : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fadeSeconds = 2f;
        [Tooltip("When the menu's boat is wrecked the music stops: this short, just so it does not click.")]
        [SerializeField, Min(0.01f)] private float cutSeconds = 0.12f;

        private static MenuMusic playing;

        private AudioSource source;

        private void Awake()
        {
            if (playing != null)
            {
                Destroy(gameObject);
                return;
            }

            playing = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.ignoreListenerPause = true;
            if (!source.isPlaying) source.Play();
            LoadingScreen.Started += FadeOut;
            Shipwreck.Impacted += Cut;
        }

        private void OnDestroy()
        {
            if (playing != this) return;
            playing = null;
            LoadingScreen.Started -= FadeOut;
            Shipwreck.Impacted -= Cut;
        }

        private void FadeOut() => Leave(fadeSeconds);

        private void Cut() => Leave(cutSeconds);

        private void Leave(float seconds)
        {
            LoadingScreen.Started -= FadeOut;
            Shipwreck.Impacted -= Cut;
            StartCoroutine(FadeAndLeave(seconds));
        }

        private IEnumerator FadeAndLeave(float seconds)
        {
            float start = source.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
