using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Typed ScriptableObject event channel for decoupled cross-system communication.
    ///
    /// USAGE — Raising an event:
    ///   [SerializeField] private GameEventSO<string> _onEntityKilled;
    ///   _onEntityKilled.Raise(persistentID);
    ///
    /// USAGE — Listening to an event (always OnEnable/OnDisable):
    ///   private void OnEnable() => _onEntityKilled.AddListener(HandleEntityKilled);
    ///   private void OnDisable() => _onEntityKilled.RemoveListener(HandleEntityKilled);
    ///   private void HandleEntityKilled(string id) { ... }
    ///
    /// To create a concrete event asset: right-click in Project → Create → Game/Events → [EventName]
    /// Concrete types are in their own files: GameEventSO_String, GameEventSO_Int, GameEventSO_Bool, GameEventSO_Void.
    /// </summary>
    public abstract class GameEventSOBase : ScriptableObject { }

    public abstract class GameEventSO<T> : GameEventSOBase
    {
        private readonly List<Action<T>> _listeners = new List<Action<T>>();

        public void Raise(T payload)
        {
            // Iterate in reverse so listeners can unsubscribe safely during dispatch
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke(payload);
            }
        }

        public void AddListener(Action<T> listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void RemoveListener(Action<T> listener)
        {
            _listeners.Remove(listener);
        }
    }
}
