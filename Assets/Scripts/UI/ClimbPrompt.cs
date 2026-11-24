using CodenameVelvet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// "Press E to climb" over the player's head while a ladder can be climbed. Lives on an overlay Canvas child of
    /// the player, placed through that player's own camera so it also works in split screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after CameraFollow has moved the camera this frame
    public class ClimbPrompt : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text label;
        [Tooltip("Height above the player's feet.")]
        [SerializeField] private float height = 6f;

        [Header("Key icon (keyboard players only)")]
        [SerializeField] private RectTransform icon;
        [Tooltip("How far it moves up and down, in canvas units.")]
        [SerializeField, Min(0f)] private float bobHeight = 12f;
        [Tooltip("Up-and-down cycles per second.")]
        [SerializeField, Min(0f)] private float bobSpeed = 1.2f;

        private PlayerClimb climb;
        private PlayerInput input;
        private Vector2 iconRest;

        private void Awake()
        {
            climb = GetComponentInParent<PlayerClimb>();
            input = GetComponentInParent<PlayerInput>();
            iconRest = icon.anchoredPosition;
            panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            Camera cam = input.camera != null ? input.camera : Camera.main;
            bool show = cam != null && climb.CanClimb;

            if (show && !panel.gameObject.activeSelf)
            {
                // The player's own device: E on keyboard, the north button on a gamepad.
                string key = input.actions["Interact"].GetBindingDisplayString(group: input.currentControlScheme);
                label.text = $"Press <b>{key}</b> to climb";
                icon.gameObject.SetActive(input.GetDevice<Keyboard>() != null);
            }

            panel.gameObject.SetActive(show);
            if (!show) return;

            icon.anchoredPosition = iconRest + Vector2.up * (Mathf.Sin(Time.time * bobSpeed * 2f * Mathf.PI) * bobHeight);
            panel.position = (Vector2)cam.WorldToScreenPoint(climb.transform.position + Vector3.up * height);
        }
    }
}
