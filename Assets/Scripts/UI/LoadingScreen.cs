using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Covers the whole screen with a looping video while a scene loads, then fades out over it. Instantiate its
    /// prefab and call <see cref="Load"/>: it survives the scene change and destroys itself. Runs in real time, so it
    /// also works while the game is frozen.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float ReadyToActivate = 0.9f; // where Unity stops while activation is not allowed
        private const float MaxPrepareSeconds = 2f;
        private const float MaxFadeStep = 0.05f; // the level's first frames are long and would skip the fade

        [SerializeField] private VideoPlayer video;
        [SerializeField] private RawImage picture;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Shown at least this long, however fast the scene loads.")]
        [SerializeField, Min(0f)] private float minSeconds = 4f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;

        public void Load(string scene)
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run(scene));
        }

        private IEnumerator Run(string scene)
        {
            // Until the first frame is ready (or if the video cannot play) only the background color shows.
            picture.enabled = false;
            video.started += _ =>
            {
                picture.texture = video.texture;
                picture.enabled = true;
            };

            // Before loading: both at once make the video start late.
            video.Prepare();
            float giveUp = Time.realtimeSinceStartup + MaxPrepareSeconds;
            while (!video.isPrepared && Time.realtimeSinceStartup < giveUp)
                yield return null;
            video.Play();

            float end = Time.realtimeSinceStartup + minSeconds;
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            load.allowSceneActivation = false;
            while (load.progress < ReadyToActivate || Time.realtimeSinceStartup < end)
                yield return null;

            load.allowSceneActivation = true;
            yield return load;
            yield return null; // let the level build itself (players, trees...) behind the screen

            while (group.alpha > 0f)
            {
                group.alpha -= Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep) / fadeSeconds;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
