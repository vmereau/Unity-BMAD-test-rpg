using NUnit.Framework;
using UnityEngine;
using Game.Inventory;

namespace Tests.EditMode
{
    public class ActionBarSystemTests
    {
        private GameObject _go;
        private ActionBarSystem _system;
        private GameObject _inventoryGO;
        private InventorySystem _inventory;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ActionBarSystemTestGO");
            _system = _go.AddComponent<ActionBarSystem>();

            _inventoryGO = new GameObject("InventorySystemTestGO");
            _inventory = _inventoryGO.AddComponent<InventorySystem>();

            typeof(ActionBarSystem)
                .GetField("_inventorySystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_system, _inventory);

            // Use own transform as stand-in for _playerTransform
            typeof(ActionBarSystem)
                .GetField("_playerTransform",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_system, _go.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_inventoryGO);
        }

        [Test]
        public void Assign_StoresSlotReference()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.name = "TestPotion";

            _system.Assign(0, 3, item);

            var result = _system.GetSlot(0);
            Assert.IsNotNull(result, "Slot 0 should not be null after Assign.");
            Assert.AreEqual(item, result.Value.Item);
            Assert.AreEqual(3, result.Value.InventoryIndex);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void ClearSlot_RemovesReference()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();

            _system.Assign(0, 2, item);
            _system.ClearSlot(0);

            Assert.IsNull(_system.GetSlot(0), "Slot 0 should be null after ClearSlot.");

            Object.DestroyImmediate(item);
        }

        [Test]
        public void ValidateSlots_ClearsStaleSlot_WhenInventoryIndexOutOfRange()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            // Assign at index 3 on empty inventory → stale
            _system.Assign(0, 3, item);

            _system.ValidateSlots();

            Assert.IsNull(_system.GetSlot(0), "Slot should be cleared when inventory index is out of range.");

            Object.DestroyImmediate(item);
        }

        [Test]
        public void ValidateSlots_ClearsStaleSlot_WhenItemMismatch()
        {
            var itemA = ScriptableObject.CreateInstance<ItemSO>();
            itemA.name = "ItemA";
            var itemB = ScriptableObject.CreateInstance<ItemSO>();
            itemB.name = "ItemB";

            _inventory.AddItem(itemB); // index 0 has itemB

            _system.Assign(0, 0, itemA); // assigned itemA, but inventory[0] is itemB

            _system.ValidateSlots();

            Assert.IsNull(_system.GetSlot(0), "Slot should be cleared when item at inventory index does not match.");

            Object.DestroyImmediate(itemA);
            Object.DestroyImmediate(itemB);
        }

        [Test]
        public void ClearSlot_OutOfRange_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _system.ClearSlot(-1), "ClearSlot(-1) should not throw.");
            Assert.DoesNotThrow(() => _system.ClearSlot(6), "ClearSlot(6) should not throw.");
        }

        [Test]
        public void Assign_OutOfRange_DoesNotThrow()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            Assert.DoesNotThrow(() => _system.Assign(-1, 0, item), "Assign(-1,...) should not throw.");
            Assert.DoesNotThrow(() => _system.Assign(6, 0, item), "Assign(6,...) should not throw.");
            Object.DestroyImmediate(item);
        }
    }
}
