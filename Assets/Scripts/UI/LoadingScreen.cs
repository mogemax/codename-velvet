using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Covers the whole screen with a looping video while a scene loads, then fades out over it. One instance lives
    /// for the whole session: <see cref="Preload"/> (called by whatever may need it, as soon as its scene starts)
    /// creates it hidden and prepares the video in the background, so <see cref="Load"/> only has to show it and
    /// press play: the first frame is there at once. Runs in real time, so it also works while the game is frozen.
    /// The background color only shows if the video cannot play.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float ReadyToActivate = 0.9f; // where Unity stops while activation is not allowed
        private const float MaxPrepareWait = 2f;     // only if Load comes before the video is ready
        private const float MaxFadeStep = 0.05f;     // the level's first frames are long and would skip the fade

        [SerializeField] private VideoPlayer video;
        [SerializeField] private RawImage picture;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Shown at least this long, however fast the scene loads.")]
        [SerializeField, Min(0f)] private float minSeconds = 3f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;

        private static LoadingScreen instance;

        /// <summary>A load starts (the screen is up). Menu music fades out on it.</summary>
        public static event Action Started;

        public bool IsBusy { get; private set; }

        /// <summary>Creates the session's loading screen from <paramref name="prefab"/> if there is none yet.</summary>
        public static LoadingScreen Preload(LoadingScreen prefab)
        {
            if (instance != null || prefab == null)
                return instance;

            instance = Instantiate(prefab);
            instance.name = prefab.name;
            DontDestroyOnLoad(instance.gameObject);
            instance.Hide();
            instance.video.prepareCompleted += instance.ShowFrame;
            instance.video.Prepare();
            return instance;
        }

        /// <summary>Loads <paramref name="scene"/> behind the session's loading screen (created now if needed).</summary>
        public static void Load(LoadingScreen prefab, string scene)
        {
            LoadingScreen screen = Preload(prefab);
            if (screen == null) SceneManager.LoadScene(scene);
            else if (!screen.IsBusy) screen.StartCoroutine(screen.Run(scene));
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Hide()
        {
            group.alpha = 0f;
            gameObject.SetActive(true);
            group.blocksRaycasts = false;
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        }

        private void Show()
        {
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true)) canvas.enabled = true;
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        private void ShowFrame(VideoPlayer source)
        {
            picture.texture = source.texture;
            picture.enabled = source.texture != null;
        }

        private IEnumerator Run(string scene)
        {
            IsBusy = true;
            picture.enabled = video.isPrepared && video.texture != null;
            Show();
            Started?.Invoke();

            // Normally prepared long ago. If not (Load right after Preload), wait a little for it, not forever.
            float giveUp = Time.realtimeSinceStartup + MaxPrepareWait;
            while (!video.isPrepared && Time.realtimeSinceStartup < giveUp)
                yield return null;
            if (video.isPrepared)
            {
                ShowFrame(video);
                video.Play();
            }

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

            // Ready for the next time: paused on its first frame, still prepared.
            video.Pause();
            video.time = 0;
            Hide();
            IsBusy = false;
        }
    }
}
