using CodenameVelvet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// This player's "You died" screen, on their own part of the split screen: only the Reborn button, locked
    /// with a countdown until <see cref="PlayerHealth.CanRespawn"/>, then lit up. The player answers it with their
    /// own Attack or Interact button (messages from their PlayerInput), so several downed players never fight
    /// over the shared UI focus. The match keeps running meanwhile; the main menu and exit only appear on the
    /// match's game over (<see cref="Core.MatchFlow"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerDeathScreen : MonoBehaviour
    {
        [Tooltip("The end screen prefab (its MenuScreen); a copy is made for this player.")]
        [SerializeField] private MenuScreen screenPrefab;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Font material with an outline, so the prompt reads over anything.")]
        [SerializeField] private Material promptMaterial;
        [SerializeField, Min(8f)] private float promptSize = 34f;
        [Tooltip("Below the match's menus (200): the game over screen covers it.")]
        [SerializeField] private int sortingOrder = 150;

        [Header("Text")]
        [SerializeField] private string waitingText = "Reborn in {0}";
        [SerializeField] private string readyText = "Press Attack to reborn";

        private PlayerHealth health;
        private Camera viewCamera;
        private MenuScreen screen;
        private GameObject screenRoot;
        private Image rebornImage;
        private Sprite lockedSprite;
        private Sprite readySprite;
        private TMP_Text prompt;
        private bool respawnRequested;

        private void Awake() => health = GetComponent<PlayerHealth>();

        private void OnEnable()
        {
            health.Died += Show;
            health.Respawned += Hide;
        }

        private void OnDisable()
        {
            health.Died -= Show;
            health.Respawned -= Hide;
        }

        private void Start() => viewCamera = TryGetComponent(out PlayerInput input) ? input.camera : null;

        private void OnDestroy()
        {
            if (screen != null)
                Destroy(screen.gameObject);
            if (screenRoot != null)
                Destroy(screenRoot);
        }

        // PlayerInput messages: only this player's devices send them.
        private void OnAttack(InputValue value) => RequestRespawn(value);

        private void OnInteract(InputValue value) => RequestRespawn(value);

        private void RequestRespawn(InputValue value)
        {
            if (enabled && value.isPressed && screen != null && screen.IsOpen && health.CanRespawn)
                respawnRequested = true;
        }

        private void Update()
        {
            if (screen == null || !screen.IsOpen)
                return;

            bool ready = health.CanRespawn;
            rebornImage.sprite = ready ? readySprite : lockedSprite;
            prompt.text = ready ? readyText : string.Format(waitingText, Mathf.CeilToInt(health.RespawnSecondsLeft));
        }

        // Late, so the button press that brings the player back has already gone by every other component (no
        // sword swing on the way back).
        private void LateUpdate()
        {
            if (!respawnRequested)
                return;
            respawnRequested = false;
            health.Respawn();
        }

        private void Show()
        {
            if (screen == null)
                Build();
            respawnRequested = false;
            screen.Open(selectFirst: false);
        }

        private void Hide()
        {
            if (screen != null)
                screen.Close();
        }

        /// <summary>Takes the screen away at once and for good (the match is over: its own screen takes over).</summary>
        public void Dismiss()
        {
            enabled = false;
            if (screenRoot != null)
                screenRoot.SetActive(false);
        }

        private void Build()
        {
            screen = Instantiate(screenPrefab);
            screen.name = $"{name} Death Screen";
            screen.Hide(screen.MainMenuButton);
            screen.Hide(screen.QuitButton);

            // A root canvas fitted to this player's view, holding the screen's own canvas whole (its animator's
            // paths stay intact), stretched over the view.
            screenRoot = new GameObject($"{name} Death Screen View");
            Canvas root = screenRoot.AddComponent<Canvas>();
            root.renderMode = RenderMode.ScreenSpaceOverlay;
            root.sortingOrder = sortingOrder;
            screenRoot.AddComponent<GraphicRaycaster>();
            RectTransform viewport = ViewportCanvas.Fit(root, viewCamera, 0f).Viewport;

            var content = (RectTransform)screen.Content.transform;
            if (content.TryGetComponent(out CanvasScaler scaler))
                scaler.enabled = false;
            content.SetParent(viewport, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.localScale = Vector3.one;

            // The button only shows the state; this player's own buttons press it.
            Button reborn = screen.RestartButton;
            reborn.enabled = false;
            rebornImage = reborn.GetComponent<Image>();
            lockedSprite = rebornImage.sprite;
            readySprite = reborn.spriteState.highlightedSprite != null ? reborn.spriteState.highlightedSprite : lockedSprite;

            prompt = NewPrompt((RectTransform)reborn.transform);
        }

        /// <summary>Countdown / prompt just under the Reborn button.</summary>
        private TMP_Text NewPrompt(RectTransform button)
        {
            var holder = new GameObject("Prompt", typeof(RectTransform));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(button.parent, false);
            rect.anchorMin = button.anchorMin;
            rect.anchorMax = button.anchorMax;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(Mathf.Max(button.rect.width * 1.6f, 300f), promptSize * 1.6f);
            rect.anchoredPosition = button.anchoredPosition + new Vector2(0f, -button.rect.height * (1f - button.pivot.y) - 8f);

            var text = holder.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            if (promptMaterial != null) text.fontSharedMaterial = promptMaterial;
            text.fontSize = promptSize;
            text.alignment = TextAlignmentOptions.Top;
            text.color = new Color(1f, 0.95f, 0.84f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
