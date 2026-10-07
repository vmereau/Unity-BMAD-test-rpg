using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.AI;
using Game.Core;
using Game.Quest;

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

        // ── RegisterKill auto-fact ────────────────────────────────────────────

        [Test]
        public void RegisterKill_AutoSetsKilledFact()
        {
            _wsm.RegisterKill(MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard")));

            Assert.That(_wsm.IsKilled(MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))), Is.True);
            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))), Is.True);
        }

        [Test]
        public void RegisterKill_RaisesEntityKilled_WithEntityAndFact()
        {
            var eventSO = ScriptableObject.CreateInstance<GameEventSO_EntityKilled>();
            _cleanup.Add(eventSO);

            typeof(WorldStateManager)
                .GetField("_onEntityKilled", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_wsm, eventSO);

            EntityKilled received = default;
            bool fired = false;
            eventSO.AddListener(payload => { received = payload; fired = true; });

            var fact = MakeFact(() => ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"));
            var entity = MakeFact(() => ScriptableObject.CreateInstance<MonsterEntity>());
            _wsm.RegisterKill(fact, entity);

            Assert.That(fired, Is.True);
            Assert.That(received.fact, Is.SameAs(fact));
            Assert.That(received.entity, Is.SameAs(entity));
        }

        // ── IsQuestFactTrue ───────────────────────────────────────────────────

        [Test]
        public void IsQuestFactTrue_Started_WhenStartFactSet_ReturnsTrue()
        {
            var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_start"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.startPart = new QuestPart { fact = startFact };
            _cleanup.Add(questSO);
            _wsm.SetWorldEvent(startFact, true);

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
        }

        [Test]
        public void IsQuestFactTrue_Started_WhenStartFactNotSet_ReturnsFalse()
        {
            var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_start"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.startPart = new QuestPart { fact = startFact };
            _cleanup.Add(questSO);
            // NOT setting startPart in WSM

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
        }

        [Test]
        public void IsQuestFactTrue_Completed_AllFactsTrue_ReturnsTrue()
        {
            var f1 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herb_delivered"));
            var f2 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("elder_thanked"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.completedParts.Add(new QuestPart { fact = f1 });
            questSO.completedParts.Add(new QuestPart { fact = f2 });
            _cleanup.Add(questSO);
            _wsm.SetWorldEvent(f1, true);
            _wsm.SetWorldEvent(f2, true);

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsCompleted));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
        }

        [Test]
        public void IsQuestFactTrue_Completed_EmptyFacts_ReturnsFalse()
        {
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            _cleanup.Add(questSO);
            // completedFacts is empty

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsCompleted));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
        }

        [Test]
        public void IsQuestFactTrue_Failed_AnyFactTrue_ReturnsTrue()
        {
            var failFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_dead"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.failedParts.Add(new QuestPart { fact = failFact });
            _cleanup.Add(questSO);
            _wsm.SetWorldEvent(failFact, true);

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsFailed));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
        }

        [Test]
        public void IsQuestFactTrue_NullFact_ReturnsFalse()
        {
            Assert.That(_wsm.IsQuestFactTrue(null), Is.False);
        }

        [Test]
        public void IsQuestFactTrue_NullQuest_ReturnsFalse()
        {
            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(null, QuestState.IsStarted));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
        }

        // ── IsQuestFactTrue — Step states ───────────────────────────────────────

        [Test]
        public void IsQuestFactTrue_StepState_AllPartsFulfilled_ReturnsTrue()
        {
            var f1 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("step0_done"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.steps.Add(new QuestStep
            {
                title = "Step A",
                parts = new List<QuestPart> { new QuestPart { fact = f1 } }
            });
            _cleanup.Add(questSO);
            _wsm.SetWorldEvent(f1, true);

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 0));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
        }

        [Test]
        public void IsQuestFactTrue_StepState_PartNotFulfilled_ReturnsFalse()
        {
            var f1 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("step0_done"));
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.steps.Add(new QuestStep
            {
                title = "Step A",
                parts = new List<QuestPart> { new QuestPart { fact = f1 } }
            });
            _cleanup.Add(questSO);
            // f1 NOT set

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 0));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
        }

        [Test]
        public void IsQuestFactTrue_StepState_EmptyParts_ReturnsFalse()
        {
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            questSO.steps.Add(new QuestStep { title = "Empty Step", parts = new List<QuestPart>() });
            _cleanup.Add(questSO);

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 0));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
        }

        [Test]
        public void IsQuestFactTrue_StepState_OutOfRangeIndex_ReturnsFalse()
        {
            var questSO = ScriptableObject.CreateInstance<QuestSO>();
            _cleanup.Add(questSO);
            // steps list is empty

            var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 99));
            Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
        }

        // ── Event broadcast ───────────────────────────────────────────────────

        [Test]
        public void SetWorldEvent_RaisesEvent_WithCorrectPayload()
        {
            var eventSO = ScriptableObject.CreateInstance<GameEventSO_Fact>();
            _cleanup.Add(eventSO);

            typeof(WorldStateManager)
                .GetField("_onFactChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_wsm, eventSO);

            FactData received = default;
            bool fired = false;
            eventSO.AddListener(data => { received = data; fired = true; });

            _wsm.SetWorldEvent(MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("mill_cleared")), true);

            Assert.That(fired, Is.True);
            Assert.That(received.key, Is.EqualTo("World.mill_cleared"));
            Assert.That(received.value, Is.True);
        }

        // ── Save / load: CaptureFacts / RestoreFacts ──────────────────────────

        private GameEventSO_Fact WireFactChangedEvent()
        {
            var eventSO = ScriptableObject.CreateInstance<GameEventSO_Fact>();
            _cleanup.Add(eventSO);
            typeof(WorldStateManager)
                .GetField("_onFactChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_wsm, eventSO);
            return eventSO;
        }

        [Test]
        public void CaptureFacts_ReturnsCopy()
        {
            var fact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("gate"));
            _wsm.SetWorldEvent(fact, true);

            var snapshot = _wsm.CaptureFacts();
            snapshot["World.gate"] = false;
            snapshot["World.extra"] = true;

            Assert.That(_wsm.GetFact(fact), Is.True);
            Assert.That(_wsm.CaptureFacts().ContainsKey("World.extra"), Is.False);
        }

        [Test]
        public void RestoreFacts_ReplacesAllFacts_AndRaisesNoEvent()
        {
            var oldFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("old"));
            var newFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("new"));
            _wsm.SetWorldEvent(oldFact, true);
            var eventSO = WireFactChangedEvent();
            int raised = 0;
            eventSO.AddListener(_ => raised++);

            _wsm.RestoreFacts(new Dictionary<string, bool> { { "World.new", true } });

            Assert.That(_wsm.GetFact(oldFact), Is.False);
            Assert.That(_wsm.GetFact(newFact), Is.True);
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void RestoreFacts_Null_ClearsFacts()
        {
            var fact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("x"));
            _wsm.SetWorldEvent(fact, true);
            _wsm.RestoreFacts(null);
            Assert.That(_wsm.CaptureFacts(), Is.Empty);
        }

        // ── QuestEventsManager.ReseedState after a load ───────────────────────

        private (QuestEventsManager qem, System.Func<int> completedCount) MakeQuestEventsManager(WorldFact completeFact)
        {
            var quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.title = "Test quest";
            quest.completedParts = new List<QuestPart> { new QuestPart { fact = completeFact } };
            _cleanup.Add(quest);

            var completedEvent = ScriptableObject.CreateInstance<GameEventSO_Quest>();
            _cleanup.Add(completedEvent);
            int count = 0;
            completedEvent.AddListener(_ => count++);

            var go = new GameObject("QuestEventsManager_Test");
            _cleanup.Add(go);
            var qem = go.AddComponent<QuestEventsManager>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(QuestEventsManager).GetField("_quests", flags).SetValue(qem, new List<QuestSO> { quest });
            typeof(QuestEventsManager).GetField("_onQuestCompleted", flags).SetValue(qem, completedEvent);
            qem.ReseedState(); // what Start() does — quest not completed yet
            return (qem, () => count);
        }

        private static void RaiseFactChanged(QuestEventsManager qem) =>
            typeof(QuestEventsManager)
                .GetMethod("HandleWorldFactChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(qem, new object[] { default(FactData) });

        [Test]
        public void ReseedState_AfterRestore_NextFactChangeRaisesNoStaleQuestEvent()
        {
            var completeFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("quest_done"));
            var (qem, completedCount) = MakeQuestEventsManager(completeFact);

            _wsm.RestoreFacts(new Dictionary<string, bool> { { "World.quest_done", true } });
            qem.ReseedState();
            RaiseFactChanged(qem); // an unrelated fact change after the load

            Assert.That(completedCount(), Is.EqualTo(0));
        }

        [Test]
        public void WithoutReseed_AfterRestore_NextFactChangeRaisesStaleQuestEvent()
        {
            var completeFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("quest_done"));
            var (qem, completedCount) = MakeQuestEventsManager(completeFact);

            _wsm.RestoreFacts(new Dictionary<string, bool> { { "World.quest_done", true } });
            RaiseFactChanged(qem);

            Assert.That(completedCount(), Is.EqualTo(1), "control: proves the reseed is what prevents the event");
        }
    }
}
