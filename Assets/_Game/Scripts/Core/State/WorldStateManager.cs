using System.Collections.Generic;
using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Central runtime state manager. Story 2.8: Minimal stub (singleton + kill tracking).
    /// Story 2.9 adds: OnEntityKilled event wiring.
    /// World Facts extension: flat key/bool store backed by typed Fact ScriptableObjects.
    /// Epic 8: Save/Load, Steam Cloud sync.
    /// Attach to the WorldStateManager GameObject in Core.unity.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        public static WorldStateManager Instance { get; private set; }

        [SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private PlayerStats _playerStats;

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

        // ── Kill tracking ──────────────────────────────────────────────────────

        public bool IsKilled(KilledFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "IsKilled called with null fact"); return false; }
            return GetFact(fact);
        }

        public void RegisterKill(KilledFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "RegisterKill called with null fact"); return; }
            SetFact(fact, true);
        }

        // ── Typed read/write ───────────────────────────────────────────────────

        /// <summary>Typed read — calls fact.ToString() to look up the key.</summary>
        public bool GetFact(Fact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "GetFact called with null fact"); return false; }
            return _worldFacts.TryGetValue(fact.ToString(), out var v) && v;
        }

        /// <summary>Typed write — calls fact.ToString() as the storage key.</summary>
        public void SetFact(Fact fact, bool value)
        {
            if (fact == null) { GameLog.Warn(TAG, "SetFact called with null fact"); return; }
            SetFactInternal(fact.ToString(), value);
        }

        // ── Typed convenience methods ──────────────────────────────────────────

        public void SetQuestStep(QuestFact fact, bool value) => SetFact(fact, value);
        public void SetWorldEvent(WorldFact fact, bool value) => SetFact(fact, value);

        public void SetDialoguePlayed(DialogueFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "SetDialoguePlayed called with null fact"); return; }
            SetFact(fact, true);
        }

        public bool IsDialoguePlayed(DialogueFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "IsDialoguePlayed called with null fact"); return false; }
            return GetFact(fact);
        }

        // ── Player checks ─────────────────────────────────────────────────────

        /// <summary>Returns true if the player has learned the skill referenced by this fact.
        /// Delegates to PlayerSkills.HasSkill() — WorldStateManager is the intermediary only.</summary>
        public bool PlayerHasSkill(SkillFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "PlayerHasSkill called with null fact"); return false; }
            if (_playerSkills == null) { GameLog.Warn(TAG, "PlayerSkills not assigned on WorldStateManager — skill check returns false"); return false; }
            if (fact.Skill == null) { GameLog.Warn(TAG, "SkillFact.Skill is null — assign a SkillSO in the Inspector"); return false; }
            return _playerSkills.HasSkill(fact.Skill.skillId);
        }

        /// <summary>Returns true if the player meets ALL stat thresholds in the fact (>= per requirement).
        /// Delegates to PlayerStats.GetStat() — WorldStateManager is the intermediary only.</summary>
        public bool PlayerStatCheck(StatFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "PlayerStatCheck called with null fact"); return false; }
            if (_playerStats == null) { GameLog.Warn(TAG, "PlayerStats not assigned on WorldStateManager — stat check returns false"); return false; }

            return _playerStats.ValidateStats(fact.Requirements);
        }

        // ── Save data (Epic 8) ────────────────────────────────────────────────

        /// <summary>Returns a snapshot of world state for Epic 8 save integration (not yet wired).</summary>
        public WorldStateSaveData GetSaveData() => new WorldStateSaveData
        {
            worldFacts = new Dictionary<string, bool>(_worldFacts)
        };

        // ── Internal ──────────────────────────────────────────────────────────

        private void SetFactInternal(string key, bool value)
        {
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
