using CodenameVelvet.Core.Events;
using UnityEngine;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Debug-only script that raises the shared <see cref="GameEvent"/> asset.
    /// Holds no reference to any listener type — only to the shared channel.
    /// </summary>
    public class DummyRaiser : MonoBehaviour
    {
        [SerializeField] private GameEvent dummyEvent;

        [ContextMenu("Raise Event")]
        public void RaiseEvent()
        {
            dummyEvent.Raise();
        }
    }
}
