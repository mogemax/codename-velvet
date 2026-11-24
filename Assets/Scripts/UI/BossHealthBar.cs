using System.Collections.Generic;
using CodenameVelvet.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Boss health bar across the top of every player's view, with the boss name above it.
    /// Builds its own overlay Canvas at runtime, so every look setting lives on this
    /// component and can be tweaked in the Inspector, even during Play mode.
    /// Hidden until the boss first sees a player, then stays until the boss dies.
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

        private sealed class Bar
        {
            public float Scale;
            public RectTransform Panel;
            public RectTransform Track;
            public RectTransform Fill;
            public Image BorderImage;
            public Image TrackImage;
            public Image FillImage;
            public TextMeshProUGUI NameText;
        }

        private readonly List<Bar> bars = new List<Bar>(); // one per view
        private GameObject canvasRoot; // built when the boss first sees a player, so every view exists

        private void Awake()
        {
            if (health == null)
                Debug.LogWarning($"{name}: BossHealthBar has no EnemyHealth assigned; the bar stays hidden.", this);
        }

        private void Update()
        {
            // EnemyHealth destroys the boss on death, which nulls the reference.
            if (health == null)
            {
                if (canvasRoot != null) canvasRoot.SetActive(false);
                enabled = false;
                return;
            }

            if (canvasRoot == null)
            {
                if (brain != null && !brain.HasTarget) return;
                Build();
                ApplyStyle();
            }

            float fraction = health.MaxHp > 0 ? (float)health.CurrentHp / health.MaxHp : 1f;
            foreach (Bar bar in bars)
                bar.Fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
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

            foreach (PlayerInput player in PlayerInput.all)
                if (player.camera != null) bars.Add(BuildBar(player.camera.rect));
            if (bars.Count == 0) bars.Add(BuildBar(new Rect(0f, 0f, 1f, 1f))); // no split screen
        }

        /// <summary>A bar at the top of a view (normalized screen rect, like <see cref="Camera.rect"/>).</summary>
        private Bar BuildBar(Rect viewport)
        {
            // Shrinks less than the view itself so the name stays readable.
            var bar = new Bar { Scale = Mathf.Sqrt(Mathf.Min(viewport.width, viewport.height)) };

            bar.Panel = CreateRect("Panel", canvasRoot.transform);
            bar.Panel.anchorMin = bar.Panel.anchorMax = new Vector2(viewport.center.x, viewport.yMax);
            bar.Panel.pivot = new Vector2(0.5f, 1f);
            bar.Panel.localScale = Vector3.one * bar.Scale;

            bar.NameText = CreateRect("Name", bar.Panel).gameObject.AddComponent<TextMeshProUGUI>();
            RectTransform nameRect = bar.NameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = Vector2.one;
            nameRect.pivot = new Vector2(0.5f, 1f);
            bar.NameText.alignment = TextAlignmentOptions.Bottom;
            bar.NameText.raycastTarget = false;

            RectTransform border = CreateRect("Border", bar.Panel);
            border.anchorMin = Vector2.zero;
            border.anchorMax = new Vector2(1f, 0f);
            border.pivot = new Vector2(0.5f, 0f);
            bar.BorderImage = AddImage(border);

            bar.Track = CreateRect("Track", border);
            bar.Track.anchorMin = Vector2.zero;
            bar.Track.anchorMax = Vector2.one;
            bar.TrackImage = AddImage(bar.Track);

            bar.Fill = CreateRect("Fill", bar.Track);
            bar.Fill.anchorMin = Vector2.zero;
            bar.Fill.anchorMax = Vector2.one;
            bar.Fill.offsetMin = bar.Fill.offsetMax = Vector2.zero;
            bar.FillImage = AddImage(bar.Fill);
            return bar;
        }

        private void ApplyStyle()
        {
            float nameHeight = fontSize * 1.3f;
            float borderedHeight = barHeight + borderThickness * 2f;

            foreach (Bar bar in bars)
            {
                bar.Panel.anchoredPosition = new Vector2(0f, -topMargin * bar.Scale);
                bar.Panel.sizeDelta = new Vector2(barWidth + borderThickness * 2f, nameHeight + borderedHeight);

                bar.NameText.rectTransform.sizeDelta = new Vector2(0f, nameHeight);
                bar.NameText.text = bossName;
                bar.NameText.font = font != null ? font : TMP_Settings.defaultFontAsset;
                bar.NameText.fontSize = fontSize;
                bar.NameText.fontStyle = fontStyle;
                bar.NameText.color = textColor;

                ((RectTransform)bar.BorderImage.transform).sizeDelta = new Vector2(0f, borderedHeight);
                bar.Track.offsetMin = new Vector2(borderThickness, borderThickness);
                bar.Track.offsetMax = new Vector2(-borderThickness, -borderThickness);

                bar.BorderImage.color = borderColor;
                bar.TrackImage.color = backgroundColor;
                bar.FillImage.color = fillColor;
            }
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
