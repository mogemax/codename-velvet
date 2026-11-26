using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// The menu's cancel (B / Esc) presses its back button, wherever the UI focus is (a mouse click may have
    /// taken it off the button).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class BackOnCancel : MonoBehaviour
    {
        [SerializeField] private InputActionReference cancelAction;

        private Button button;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable()
        {
            if (cancelAction == null) return;
            cancelAction.action.performed += Press;
            cancelAction.action.Enable();
        }

        private void OnDisable()
        {
            if (cancelAction != null) cancelAction.action.performed -= Press;
        }

        private void Press(InputAction.CallbackContext _)
        {
            if (button.interactable) button.onClick.Invoke();
        }
    }
}
