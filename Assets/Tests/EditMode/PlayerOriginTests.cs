using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Player;
using Game.Progression;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>
    /// New-game origins (spec-player-origins): catalog lookup, silent stat / skill grants, GameSession hand-off and
    /// the pick-screen detail text. Edit Mode: Awake doesn't run on AddComponent, so fields are wired by reflection.
    /// </summary>
    public class PlayerOriginTests
    {
        private GameObject _go;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.Reset();
            _go = new GameObject("TestPlayer");
            _cleanup.Add(_go);
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.Reset();
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private T SO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private SkillSO Skill(string id, string name)
        {
            var skill = SO<SkillSO>();
            Set(skill, "_skillId", id);
            Set(skill, "_displayName", name);
            return skill;
        }

        private OriginCatalogSO Catalog(out OriginSO smith, out OriginSO wretch)
        {
            smith = SO<OriginSO>().Init("Blacksmith", "Blacksmith's Apprentice");
            wretch = SO<OriginSO>().Init("Wretch", "Wretch");
            var catalog = SO<OriginCatalogSO>();
            catalog.Init(new List<OriginSO> { smith, null, wretch }, wretch);
            return catalog;
        }

        // ── Catalog ───────────────────────────────────────────────────────────

        [Test]
        public void Find_KnownId_ReturnsOrigin()
        {
            var catalog = Catalog(out var smith, out _);
            Assert.AreSame(smith, catalog.Find("Blacksmith"));
        }

        [Test]
        public void Find_NullOrEmpty_ReturnsDefault()
        {
            var catalog = Catalog(out _, out var wretch);
            Assert.AreSame(wretch, catalog.Find(null));
            Assert.AreSame(wretch, catalog.Find(string.Empty));
        }

        [Test]
        public void Find_UnknownId_WarnsAndReturnsDefault()
        {
            var catalog = Catalog(out _, out var wretch);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Unknown origin 'Poacher'"));
            Assert.AreSame(wretch, catalog.Find("Poacher"));
        }

        [Test]
        public void Find_NoDefault_FallsBackToFirstOrigin()
        {
            var smith = SO<OriginSO>().Init("Blacksmith", "Blacksmith's Apprentice");
            var catalog = SO<OriginCatalogSO>();
            catalog.Init(new List<OriginSO> { null, smith }, null);
            Assert.AreSame(smith, catalog.Find(null));
        }

        [Test]
        public void FormatDetails_SkipsBonusesThatAreNeverApplied()
        {
            var origin = SO<OriginSO>().Init("Odd", "Odd", new List<OriginSO.StatBonus>
            {
                new OriginSO.StatBonus { stat = StatType.Defense, points = 2 },
                new OriginSO.StatBonus { stat = StatType.Strength, points = 0 }
            });
            StringAssert.Contains("Bonus: none", OriginPickUI.FormatDetails(origin));
        }

        // ── Silent grants ─────────────────────────────────────────────────────

        [Test]
        public void AddBaseStat_RaisesBaseStat_OneStatsChanged_IgnoresNonPositive()
        {
            var stats = _go.AddComponent<PlayerStats>();
            var changed = SO<GameEventSO_Void>();
            Set(stats, "_onStatsChanged", changed);
            int raises = 0;
            changed.AddListener(_ => raises++);
            stats.RestoreBaseStats(10, 10, 10, 10);
            raises = 0;

            stats.AddBaseStat(StatType.Dexterity, 2);
            stats.AddBaseStat(StatType.Strength, 0);

            Assert.AreEqual(12, stats.GetBaseStat(StatType.Dexterity));
            Assert.AreEqual(10, stats.GetBaseStat(StatType.Strength));
            Assert.AreEqual(1, raises);
        }

        [Test]
        public void GrantSkillSilently_AddsOnce_NoEvent_NoLPSpent()
        {
            var lp = _go.AddComponent<LearningPointSystem>();
            var skills = _go.AddComponent<PlayerSkills>();
            var learned = SO<GameEventSO_String>();
            Set(skills, "_onSkillLearned", learned);
            Set(skills, "_lpSystem", lp);
            lp.RestoreLP(3);
            int raises = 0;
            learned.AddListener(_ => raises++);
            var smithing = Skill("smithing", "Smithing");

            Assert.IsTrue(skills.GrantSkillSilently(smithing));
            Assert.IsFalse(skills.GrantSkillSilently(smithing));
            Assert.IsFalse(skills.GrantSkillSilently(null));

            Assert.IsTrue(skills.HasSkill("smithing"));
            Assert.AreEqual(1, skills.GetLearnedSkillIds().Count);
            Assert.AreEqual(0, raises);
            Assert.AreEqual(3, lp.CurrentLP);
        }

        // ── GameSession ───────────────────────────────────────────────────────

        [Test]
        public void Origin_SetThenTake_ReturnsOnce()
        {
            Assert.IsNull(GameSession.TakeOriginId());
            GameSession.SetOrigin("Joiner");
            Assert.AreEqual("Joiner", GameSession.TakeOriginId());
            Assert.IsNull(GameSession.TakeOriginId());
        }

        [Test]
        public void Origin_EmptyOrReset_Clears()
        {
            GameSession.SetOrigin("Joiner");
            GameSession.SetOrigin(string.Empty);
            Assert.IsNull(GameSession.TakeOriginId());

            GameSession.SetOrigin("Joiner");
            GameSession.Reset();
            Assert.IsNull(GameSession.TakeOriginId());
        }

        // ── Pick-screen text ──────────────────────────────────────────────────

        [Test]
        public void FormatDetails_WithBelongings_ListsBonusSkillItemsAndGold()
        {
            var origin = SO<OriginSO>().Init("Blacksmith", "Blacksmith's Apprentice",
                new List<OriginSO.StatBonus>
                {
                    new OriginSO.StatBonus { stat = StatType.Strength, points = 1 },
                    new OriginSO.StatBonus { stat = StatType.Endurance, points = 1 }
                },
                Skill("smithing", "Smithing"), true, new List<string> { "Smith's hammer" }, 30);

            string text = OriginPickUI.FormatDetails(origin);

            StringAssert.Contains("Bonus: Strength +1, Endurance +1", text);
            StringAssert.Contains("Skill: Smithing", text);
            StringAssert.Contains(OriginPickUI.BELONGINGS_HEADER, text);
            StringAssert.Contains("- Smith's hammer", text);
            StringAssert.Contains("- 30 gold", text);
        }

        [Test]
        public void FormatDetails_NoBelongings_SaysNothing()
        {
            var origin = SO<OriginSO>().Init("Wretch", "Wretch");

            string text = OriginPickUI.FormatDetails(origin);

            StringAssert.Contains("Bonus: none", text);
            StringAssert.Contains("Skill: none", text);
            StringAssert.Contains(OriginPickUI.NO_BELONGINGS_TEXT, text);
            StringAssert.DoesNotContain(OriginPickUI.BELONGINGS_HEADER, text);
            Assert.AreEqual(string.Empty, OriginPickUI.FormatDetails(null));
        }
    }
}
