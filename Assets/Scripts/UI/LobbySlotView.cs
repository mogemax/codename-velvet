using CodenameVelvet.Characters;
using CodenameVelvet.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Draws one CharacterMenu frame from the lobby state it is given. Holds no
    /// state of its own; <see cref="LobbyManager"/> calls <see cref="Show"/> after
    /// every change. The player badge (P1-P4), the arrows and the choosing hint are drawings placed in the scene;
    /// the other states' hints are text for now.
    /// </summary>
    public class LobbySlotView : MonoBehaviour
    {
        [Header("Frame")]
        [SerializeField] private Image frame;
        [Tooltip("Same frame in every state (free, choosing, locked).")]
        [FormerlySerializedAs("choosingSprite")]
        [SerializeField] private Sprite frameSprite;

        [Header("Content")]
        [SerializeField] private Image portrait;
        [Tooltip("Fill of an empty slot; matches the banners' background.")]
        [SerializeField] private Color emptyColor = new Color32(0xAA, 0x5D, 0x01, 0xFF);
        [Tooltip("The < > hint; only visible while choosing.")]
        [SerializeField] private GameObject arrows;
        [Tooltip("The confirm / leave plate; shown instead of the status text while choosing.")]
        [SerializeField] private GameObject choosingHint;
        [SerializeField] private TMP_Text status;

        public void Show(int slot, LobbySlotState state, CharacterDefinition character, bool isHost, bool canStart)
        {
            bool joined = state != LobbySlotState.Free;

            frame.sprite = frameSprite;

            bool choosing = state == LobbySlotState.Choosing;
            arrows.SetActive(choosing);
            if (choosingHint != null) choosingHint.SetActive(choosing);
            status.gameObject.SetActive(!choosing || choosingHint == null);

            portrait.gameObject.SetActive(true);
            portrait.sprite = joined ? character.Portrait : null;
            portrait.color = !joined ? emptyColor
                : character.Portrait != null ? Color.white : character.Color;

            status.text = StatusText(state, isHost, canStart);
        }

        private static string StatusText(LobbySlotState state, bool isHost, bool canStart)
        {
            switch (state)
            {
                case LobbySlotState.Choosing:
                    return "A / Space: confirm\nB / Esc: leave";
                case LobbySlotState.Locked when isHost && canStart:
                    return "Ready!\nStart / Enter: start";
                case LobbySlotState.Locked when isHost:
                    return "Ready!\nWaiting for the others";
                case LobbySlotState.Locked:
                    return "Ready!\nB / Esc: change";
                default:
                    return "Press A or Space\nto join";
            }
        }
    }
}
