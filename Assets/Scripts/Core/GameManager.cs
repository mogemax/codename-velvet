using UnityEngine;

namespace CodenameVelvet.Core
{
    /// <summary>
    /// Session/scene-lifecycle-only persistent manager. Deliberately owns no
    /// gameplay or player-specific state — that precedent is enforced for
    /// every later phase's domain-specific managers.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public string CurrentSceneName { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentSceneName = gameObject.scene.name;
        }
    }
}
