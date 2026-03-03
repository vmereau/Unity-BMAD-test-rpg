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
    /// Each event type needs its own CreateAssetMenu subclass (see GameEventSO_String example below).
    /// </summary>
    public abstract class GameEventSOBase : ScriptableObject { }

    public class GameEventSO<T> : GameEventSOBase
    {
        private const string TAG = "[Event]";

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

    // --- Concrete event types (add one per payload type needed) ---

    /// <summary>String event channel — used for OnEntityKilled, OnNPCDied, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/String Event", fileName = "NewStringEvent")]
    public class GameEventSO_String : GameEventSO<string> { }

    /// <summary>Int event channel — used for OnLevelUp, OnActAdvanced, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Int Event", fileName = "NewIntEvent")]
    public class GameEventSO_Int : GameEventSO<int> { }

    /// <summary>Bool event channel — used for OnDayNightChanged, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Bool Event", fileName = "NewBoolEvent")]
    public class GameEventSO_Bool : GameEventSO<bool> { }

    /// <summary>Void/signal event channel — used for OnPlayerDied, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Void Event", fileName = "NewVoidEvent")]
    public class GameEventSO_Void : GameEventSO<bool> { }
}
