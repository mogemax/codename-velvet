using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Keyboard-driven test mover for Debug_SpriteSorting.unity.
    /// WASD/arrows move ground position (X/Z) on the root transform.
    /// Space triggers a jump/roll arc that offsets ONLY the visual body's
    /// local Y position, leaving ground position (and therefore sort order)
    /// unaffected. If no "Body" child exists, falls back to offsetting the
    /// root transform itself. Not a real player controller.
    /// </summary>
    public class DebugJumpMover : MonoBehaviour
    {
        [SerializeField] private Transform visualBody;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float jumpDuration = 0.6f;

        private bool isJumping;
        private float jumpTimer;
        private float baseLocalY;

        private void Awake()
        {
            if (visualBody == null)
            {
                var found = transform.Find("Body");
                visualBody = found != null ? found : transform;
            }

            if (visualBody != null)
            {
                baseLocalY = visualBody.localPosition.y;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float h = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float v = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            Vector3 move = new Vector3(h, 0f, v);
            if (move.sqrMagnitude > 1f) move.Normalize();
            transform.position += move * (moveSpeed * Time.deltaTime);

            if (!isJumping && keyboard.spaceKey.wasPressedThisFrame)
            {
                isJumping = true;
                jumpTimer = 0f;
            }

            if (isJumping)
            {
                jumpTimer += Time.deltaTime;
                float t = Mathf.Clamp01(jumpTimer / jumpDuration);
                float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;

                if (visualBody != null)
                {
                    var localPos = visualBody.localPosition;
                    localPos.y = baseLocalY + arc;
                    visualBody.localPosition = localPos;
                }

                if (t >= 1f)
                {
                    isJumping = false;
                    if (visualBody != null)
                    {
                        var localPos = visualBody.localPosition;
                        localPos.y = baseLocalY;
                        visualBody.localPosition = localPos;
                    }
                }
            }
        }
    }
}
