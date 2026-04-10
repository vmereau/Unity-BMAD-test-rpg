using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for WorldStateManager world facts extension.
    /// Uses AddComponent to get a live instance with Awake-based singleton init.
    /// Instance is force-injected via reflection to guarantee test isolation — Awake's
    /// DontDestroyOnLoad can leave a stale Instance across test-class boundaries.
    /// </summary>
    public class WorldStateManagerFactsTests
    {
        private WorldStateManager _wsm;
        private readonly List<Object> _cleanup = new List<Object>();

        // "<Instance>k__BackingField" is the compiler-generated backing field for the
        // auto-property "public static WorldStateManager Instance { get; private set; }".
        // We force-set it in SetUp/TearDown because DontDestroyOnLoad can keep a stale
        // Instance alive across test classes, making tests interfere with each other.
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

        // Helper — creates a typed Fact, registers it for cleanup, and returns it.
        private T MakeFact<T>(System.Func<T> factory) where T : Object
        {
            var f = factory();
            _cleanup.Add(f);
            return f;
        }

        // ── SetWorldEvent / GetFact ───────────────────────────────────────────

        [Test]
        public void SetWorldEvent_StoresBoolValue()
        {
            var fact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("test"));
            _wsm.SetWorldEvent(fact, true);

            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("test"))), Is.True);
        }

        [Test]
        public void GetFact_MissingKey_ReturnsFalse()
        {
            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("nonexistent"))), Is.False);
        }

        [Test]
        public void GetFact_NullFact_ReturnsFalse()
        {
            Assert.That(_wsm.GetFact((Fact)null), Is.False);
        }

        [Test]
        public void SetWorldEvent_Overwrite_UpdatesValue()
        {
            var factA = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("test"));
            var factB = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("test"));
            _wsm.SetWorldEvent(factA, true);
            _wsm.SetWorldEvent(factB, false);

            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("test"))), Is.False);
        }

        // ── SetQuestStep ──────────────────────────────────────────────────────

        [Test]
        public void SetQuestStep_FormatsKey()
        {
            _wsm.SetQuestStep(MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "monster_killed")), true);

            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "monster_killed"))), Is.True);
        }

        // ── RegisterKill auto-fact ────────────────────────────────────────────

        [Test]
        public void RegisterKill_AutoSetsKilledFact()
        {
            _wsm.RegisterKill(MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard")));

            Assert.That(_wsm.IsKilled(MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))), Is.True);
            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))), Is.True);
        }

        // ── Event broadcast ───────────────────────────────────────────────────

        [Test]
        public void SetWorldEvent_RaisesEvent_WithCorrectPayload()
        {
            var eventSO = ScriptableObject.CreateInstance<GameEventSO_WorldFact>();
            _cleanup.Add(eventSO);

            typeof(WorldStateManager)
                .GetField("_onWorldFactChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_wsm, eventSO);

            WorldFactData received = default;
            bool fired = false;
            eventSO.AddListener(data => { received = data; fired = true; });

            _wsm.SetWorldEvent(MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("mill_cleared")), true);

            Assert.That(fired, Is.True);
            Assert.That(received.key, Is.EqualTo("World.mill_cleared"));
            Assert.That(received.value, Is.True);
        }
    }
}
