using CodenameVelvet.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Boss health bar across the top of the screen, with the boss name above it.
    /// Builds its own overlay Canvas at runtime, so every look setting lives on this
    /// component and can be tweaked in the Inspector, even during Play mode.
    /// Hidden until the boss first sees a player, then stays until the boss dies.
    /// shortcut: one overlay for the whole screen; when split-screen lands it reads as
    /// a single shared bar over every view, which is fine for a boss fight.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        [Header("Boss")]
        [SerializeField] private EnemyHealth health;
        [Tooltip("Shows the bar once this brain sees a player. Empty = shown from the start.")]
        [SerializeField] private CyclopsBrain brain;
        [SerializeField] private string bossName = "Cíclope";

        [Header("Texto")]
        [Tooltip("Empty = TextMesh Pro's default font.")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float fontSize = 36f;
        [SerializeField] private FontStyles fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        [SerializeField] private Color textColor = new Color(0.95f, 0.9f, 0.8f);

        [Header("Barra")]
        [SerializeField] private Color fillColor = new Color(0.7f, 0.08f, 0.08f);
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.05f, 0.05f, 0.85f);
        [SerializeField] private Color borderColor = new Color(0.75f, 0.62f, 0.4f);
        [SerializeField, Min(0f)] private float borderThickness = 3f;

        [Header("Tamaño y posición (en una pantalla de 1920×1080)")]
        [SerializeField, Min(1f)] private float barWidth = 900f;
        [SerializeField, Min(1f)] private float barHeight = 20f;
        [Tooltip("Gap from the top edge; leaves room for the day/night clock above.")]
        [SerializeField, Min(0f)] private float topMargin = 75f;

        private GameObject canvasRoot;
        private RectTransform panel;
        private RectTransform track;
        private RectTransform fill;
        private Image borderImage;
        private Image trackImage;
        private Image fillImage;
        private TextMeshProUGUI nameText;
        private bool hasBeenSeen;

        private void Awake()
        {
            if (health == null)
                Debug.LogWarning($"{name}: BossHealthBar has no EnemyHealth assigned; the bar stays hidden.", this);

            Build();
            ApplyStyle();
            canvasRoot.SetActive(false);
        }

        private void Update()
        {
            // EnemyHealth destroys the boss on death, which nulls the reference.
            if (health == null)
            {
                canvasRoot.SetActive(false);
                enabled = false;
                return;
            }

            hasBeenSeen |= brain == null || brain.HasTarget;
            canvasRoot.SetActive(hasBeenSeen);

            float fraction = health.MaxHp > 0 ? (float)health.CurrentHp / health.MaxHp : 1f;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        private void OnValidate()
        {
            if (canvasRoot != null)
                ApplyStyle();
        }

        private void Build()
        {
            canvasRoot = new GameObject("BossHealthBarCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(transform, false);

            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreateRect("Panel", canvasRoot.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);

            nameText = CreateRect("Name", panel).gameObject.AddComponent<TextMeshProUGUI>();
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = Vector2.one;
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameText.alignment = TextAlignmentOptions.Bottom;
            nameText.raycastTarget = false;

            RectTransform border = CreateRect("Border", panel);
            border.anchorMin = Vector2.zero;
            border.anchorMax = new Vector2(1f, 0f);
            border.pivot = new Vector2(0.5f, 0f);
            borderImage = AddImage(border);

            track = CreateRect("Track", border);
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.one;
            trackImage = AddImage(track);

            fill = CreateRect("Fill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage = AddImage(fill);
        }

        private void ApplyStyle()
        {
            float nameHeight = fontSize * 1.3f;
            float borderedHeight = barHeight + borderThickness * 2f;

            panel.anchoredPosition = new Vector2(0f, -topMargin);
            panel.sizeDelta = new Vector2(barWidth + borderThickness * 2f, nameHeight + borderedHeight);

            nameText.rectTransform.sizeDelta = new Vector2(0f, nameHeight);
            nameText.text = bossName;
            nameText.font = font != null ? font : TMP_Settings.defaultFontAsset;
            nameText.fontSize = fontSize;
            nameText.fontStyle = fontStyle;
            nameText.color = textColor;

            ((RectTransform)borderImage.transform).sizeDelta = new Vector2(0f, borderedHeight);
            track.offsetMin = new Vector2(borderThickness, borderThickness);
            track.offsetMax = new Vector2(-borderThickness, -borderThickness);

            borderImage.color = borderColor;
            trackImage.color = backgroundColor;
            fillImage.color = fillColor;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image AddImage(RectTransform rect)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
