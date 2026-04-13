using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Game.NPC;
using Game.Player;
using Game.Progression;
using Game.Quest;
using Game.World;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for TopicUnlockEvaluator and NPCMemoryEntrySO.IsActive().
    /// Requires a live WorldStateManager instance — created via AddComponent in SetUp.
    /// Instance is force-injected via reflection to guarantee test isolation — Awake's
    /// DontDestroyOnLoad can leave a stale Instance across test-class boundaries.
    /// </summary>
    public class TopicUnlockEvaluatorTests
    {
        private WorldStateManager _wsm;
        private readonly List<Object> _cleanup = new List<Object>();

        // See WorldStateManagerFactsTests for explanation of the backing-field reflection.
        private static readonly FieldInfo s_instanceField =
            typeof(WorldStateManager).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("WorldStateManager_Test");
            _wsm = go.AddComponent<WorldStateManager>();
            _cleanup.Add(go);
            s_instanceField.SetValue(null, _wsm);   // guarantee Instance == _wsm
        }

        [TearDown]
        public void TearDown()
        {
            s_instanceField.SetValue(null, null);   // clear before destroy
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private T MakeFact<T>(System.Func<T> factory) where T : Object
        {
            var f = factory();
            _cleanup.Add(f);
            return f;
        }

        private NPCMemoryEntrySO CreateMemory(Fact[] unlock, Fact[] invalidate)
        {
            var so = ScriptableObject.CreateInstance<NPCMemoryEntrySO>();
            so.unlockConditions = unlock;
            so.invalidationConditions = invalidate;
            _cleanup.Add(so);
            return so;
        }

        private SkillSO CreateSkillSO(string skillId)
        {
            var so = ScriptableObject.CreateInstance<SkillSO>();
            typeof(SkillSO)
                .GetField("_skillId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(so, skillId);
            _cleanup.Add(so);
            return so;
        }

        private PlayerSkills CreatePlayerSkillsWithSkill(string skillId)
        {
            var go = new GameObject("PlayerSkills_Test");
            _cleanup.Add(go);
            var ps = go.AddComponent<PlayerSkills>();
            var learnedSkills = (System.Collections.Generic.HashSet<string>)
                typeof(PlayerSkills)
                    .GetField("_learnedSkills", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(ps);
            learnedSkills.Add(skillId);
            return ps;
        }

        private PlayerStats CreatePlayerStats(int strength = 0, int dexterity = 0,
                                              int endurance = 0, int intelligence = 0)
        {
            var go = new GameObject("PlayerStats_Test");
            _cleanup.Add(go);
            var ps = go.AddComponent<PlayerStats>();
            var t = typeof(PlayerStats);
            t.GetField("_baseStrength",     BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, strength);
            t.GetField("_baseDexterity",    BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, dexterity);
            t.GetField("_baseEndurance",    BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, endurance);
            t.GetField("_baseIntelligence", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, intelligence);
            return ps;
        }

        private void InjectWsmField(string fieldName, object value)
        {
            typeof(WorldStateManager)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_wsm, value);
        }

        // ── AllTrue ───────────────────────────────────────────────────────────

        [Test]
        public void AllTrue_EmptyArray_ReturnsTrue()
        {
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[0]), Is.True);
        }

        // ── AnyTrue ───────────────────────────────────────────────────────────

        [Test]
        public void AnyTrue_EmptyArray_ReturnsFalse()
        {
            Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[0]), Is.False);
        }

        [Test]
        public void AnyTrue_OneFact_ReturnsTrue()
        {
            _wsm.SetWorldEvent(MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("mill_burned")), true);

            Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[]
            {
                MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("mill_burned"))
            }), Is.True);
        }

        [Test]
        public void AnyTrue_NoFactsTrue_ReturnsFalse()
        {
            // QuestSO with no startFact — IsStarted will always be false
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            _cleanup.Add(questSO);
            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>()
                .Init(questSO, QuestState.IsStarted));

            Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[]
            {
                MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("mill_burned")),
                questFact
            }), Is.False);
        }

        [Test]
        public void AllTrue_QuestFact_QuestStarted_ReturnsTrue()
        {
            var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_quest_start"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.startFact = startFact;
            _cleanup.Add(questSO);
            _wsm.SetWorldEvent(startFact, true);

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));

            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { questFact }), Is.True);
        }

        [Test]
        public void AllTrue_QuestFact_QuestNotStarted_ReturnsFalse()
        {
            var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_quest_start"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.startFact = startFact;
            _cleanup.Add(questSO);
            // startFact NOT set — quest not started

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));

            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { questFact }), Is.False);
        }

        // ── SkillFact ─────────────────────────────────────────────────────────

        [Test]
        public void AllTrue_SkillFact_PlayerHasSkill_ReturnsTrue()
        {
            var skill = CreateSkillSO("power_strike");
            var playerSkills = CreatePlayerSkillsWithSkill("power_strike");
            InjectWsmField("_playerSkills", playerSkills);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.True);
        }

        [Test]
        public void AllTrue_SkillFact_PlayerMissingSkill_ReturnsFalse()
        {
            var skill = CreateSkillSO("power_strike");
            var playerSkills = CreatePlayerSkillsWithSkill("other_skill");
            InjectWsmField("_playerSkills", playerSkills);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
        }

        [Test]
        public void AllTrue_SkillFact_PlayerSkillsNotAssigned_ReturnsFalse()
        {
            var skill = CreateSkillSO("power_strike");
            // _playerSkills intentionally not injected
            var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
        }

        [Test]
        public void AnyTrue_SkillFact_PlayerHasSkill_ReturnsTrue()
        {
            var skill = CreateSkillSO("power_strike");
            var playerSkills = CreatePlayerSkillsWithSkill("power_strike");
            InjectWsmField("_playerSkills", playerSkills);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
            Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[] { fact }), Is.True);
        }

        // ── StatFact ──────────────────────────────────────────────────────────

        [Test]
        public void AllTrue_StatFact_AllStatsMet_ReturnsTrue()
        {
            var playerStats = CreatePlayerStats(strength: 10, endurance: 6);
            InjectWsmField("_playerStats", playerStats);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
                new StatRequirement { statType = StatType.Strength, value = 8 },
                new StatRequirement { statType = StatType.Endurance, value = 5 }
            ));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.True);
        }

        [Test]
        public void AllTrue_StatFact_ExactThreshold_ReturnsTrue()
        {
            var playerStats = CreatePlayerStats(strength: 8, endurance: 5);
            InjectWsmField("_playerStats", playerStats);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
                new StatRequirement { statType = StatType.Strength, value = 8 },
                new StatRequirement { statType = StatType.Endurance, value = 5 }
            ));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.True);
        }

        [Test]
        public void AllTrue_StatFact_OneStatFails_ReturnsFalse()
        {
            var playerStats = CreatePlayerStats(strength: 10, endurance: 3);
            InjectWsmField("_playerStats", playerStats);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
                new StatRequirement { statType = StatType.Strength, value = 8 },
                new StatRequirement { statType = StatType.Endurance, value = 5 }
            ));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
        }

        [Test]
        public void AllTrue_StatFact_PlayerStatsNotAssigned_ReturnsFalse()
        {
            // _playerStats intentionally not injected
            var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
                new StatRequirement { statType = StatType.Strength, value = 5 }
            ));
            Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
        }

        [Test]
        public void AnyTrue_StatFact_AllStatsMet_ReturnsTrue()
        {
            var playerStats = CreatePlayerStats(strength: 10);
            InjectWsmField("_playerStats", playerStats);

            var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
                new StatRequirement { statType = StatType.Strength, value = 8 }
            ));
            Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[] { fact }), Is.True);
        }
    }
}
