using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// An in-game menu screen (pause, "You died"): shows its content with the intro animation, selects the first
    /// button (so a gamepad can use it), and hides after the outro (the animator's "Exit" trigger). Works while the
    /// game is frozen (timeScale 0): the animator must run in Unscaled Time. The buttons only raise requests; the
    /// match (<see cref="Core.MatchFlow"/>) decides what they do. Buttons left empty are not on this screen.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        private static readonly int ExitTrigger = Animator.StringToHash("Exit");

        [Tooltip("What shows and hides (the canvas); this object stays active.")]
        [SerializeField] private GameObject content;
        [SerializeField] private Animator animator;
        [Tooltip("Length of the outro, in real seconds.")]
        [SerializeField, Min(0f)] private float exitSeconds = 0.5f;
        [SerializeField] private Selectable firstSelected;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action MainMenuRequested;
        public event Action QuitRequested;

        private bool isClosing;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Hook(resumeButton, () => ResumeRequested?.Invoke());
            Hook(restartButton, () => RestartRequested?.Invoke());
            Hook(mainMenuButton, () => MainMenuRequested?.Invoke());
            Hook(quitButton, () => QuitRequested?.Invoke());
            content.SetActive(false);
        }

        public void Open()
        {
            StopAllCoroutines();
            isClosing = false;
            IsOpen = true;
            content.SetActive(true); // enabling the animator plays the intro from the start
            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }

        /// <summary>Plays the outro, hides, then calls <paramref name="closed"/>. Ignored if closed or closing.</summary>
        public void Close(Action closed = null)
        {
            if (!IsOpen || isClosing)
                return;
            isClosing = true;
            if (animator != null)
                animator.SetTrigger(ExitTrigger);
            StartCoroutine(HideAfterOutro(closed));
        }

        private IEnumerator HideAfterOutro(Action closed)
        {
            yield return new WaitForSecondsRealtime(exitSeconds);
            content.SetActive(false);
            IsOpen = false;
            isClosing = false;
            closed?.Invoke();
        }

        /// <summary>Buttons answer only while the screen is open and not leaving (no double clicks during the outro).</summary>
        private void Hook(Button button, Action request)
        {
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    if (IsOpen && !isClosing)
                        request();
                });
        }

        /// <summary>Editor setup only.</summary>
        public void Configure(GameObject screenContent, Animator screenAnimator, Selectable first,
            Button resume, Button restart, Button mainMenu, Button quit)
        {
            content = screenContent;
            animator = screenAnimator;
            firstSelected = first;
            resumeButton = resume;
            restartButton = restart;
            mainMenuButton = mainMenu;
            quitButton = quit;
        }
    }
}
