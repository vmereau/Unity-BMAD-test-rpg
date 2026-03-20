using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Game.Inventory;
using Game.UI;
using UnityEngine;

namespace Tests.EditMode
{
    public class InventoryPrimaryActionTests
    {
        // Test double — records whether OnUse was invoked without needing player systems
        private class TrackingUsableItem : UsableItemSO
        {
            public bool WasCalled;
            public override bool OnUse(GameObject user) { WasCalled = true; return false; }
        }

        private GameObject _testRoot;
        private GameObject _panelRootGO;
        private GameObject _contentRootGO;
        private GameObject _playerGO;
        private InventorySystem _inventory;
        private EquipmentSystem _equipment;
        private InventoryUI _inventoryUI;
        private readonly List<Object> _createdAssets = new();

        [SetUp]
        public void SetUp()
        {
            _playerGO = new GameObject("TestPlayer");

            // Keep root inactive so Awake fires only after all fields are injected
            _testRoot = new GameObject("InventoryUIRoot");
            _testRoot.SetActive(false);

            _panelRootGO = new GameObject("PanelRoot");
            _panelRootGO.transform.SetParent(_testRoot.transform);
            _contentRootGO = new GameObject("ContentRoot");
            _contentRootGO.transform.SetParent(_testRoot.transform);

            _inventory  = _testRoot.AddComponent<InventorySystem>();
            _equipment  = _testRoot.AddComponent<EquipmentSystem>();
            _inventoryUI = _testRoot.AddComponent<InventoryUI>();

            // Wire EquipmentSystem
            SetField(_equipment, "_inventorySystem", _inventory);

            // Wire InventoryUI
            SetField(_inventoryUI, "_panelRoot",        _panelRootGO);
            SetField(_inventoryUI, "_contentRoot",      _contentRootGO.transform);
            SetField(_inventoryUI, "_inventorySystem",  _inventory);
            SetField(_inventoryUI, "_equipmentSystem",  _equipment);
            SetField(_inventoryUI, "_playerTransform",  _playerGO.transform);

            // Activate — all Awakes fire now with dependencies set
            _testRoot.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_testRoot);
            Object.DestroyImmediate(_playerGO);
            foreach (var asset in _createdAssets)
                Object.DestroyImmediate(asset);
            _createdAssets.Clear();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        private WeaponSO CreateWeapon(string name = "Test Sword")
        {
            var item = ScriptableObject.CreateInstance<WeaponSO>();
            item.itemName = name;
            _createdAssets.Add(item);
            return item;
        }

        private TrackingUsableItem CreateUsable(string name = "Test Potion")
        {
            var item = ScriptableObject.CreateInstance<TrackingUsableItem>();
            item.itemName = name;
            _createdAssets.Add(item);
            return item;
        }

        private ItemSO CreateBaseItem(string name = "Key")
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = name;
            _createdAssets.Add(item);
            return item;
        }

        // ── AC 4 Tests ───────────────────────────────────────────────────────

        [Test]
        public void PrimaryAction_WeaponSO_CallsEquip()
        {
            var sword = CreateWeapon();
            _inventory.AddItem(sword);

            _inventoryUI.PrimaryAction(0);

            Assert.AreEqual(sword, _equipment.GetEquipped(EquipmentSlot.Weapon),
                "Weapon should be equipped in Weapon slot after PrimaryAction");
            Assert.AreEqual(0, _inventory.Count,
                "Weapon should be removed from inventory after equip");
        }

        [Test]
        public void PrimaryAction_UsableItemSO_CallsUse()
        {
            var potion = CreateUsable();
            _inventory.AddItem(potion);

            _inventoryUI.PrimaryAction(0);

            Assert.IsTrue(potion.WasCalled,
                "OnUse should have been called for a UsableItemSO");
        }

        [Test]
        public void PrimaryAction_BaseItemSO_IsNoOp()
        {
            var key = CreateBaseItem();
            _inventory.AddItem(key);

            Assert.DoesNotThrow(() => _inventoryUI.PrimaryAction(0),
                "PrimaryAction on a base ItemSO must not throw");

            // Item stays in inventory — no equip or use occurred
            Assert.AreEqual(1, _inventory.Count,
                "Base item should remain in inventory (no action taken)");
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                Assert.IsNull(_equipment.GetEquipped(slot),
                    $"No equipment slot should be filled for a base item (checked {slot})");
        }

        [Test]
        public void PrimaryAction_OutOfRange_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _inventoryUI.PrimaryAction(-1),
                "PrimaryAction(-1) must not throw");
            Assert.DoesNotThrow(() => _inventoryUI.PrimaryAction(999),
                "PrimaryAction(999) must not throw");
        }
    }
}
