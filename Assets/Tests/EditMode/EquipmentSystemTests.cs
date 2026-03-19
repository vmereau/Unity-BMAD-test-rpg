using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Game.Inventory;
using UnityEngine;

namespace Tests.EditMode
{
    public class EquipmentSystemTests
    {
        private InventorySystem _inventory;
        private EquipmentSystem _equipment;
        private readonly List<ItemSO> _createdItems = new();

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestPlayer");
            _inventory = go.AddComponent<InventorySystem>();
            _equipment = go.AddComponent<EquipmentSystem>();

            // Wire _inventorySystem into EquipmentSystem via reflection (bypasses Awake null-guard)
            typeof(EquipmentSystem)
                .GetField("_inventorySystem", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_equipment, _inventory);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_equipment.gameObject);
            foreach (var item in _createdItems)
                Object.DestroyImmediate(item);
            _createdItems.Clear();
        }

        private WeaponSO CreateWeapon(string name = "Test Sword")
        {
            var item = ScriptableObject.CreateInstance<WeaponSO>();
            item.itemName = name;
            _createdItems.Add(item);
            return item;
        }

        private ArmorSO CreateArmor(string name, EquipmentSlot slot)
        {
            var item = ScriptableObject.CreateInstance<ArmorSO>();
            item.itemName = name;
            item.slot = slot;
            _createdItems.Add(item);
            return item;
        }

        private ArmorSO CreateRing(string name = "Test Ring")
            => CreateArmor(name, EquipmentSlot.Ring1);

        // ── AC 9 Tests ──────────────────────────────────────────────────────

        [Test]
        public void Equip_Weapon_OccupiesWeaponSlot()
        {
            var sword = CreateWeapon();
            _inventory.AddItem(sword);

            _equipment.Equip(0);

            Assert.AreEqual(sword, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(0, _inventory.Count, "Item should be removed from inventory after equip");
        }

        [Test]
        public void Equip_Ring_FillsRing1First()
        {
            var ring = CreateRing();
            _inventory.AddItem(ring);

            _equipment.Equip(0);

            Assert.AreEqual(ring, _equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Ring2), "Ring2 should be empty");
        }

        [Test]
        public void Equip_SecondRing_FillsRing2()
        {
            var ring1 = CreateRing("Ring A");
            var ring2 = CreateRing("Ring B");
            _inventory.AddItem(ring1);
            _inventory.AddItem(ring2);

            _equipment.Equip(0); // ring1 → Ring1
            _equipment.Equip(0); // ring2 is now at index 0 (ring1 removed from inventory)

            Assert.AreEqual(ring1, _equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.AreEqual(ring2, _equipment.GetEquipped(EquipmentSlot.Ring2));
        }

        [Test]
        public void Equip_ThirdRing_BumpsRing1ToInventory()
        {
            var ring1 = CreateRing("Ring A");
            var ring2 = CreateRing("Ring B");
            var ring3 = CreateRing("Ring C");
            _inventory.AddItem(ring1);
            _inventory.AddItem(ring2);
            _inventory.AddItem(ring3);

            _equipment.Equip(0); // ring1 → Ring1
            _equipment.Equip(0); // ring2 → Ring2 (ring1 is still at inventory index 0? no — ring1 removed, ring2 is now 0)
            // After first equip: inventory=[ring2, ring3]
            // After second equip: inventory=[ring3], Ring1=ring1, Ring2=ring2
            _equipment.Equip(0); // ring3 → Ring1 (bumps ring1 back to inventory)
            // After third equip: Ring1=ring3, Ring2=ring2, inventory=[ring1]

            Assert.AreEqual(ring3, _equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.AreEqual(ring2, _equipment.GetEquipped(EquipmentSlot.Ring2));
            Assert.AreEqual(1, _inventory.Count, "Bumped ring should be back in inventory");
            Assert.AreEqual(ring1, _inventory.Items[0].Item);
        }

        [Test]
        public void Equip_SwapsExistingItem_WhenSlotOccupied()
        {
            var helmet1 = CreateArmor("Helmet A", EquipmentSlot.Helmet);
            var helmet2 = CreateArmor("Helmet B", EquipmentSlot.Helmet);
            _inventory.AddItem(helmet1);
            _inventory.AddItem(helmet2);

            _equipment.Equip(0); // helmet1 → Helmet slot; inventory=[helmet2]
            _equipment.Equip(0); // helmet2 → Helmet slot; helmet1 bumped back; inventory=[helmet1]

            Assert.AreEqual(helmet2, _equipment.GetEquipped(EquipmentSlot.Helmet));
            Assert.AreEqual(1, _inventory.Count, "Old helmet should be returned to inventory");
            Assert.AreEqual(helmet1, _inventory.Items[0].Item);
        }

        [Test]
        public void Unequip_ReturnsItemToInventory()
        {
            var sword = CreateWeapon();
            _inventory.AddItem(sword);
            _equipment.Equip(0);

            _equipment.Unequip(EquipmentSlot.Weapon);

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon), "Weapon slot should be empty");
            Assert.AreEqual(1, _inventory.Count, "Item should be back in inventory");
            Assert.AreEqual(sword, _inventory.Items[0].Item);
        }

        [Test]
        public void Equip_NonEquippable_DoesNothing()
        {
            var plainItem = ScriptableObject.CreateInstance<ItemSO>();
            plainItem.itemName = "Rock";
            _createdItems.Add(plainItem);
            _inventory.AddItem(plainItem);

            Assert.DoesNotThrow(() => _equipment.Equip(0));

            // No slot should be occupied
            foreach (EquipmentSlot s in System.Enum.GetValues(typeof(EquipmentSlot)))
                Assert.IsNull(_equipment.GetEquipped(s), $"Slot {s} should be empty");

            Assert.AreEqual(1, _inventory.Count, "Non-equippable item should remain in inventory");
        }

        [Test]
        public void IsEquipped_ReturnsTrue_WhenItemEquipped()
        {
            var sword = CreateWeapon();
            _inventory.AddItem(sword);
            _equipment.Equip(0);

            Assert.IsTrue(_equipment.IsEquipped(sword));
        }
    }
}
