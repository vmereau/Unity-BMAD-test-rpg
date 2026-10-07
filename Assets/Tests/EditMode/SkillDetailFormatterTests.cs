using System;
using System.Collections.Generic;
using Game.Player;
using Game.Progression;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
    public class SkillDetailFormatterTests
    {
        private readonly List<Object> _created = new();
        private readonly List<ItemStatLine> _lines = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
            _lines.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, $"Serialized field '{field}' not found on {target.GetType().Name}");
            assign(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private SkillSO CreateSkill(string id, string name, int lpCost, string description = "", string effect = "")
        {
            var skill = Create<SkillSO>();
            SetSerialized(skill, "_skillId", p => p.stringValue = id);
            SetSerialized(skill, "_displayName", p => p.stringValue = name);
            SetSerialized(skill, "_lpCost", p => p.intValue = lpCost);
            SetSerialized(skill, "_description", p => p.stringValue = description);
            SetSerialized(skill, "_effectDescription", p => p.stringValue = effect);
            return skill;
        }

        private static void AddStatRequirement(SkillSO skill, StatType stat, int value)
        {
            SetSerialized(skill, "_statsRequirements", p =>
            {
                int i = p.arraySize;
                p.arraySize = i + 1;
                var element = p.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("statType").enumValueIndex = (int)stat;
                element.FindPropertyRelative("value").intValue = value;
            });
        }

        private static void AddPrerequisite(SkillSO skill, SkillSO prereq)
        {
            SetSerialized(skill, "_skillRequirements", p =>
            {
                int i = p.arraySize;
                p.arraySize = i + 1;
                p.GetArrayElementAtIndex(i).objectReferenceValue = prereq;
            });
        }

        private static Func<StatType, int> Stats(int str = 0, int dex = 0, int end = 0) => stat => stat switch
        {
            StatType.Strength => str,
            StatType.Dexterity => dex,
            StatType.Endurance => end,
            _ => 0
        };

        private static void AssertLine(ItemStatLine line, string label, string value, StatPolarity polarity)
        {
            Assert.AreEqual(label, line.Label);
            Assert.AreEqual(value, line.Value);
            Assert.AreEqual(polarity, line.Polarity);
        }

        // ---------- Status / text ----------

        [Test]
        public void GetStatus_ReturnsLearnedOrNotLearned()
        {
            Assert.AreEqual("Learned", SkillDetailFormatter.GetStatus(true));
            Assert.AreEqual("Not learned", SkillDetailFormatter.GetStatus(false));
        }

        // AC 7
        [Test]
        public void GetDescriptionAndEffect_ReturnTrimmedText()
        {
            var skill = CreateSkill("power_strike", "Power Strike", 2, "  A strong swing. ", "+10 damage on all melee attacks.\n");

            Assert.AreEqual("A strong swing.", SkillDetailFormatter.GetDescription(skill));
            Assert.AreEqual("+10 damage on all melee attacks.", SkillDetailFormatter.GetEffect(skill));
        }

        // AC 11
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\n\t")]
        public void GetDescriptionAndEffect_BlankText_ReturnsNull(string blank)
        {
            var skill = CreateSkill("s", "S", 1, blank, blank);

            Assert.IsNull(SkillDetailFormatter.GetDescription(skill));
            Assert.IsNull(SkillDetailFormatter.GetEffect(skill));
        }

        [Test]
        public void GetDescriptionAndEffect_NullSkill_ReturnsNull()
        {
            Assert.IsNull(SkillDetailFormatter.GetDescription(null));
            Assert.IsNull(SkillDetailFormatter.GetEffect(null));
        }

        [Test]
        public void GetStatLabel_MapsEveryStat()
        {
            Assert.AreEqual("Strength", SkillDetailFormatter.GetStatLabel(StatType.Strength));
            Assert.AreEqual("Dexterity", SkillDetailFormatter.GetStatLabel(StatType.Dexterity));
            Assert.AreEqual("Endurance", SkillDetailFormatter.GetStatLabel(StatType.Endurance));
            Assert.AreEqual("Intelligence", SkillDetailFormatter.GetStatLabel(StatType.Intelligence));
            Assert.AreEqual("Defense", SkillDetailFormatter.GetStatLabel(StatType.Defense));
        }

        // ---------- BuildDetailLines ----------

        // AC 7
        [Test]
        public void BuildDetailLines_LpCostFirst_ThenStatsInAuthoredOrder()
        {
            var skill = CreateSkill("power_strike", "Power Strike", 2);
            AddStatRequirement(skill, StatType.Strength, 8);
            AddStatRequirement(skill, StatType.Endurance, 5);

            SkillDetailFormatter.BuildDetailLines(skill, false, Stats(str: 10, end: 10), _ => false, _lines);

            Assert.AreEqual(3, _lines.Count);
            AssertLine(_lines[0], "LP Cost", "2", StatPolarity.Neutral);
            AssertLine(_lines[1], "Strength", "8", StatPolarity.Positive);
            AssertLine(_lines[2], "Endurance", "5", StatPolarity.Positive);
        }

        // AC 8
        [Test]
        public void BuildDetailLines_Unlearned_MetIsPositive_UnmetIsNegative()
        {
            var skill = CreateSkill("power_strike", "Power Strike", 2);
            AddStatRequirement(skill, StatType.Strength, 8);
            AddStatRequirement(skill, StatType.Endurance, 5);

            SkillDetailFormatter.BuildDetailLines(skill, false, Stats(str: 7, end: 5), _ => false, _lines);

            AssertLine(_lines[1], "Strength", "8", StatPolarity.Negative);
            AssertLine(_lines[2], "Endurance", "5", StatPolarity.Positive); // equal value meets (>=)
        }

        [Test]
        public void BuildDetailLines_NullGetStat_StatRowsNeutral()
        {
            var skill = CreateSkill("s", "S", 1);
            AddStatRequirement(skill, StatType.Dexterity, 5);

            SkillDetailFormatter.BuildDetailLines(skill, false, null, _ => false, _lines);

            AssertLine(_lines[1], "Dexterity", "5", StatPolarity.Neutral);
        }

        // AC 9
        [Test]
        public void BuildDetailLines_Prerequisite_RedUntilLearned_ThenGreen()
        {
            var beginner = CreateSkill("beginner_lockpicking", "Beginner Lockpicking", 3);
            var expert = CreateSkill("expert_lockpicking", "Expert Lockpicking", 5);
            AddStatRequirement(expert, StatType.Dexterity, 10);
            AddPrerequisite(expert, beginner);

            SkillDetailFormatter.BuildDetailLines(expert, false, Stats(dex: 10), _ => false, _lines);
            Assert.AreEqual(3, _lines.Count);
            AssertLine(_lines[2], "Requires", "Beginner Lockpicking", StatPolarity.Negative);

            SkillDetailFormatter.BuildDetailLines(expert, false, Stats(dex: 10), id => id == "beginner_lockpicking", _lines);
            AssertLine(_lines[2], "Requires", "Beginner Lockpicking", StatPolarity.Positive);
        }

        [Test]
        public void BuildDetailLines_NullHasSkill_PrerequisiteNeutral()
        {
            var beginner = CreateSkill("beginner_lockpicking", "Beginner Lockpicking", 3);
            var expert = CreateSkill("expert_lockpicking", "Expert Lockpicking", 5);
            AddPrerequisite(expert, beginner);

            SkillDetailFormatter.BuildDetailLines(expert, false, Stats(), null, _lines);

            AssertLine(_lines[1], "Requires", "Beginner Lockpicking", StatPolarity.Neutral);
        }

        // AC 10
        [Test]
        public void BuildDetailLines_Learned_AllRequirementRowsNeutral()
        {
            var beginner = CreateSkill("beginner_lockpicking", "Beginner Lockpicking", 3);
            var expert = CreateSkill("expert_lockpicking", "Expert Lockpicking", 5);
            AddStatRequirement(expert, StatType.Dexterity, 10);
            AddPrerequisite(expert, beginner);

            SkillDetailFormatter.BuildDetailLines(expert, true, Stats(dex: 0), _ => false, _lines);

            AssertLine(_lines[0], "LP Cost", "5", StatPolarity.Neutral);
            AssertLine(_lines[1], "Dexterity", "10", StatPolarity.Neutral);
            AssertLine(_lines[2], "Requires", "Beginner Lockpicking", StatPolarity.Neutral);
        }

        // AC 12
        [Test]
        public void BuildDetailLines_NullPrerequisiteEntry_Skipped()
        {
            var beginner = CreateSkill("beginner_lockpicking", "Beginner Lockpicking", 3);
            var expert = CreateSkill("expert_lockpicking", "Expert Lockpicking", 5);
            AddPrerequisite(expert, null);
            AddPrerequisite(expert, beginner);

            SkillDetailFormatter.BuildDetailLines(expert, false, Stats(), _ => true, _lines);

            Assert.AreEqual(2, _lines.Count);
            AssertLine(_lines[1], "Requires", "Beginner Lockpicking", StatPolarity.Positive);
        }

        // AC 12
        [Test]
        public void BuildDetailLines_NullSkill_ClearsList()
        {
            _lines.Add(new ItemStatLine("stale", "row"));

            SkillDetailFormatter.BuildDetailLines(null, false, Stats(), _ => false, _lines);

            Assert.AreEqual(0, _lines.Count);
        }

        // AC 12
        [Test]
        public void BuildDetailLines_NullList_WarnsAndDoesNotThrow()
        {
            var skill = CreateSkill("s", "S", 1);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("target list is null"));

            Assert.DoesNotThrow(() => SkillDetailFormatter.BuildDetailLines(skill, false, Stats(), _ => false, null));
        }
    }
}
