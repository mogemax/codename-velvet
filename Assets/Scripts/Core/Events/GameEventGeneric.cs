using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Core.Events
{
    /// <summary>
    /// Abstract base for ScriptableObject event channels that carry a payload.
    /// Concrete subclasses (e.g. an int or enum event) are created by the
    /// phase that first needs one — no concrete asset ships with Phase 1.
    /// </summary>
    public abstract class GameEvent<T> : ScriptableObject
    {
        private readonly List<GameEventListener<T>> listeners = new List<GameEventListener<T>>();

        public event Action<T> OnRaised;

        public void Raise(T value)
        {
            OnRaised?.Invoke(value);

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i].OnEventRaised(value);
            }
        }

        public void RegisterListener(GameEventListener<T> listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        public void UnregisterListener(GameEventListener<T> listener)
        {
            listeners.Remove(listener);
        }
    }
}
