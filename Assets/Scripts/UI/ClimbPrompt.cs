using CodenameVelvet.Enemies;
using CodenameVelvet.Map;
using CodenameVelvet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// The Interact key, bobbing over the player's head while a ladder can be climbed or a boat can be used (what it
    /// has so far goes under the key); otherwise, a call to the boss arena while the fight waits for everyone, or a
    /// reminder to repair the boat first near a closed arena. Lives
    /// on an overlay Canvas child of the player, placed through that player's own camera so it also works in split screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after CameraFollow has moved the camera this frame
    public class ClimbPrompt : MonoBehaviour
    {
        [Header("Interact key")]
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
        private PlayerBoatBuilder builder;
        private PlayerInput input;
        private BossEncounter encounter; // only in boss levels
        private Canvas canvas;

        private void Awake()
        {
            climb = GetComponentInParent<PlayerClimb>();
            builder = GetComponentInParent<PlayerBoatBuilder>();
            input = GetComponentInParent<PlayerInput>();
            encounter = FindFirstObjectByType<BossEncounter>();
            canvas = GetComponent<Canvas>();
            key.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            Camera cam = input.camera != null ? input.camera : Camera.main;
            Boat boat = cam != null ? builder.NearbyBoat : null;
            bool canUseBoat = boat != null && boat.CanUse;
            bool showKey = cam != null && (climb.CanClimb || canUseBoat);
            bool waiting = cam != null && !showKey && boat == null && encounter != null && encounter.WaitingForPlayers;
            bool gated = cam != null && !showKey && boat == null && encounter != null && encounter.IsGatedFor(climb.transform.position);

            // The player's own device: E on keyboard, the north button on a gamepad.
            if (showKey && !key.gameObject.activeSelf)
                keyLabel.text = input.actions["Interact"].GetBindingDisplayString(group: input.currentControlScheme);
            if (boat != null)
                label.text = !boat.IsBuilt ? boat.Status
                    : !boat.CanSail ? "The boat is repaired. Go defeat the boss!"
                    : canUseBoat ? "The boat is ready. Set sail!"
                    : "All players must come to the boat";
            else if (waiting)
                label.text = "All players must go to the boss arena";
            else if (gated)
                label.text = "You must repair the boat first";

            key.gameObject.SetActive(showKey);
            panel.gameObject.SetActive(boat != null || waiting || gated);
            if (!key.gameObject.activeSelf && !panel.gameObject.activeSelf) return;

            Vector2 position = cam.WorldToScreenPoint(climb.transform.position + Vector3.up * height);
            panel.position = position;
            float lift = boat != null ? panel.rect.height * 0.5f + bobHeight : 0f; // above the panel, not over it
            lift += Mathf.Sin(Time.time * bobSpeed * 2f * Mathf.PI) * bobHeight;
            key.position = position + Vector2.up * (lift * canvas.scaleFactor);
        }
    }
}
