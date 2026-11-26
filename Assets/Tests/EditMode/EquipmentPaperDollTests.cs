using System.Collections.Generic;
using System.Reflection;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>spec-inventory-equipment-paper-doll: slot-targeted equip, slot swap and the paper-doll display rules.</summary>
    public class EquipmentPaperDollTests
    {
        private InventorySystem _inventory;
        private EquipmentSystem _equipment;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestPlayer");
            _inventory = go.AddComponent<InventorySystem>();
            _equipment = go.AddComponent<EquipmentSystem>();
            typeof(EquipmentSystem)
                .GetField("_inventorySystem", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_equipment, _inventory);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_equipment.gameObject);
            foreach (var o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T Make<T>(string name) where T : ItemSO
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.itemName = name;
            _created.Add(item);
            return item;
        }

        private ArmorSO Ring(string name = "Ring")
        {
            var ring = Make<ArmorSO>(name);
            ring.slot = EquipmentSlot.Ring1;
            return ring;
        }

        private WeaponSO TwoHander()
        {
            var archetype = ScriptableObject.CreateInstance<WeaponArchetypeSO>();
            archetype.twoHandedGrip = true;
            _created.Add(archetype);
            var weapon = Make<SwordSO>("Flamberge");
            weapon.archetype = archetype;
            return weapon;
        }

        private int IndexOf(ItemSO item)
        {
            for (int i = 0; i < _inventory.Count; i++)
                if (_inventory.Items[i].Item == item) return i;
            return -1;
        }

        // ── EquipmentSystem.Equip(index, slot) ──────────────────────────────────

        [Test]
        public void EquipIntoSlot_RingDroppedOnRing2_LandsInRing2EvenWithRing1Empty()
        {
            var ring = Ring();
            _inventory.AddItem(ring);

            Assert.IsTrue(_equipment.Equip(0, EquipmentSlot.Ring2));

            Assert.AreEqual(ring, _equipment.GetEquipped(EquipmentSlot.Ring2));
            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void EquipIntoSlot_InvalidSlot_RefusedAndNothingChanges()
        {
            var ring = Ring();
            _inventory.AddItem(ring);

            Assert.IsFalse(_equipment.Equip(0, EquipmentSlot.Weapon));

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(1, _inventory.Count);
        }

        [Test]
        public void EquipIntoSlot_OccupiedSlot_SwapsOldItemBackToInventory()
        {
            var sword = Make<SwordSO>("Sword");
            var axe = Make<SwordSO>("Axe");
            _inventory.AddItem(sword);
            _equipment.Equip(0);
            _inventory.AddItem(axe);

            Assert.IsTrue(_equipment.Equip(IndexOf(axe), EquipmentSlot.Weapon));

            Assert.AreEqual(axe, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreNotEqual(-1, IndexOf(sword));
        }

        [Test]
        public void EquipIntoSlot_ShieldOnOffhandNextToTwoHander_TwoHanderReturnsAndWeaponEmpties()
        {
            var flamberge = TwoHander();
            var shield = Make<ShieldSO>("Kite Shield");
            _inventory.AddItem(flamberge);
            _equipment.Equip(0);
            _inventory.AddItem(shield);

            Assert.IsTrue(_equipment.Equip(IndexOf(shield), EquipmentSlot.Offhand));

            Assert.AreEqual(shield, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreNotEqual(-1, IndexOf(flamberge));
        }

        // ── SwapEquipped / CanSwap ──────────────────────────────────────────────

        [Test]
        public void SwapEquipped_BothRings_TradePlaces()
        {
            var a = Ring("A");
            var b = Ring("B");
            _inventory.AddItem(a);
            _inventory.AddItem(b);
            _equipment.Equip(0);
            _equipment.Equip(0);

            Assert.IsTrue(_equipment.SwapEquipped(EquipmentSlot.Ring1, EquipmentSlot.Ring2));

            Assert.AreEqual(b, _equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.AreEqual(a, _equipment.GetEquipped(EquipmentSlot.Ring2));
        }

        [Test]
        public void SwapEquipped_RingOntoEmptyRing2_Moves()
        {
            var a = Ring("A");
            _inventory.AddItem(a);
            _equipment.Equip(0);

            Assert.IsTrue(_equipment.SwapEquipped(EquipmentSlot.Ring1, EquipmentSlot.Ring2));

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.AreEqual(a, _equipment.GetEquipped(EquipmentSlot.Ring2));
        }

        [Test]
        public void SwapEquipped_WeaponOntoOffhand_Refused()
        {
            var sword = Make<SwordSO>("Sword");
            _inventory.AddItem(sword);
            _equipment.Equip(0);

            Assert.IsFalse(_equipment.SwapEquipped(EquipmentSlot.Weapon, EquipmentSlot.Offhand));
            Assert.AreEqual(sword, _equipment.GetEquipped(EquipmentSlot.Weapon));
        }

        [Test]
        public void CanSwap_SameSlotOrBothEmpty_False()
        {
            var equipped = new Dictionary<EquipmentSlot, ItemSO> { [EquipmentSlot.Ring1] = Ring() };
            Assert.IsFalse(EquipmentRules.CanSwap(equipped, EquipmentSlot.Ring1, EquipmentSlot.Ring1));
            Assert.IsFalse(EquipmentRules.CanSwap(equipped, EquipmentSlot.Helmet, EquipmentSlot.Armor));
        }

        // ── EquipmentPanelRules ─────────────────────────────────────────────────

        [Test]
        public void IsValidTarget_RingFitsBothRingSlotsOnly()
        {
            var ring = Ring();
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                Assert.AreEqual(slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2,
                    EquipmentPanelRules.IsValidTarget(ring, slot), slot.ToString());
        }

        [Test]
        public void IsValidTarget_ShieldAndTorchOffhandOnly_WeaponWeaponOnly_NonEquippableNowhere()
        {
            var shield = Make<ShieldSO>("Shield");
            var torch = Make<TorchSO>("Torch");
            var sword = Make<SwordSO>("Sword");
            var food = Make<ItemSO>("Bread");
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                Assert.AreEqual(slot == EquipmentSlot.Offhand, EquipmentPanelRules.IsValidTarget(shield, slot));
                Assert.AreEqual(slot == EquipmentSlot.Offhand, EquipmentPanelRules.IsValidTarget(torch, slot));
                Assert.AreEqual(slot == EquipmentSlot.Weapon, EquipmentPanelRules.IsValidTarget(sword, slot));
                Assert.IsFalse(EquipmentPanelRules.IsValidTarget(food, slot));
            }
            Assert.IsFalse(EquipmentPanelRules.IsValidTarget(null, EquipmentSlot.Weapon));
        }

        [Test]
        public void ShowsOffhandGhost_OnlyForEmptyOffhandNextToTwoHandsWeapon()
        {
            var flamberge = TwoHander();
            var sword = Make<SwordSO>("Sword");
            var torch = Make<TorchSO>("Torch");
            Assert.IsTrue(EquipmentPanelRules.ShowsOffhandGhost(flamberge, null));
            Assert.IsTrue(EquipmentPanelRules.ShowsOffhandGhost(Make<BowSO>("Bow"), null));
            Assert.IsFalse(EquipmentPanelRules.ShowsOffhandGhost(flamberge, torch), "a torch is shown, not the ghost");
            Assert.IsFalse(EquipmentPanelRules.ShowsOffhandGhost(sword, null));
            Assert.IsFalse(EquipmentPanelRules.ShowsOffhandGhost(null, null));
        }

        [Test]
        public void ActionSlot_GhostOffhandActsOnWeapon()
        {
            Assert.AreEqual(EquipmentSlot.Weapon, EquipmentPanelRules.ActionSlot(EquipmentSlot.Offhand, true));
            Assert.AreEqual(EquipmentSlot.Offhand, EquipmentPanelRules.ActionSlot(EquipmentSlot.Offhand, false));
            Assert.AreEqual(EquipmentSlot.Ring2, EquipmentPanelRules.ActionSlot(EquipmentSlot.Ring2, true));
        }

        [TestCase(false, false, false, false, EquipmentSlotFrame.Normal)]
        [TestCase(false, false, true, false, EquipmentSlotFrame.Hover)]
        [TestCase(true, false, true, false, EquipmentSlotFrame.Selected)]
        [TestCase(false, true, false, false, EquipmentSlotFrame.Valid)]
        [TestCase(false, true, true, false, EquipmentSlotFrame.Valid)]
        [TestCase(false, true, true, true, EquipmentSlotFrame.ValidHover)]
        [TestCase(true, true, false, true, EquipmentSlotFrame.Valid)]
        public void ResolveFrame_Precedence(bool selected, bool valid, bool hovered, bool dragging, EquipmentSlotFrame expected)
        {
            Assert.AreEqual(expected, EquipmentPanelRules.ResolveFrame(selected, valid, hovered, dragging));
        }

        [Test]
        public void Labels_MatchVoiceTable()
        {
            Assert.AreEqual("Ring", EquipmentPanelRules.Caption(EquipmentSlot.Ring2));
            Assert.AreEqual("Necklace", EquipmentPanelRules.Caption(EquipmentSlot.Necklace));
            Assert.AreEqual("NECK", EquipmentPanelRules.EmptyLabel(EquipmentSlot.Necklace));
            Assert.AreEqual("OFFHAND", EquipmentPanelRules.EmptyLabel(EquipmentSlot.Offhand));
            Assert.AreEqual("RING", EquipmentPanelRules.EmptyLabel(EquipmentSlot.Ring1));
        }
    }
}
