using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using Game.Inventory;
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
        [Tooltip("Raised the first time the player knocks out each entity (knockout XP, spec-knockout-xp-reward).")]
        [SerializeField] private GameEventSO_EntityKilled _onEntityKnockedOut;
        [SerializeField] private GameEventSO_DialogueFact _onDialoguePlayed;

        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private PlayerStats _playerStats;

        private readonly Dictionary<string, bool> _worldFacts = new Dictionary<string, bool>();
        /// <summary>Times each recipe was cooked, keyed by its <see cref="RecipeFact"/> key. Kept apart so facts stay bool-only.</summary>
        private readonly Dictionary<string, int> _recipeCrafts = new Dictionary<string, int>();
        /// <summary>
        /// Game time (<see cref="GameClock.Now"/>) at which each stored fact last turned false → true, keyed like
        /// <see cref="_worldFacts"/>. Read by <see cref="ElapsedFact"/>. No entry = set before timing existed or without a clock.
        /// </summary>
        private readonly Dictionary<string, double> _factSetTimes = new Dictionary<string, double>();
        /// <summary>
        /// ElapsedFacts last evaluated as "source true, delay running" → game time their delay ends. Filled by
        /// <see cref="IsElapsed"/> (quests re-evaluate on every fact change and on load, so theirs register themselves);
        /// <see cref="Update"/> raises <c>OnFactChanged</c> once when the clock passes a deadline. Not saved.
        /// </summary>
        private readonly Dictionary<ElapsedFact, double> _pendingElapsed = new Dictionary<ElapsedFact, double>();
        private readonly List<ElapsedFact> _dueElapsed = new List<ElapsedFact>();

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

        /// <summary>
        /// Records the kill (always — quests / memories read the fact) and raises <c>OnEntityKilled</c>.
        /// <paramref name="playerCredited"/> decides whether the player earns the kill XP (spec-kill-xp-credit-assist).
        /// </summary>
        public void RegisterKill(KilledFact fact, Entity entity = null, bool playerCredited = false)
        {
            if (fact == null) { GameLog.Warn(TAG, "RegisterKill called with null fact"); return; }
            SetFact(fact, true);
            _onEntityKilled?.Raise(new EntityKilled(entity, fact, playerCredited));
        }

        /// <summary>
        /// The player knocked out the entity identified by <paramref name="fact"/>. The first time per identity, records
        /// the claim (key <c>Knockout.XpClaimed.{killedFact}</c>, saved with the facts, no fact event — not a quest /
        /// memory condition) and raises <c>OnEntityKnockedOut</c>. Later knockouts of the same entity do nothing.
        /// </summary>
        public void RegisterKnockout(KilledFact fact, Entity entity = null)
        {
            if (fact == null) { GameLog.Warn(TAG, "RegisterKnockout called with null fact"); return; }
            if (IsKnockoutClaimed(fact)) return;
            // Unwired channel: keep the claim free, or the saved claim would burn the reward for good.
            if (_onEntityKnockedOut == null) { GameLog.Warn(TAG, "OnEntityKnockedOut not assigned — knockout not claimed, no reward"); return; }
            _worldFacts[KnockoutKey(fact)] = true;
            GameLog.Info(TAG, $"first knockout recorded: {fact}");
            _onEntityKnockedOut?.Raise(new EntityKilled(entity, fact, playerCredited: true)); // only credited knockouts reach here
        }

        public bool IsKnockoutClaimed(KilledFact fact) =>
            fact != null && _worldFacts.TryGetValue(KnockoutKey(fact), out var v) && v;

        private static string KnockoutKey(KilledFact fact) => $"{WorldFactPrefix.Knockout}.XpClaimed.{fact}";

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
                ElapsedFact ef => IsElapsed(ef),
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
            bool wasTrue = _worldFacts.TryGetValue(key, out var previous) && previous;
            _worldFacts[key] = value;
            RecordSetTime(key, wasTrue, value);
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

        public void SetRecipeKnown(RecipeFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "SetRecipeKnown called with null fact"); return; }
            SetFact(fact, true);
        }

        public bool IsRecipeKnown(RecipeFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "IsRecipeKnown called with null fact"); return false; }
            return GetFact(fact);
        }

        /// <summary>Counts one successful cook of the recipe identified by <paramref name="fact"/>. Raises no event.</summary>
        public void AddRecipeCraft(RecipeFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "AddRecipeCraft called with null fact"); return; }
            string key = fact.ToString();
            _recipeCrafts[key] = GetRecipeCraftCount(fact) + 1;
        }

        public int GetRecipeCraftCount(RecipeFact fact)
        {
            if (fact == null) return 0;
            return _recipeCrafts.TryGetValue(fact.ToString(), out int count) ? count : 0;
        }

        /// <summary>
        /// Records that the player picked a dialogue topic / choice (<paramref name="id"/> from
        /// <c>DialogueNode.Id</c>). Stored with the facts so it is saved, but raises no event —
        /// quests and memories must never react to it.
        /// </summary>
        public void MarkDialogueSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _worldFacts[SeenKey(id)] = true;
        }

        public bool IsDialogueSeen(string id) =>
            !string.IsNullOrEmpty(id) && _worldFacts.TryGetValue(SeenKey(id), out var v) && v;

        private static string SeenKey(string id) => $"{WorldFactPrefix.Dialogue}.Seen.{id}";

        /// <summary>
        /// Records that the first-meal bonus of <paramref name="meal"/> was granted (key <c>Meal.BonusClaimed.{itemId}</c>).
        /// Stored with the facts so it is saved, but raises no event — not a quest / memory condition.
        /// </summary>
        public void ClaimMealBonus(ItemSO meal)
        {
            if (meal == null || string.IsNullOrEmpty(meal.itemId)) { GameLog.Warn(TAG, "ClaimMealBonus called with a null meal or an empty itemId"); return; }
            _worldFacts[MealBonusKey(meal.itemId)] = true;
        }

        public bool IsMealBonusClaimed(ItemSO meal) =>
            meal != null && !string.IsNullOrEmpty(meal.itemId) && _worldFacts.TryGetValue(MealBonusKey(meal.itemId), out var v) && v;

        private static string MealBonusKey(string itemId) => $"{WorldFactPrefix.Meal}.BonusClaimed.{itemId}";

        // ── Timed facts ───────────────────────────────────────────────────────

        // First false → true keeps its time (a re-set doesn't restart the delay); false clears it.
        private void RecordSetTime(string key, bool wasTrue, bool value)
        {
            if (!value) { _factSetTimes.Remove(key); return; }
            if (wasTrue) return;
            GameClock clock = GameClock.Active;
            if (clock != null) _factSetTimes[key] = clock.Now;
            else _factSetTimes.Remove(key); // no clock → counts as set long ago
        }

        /// <summary>Game time at which the stored <paramref name="fact"/> turned true. False when not recorded.</summary>
        public bool TryGetFactSetTime(Fact fact, out double setAt)
        {
            setAt = 0d;
            return fact != null && _factSetTimes.TryGetValue(fact.ToString(), out setAt);
        }

        /// <summary>Evaluates an <see cref="ElapsedFact"/>: its source is true and its hours have passed since then.</summary>
        public bool IsElapsed(ElapsedFact fact)
        {
            if (fact == null) { GameLog.Warn(TAG, "IsElapsed called with null fact"); return false; }
            Fact source = fact.Source;
            if (source == null) { GameLog.Warn(TAG, $"ElapsedFact '{fact.name}' has no source — assign a stored fact in the Inspector"); return false; }
            if (source is ElapsedFact) { GameLog.Warn(TAG, $"ElapsedFact '{fact.name}' uses another ElapsedFact as its source — evaluated as false"); return false; }

            bool sourceTrue = GetFact(source);
            double? setAt = TryGetFactSetTime(source, out double t) ? t : (double?)null;
            double? now = GameClock.Active != null ? GameClock.Active.Now : (double?)null;
            bool elapsed = ElapsedFactRules.IsElapsed(sourceTrue, setAt, now, fact.Hours);

            // Delay running → remember when it ends, so event-driven readers (quests, quest log) hear about it.
            if (!elapsed && sourceTrue && setAt.HasValue)
                _pendingElapsed[fact] = setAt.Value + fact.Hours * GameTime.SECONDS_PER_HOUR;
            else
                _pendingElapsed.Remove(fact);
            return elapsed;
        }

        private void Update() => RaiseDueElapsedFacts();

        // Raises OnFactChanged once for each pending ElapsedFact whose deadline the clock passed and which is really
        // true now (re-checked: a source set false, or set true again later, re-registers or drops it).
        private void RaiseDueElapsedFacts()
        {
            if (_pendingElapsed.Count == 0) return;
            GameClock clock = GameClock.Active;
            if (clock == null) return;
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) return;

            double now = clock.Now;
            _dueElapsed.Clear();
            foreach (var pair in _pendingElapsed)
                if (pair.Key == null || pair.Value <= now) _dueElapsed.Add(pair.Key);
            if (_dueElapsed.Count == 0) return;

            foreach (ElapsedFact fact in _dueElapsed)
            {
                _pendingElapsed.Remove(fact);
                if (fact == null || !IsElapsed(fact)) continue;
                GameLog.Info(TAG, $"delay ended: {fact.name}");
                RaiseFactEvent(fact, true);
            }
            _dueElapsed.Clear();
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
            _pendingElapsed.Clear();
            if (facts != null)
                foreach (var pair in facts)
                    _worldFacts[pair.Key] = pair.Value;
            GameLog.Info(TAG, $"Restored {_worldFacts.Count} world fact(s)");
        }

        /// <summary>Copy of every recorded fact "became true" time (game seconds), for <see cref="ElapsedFact"/>.</summary>
        public Dictionary<string, double> CaptureFactSetTimes() => new Dictionary<string, double>(_factSetTimes);

        /// <summary>Replaces every fact "became true" time (null → none, e.g. older saves = all elapsed). Raises no events.</summary>
        public void RestoreFactSetTimes(Dictionary<string, double> times)
        {
            _factSetTimes.Clear();
            _pendingElapsed.Clear(); // re-registered as readers re-evaluate (quest reseed)
            if (times == null) return;
            foreach (var pair in times)
                if (!double.IsNaN(pair.Value) && !double.IsInfinity(pair.Value)) _factSetTimes[pair.Key] = pair.Value;
        }

        /// <summary>Copy of every recipe craft count.</summary>
        public Dictionary<string, int> CaptureRecipeCrafts() => new Dictionary<string, int>(_recipeCrafts);

        /// <summary>Replaces every recipe craft count with <paramref name="crafts"/> (null → empty, e.g. older saves). Raises no events.</summary>
        public void RestoreRecipeCrafts(Dictionary<string, int> crafts)
        {
            _recipeCrafts.Clear();
            if (crafts != null)
                foreach (var pair in crafts)
                    if (pair.Value > 0) _recipeCrafts[pair.Key] = pair.Value;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
