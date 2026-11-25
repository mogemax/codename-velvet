using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// The animated sea behind the menus. Put one (the MenuBackground prefab) in each menu scene that shows it. The
    /// first one lives on through the menus, so the boat sets sail once and keeps sailing; the others, finding it,
    /// remove themselves and just mark their scene as one where it shows. In menu scenes without one it is hidden
    /// (but keeps sailing). Goes away when a loading screen comes up, on the way to a level.
    /// Draws behind the menu: a camera canvas far from the camera, sorted below the others, with no raycaster.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MenuBackground : MonoBehaviour
    {
        private static MenuBackground current;

        private Canvas canvas;
        private Scene shownIn;

        private void Awake()
        {
            if (current != null)
            {
                current.shownIn = gameObject.scene;
                Destroy(gameObject);
                return;
            }

            current = this;
            canvas = GetComponent<Canvas>();
            shownIn = gameObject.scene;
            canvas.worldCamera = Camera.main;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadingScreen.Started += Leave;
        }

        private void OnDestroy()
        {
            if (current != this) return;
            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LoadingScreen.Started -= Leave;
        }

        // After the new scene's objects woke up: a copy in it has already claimed it.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            canvas.enabled = scene == shownIn;
            canvas.worldCamera = Camera.main;
        }

        private void Leave() => Destroy(gameObject);
    }
}
