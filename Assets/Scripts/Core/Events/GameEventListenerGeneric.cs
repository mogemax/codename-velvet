using UnityEngine;
using UnityEngine.Events;

namespace CodenameVelvet.Core.Events
{
    /// <summary>
    /// Inspector-wireable listener for a generic <see cref="GameEvent{T}"/>.
    /// Mirrors the non-generic listener's OnEnable/OnDisable pairing.
    /// </summary>
    public abstract class GameEventListener<T> : MonoBehaviour
    {
        [SerializeField] private GameEvent<T> gameEvent;
        [SerializeField] private UnityEvent<T> response;

        private void OnEnable()
        {
            gameEvent.RegisterListener(this);
        }

        private void OnDisable()
        {
            gameEvent.UnregisterListener(this);
        }

        public void OnEventRaised(T value)
        {
            response.Invoke(value);
        }
    }
}
