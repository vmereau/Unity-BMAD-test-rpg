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

        // spec-action-bar-equip-swap ------------------------------------------------------------------

        private EquipmentSystem AddEquipment()
        {
            var eq = _inventoryGO.AddComponent<EquipmentSystem>();
            typeof(EquipmentSystem).GetField("_inventorySystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(eq, _inventory);
            typeof(ActionBarSystem).GetField("_equipmentSystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_system, eq);
            return eq;
        }

        [Test]
        public void ValidateSlots_KeepsSlot_WhenItemIsEquipped()
        {
            var eq = AddEquipment();
            var sword = ScriptableObject.CreateInstance<SwordSO>();
            _inventory.AddItem(sword);
            _system.Assign(0, 0, sword);

            eq.Equip(sword);
            _system.ValidateSlots();

            var slot = _system.GetSlot(0);
            Assert.IsNotNull(slot, "An equipped item's bar slot must survive validation.");
            Assert.AreEqual(ActionBarSystem.EQUIPPED_INDEX, slot.Value.InventoryIndex);
            Assert.IsTrue(_system.IsSlotEquipped(0));
            Object.DestroyImmediate(sword);
        }

        [Test]
        public void TryUseSlot_EquipsInPlaceOfCurrentWeapon_AndKeepsBothSlots()
        {
            var eq = AddEquipment();
            var sword = ScriptableObject.CreateInstance<SwordSO>();
            var bow = ScriptableObject.CreateInstance<BowSO>();
            _inventory.AddItem(sword);
            _inventory.AddItem(bow);
            _system.Assign(0, 0, sword);
            _system.Assign(1, 1, bow);

            _system.TryUseSlot(0);
            Assert.AreEqual(sword, eq.GetEquipped(EquipmentSlot.Weapon), "Empty weapon slot: the sword is equipped.");

            _system.TryUseSlot(1);
            Assert.AreEqual(bow, eq.GetEquipped(EquipmentSlot.Weapon), "The bow replaces the sword.");
            Assert.IsTrue(_inventory.Items.Count == 1 && _inventory.Items[0].Item == sword, "The sword goes back to the inventory.");

            _system.ValidateSlots();
            Assert.IsNotNull(_system.GetSlot(0));
            Assert.IsNotNull(_system.GetSlot(1));
            Assert.IsFalse(_system.IsSlotEquipped(0));
            Assert.IsTrue(_system.IsSlotEquipped(1));
            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(bow);
        }

        [Test]
        public void Equip_KeepsBarSlot_WhenInventoryListenerValidatesDuringEquip()
        {
            // In game ActionBarUI re-validates on OnPlayerInventoryChanged, raised inside Equip's RemoveItem.
            var eq = AddEquipment();
            var channel = ScriptableObject.CreateInstance<Game.Core.GameEventSO_Void>();
            typeof(InventorySystem).GetField("_onInventoryChanged",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_inventory, channel);
            System.Action<bool> validate = _ => _system.ValidateSlots();
            channel.AddListener(validate);

            var sword = ScriptableObject.CreateInstance<SwordSO>();
            _inventory.AddItem(sword);
            _system.Assign(0, 0, sword);
            _system.TryUseSlot(0);

            Assert.IsNotNull(_system.GetSlot(0), "The bar slot must survive the inventory event raised during Equip.");
            Assert.IsTrue(_system.IsSlotEquipped(0));
            channel.RemoveListener(validate);
            Object.DestroyImmediate(channel);
            Object.DestroyImmediate(sword);
        }

        [Test]
        public void TryUseSlot_AlreadyEquipped_DoesNothing()
        {
            var eq = AddEquipment();
            var sword = ScriptableObject.CreateInstance<SwordSO>();
            _inventory.AddItem(sword);
            _system.Assign(0, 0, sword);
            _system.TryUseSlot(0);

            Assert.DoesNotThrow(() => _system.TryUseSlot(0));
            Assert.AreEqual(sword, eq.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(0, _inventory.Count, "Using an equipped item's slot must not unequip it.");
            Object.DestroyImmediate(sword);
        }
    }
}
