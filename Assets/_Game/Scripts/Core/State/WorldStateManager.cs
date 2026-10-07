using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using Game.Player;
using Game.Progression;
using Game.Quest;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Central runtime state manager: kill tracking + flat key/bool world-fact store backed by
    /// typed Fact ScriptableObjects. The save system snapshots the stored facts with
    /// <see cref="CaptureFacts"/> and puts them back with <see cref="RestoreFacts"/>.
    /// Attach to the WorldStateManager GameObject in Core.unity.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        public static WorldStateManager Instance { get; private set; }

        [SerializeField] private GameEventSO_Fact _onFactChanged;
        [SerializeField] private GameEventSO_EntityKilled _onEntityKilled;
        [SerializeField] private GameEventSO_DialogueFact _onDialoguePlayed;

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

        public void RegisterKill(KilledFact fact, Entity entity = null)
        {
            if (fact == null) { GameLog.Warn(TAG, "RegisterKill called with null fact"); return; }
            SetFact(fact, true);
            _onEntityKilled?.Raise(new EntityKilled(entity, fact));
        }

        // ── Typed read/write ───────────────────────────────────────────────────

        /// <summary>Typed read — Either reads from _worldFacts or computed fact</summary>
        public bool GetFact(Fact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "GetFact called with null fact"); return false; }
            bool result = fact switch
            {
                SkillFact sf  => PlayerHasSkill(sf),
                StatFact  stf => PlayerStatCheck(stf),
                QuestFact qf  => IsQuestFactTrue(qf),
                _             => _worldFacts.TryGetValue(fact.ToString(), out var v) && v
            };
            return result;
        }

        /// <summary>Typed write — calls fact.ToString() as the storage key.</summary>
        /// <remarks>
        /// NOTE: For a <see cref="KilledFact"/> always go through <see cref="RegisterKill"/>, not this
        /// method directly — the enriched <c>OnEntityKilled</c> reward event is raised by RegisterKill,
        /// not by the generic fact path. A raw SetFact(killedFact, true) records the kill for persistence
        /// but grants no XP/rewards.
        /// </remarks>
        public void SetFact(Fact fact, bool value)
        {
            if (fact == null) { GameLog.Warn(TAG, "SetFact called with null fact"); return; }
            
            string key = fact.ToString();
            _worldFacts[key] = value;
            GameLog.Info(TAG, $"fact set: {key} = {value}");
            
            RaiseFactEvent(fact, value);
        }

        private void RaiseFactEvent(Fact fact, bool value)
        {
            switch (fact)
            {
                case DialogueFact dialogueFact:
                    _onDialoguePlayed?.Raise(dialogueFact);
                    break;
            }

            _onFactChanged?.Raise(new FactData(fact.ToString(), value));
        }

        // ── Typed convenience methods ──────────────────────────────────────────
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

        /// <summary>Evaluates a QuestFact by delegating to the referenced QuestSO's computed properties.
        /// QuestFacts are NOT stored in _worldFacts — always evaluate via this method.</summary>
        public bool IsQuestFactTrue(QuestFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "IsQuestFactTrue called with null fact"); return false; }
            if (fact.Quest == null) { GameLog.Warn(TAG, "QuestFact.Quest is null — assign a QuestSO in the Inspector"); return false; }

            if (fact.IsStepState)
                return fact.Quest.IsStepCompleted(fact.QuestStepIndex);

            return fact.QuestState switch
            {
                QuestState.IsStarted   => fact.Quest.IsStarted,
                QuestState.IsCompleted => fact.Quest.IsCompleted,
                QuestState.IsFailed    => fact.Quest.IsFailed,
                _                      => false
            };
        }

        // ── Save / load ───────────────────────────────────────────────────────

        /// <summary>Copy of every stored fact (computed Skill / Stat / Quest facts are not stored).</summary>
        public Dictionary<string, bool> CaptureFacts() => new Dictionary<string, bool>(_worldFacts);

        /// <summary>
        /// Replaces every stored fact with <paramref name="facts"/> (null → empty). Raises no events and
        /// grants nothing — listeners that cache fact-derived state must re-read it after a load.
        /// </summary>
        public void RestoreFacts(Dictionary<string, bool> facts)
        {
            _worldFacts.Clear();
            if (facts != null)
                foreach (var pair in facts)
                    _worldFacts[pair.Key] = pair.Value;
            GameLog.Info(TAG, $"Restored {_worldFacts.Count} world fact(s)");
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
