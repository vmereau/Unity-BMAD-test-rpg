using System.Collections.Generic;
using System.Reflection;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Off-hand torch (spec-torch-offhand-item): slot rules next to two-handers / bows / shields, save restore, the held / lit
    /// rules, the arm-pose gate, the detail category and the flicker math.
    /// </summary>
    public class TorchTests
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

        private T Create<T>(string name) where T : ItemSO
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.itemName = name;
            _created.Add(item);
            return item;
        }

        private TorchSO CreateTorch() => Create<TorchSO>("Torch");
        private WeaponSO CreateSword() => Create<SwordSO>("Sword");
        private BowSO CreateBow() => Create<BowSO>("Bow");

        private WeaponSO CreateTwoHander()
        {
            var archetype = ScriptableObject.CreateInstance<WeaponArchetypeSO>();
            archetype.twoHandedGrip = true;
            _created.Add(archetype);
            var item = Create<SwordSO>("Flamberge");
            item.archetype = archetype;
            return item;
        }

        // ── Slot rules ──────────────────────────────────────────────────────

        [Test]
        public void Equip_Torch_GoesToOffhand()
        {
            var torch = CreateTorch();
            _inventory.AddItem(torch);

            _equipment.Equip(torch);

            Assert.AreEqual(torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.IsNull(_equipment.EquippedShield, "a torch is not a shield");
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void Equip_TwoHander_KeepsTorch()
        {
            var torch = CreateTorch();
            var flamberge = CreateTwoHander();
            _inventory.AddItem(torch);
            _inventory.AddItem(flamberge);

            _equipment.Equip(torch);
            _equipment.Equip(flamberge);

            Assert.AreEqual(flamberge, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void Equip_Torch_KeepsBow()
        {
            var bow = CreateBow();
            var torch = CreateTorch();
            _inventory.AddItem(bow);
            _inventory.AddItem(torch);

            _equipment.Equip(bow);
            _equipment.Equip(torch);

            Assert.AreEqual(bow, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
        }

        [Test]
        public void Equip_Shield_ReplacesTorch()
        {
            var torch = CreateTorch();
            var shield = Create<ShieldSO>("Shield");
            _inventory.AddItem(torch);
            _inventory.AddItem(shield);

            _equipment.Equip(torch);
            _equipment.Equip(shield);

            Assert.AreEqual(shield, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(torch, _inventory.Items[0].Item);
        }

        [Test]
        public void Equip_TwoHander_StillBumpsShield()
        {
            var shield = Create<ShieldSO>("Shield");
            var flamberge = CreateTwoHander();
            _inventory.AddItem(shield);
            _inventory.AddItem(flamberge);

            _equipment.Equip(shield);
            _equipment.Equip(flamberge);

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(shield, _inventory.Items[0].Item);
        }

        [Test]
        public void RestoreEquipped_TwoHanderAndTorch_KeepsBoth()
        {
            var flamberge = CreateTwoHander();
            var torch = CreateTorch();

            _equipment.RestoreEquipped(new Dictionary<EquipmentSlot, ItemSO>
            {
                { EquipmentSlot.Weapon, flamberge },
                { EquipmentSlot.Offhand, torch },
            });

            Assert.AreEqual(flamberge, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void FitsSlot_Torch_OffhandOnly()
        {
            var torch = CreateTorch();
            Assert.IsTrue(EquipmentRules.FitsSlot(torch, EquipmentSlot.Offhand));
            Assert.IsFalse(EquipmentRules.FitsSlot(torch, EquipmentSlot.Weapon));
        }

        // ── Held / lit rules ────────────────────────────────────────────────

        [Test]
        public void ShouldHoldTorch_InHandUnlessTwoHandsDrawn()
        {
            Assert.IsTrue(EquipmentVisuals.ShouldHoldTorch(true, false, null), "unarmed, sheathed");
            Assert.IsTrue(EquipmentVisuals.ShouldHoldTorch(true, true, null), "fists drawn");
            Assert.IsTrue(EquipmentVisuals.ShouldHoldTorch(true, true, CreateSword()), "one-hander drawn");
            Assert.IsTrue(EquipmentVisuals.ShouldHoldTorch(true, false, CreateTwoHander()), "two-hander sheathed");
            Assert.IsTrue(EquipmentVisuals.ShouldHoldTorch(true, false, CreateBow()), "bow sheathed");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldTorch(true, true, CreateTwoHander()), "two-hander drawn");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldTorch(true, true, CreateBow()), "bow drawn");
            Assert.IsFalse(EquipmentVisuals.ShouldHoldTorch(false, false, null), "no torch");
            var leftHand = ScriptableObject.CreateInstance<WeaponArchetypeSO>();
            leftHand.drawnHand = WeaponHand.Left;
            _created.Add(leftHand);
            var dagger = CreateSword();
            dagger.archetype = leftHand;
            Assert.IsFalse(EquipmentVisuals.ShouldHoldTorch(true, true, dagger), "left-hand one-hander drawn");
            Assert.IsTrue(EquipmentVisuals.ShouldHoldTorch(true, false, dagger), "left-hand one-hander sheathed");
        }

        [Test]
        public void IsTorchLit_HeldAndNotDoused()
        {
            Assert.IsTrue(EquipmentVisuals.IsTorchLit(true, false));
            Assert.IsFalse(EquipmentVisuals.IsTorchLit(true, true), "swimming");
            Assert.IsFalse(EquipmentVisuals.IsTorchLit(false, false), "stowed");
        }

        [Test]
        public void ResolveOffhandArmPose_TorchOnlyWhileHeld()
        {
            Assert.AreEqual(OffhandArmPose.TorchHold,
                EquipmentVisuals.ResolveOffhandArmPose(OffhandArmPose.TorchHold, true, true));
            Assert.AreEqual(OffhandArmPose.None,
                EquipmentVisuals.ResolveOffhandArmPose(OffhandArmPose.TorchHold, true, false), "stowed on the hip");
            Assert.AreEqual(OffhandArmPose.TorchHold,
                EquipmentVisuals.ResolveOffhandArmPose(OffhandArmPose.TorchHold, false, false), "non-torch item keeps its pose");
        }

        // ── Detail panel / flicker ──────────────────────────────────────────

        [Test]
        public void Torch_CategoryTorch_NoStatRows()
        {
            var torch = CreateTorch();
            var lines = new List<ItemStatLine>();

            ItemDetailFormatter.BuildStatLines(torch, lines);

            Assert.AreEqual("Torch", ItemDetailFormatter.GetCategory(torch));
            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void Flicker_StaysWithinAmount()
        {
            Assert.AreEqual(1f, TorchLight.Flicker(0.5f, 0.15f), 1e-5f);
            Assert.AreEqual(0.85f, TorchLight.Flicker(0f, 0.15f), 1e-5f);
            Assert.AreEqual(1.15f, TorchLight.Flicker(1f, 0.15f), 1e-5f);
            Assert.AreEqual(1.15f, TorchLight.Flicker(2f, 0.15f), 1e-5f, "noise clamped");
        }
    }
}
