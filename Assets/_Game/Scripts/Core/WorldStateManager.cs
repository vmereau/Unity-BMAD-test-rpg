using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Central runtime state manager. Story 2.8: Minimal stub (singleton + kill tracking).
    /// Story 2.9 adds: OnEntityKilled event wiring.
    /// World Facts extension: flat key/bool store for quest steps, kills, and world events.
    /// Epic 8: Save/Load, Steam Cloud sync.
    /// Attach to the WorldStateManager GameObject in Core.unity.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        public static WorldStateManager Instance { get; private set; }

        [SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;

        private readonly Dictionary<string, bool> _worldFacts = new Dictionary<string, bool>();

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

        // ── Kill tracking ─────────────────────────────────────────────────────

        public bool IsKilled(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                GameLog.Warn(TAG, "IsKilled called with null or empty GUID");
                return false;
            }
            return GetFact($"{WorldFactPrefix.Killed}.{guid}");
        }

        public void RegisterKill(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                GameLog.Warn(TAG, "RegisterKill called with null or empty GUID");
                return;
            }
            SetFact($"{WorldFactPrefix.Killed}.{guid}", true);
        }

        // ── World fact read ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the stored value for <paramref name="key"/>, or false if unknown.
        /// Hot path — no logging on cache miss.
        /// </summary>
        public bool GetFact(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLog.Warn(TAG, "GetFact called with null or empty key");
                return false;
            }
            return _worldFacts.TryGetValue(key, out var v) && v;
        }

        // ── Typed write APIs (callers never construct key strings manually) ───

        /// <summary>Formats <c>Quest.{questId}.{stepKey}</c> and writes the fact.</summary>
        public void SetQuestStep(string questId, string stepKey, bool value)
        {
            SetFact($"{WorldFactPrefix.Quest}.{questId}.{stepKey}", value);
        }

        /// <summary>Formats <c>World.{eventKey}</c> and writes the fact.</summary>
        public void SetWorldEvent(string eventKey, bool value)
        {
            SetFact($"{WorldFactPrefix.World}.{eventKey}", value);
        }

        /// <summary>Formats <c>Dialogue.Played.{nodeId}</c> and marks the dialogue as played.</summary>
        public void SetDialoguePlayed(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                GameLog.Warn(TAG, "SetDialoguePlayed called with null or empty nodeId");
                return;
            }
            SetFact($"{WorldFactPrefix.Dialogue}.Played.{nodeId}", true);
        }

        /// <summary>Returns true if the dialogue node with <paramref name="nodeId"/> has been played.</summary>
        public bool IsDialoguePlayed(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                GameLog.Warn(TAG, "IsDialoguePlayed called with null or empty nodeId");
                return false;
            }
            return GetFact($"{WorldFactPrefix.Dialogue}.Played.{nodeId}");
        }

        // ── Save data (Epic 8) ────────────────────────────────────────────────

        /// <summary>Returns a snapshot of world state for Epic 8 save integration (not yet wired).</summary>
        public WorldStateSaveData GetSaveData() => new WorldStateSaveData
        {
            worldFacts = new Dictionary<string, bool>(_worldFacts)
        };

        // ── Internal ──────────────────────────────────────────────────────────

        private void SetFact(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLog.Warn(TAG, "SetFact called with null or empty key");
                return;
            }
            _worldFacts[key] = value;
            GameLog.Info(TAG, $"World fact set: {key} = {value}");
            _onWorldFactChanged?.Raise(new WorldFactData(key, value));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Save-data shape (not yet wired — Epic 8) ─────────────────────────

        /// <summary>
        /// Snapshot struct for Epic 8 save integration.
        /// WARNING: Unity's JsonUtility cannot serialize <c>Dictionary&lt;string,bool&gt;</c>.
        /// Convert to parallel arrays or use Newtonsoft Json.NET before serializing.
        /// </summary>
        [System.Serializable]
        public struct WorldStateSaveData
        {
            public Dictionary<string, bool> worldFacts;
        }
    }
}
