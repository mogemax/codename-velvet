using CodenameVelvet.Feedback;
using CodenameVelvet.Gathering;
using CodenameVelvet.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>One inventory slot of the HUD: the wooden frame, the resource's drawing and the count. Pops when something lands in it.</summary>
    public class HudSlot : MonoBehaviour
    {
        private Image icon;
        private TMP_Text count;
        private Spring pop;

        public void Setup(Image iconImage, TMP_Text countText)
        {
            icon = iconImage;
            count = countText;
        }

        public void Show(ResourceInventory.Slot slot, ResourceVisuals visuals)
        {
            ResourceVisuals.Entry look = default;
            bool hasLook = !slot.IsEmpty && visuals != null && visuals.TryGet(slot.Type, out look);
            if (hasLook)
            {
                icon.sprite = look.sprite;
                icon.color = look.tint;
            }
            icon.enabled = hasLook;
            count.text = slot.IsEmpty ? string.Empty : slot.Count.ToString();
        }

        public void Pop() => pop.Displace(0.25f, 0.5f);

        private void Update()
        {
            pop.Step(300f, 13f, Time.unscaledDeltaTime);
            float scale = 1f + pop.Value;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
