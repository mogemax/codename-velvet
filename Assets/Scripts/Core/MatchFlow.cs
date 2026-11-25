using System.Collections;
using System.Collections.Generic;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Player;
using CodenameVelvet.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CodenameVelvet.Core
{
    /// <summary>
    /// The match's flow: pausing, and how it ends.
    /// <list type="bullet">
    /// <item><b>Pause</b>: Esc or any gamepad's Start opens the pause menu (resume, main menu, quit); pressing it
    /// again resumes.</item>
    /// <item><b>Game over</b>: every player is down at the same time. While one is alive, downed players
    /// respawn as usual (PlayerHealth). Playing solo, dying is game over. The end screen offers to restart the
    /// level from the start, go to the main menu or quit.</item>
    /// <item><b>Victory</b>: someone calls <see cref="Victory"/> (the boss on death). Its panel shows and the
    /// menu loads after a few real-time seconds.</item>
    /// </list>
    /// While a menu or end screen is up the game is frozen (timeScale 0, so nobody respawns or moves) and the
    /// players' controls are off, so the button that confirms a menu choice doesn't also swing a sword.
    /// </summary>
    public class MatchFlow : MonoBehaviour
    {
        private const string LogCategory = "Match";

        [SerializeField] private MenuScreen pauseMenu;
        [SerializeField] private MenuScreen endScreen;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private string menuScene = "MainMenu";
        [Tooltip("After victory, real seconds before the menu loads.")]
        [SerializeField, Min(0f)] private float secondsBeforeMenu = 4f;

        private readonly List<PlayerHealth> players = new List<PlayerHealth>();
        private bool isOver;
        private bool isLeaving;

        private void Awake()
        {
            SetActive(victoryPanel, false);
            if (pauseMenu != null)
            {
                pauseMenu.ResumeRequested += Resume;
                pauseMenu.MainMenuRequested += GoToMenu;
                pauseMenu.QuitRequested += Quit;
            }
            if (endScreen != null)
            {
                endScreen.RestartRequested += Restart;
                endScreen.MainMenuRequested += GoToMenu;
                endScreen.QuitRequested += Quit;
            }
        }

        private void OnDestroy()
        {
            foreach (PlayerHealth player in players)
                if (player != null) player.Died -= HandlePlayerDied;
            Time.timeScale = 1f; // in case the scene unloads frozen
        }

        private void Update()
        {
            if (isOver || isLeaving || pauseMenu == null || !PausePressed())
                return;

            if (pauseMenu.IsOpen) Resume();
            else Pause();
        }

        public void RegisterPlayer(PlayerHealth player)
        {
            players.Add(player);
            player.Died += HandlePlayerDied;
        }

        public void Victory()
        {
            if (!End("Victory")) return;
            SetActive(victoryPanel, true);
            StartCoroutine(GoToMenuAfter(secondsBeforeMenu));
        }

        // ---- Pause ---------------------------------------------------------------------------------------------

        /// <summary>Read straight from the devices: any player can pause, and it works while their controls are off.</summary>
        private static bool PausePressed()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
            foreach (Gamepad pad in Gamepad.all)
                if (pad.startButton.wasPressedThisFrame)
                    return true;
            return false;
        }

        private void Pause()
        {
            Freeze(true);
            pauseMenu.Open();
        }

        private void Resume() => pauseMenu.Close(() => Freeze(false));

        // ---- End -----------------------------------------------------------------------------------------------

        // Died fires once the player already counts as down, so it is enough to look for anyone standing.
        private void HandlePlayerDied()
        {
            foreach (PlayerHealth player in players)
                if (player != null && player.IsAlive) return;

            if (!End("Game over")) return;
            if (endScreen != null) endScreen.Open();
            else StartCoroutine(GoToMenuAfter(secondsBeforeMenu));
        }

        /// <summary>Freezes the match for good. False if it was already over.</summary>
        private bool End(string reason)
        {
            if (isOver) return false;
            isOver = true;
            VelvetLog.Info(LogCategory, this, reason);
            if (pauseMenu != null && pauseMenu.IsOpen) pauseMenu.Close();
            Freeze(true);
            return true;
        }

        private static void Freeze(bool frozen)
        {
            Time.timeScale = frozen ? 0f : 1f;
            foreach (PlayerInput input in PlayerInput.all)
            {
                if (frozen) input.DeactivateInput();
                else input.ActivateInput();
            }
        }

        // ---- Leaving -------------------------------------------------------------------------------------------

        private void Restart() => Load(SceneManager.GetActiveScene().name);

        private void GoToMenu() => Load(menuScene);

        private void Quit()
        {
            if (isLeaving) return;
            isLeaving = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator GoToMenuAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            GoToMenu();
        }

        private void Load(string scene)
        {
            if (isLeaving) return;
            isLeaving = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
