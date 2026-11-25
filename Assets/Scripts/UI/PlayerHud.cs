using System.Collections.Generic;
using CodenameVelvet.Feedback;
using CodenameVelvet.Gathering;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// This player's HUD, inside their own part of the split screen: hearts at the top left (one per HP, full or
    /// lost) and the inventory slots at the bottom center (drawing + count). Built when the player starts, on an
    /// overlay canvas fitted to their camera's viewport (overlay, so night and dusk post-processing never tint it).
    /// It scales with the viewport, and sits under the pause menu.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerHud : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;

        [Header("Art")]
        [SerializeField] private Sprite heartFull;
        [SerializeField] private Sprite heartLost;
        [SerializeField] private Sprite slotFrame;
        [SerializeField] private ResourceVisuals visuals;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Material preset of the font with an outline, so the count reads over any ground.")]
        [SerializeField] private Material countMaterial;

        [Header("Layout (pixels at 1080p, full screen)")]
        [SerializeField, Min(8f)] private float heartSize = 76f;
        [SerializeField] private float heartSpacing = 6f;
        [SerializeField, Min(8f)] private float slotSize = 112f;
        [SerializeField] private float slotSpacing = 12f;
        [SerializeField] private float margin = 28f;
        [SerializeField, Min(8f)] private float countSize = 40f;
        [SerializeField] private Color countColor = new Color(1f, 0.95f, 0.84f);
        [Tooltip("Size of the resource drawing inside the frame.")]
        [SerializeField, Range(0.3f, 1f)] private float iconFill = 0.72f;
        [SerializeField] private int sortingOrder = -10;

        [Header("Low health")]
        [SerializeField, Range(0f, 1f)] private float lowHealthFraction = 0.3f;
        [SerializeField, Min(0.1f)] private float heartbeatRate = 1.3f;
        [SerializeField, Range(0f, 0.5f)] private float heartbeatSize = 0.18f;

        private readonly List<HudHeart> hearts = new List<HudHeart>();
        private readonly List<HudSlot> slots = new List<HudSlot>();
        private PlayerHealth health;
        private PlayerInventory inventory;
        private Camera viewCamera;
        private GameObject canvasRoot;
        private RectTransform viewport;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            inventory = GetComponent<PlayerInventory>();
        }

        private void Start()
        {
            // The spawner gives the player its camera right after creating it, before Start.
            viewCamera = TryGetComponent(out PlayerInput input) ? input.camera : null;
            Build();
            RefreshHearts(animate: false);
            RefreshSlots();
            if (inventory != null)
            {
                inventory.Changed += RefreshSlots;
                inventory.Added += PopSlot;
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= RefreshSlots;
                inventory.Added -= PopSlot;
            }
            if (canvasRoot != null)
                Destroy(canvasRoot);
        }

        private void Update()
        {
            if (viewport == null)
                return;
            RefreshHearts(animate: true);
            BeatLastHeart();
        }

        // ---- Hearts --------------------------------------------------------------------------------------------

        /// <summary>Polled: catches every change (hits, food, respawn refill) without extra events.</summary>
        private void RefreshHearts(bool animate)
        {
            for (int i = 0; i < hearts.Count; i++)
                hearts[i].SetFull(i < health.CurrentHp, animate);
        }

        private void BeatLastHeart()
        {
            bool low = FeedbackRules.IsLowHealth(health.CurrentHp, health.MaxHp, lowHealthFraction);
            float beat = low ? heartbeatSize * FeedbackRules.Heartbeat(Time.time, heartbeatRate) : 0f;
            for (int i = 0; i < hearts.Count; i++)
                hearts[i].Beat(i == health.CurrentHp - 1 ? beat : 0f);
        }

        // ---- Slots ---------------------------------------------------------------------------------------------

        private void RefreshSlots()
        {
            if (inventory == null)
                return;
            IReadOnlyList<ResourceInventory.Slot> contents = inventory.Slots;
            for (int i = 0; i < slots.Count && i < contents.Count; i++)
                slots[i].Show(contents[i], visuals);
        }

        private void PopSlot(ResourceType type, int amount)
        {
            IReadOnlyList<ResourceInventory.Slot> contents = inventory.Slots;
            for (int i = 0; i < slots.Count && i < contents.Count; i++)
                if (!contents[i].IsEmpty && contents[i].Type == type)
                    slots[i].Pop();
        }

        // ---- Building ------------------------------------------------------------------------------------------

        private void Build()
        {
            canvasRoot = new GameObject($"{name} HUD");
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            viewport = ViewportCanvas.Fit(canvas, viewCamera, ReferenceHeight).Viewport;

            for (int i = 0; i < health.MaxHp; i++)
            {
                RectTransform rect = NewRect($"Heart {i + 1}", viewport);
                Place(rect, new Vector2(0f, 1f), new Vector2(margin + heartSize * 0.5f + i * (heartSize + heartSpacing), -margin - heartSize * 0.5f), heartSize);
                HudHeart heart = rect.gameObject.AddComponent<HudHeart>();
                heart.Setup(NewImage(rect, heartFull), heartFull, heartLost);
                hearts.Add(heart);
            }

            int slotCount = inventory != null ? inventory.Slots.Count : 0;
            float rowWidth = slotCount * slotSize + (slotCount - 1) * slotSpacing;
            for (int i = 0; i < slotCount; i++)
            {
                RectTransform rect = NewRect($"Slot {i + 1}", viewport);
                float x = -rowWidth * 0.5f + slotSize * 0.5f + i * (slotSize + slotSpacing);
                Place(rect, new Vector2(0.5f, 0f), new Vector2(x, margin + slotSize * 0.5f), slotSize);
                NewImage(rect, slotFrame);

                RectTransform iconRect = NewRect("Icon", rect);
                Place(iconRect, new Vector2(0.5f, 0.5f), Vector2.zero, slotSize * iconFill);
                Image icon = NewImage(iconRect, null);

                RectTransform countRect = NewRect("Count", rect);
                countRect.anchorMin = Vector2.zero;
                countRect.anchorMax = Vector2.one;
                countRect.offsetMin = new Vector2(0f, slotSize * 0.06f);
                countRect.offsetMax = new Vector2(-slotSize * 0.12f, 0f);
                TextMeshProUGUI count = countRect.gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) count.font = font;
                if (countMaterial != null) count.fontSharedMaterial = countMaterial;
                count.fontSize = countSize;
                count.alignment = TextAlignmentOptions.BottomRight;
                count.fontStyle = FontStyles.Bold;
                count.color = countColor;
                count.raycastTarget = false;

                HudSlot slot = rect.gameObject.AddComponent<HudSlot>();
                slot.Setup(icon, count);
                slots.Add(slot);
            }
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            var holder = new GameObject(objectName, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            return (RectTransform)holder.transform;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, float size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
        }

        private static Image NewImage(RectTransform rect, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
