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

        private PlayerClimb climb;
        private PlayerInput input;

        private void Awake()
        {
            climb = GetComponentInParent<PlayerClimb>();
            input = GetComponentInParent<PlayerInput>();
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
            }

            panel.gameObject.SetActive(show);
            if (show) panel.position = (Vector2)cam.WorldToScreenPoint(climb.transform.position + Vector3.up * height);
        }
    }
}
