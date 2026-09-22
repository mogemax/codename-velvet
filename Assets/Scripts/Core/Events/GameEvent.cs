using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Core.Events
{
    /// <summary>
    /// Non-generic ScriptableObject event channel. Raisers and listeners
    /// reference only this asset — never each other — keeping cross-domain
    /// communication fully decoupled.
    /// </summary>
    [CreateAssetMenu(menuName = "Events/Game Event", fileName = "New GameEvent")]
    public class GameEvent : ScriptableObject
    {
        private readonly List<GameEventListener> listeners = new List<GameEventListener>();

        public event Action OnRaised;

        public void Raise()
        {
            OnRaised?.Invoke();

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i].OnEventRaised();
            }
        }

        public void RegisterListener(GameEventListener listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        public void UnregisterListener(GameEventListener listener)
        {
            listeners.Remove(listener);
        }
    }
}
