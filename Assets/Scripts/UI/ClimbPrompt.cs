using CodenameVelvet.Enemies;
using CodenameVelvet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// The Interact key, bobbing over the player's head while a ladder can be climbed; otherwise, a call to the boss
    /// arena while the fight waits for everyone. Lives on an overlay Canvas child of the player, placed through that
    /// player's own camera so it also works in split screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after CameraFollow has moved the camera this frame
    public class ClimbPrompt : MonoBehaviour
    {
        [Header("Climb key")]
        [SerializeField] private RectTransform key;
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("How far it moves up and down, in canvas units.")]
        [SerializeField, Min(0f)] private float bobHeight = 12f;
        [Tooltip("Up-and-down cycles per second.")]
        [SerializeField, Min(0f)] private float bobSpeed = 1.2f;

        [Header("Message")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text label;
        [Tooltip("Height above the player's feet.")]
        [SerializeField] private float height = 6f;

        private PlayerClimb climb;
        private PlayerInput input;
        private BossEncounter encounter; // only in boss levels
        private Canvas canvas;

        private void Awake()
        {
            climb = GetComponentInParent<PlayerClimb>();
            input = GetComponentInParent<PlayerInput>();
            encounter = FindFirstObjectByType<BossEncounter>();
            canvas = GetComponent<Canvas>();
            key.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            Camera cam = input.camera != null ? input.camera : Camera.main;
            bool canClimb = cam != null && climb.CanClimb;
            bool waiting = cam != null && !canClimb && encounter != null && encounter.WaitingForPlayers;

            // The player's own device: E on keyboard, the north button on a gamepad.
            if (canClimb && !key.gameObject.activeSelf)
                keyLabel.text = input.actions["Interact"].GetBindingDisplayString(group: input.currentControlScheme);
            if (waiting && !panel.gameObject.activeSelf)
                label.text = "All players must go to the boss arena";

            key.gameObject.SetActive(canClimb);
            panel.gameObject.SetActive(waiting);
            if (!canClimb && !waiting) return;

            Vector2 position = cam.WorldToScreenPoint(climb.transform.position + Vector3.up * height);
            panel.position = position;
            key.position = position + Vector2.up * (Mathf.Sin(Time.time * bobSpeed * 2f * Mathf.PI) * bobHeight * canvas.scaleFactor);
        }
    }
}
