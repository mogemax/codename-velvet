using System.Collections;
using System.Collections.Generic;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodenameVelvet.Core
{
    /// <summary>
    /// How a match ends:
    /// <list type="bullet">
    /// <item><b>Game over</b>: every player is down at the same time. While one is alive, downed players
    /// respawn as usual (PlayerHealth). Playing solo, dying is game over.</item>
    /// <item><b>Victory</b>: someone calls <see cref="Victory"/> (the boss on death).</item>
    /// </list>
    /// Either way the game freezes (timeScale 0, so nobody respawns or moves), the matching panel shows,
    /// and the menu loads after a few real-time seconds.
    /// </summary>
    public class MatchFlow : MonoBehaviour
    {
        private const string LogCategory = "Match";

        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private string menuScene = "MainMenu";
        [SerializeField, Min(0f)] private float secondsBeforeMenu = 4f;

        private readonly List<PlayerHealth> players = new List<PlayerHealth>();
        private bool isOver;

        private void Awake()
        {
            SetActive(gameOverPanel, false);
            SetActive(victoryPanel, false);
        }

        private void OnDestroy()
        {
            foreach (PlayerHealth player in players)
                if (player != null) player.Died -= HandlePlayerDied;
            Time.timeScale = 1f; // in case the scene unloads during the end screen
        }

        public void RegisterPlayer(PlayerHealth player)
        {
            players.Add(player);
            player.Died += HandlePlayerDied;
        }

        public void Victory() => End(victoryPanel, "Victory");

        // Died fires once the player already counts as down, so it is enough to look for anyone standing.
        private void HandlePlayerDied()
        {
            foreach (PlayerHealth player in players)
                if (player != null && player.IsAlive) return;

            End(gameOverPanel, "Game over");
        }

        private void End(GameObject panel, string reason)
        {
            if (isOver) return;
            isOver = true;

            VelvetLog.Info(LogCategory, this, reason);
            SetActive(panel, true);
            Time.timeScale = 0f;
            StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ReturnToMenu()
        {
            yield return new WaitForSecondsRealtime(secondsBeforeMenu);
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
