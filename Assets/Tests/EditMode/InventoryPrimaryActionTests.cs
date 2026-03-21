using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Game.Inventory;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode
{
    public class InventoryPrimaryActionTests
    {
        private static readonly EquipmentSlot[] AllEquipmentSlots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

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
        private GameObject _slotPrefab;
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

            // Minimal slot prefab — required for RefreshSlots to not crash when inventory is non-empty
            _slotPrefab = new GameObject("SlotPrefab");
            _slotPrefab.SetActive(false); // defer Awake until Instantiate in the scene hierarchy
            var slotUI = _slotPrefab.AddComponent<ItemSlotUI>();
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(_slotPrefab.transform);
            var iconImage = iconGO.AddComponent<Image>();
            var nameGO = new GameObject("NameText");
            nameGO.transform.SetParent(_slotPrefab.transform);
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            SetField(slotUI, "_iconImage", iconImage);
            SetField(slotUI, "_nameText", nameText);

            // Wire InventoryUI
            SetField(_inventoryUI, "_panelRoot",        _panelRootGO);
            SetField(_inventoryUI, "_contentRoot",      _contentRootGO.transform);
            SetField(_inventoryUI, "_inventorySystem",  _inventory);
            SetField(_inventoryUI, "_equipmentSystem",  _equipment);
            SetField(_inventoryUI, "_playerTransform",  _playerGO.transform);
            SetField(_inventoryUI, "_itemSlotPrefab",   _slotPrefab);

            // Activate — all Awakes fire now with dependencies set
            _testRoot.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_testRoot);
            Object.DestroyImmediate(_playerGO);
            Object.DestroyImmediate(_slotPrefab);
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

        private ArmorSO CreateArmor(EquipmentSlot slot = EquipmentSlot.Helmet, string name = "Test Helmet")
        {
            var item = ScriptableObject.CreateInstance<ArmorSO>();
            item.itemName = name;
            item.slot = slot;
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
        public void PrimaryAction_EquipableItemSO_CallsEquip()
        {
            var helmet = CreateArmor(EquipmentSlot.Helmet);
            _inventory.AddItem(helmet);

            _inventoryUI.PrimaryAction(0);

            Assert.AreEqual(helmet, _equipment.GetEquipped(EquipmentSlot.Helmet),
                "ArmorSO (EquipableItemSO) should be equipped in Helmet slot after PrimaryAction");
            Assert.AreEqual(0, _inventory.Count,
                "Armor should be removed from inventory after equip");
        }

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
            foreach (EquipmentSlot slot in AllEquipmentSlots)
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
