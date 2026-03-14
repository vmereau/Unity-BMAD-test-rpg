using System.Collections.Generic;
using NUnit.Framework;
using Game.Inventory;
using UnityEngine;

namespace Tests.EditMode
{
    public class InventorySystemTests
    {
        private InventorySystem _inventory;
        private readonly List<ItemSO> _createdItems = new List<ItemSO>();

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestInventory");
            _inventory = go.AddComponent<InventorySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inventory.gameObject);
            foreach (var item in _createdItems)
                Object.DestroyImmediate(item);
            _createdItems.Clear();
        }

        private ItemSO CreateTestItem(string name = "Test Item")
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = name;
            _createdItems.Add(item);
            return item;
        }

        [Test]
        public void AddItem_ValidItem_ReturnsTrue()
        {
            var item = CreateTestItem();
            Assert.IsTrue(_inventory.AddItem(item));
        }

        [Test]
        public void AddItem_ValidItem_IncreasesCount()
        {
            var item = CreateTestItem();
            _inventory.AddItem(item);
            Assert.AreEqual(1, _inventory.Count);
        }

        [Test]
        public void AddItem_NullItem_ReturnsFalse()
        {
            Assert.IsFalse(_inventory.AddItem(null));
        }

        [Test]
        public void AddItem_NullItem_CountUnchanged()
        {
            _inventory.AddItem(null);
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void AddItem_MultipleItems_AllAdded()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));
            _inventory.AddItem(CreateTestItem("C"));
            Assert.AreEqual(3, _inventory.Count);
        }

        [Test]
        public void Items_ReflectsAddedItems()
        {
            var item1 = CreateTestItem("Sword");
            var item2 = CreateTestItem("Shield");
            _inventory.AddItem(item1);
            _inventory.AddItem(item2);

            Assert.AreEqual(2, _inventory.Items.Count);
            Assert.AreEqual(item1, _inventory.Items[0]);
            Assert.AreEqual(item2, _inventory.Items[1]);
        }

        [Test]
        public void ItemPickup_Configure_SetsInteractPrompt()
        {
            var go = new GameObject("TestPickup");
            var pickup = go.AddComponent<ItemPickup>();
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = "Magic Sword";

            pickup.Configure(item);

            Assert.AreEqual("Press E to pick up Magic Sword", pickup.InteractPrompt);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(item);
        }

        // ItemPickup null-guard: disabled component must not throw when Interact() is called
        // (InteractionSystem.GetComponentInParent returns disabled components — HIGH-1 regression guard)
        [Test]
        public void ItemPickup_Interact_WhenDisabledDueToNullItem_DoesNotThrow()
        {
            var go = new GameObject("TestPickup");
            var pickup = go.AddComponent<ItemPickup>();
            // Awake ran: _item == null → enabled = false, _inventory == null
            Assert.IsFalse(pickup.enabled, "ItemPickup should be disabled when _item is null");
            Assert.DoesNotThrow(() => pickup.Interact());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MoveItem_ValidIndices_SwapsItems()
        {
            var itemA = CreateTestItem("A");
            var itemB = CreateTestItem("B");
            var itemC = CreateTestItem("C");
            _inventory.AddItem(itemA);
            _inventory.AddItem(itemB);
            _inventory.AddItem(itemC);

            _inventory.MoveItem(0, 2);

            Assert.AreEqual(itemC, _inventory.Items[0]);
            Assert.AreEqual(itemB, _inventory.Items[1]);
            Assert.AreEqual(itemA, _inventory.Items[2]);
        }

        [Test]
        public void MoveItem_SameIndex_NoChange()
        {
            var itemA = CreateTestItem("A");
            _inventory.AddItem(itemA);

            _inventory.MoveItem(0, 0);

            Assert.AreEqual(1, _inventory.Count);
            Assert.AreEqual(itemA, _inventory.Items[0]);
        }

        [Test]
        public void MoveItem_OutOfBoundsFrom_LogsWarn_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));

            Assert.DoesNotThrow(() => _inventory.MoveItem(-1, 0));
            Assert.AreEqual(2, _inventory.Count);
        }

        [Test]
        public void MoveItem_OutOfBoundsTo_LogsWarn_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));

            Assert.DoesNotThrow(() => _inventory.MoveItem(0, 5));
            Assert.AreEqual(2, _inventory.Count);
        }

        [Test]
        public void RemoveItem_ValidIndex_RemovesAndReturnsItem()
        {
            var itemA = CreateTestItem("A");
            var itemB = CreateTestItem("B");
            var itemC = CreateTestItem("C");
            _inventory.AddItem(itemA);
            _inventory.AddItem(itemB);
            _inventory.AddItem(itemC);

            var removed = _inventory.RemoveItem(1);

            Assert.AreEqual(itemB, removed);
            Assert.AreEqual(2, _inventory.Count);
            Assert.AreEqual(itemA, _inventory.Items[0]);
            Assert.AreEqual(itemC, _inventory.Items[1]);
        }

        [Test]
        public void RemoveItem_OutOfBoundsIndex_ReturnsNull_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));

            var result = _inventory.RemoveItem(5);

            Assert.IsNull(result);
            Assert.AreEqual(2, _inventory.Count);
        }

        [Test]
        public void RemoveItem_NegativeIndex_ReturnsNull_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));

            var result = _inventory.RemoveItem(-1);

            Assert.IsNull(result);
            Assert.AreEqual(1, _inventory.Count);
        }
    }
}
