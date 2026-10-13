using System.Collections.Generic;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Debug-only HP overlay for every <see cref="PlayerHealth"/> in the scene.
    /// Attach once to a scene object. Replaced by the real HUD in Phase 6.
    /// </summary>
    public class DebugPlayerHUD : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.5f;
        private const float LineHeight = 22f;
        private const float LineWidth = 200f;

        private readonly List<PlayerHealth> players = new List<PlayerHealth>();
        private readonly List<int> slots = new List<int>();
        private float nextRefreshTime;

        private void OnGUI()
        {
            if (Time.unscaledTime >= nextRefreshTime)
                Refresh();

            for (int i = 0; i < players.Count; i++)
            {
                PlayerHealth player = players[i];
                if (player == null)
                    continue;

                string label = $"P{slots[i] + 1}: HP {player.CurrentHp}/{player.MaxHp}";
                if (!player.IsAlive)
                    label += " DEAD";

                GUI.Label(new Rect(10f, 10f + i * LineHeight, LineWidth, LineHeight), label);
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
        }

        /// <summary>Unjoined players (no join flow yet) display as slot 0.</summary>
        private int SlotOf(PlayerHealth player)
        {
            PlayerIdentity identity = player.GetComponent<PlayerIdentity>();
            return identity != null && identity.IsInitialized ? identity.SlotIndex : 0;
        }
    }
}
