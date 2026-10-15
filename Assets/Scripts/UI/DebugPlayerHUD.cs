using System.Collections.Generic;
using System.Text;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Debug-only HP overlay for every <see cref="PlayerHealth"/> in the scene, with the
    /// player's inventory slots on the line below when it has a <see cref="PlayerInventory"/>.
    /// Attach once to a scene object. Replaced by the real HUD in Phase 6.
    /// </summary>
    public class DebugPlayerHUD : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.5f;
        private const float LineHeight = 22f;
        private const float LineWidth = 400f;

        private readonly List<PlayerHealth> players = new List<PlayerHealth>();
        private readonly List<int> slots = new List<int>();
        private readonly List<PlayerInventory> inventories = new List<PlayerInventory>();
        private readonly StringBuilder inventoryText = new StringBuilder();
        private float nextRefreshTime;

        private void OnGUI()
        {
            if (Time.unscaledTime >= nextRefreshTime)
                Refresh();

            float y = 10f;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerHealth player = players[i];
                if (player == null)
                    continue;

                string label = $"P{slots[i] + 1}: HP {player.CurrentHp}/{player.MaxHp}";
                if (!player.IsAlive)
                    label += " DEAD";

                GUI.Label(new Rect(10f, y, LineWidth, LineHeight), label);
                y += LineHeight;

                if (inventories[i] == null)
                    continue;

                GUI.Label(new Rect(10f, y, LineWidth, LineHeight), InventoryLabel(inventories[i]));
                y += LineHeight;
            }
        }

        private void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;

            players.Clear();
            players.AddRange(FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None));
            players.Sort((a, b) => SlotOf(a).CompareTo(SlotOf(b)));

            slots.Clear();
            foreach (PlayerHealth player in players)
                slots.Add(SlotOf(player));

            inventories.Clear();
            foreach (PlayerHealth player in players)
                inventories.Add(player.GetComponent<PlayerInventory>());
        }

        /// <summary>Formats slots as "  Wood 12/20 · Stone 3/20 · – · –".</summary>
        private string InventoryLabel(PlayerInventory inventory)
        {
            inventoryText.Clear();
            inventoryText.Append("  ");

            IReadOnlyList<ResourceInventory.Slot> inventorySlots = inventory.Slots;
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (i > 0)
                    inventoryText.Append(" · ");

                ResourceInventory.Slot slot = inventorySlots[i];
                if (slot.IsEmpty)
                    inventoryText.Append('–');
                else
                    inventoryText.Append(slot.Type).Append(' ').Append(slot.Count).Append('/').Append(inventory.MaxPerSlot);
            }

            return inventoryText.ToString();
        }

        /// <summary>Unjoined players (no join flow yet) display as slot 0.</summary>
        private int SlotOf(PlayerHealth player)
        {
            PlayerIdentity identity = player.GetComponent<PlayerIdentity>();
            return identity != null && identity.IsInitialized ? identity.SlotIndex : 0;
        }
    }
}
