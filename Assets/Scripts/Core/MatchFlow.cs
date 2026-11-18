using System.Collections;
using System.Collections.Generic;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodenameVelvet.Core
{
    /// <summary>
    /// Fin de la partida en el nivel:
    /// <list type="bullet">
    /// <item><b>Game over</b>: todos los jugadores caídos al mismo tiempo. Mientras quede uno vivo, los
    /// caídos reaparecen como siempre (PlayerHealth). Jugando solo, morir es game over.</item>
    /// <item><b>Victoria</b>: alguien llama a <see cref="Victory"/> (el jefe al morir).</item>
    /// </list>
    /// En ambos casos congela el juego (timeScale 0, así nadie reaparece ni se mueve), muestra su panel
    /// y regresa al menú tras unos segundos reales.
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
            Time.timeScale = 1f; // por si la escena se descarga a mitad de la pantalla final
        }

        public void RegisterPlayer(PlayerHealth player)
        {
            players.Add(player);
            player.Died += HandlePlayerDied;
        }

        public void Victory() => End(victoryPanel, "Victory");

        // Died llega cuando el jugador ya cuenta como caído, así que basta revisar si queda alguien de pie.
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
