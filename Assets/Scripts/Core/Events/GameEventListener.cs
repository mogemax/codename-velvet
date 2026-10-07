using UnityEngine;
using UnityEngine.Events;

namespace CodenameVelvet.Core.Events
{
    /// <summary>
    /// Inspector-wireable listener for a non-generic <see cref="GameEvent"/>.
    /// Registers on enable and unregisters on disable to avoid dangling
    /// references after scene reloads.
    /// </summary>
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private GameEvent gameEvent;
        [SerializeField] private UnityEvent response;

        private void OnEnable()
        {
            gameEvent.RegisterListener(this);
        }

        private void OnDisable()
        {
            gameEvent.UnregisterListener(this);
        }

        public void OnEventRaised()
        {
            response.Invoke();
        }
    }
}
