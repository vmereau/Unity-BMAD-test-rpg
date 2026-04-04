using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Game.NPC;
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

        private NPCMemoryEntrySO CreateMemory(string[] unlock, string[] invalidate)
        {
            var so = ScriptableObject.CreateInstance<NPCMemoryEntrySO>();
            so.unlockConditions = unlock;
            so.invalidationConditions = invalidate;
            _cleanup.Add(so);
            return so;
        }

        // ── AllTrue ───────────────────────────────────────────────────────────

        [Test]
        public void AllTrue_EmptyArray_ReturnsTrue()
        {
            Assert.That(TopicUnlockEvaluator.AllTrue(new string[0]), Is.True);
        }

        [Test]
        public void AllTrue_AllFactsTrue_ReturnsTrue()
        {
            _wsm.SetQuestStep("Mill", "a", true);
            _wsm.SetWorldEvent("b", true);

            Assert.That(TopicUnlockEvaluator.AllTrue(new[] { "Quest.Mill.a", "World.b" }), Is.True);
        }

        [Test]
        public void AllTrue_OneFactFalse_ReturnsFalse()
        {
            _wsm.SetQuestStep("Mill", "a", true);
            // "World.b" not set — defaults to false

            Assert.That(TopicUnlockEvaluator.AllTrue(new[] { "Quest.Mill.a", "World.b" }), Is.False);
        }

        // ── AnyTrue ───────────────────────────────────────────────────────────

        [Test]
        public void AnyTrue_EmptyArray_ReturnsFalse()
        {
            Assert.That(TopicUnlockEvaluator.AnyTrue(new string[0]), Is.False);
        }

        [Test]
        public void AnyTrue_OneFact_ReturnsTrue()
        {
            _wsm.SetWorldEvent("mill_burned", true);

            Assert.That(TopicUnlockEvaluator.AnyTrue(new[] { "World.mill_burned" }), Is.True);
        }

        [Test]
        public void AnyTrue_NoFactsTrue_ReturnsFalse()
        {
            Assert.That(TopicUnlockEvaluator.AnyTrue(new[] { "World.mill_burned", "Quest.Mill.x" }), Is.False);
        }

        // ── IsActive (via NPCMemoryEntrySO) ───────────────────────────────────

        [Test]
        public void IsActive_UnlockedNotInvalidated_ReturnsTrue()
        {
            _wsm.SetQuestStep("Mill", "monster_killed", true);

            var memory = CreateMemory(
                unlock: new[] { "Quest.Mill.monster_killed" },
                invalidate: new[] { "World.quest_failed" }
            );

            Assert.That(memory.IsActive(), Is.True);
        }

        [Test]
        public void IsActive_Invalidated_ReturnsFalse()
        {
            _wsm.SetQuestStep("Mill", "monster_killed", true);
            _wsm.SetWorldEvent("quest_failed", true);

            var memory = CreateMemory(
                unlock: new[] { "Quest.Mill.monster_killed" },
                invalidate: new[] { "World.quest_failed" }
            );

            // Invalidation supersedes unlock
            Assert.That(memory.IsActive(), Is.False);
        }
    }
}
