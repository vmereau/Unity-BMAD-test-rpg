using System.Collections.Generic;
using Game.Core;
using Game.Editor.QuestExplorer;
using Game.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the Undo-aware NPC memory condition edits in <see cref="QuestEditActions"/>.
    /// Uses in-memory ScriptableObjects only; their Undo history is cleared in TearDown.
    /// </summary>
    public class QuestMemoryEditActionsTests
    {
        private const MemoryConditionList UNLOCK = MemoryConditionList.Unlock;

        private readonly List<Object> _cleanup = new List<Object>();
        private NPCMemoryEntrySO _memory;
        private DialogueFact _a;
        private DialogueFact _b;

        [SetUp]
        public void SetUp()
        {
            _memory = Make<NPCMemoryEntrySO>("Mem_Test");
            _a = Make<DialogueFact>("A").Init("A");
            _b = Make<DialogueFact>("B").Init("B");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
            {
                if (obj == null) continue;
                Undo.ClearUndo(obj);
                Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            so.name = name;
            _cleanup.Add(so);
            return so;
        }

        [Test]
        public void Add_AppendsFact_AndRejectsNullAndDuplicates()
        {
            Assert.IsTrue(QuestEditActions.AddMemoryCondition(_memory, UNLOCK, _a));
            Assert.IsTrue(QuestEditActions.AddMemoryCondition(_memory, UNLOCK, _b));
            Assert.IsFalse(QuestEditActions.AddMemoryCondition(_memory, UNLOCK, _a), "duplicate");
            Assert.IsFalse(QuestEditActions.AddMemoryCondition(_memory, UNLOCK, null), "null");
            CollectionAssert.AreEqual(new Fact[] { _a, _b }, _memory.unlockConditions);
            Assert.IsTrue(_memory.invalidationConditions == null || _memory.invalidationConditions.Length == 0);
        }

        [Test]
        public void Add_IsUndoable()
        {
            QuestEditActions.AddMemoryCondition(_memory, MemoryConditionList.Invalidation, _a);
            Undo.PerformUndo();
            Assert.AreEqual(0, _memory.invalidationConditions?.Length ?? 0);
        }

        [Test]
        public void Remove_ShrinksArray_AndUndoRestores()
        {
            _memory.unlockConditions = new Fact[] { _a, _b };

            Assert.IsTrue(QuestEditActions.RemoveMemoryCondition(_memory, UNLOCK, 0));
            CollectionAssert.AreEqual(new Fact[] { _b }, _memory.unlockConditions);

            Undo.PerformUndo();
            CollectionAssert.AreEqual(new Fact[] { _a, _b }, _memory.unlockConditions);
        }

        [Test]
        public void Remove_NullSlot_ShrinksArray()
        {
            _memory.unlockConditions = new Fact[] { null, _a };
            Assert.IsTrue(QuestEditActions.RemoveMemoryCondition(_memory, UNLOCK, 0));
            CollectionAssert.AreEqual(new Fact[] { _a }, _memory.unlockConditions);
        }

        [Test]
        public void Set_ReplacesInBounds_RejectsOutOfRangeAndDuplicates()
        {
            _memory.unlockConditions = new Fact[] { _a, null };

            Assert.IsTrue(QuestEditActions.SetMemoryCondition(_memory, UNLOCK, 1, _b));
            Assert.IsFalse(QuestEditActions.SetMemoryCondition(_memory, UNLOCK, 1, _a), "duplicate");
            Assert.IsFalse(QuestEditActions.SetMemoryCondition(_memory, UNLOCK, 2, _a), "out of range");
            Assert.IsFalse(QuestEditActions.RemoveMemoryCondition(_memory, UNLOCK, -1), "out of range");
            CollectionAssert.AreEqual(new Fact[] { _a, _b }, _memory.unlockConditions);

            Undo.PerformUndo();
            CollectionAssert.AreEqual(new Fact[] { _a, null }, _memory.unlockConditions);
        }
    }
}
