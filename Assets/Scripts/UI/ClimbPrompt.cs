using CodenameVelvet.Enemies;
using CodenameVelvet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// "Press E to climb" over the player's head while a ladder can be climbed; otherwise, a call to the boss
    /// arena while the fight waits for everyone. Lives on an overlay Canvas child of the player, placed through that
    /// player's own camera so it also works in split screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after CameraFollow has moved the camera this frame
    public class ClimbPrompt : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text label;
        [Tooltip("Height above the player's feet.")]
        [SerializeField] private float height = 6f;

        private PlayerClimb climb;
        private PlayerInput input;
        private BossEncounter encounter; // only in boss levels
        private bool couldClimb;

        private void Awake()
        {
            climb = GetComponentInParent<PlayerClimb>();
            input = GetComponentInParent<PlayerInput>();
            encounter = FindFirstObjectByType<BossEncounter>();
            panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            Camera cam = input.camera != null ? input.camera : Camera.main;
            bool canClimb = climb.CanClimb;
            bool waiting = encounter != null && encounter.WaitingForPlayers;
            bool show = cam != null && (canClimb || waiting);

            if (canClimb && !couldClimb)
            {
                // The player's own device: E on keyboard, the north button on a gamepad.
                string key = input.actions["Interact"].GetBindingDisplayString(group: input.currentControlScheme);
                label.text = $"Press <b>{key}</b> to climb";
            }
            else if (waiting && !canClimb)
            {
                label.text = "All players must go to the boss arena";
            }
            couldClimb = canClimb;

            panel.gameObject.SetActive(show);
            if (show) panel.position = (Vector2)cam.WorldToScreenPoint(climb.transform.position + Vector3.up * height);
        }
    }
}
