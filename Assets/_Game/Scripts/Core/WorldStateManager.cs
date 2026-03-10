using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Central runtime state manager. Story 2.8: Minimal stub (singleton + kill tracking).
    /// Story 2.9 adds: OnEntityKilled event wiring.
    /// Epic 8: Save/Load, Steam Cloud sync.
    /// Attach to the WorldStateManager GameObject in Core.unity.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        public static WorldStateManager Instance { get; private set; }

        private readonly HashSet<string> _killedEntities = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                GameLog.Warn(TAG, "Duplicate WorldStateManager detected — destroying new instance");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool IsKilled(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                GameLog.Warn(TAG, "IsKilled called with null or empty GUID");
                return false;
            }
            return _killedEntities.Contains(guid);
        }

        public void RegisterKill(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                GameLog.Warn(TAG, "RegisterKill called with null or empty GUID");
                return;
            }
            _killedEntities.Add(guid);
            GameLog.Info(TAG, $"Entity killed: {guid}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
