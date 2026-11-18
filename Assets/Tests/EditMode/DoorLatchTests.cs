using System.Reflection;
using Game.Core;
using Game.Editor.QuestExplorer;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DoorLatchTests
    {
        private const string GUID_A = "e9a8939663dfd304ca7170ade154ddeb";
        private const string GUID_B = "0123456789abcdef0123456789abcdef";

        [TearDown]
        public void Cleanup() =>
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);

        [Test]
        public void IsOnLatchSide_OnlyInFrontOfTheLatch()
        {
            Assert.IsTrue(DoorLatch.IsOnLatchSide(Vector3.zero, Vector3.forward, new Vector3(0.5f, 0f, 1f)));
            Assert.IsFalse(DoorLatch.IsOnLatchSide(Vector3.zero, Vector3.forward, new Vector3(0.5f, 0f, -1f)));
        }

        [Test]
        public void RecordUnlatch_SetsTheFact_AndRefusesWithoutFactOrState()
        {
            var go = new GameObject("DoorLatchTest");
            go.SetActive(false);
            var fact = ScriptableObject.CreateInstance<WorldFact>().Init("test.unlatched");
            try
            {
                var state = go.AddComponent<WorldStateManager>();
                Assert.IsFalse(DoorLatch.RecordUnlatch(fact, null));
                Assert.IsFalse(DoorLatch.RecordUnlatch(null, state));
                Assert.IsFalse(state.GetFact(fact));
                Assert.IsTrue(DoorLatch.RecordUnlatch(fact, state));
                Assert.IsTrue(state.GetFact(fact));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(fact);
            }
        }

        [Test]
        public void FactGuidInBlock_MatchesTheWholeFieldName()
        {
            string latchBlock = "  _door: {fileID: 1971583701}\n  _unlatchedFact: {fileID: 11400000, guid: " + GUID_A + ", type: 2}\n";
            string triggerBlock = "  _fact: {fileID: 11400000, guid: " + GUID_B + ", type: 2}\n  _pollInterval: 0.25\n";

            Assert.AreEqual(GUID_A, QuestIndexCollector.FactGuidInBlock(latchBlock, "_unlatchedFact"));
            Assert.IsNull(QuestIndexCollector.FactGuidInBlock(latchBlock, "_fact"));
            Assert.AreEqual(GUID_B, QuestIndexCollector.FactGuidInBlock(triggerBlock, "_fact"));
            Assert.IsNull(QuestIndexCollector.FactGuidInBlock("  _unlatchedFact: {fileID: 0}\n", "_unlatchedFact"));
            Assert.IsNull(QuestIndexCollector.FactGuidInBlock(null, "_fact"));
        }
    }
}
