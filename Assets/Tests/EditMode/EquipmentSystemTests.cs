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
        private readonly List<WeaponArchetypeSO> _createdArchetypes = new();

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
            foreach (var archetype in _createdArchetypes)
                Object.DestroyImmediate(archetype);
            _createdArchetypes.Clear();
        }

        private WeaponSO CreateWeapon(string name = "Test Sword")
        {
            var item = ScriptableObject.CreateInstance<SwordSO>();
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

        private ShieldSO CreateShield(string name = "Test Shield")
        {
            var item = ScriptableObject.CreateInstance<ShieldSO>();
            item.itemName = name;
            _createdItems.Add(item);
            return item;
        }

        private WeaponSO CreateTwoHander(string name = "Test Flamberge")
        {
            var archetype = ScriptableObject.CreateInstance<WeaponArchetypeSO>();
            archetype.twoHandedGrip = true;
            _createdArchetypes.Add(archetype);
            var item = CreateWeapon(name);
            item.archetype = archetype;
            return item;
        }

        private BowSO CreateBow(string name = "Test Bow")
        {
            var item = ScriptableObject.CreateInstance<BowSO>();
            item.itemName = name;
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

        // ── Offhand / shields (spec-shield-offhand-blocking) ────────────────

        [Test]
        public void Equip_Shield_GoesToOffhand_NextToOneHandedWeapon()
        {
            var sword = CreateWeapon();
            var shield = CreateShield();
            _inventory.AddItem(sword);
            _inventory.AddItem(shield);

            _equipment.Equip(sword);
            _equipment.Equip(shield);

            Assert.AreEqual(sword, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(shield, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(shield, _equipment.EquippedShield);
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void Equip_TwoHander_BumpsShieldToInventory()
        {
            var shield = CreateShield();
            var flamberge = CreateTwoHander();
            _inventory.AddItem(shield);
            _inventory.AddItem(flamberge);

            _equipment.Equip(shield);
            _equipment.Equip(flamberge);

            Assert.AreEqual(flamberge, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.Count);
            Assert.AreEqual(shield, _inventory.Items[0].Item);
        }

        [Test]
        public void Equip_Shield_BumpsBowToInventory()
        {
            var bow = CreateBow();
            var shield = CreateShield();
            _inventory.AddItem(bow);
            _inventory.AddItem(shield);

            _equipment.Equip(bow);
            _equipment.Equip(shield);

            Assert.AreEqual(shield, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(bow, _inventory.Items[0].Item);
        }

        [Test]
        public void Equip_SecondShield_ReplacesFirst()
        {
            var a = CreateShield("A");
            var b = CreateShield("B");
            _inventory.AddItem(a);
            _inventory.AddItem(b);

            _equipment.Equip(a);
            _equipment.Equip(b);

            Assert.AreEqual(b, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(a, _inventory.Items[0].Item);
        }

        [Test]
        public void RestoreEquipped_TwoHanderAndShield_DropsShield()
        {
            var flamberge = CreateTwoHander();
            var shield = CreateShield();

            _equipment.RestoreEquipped(new Dictionary<EquipmentSlot, ItemSO>
            {
                { EquipmentSlot.Weapon, flamberge },
                { EquipmentSlot.Offhand, shield },
            });

            Assert.AreEqual(flamberge, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.CountOf(shield), "the dropped shield goes back into the inventory, never lost");
        }

        [Test]
        public void ShouldHoldShield_OnlyWithDrawnWeaponLeavingOffhandFree()
        {
            var sword = CreateWeapon();
            Assert.IsTrue(EquipmentVisuals.ShouldHoldShield(true, true, true, sword));
            Assert.IsFalse(EquipmentVisuals.ShouldHoldShield(true, false, true, sword), "sheathed");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldShield(true, true, false, null), "fists");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldShield(false, true, true, sword), "no shield");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldShield(true, true, true, CreateBow()), "bow");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldShield(true, true, true, CreateTwoHander()), "two-hander");
        }

        [Test]
        public void RestoreEquipped_ShieldInWeaponSlot_Skipped()
        {
            var shield = CreateShield();

            _equipment.RestoreEquipped(new Dictionary<EquipmentSlot, ItemSO> { { EquipmentSlot.Weapon, shield } });

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon));
        }

        [Test]
        public void OccupiesOffhand_OneHanderFalse_TwoHanderAndBowTrue()
        {
            Assert.IsFalse(CreateWeapon().OccupiesOffhand);
            Assert.IsTrue(CreateTwoHander().OccupiesOffhand);
            Assert.IsTrue(CreateBow().OccupiesOffhand);
        }

        [Test]
        public void ResolveOffhandSocket_IsTheHandNotHoldingTheWeapon()
        {
            var right = new GameObject("R").transform;
            var left = new GameObject("L").transform;
            try
            {
                Assert.AreEqual(left, EquipmentVisuals.ResolveOffhandSocket(right, right, left));
                Assert.AreEqual(right, EquipmentVisuals.ResolveOffhandSocket(left, right, left));
                Assert.AreEqual(left, EquipmentVisuals.ResolveOffhandSocket(null, right, left));
            }
            finally
            {
                Object.DestroyImmediate(right.gameObject);
                Object.DestroyImmediate(left.gameObject);
            }
        }
    }
}
