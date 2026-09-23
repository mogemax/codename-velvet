using CodenameVelvet.Core.Events;
using UnityEngine;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Debug-only script that subscribes directly to the shared
    /// <see cref="GameEvent"/> asset and toggles a target renderer's color
    /// so the response is visible without opening the Console. Holds no
    /// reference to any raiser type — only to the shared channel.
    /// </summary>
    public class DummyListener : MonoBehaviour
    {
        [SerializeField] private GameEvent dummyEvent;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color raisedColor = Color.green;
        [SerializeField] private Color idleColor = Color.red;

        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            dummyEvent.OnRaised += HandleEventRaised;
            SetColor(idleColor);
        }

        private void OnDisable()
        {
            dummyEvent.OnRaised -= HandleEventRaised;
        }

        private void HandleEventRaised()
        {
            SetColor(raisedColor);
        }

        private void SetColor(Color color)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            // Set both property names so the response is visible regardless
            // of whether the assigned material uses URP Lit (_BaseColor) or
            // the legacy Standard shader (_Color).
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
