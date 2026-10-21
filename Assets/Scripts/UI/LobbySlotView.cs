using CodenameVelvet.Characters;
using CodenameVelvet.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Draws one CharacterMenu frame from the lobby state it is given. Holds no
    /// state of its own; <see cref="LobbyManager"/> calls <see cref="Show"/> after
    /// every change. Placeholder look: color swatch portrait plus text labels.
    /// </summary>
    public class LobbySlotView : MonoBehaviour
    {
        [Header("Frame")]
        [SerializeField] private Image frame;
        [SerializeField] private Sprite freeSprite;
        [SerializeField] private Sprite choosingSprite;
        [SerializeField] private Sprite lockedSprite;

        [Header("Content")]
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text playerLabel;
        [SerializeField] private TMP_Text characterName;
        [Tooltip("The < > hint; only visible while choosing.")]
        [SerializeField] private GameObject arrows;
        [SerializeField] private TMP_Text status;

        public void Show(int slot, LobbySlotState state, CharacterDefinition character, bool isHost, bool canStart)
        {
            bool joined = state != LobbySlotState.Free;

            frame.sprite = state switch
            {
                LobbySlotState.Choosing => choosingSprite,
                LobbySlotState.Locked => lockedSprite,
                _ => freeSprite,
            };

            playerLabel.text = isHost ? $"P{slot + 1} · Host" : $"P{slot + 1}";

            portrait.gameObject.SetActive(joined);
            characterName.gameObject.SetActive(joined);
            arrows.SetActive(state == LobbySlotState.Choosing);

            if (joined)
            {
                portrait.sprite = character.Portrait;
                portrait.color = character.Portrait != null ? Color.white : character.Color;
                characterName.text = character.DisplayName;
            }

            status.text = StatusText(state, isHost, canStart);
        }

        private static string StatusText(LobbySlotState state, bool isHost, bool canStart)
        {
            switch (state)
            {
                case LobbySlotState.Choosing:
                    return "A / Espacio: confirmar\nB / Esc: salir";
                case LobbySlotState.Locked when isHost && canStart:
                    return "¡Listo!\nStart / Enter: comenzar";
                case LobbySlotState.Locked when isHost:
                    return "¡Listo!\nEsperando a los demás";
                case LobbySlotState.Locked:
                    return "¡Listo!\nB / Esc: cambiar";
                default:
                    return "Presiona A o Espacio\npara unirte";
            }
        }
    }
}
