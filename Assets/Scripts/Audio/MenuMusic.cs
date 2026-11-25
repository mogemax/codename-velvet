using System.Collections;
using CodenameVelvet.UI;
using UnityEngine;

namespace CodenameVelvet.Audio
{
    /// <summary>
    /// The menus' music, one track through every menu scene: put one in each menu scene. The first one keeps playing
    /// across scene changes; the others, finding it there, remove themselves (so it never starts over). When a loading
    /// screen comes up (on the way to a level) it fades out and goes away. Real time, so a frozen game does not stop the fade.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusic : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fadeSeconds = 2f;

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
        }

        private void OnDestroy()
        {
            if (playing != this) return;
            playing = null;
            LoadingScreen.Started -= FadeOut;
        }

        private void FadeOut()
        {
            LoadingScreen.Started -= FadeOut;
            StartCoroutine(FadeAndLeave());
        }

        private IEnumerator FadeAndLeave()
        {
            float start = source.volume;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, 0f, t / fadeSeconds);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
