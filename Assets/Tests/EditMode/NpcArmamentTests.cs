using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.AI;
using Game.Inventory;
using Game.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>spec-npc-equipment: wielded-weapon stance and the down / wake weapon loot rule.</summary>
    public class NpcArmamentTests
    {
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;

        private GameObject _go;
        private InventorySystem _inventory;
        private EquipmentSystem _equipment;
        private NpcArmament _armament;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestNpc");
            _inventory = _go.AddComponent<InventorySystem>();
            _equipment = _go.AddComponent<EquipmentSystem>();
            var visuals = _go.AddComponent<EquipmentVisuals>();
            _go.AddComponent<EntityMeleeAttacker>();
            _armament = _go.AddComponent<NpcArmament>();

            Set(_equipment, "_inventorySystem", _inventory);
            Set(visuals, "_equipmentSystem", _equipment);
            Set(_armament, "_equipment", _equipment);
            Set(_armament, "_visuals", visuals);
            Set(_armament, "_inventory", _inventory);
            typeof(NpcArmament).GetMethod("Awake", PRIVATE).Invoke(_armament, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PRIVATE).SetValue(target, value);

        private T Create<T>(string name) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            so.name = name;
            _created.Add(so);
            return so;
        }

        private SwordSO Sword(float stance = 25f, WeaponSO.KnockDownOverride knockDown = WeaponSO.KnockDownOverride.No)
        {
            var sword = Create<SwordSO>("Sword");
            sword.itemName = "Sword";
            sword.stanceDamage = stance;
            sword.knockDown = knockDown;
            return sword;
        }

        private ArmorSO Armor()
        {
            var armor = Create<ArmorSO>("Armor");
            armor.itemName = "Armor";
            armor.slot = EquipmentSlot.Armor;
            return armor;
        }

        private void GiveAndEquip(ItemSO item)
        {
            _inventory.AddItem(item);
            _equipment.Equip(item);
        }

        [Test]
        public void ResolveAttackStance_Fists_BaseOnly_NeverKnocksDown()
        {
            var npc = Create<NPCEntity>("Npc");
            float stance = npc.ResolveAttackStance(1, 1, null, out bool kd);
            Assert.AreEqual(npc.AttackStanceDamage, stance, 0.001f);
            Assert.IsFalse(kd);
        }

        [Test]
        public void ResolveAttackStance_Wielded_AddsWeaponStance_AndItsKnockdown()
        {
            var npc = Create<NPCEntity>("Npc");
            float stance = npc.ResolveAttackStance(1, 1, Sword(25f, WeaponSO.KnockDownOverride.Yes), out bool kd);
            Assert.AreEqual(npc.AttackStanceDamage + 25f, stance, 0.001f);
            Assert.IsTrue(kd);
        }

        [Test]
        public void OnDowned_WeaponGoesToInventory_ArmorStaysEquipped()
        {
            SwordSO sword = Sword();
            ArmorSO armor = Armor();
            GiveAndEquip(armor);
            GiveAndEquip(sword);
            Assert.AreEqual(0, _inventory.Count, "equipped items are not lootable");

            _armament.OnDowned();

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(1, _inventory.CountOf(sword), "the weapon is lootable");
            Assert.AreSame(armor, _equipment.GetEquipped(EquipmentSlot.Armor));
            Assert.AreEqual(0, _inventory.CountOf(armor), "the armor is not lootable");
        }

        [Test]
        public void OnWokeUp_WeaponStillThere_ReEquips()
        {
            SwordSO sword = Sword();
            GiveAndEquip(sword);
            _armament.OnDowned();

            _armament.OnWokeUp();

            Assert.AreSame(sword, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreEqual(0, _inventory.CountOf(sword));
        }

        [Test]
        public void OnWokeUp_WeaponLooted_StaysUnarmed()
        {
            SwordSO sword = Sword();
            GiveAndEquip(sword);
            _armament.OnDowned();
            _inventory.RemoveItem(0); // the player loots it

            _armament.OnWokeUp();

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.IsNull(_armament.WieldedWeapon);
            Assert.AreEqual(0f, _armament.DamageBonus);
        }

        [Test]
        public void OnDowned_Unarmed_IsNoOp()
        {
            _armament.OnDowned();
            Assert.AreEqual(0, _inventory.Count);
        }

        private ShieldSO Shield()
        {
            var shield = Create<ShieldSO>("Shield");
            shield.itemName = "Shield";
            return shield;
        }

        // Re-equipping a shield in EditMode builds its placeholder visual (Destroy + renderer.material: edit-mode-only errors).
        private static void ExpectShieldPlaceholderError()
        {
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            LogAssert.Expect(LogType.Error, new Regex("Instantiating material due to calling renderer.material"));
        }

        // spec-npc-shield-loot-on-down
        [Test]
        public void OnDowned_ShieldGoesToInventory_WithWeapon_ArmorStaysEquipped()
        {
            SwordSO sword = Sword();
            ShieldSO shield = Shield();
            ArmorSO armor = Armor();
            GiveAndEquip(armor);
            GiveAndEquip(sword);
            GiveAndEquip(shield);

            _armament.OnDowned();

            Assert.IsNull(_equipment.EquippedShield);
            Assert.AreEqual(1, _inventory.CountOf(shield), "the shield is lootable");
            Assert.AreEqual(1, _inventory.CountOf(sword), "the weapon is lootable");
            Assert.AreSame(armor, _equipment.GetEquipped(EquipmentSlot.Armor));
        }

        [Test]
        public void OnWokeUp_ShieldStillThere_ReEquipsWeaponAndShield()
        {
            SwordSO sword = Sword();
            ShieldSO shield = Shield();
            GiveAndEquip(sword);
            GiveAndEquip(shield);
            _armament.OnDowned();

            ExpectShieldPlaceholderError();
            _armament.OnWokeUp();

            Assert.AreSame(sword, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreSame(shield, _equipment.EquippedShield);
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void OnWokeUp_ShieldLooted_NoShield()
        {
            SwordSO sword = Sword();
            ShieldSO shield = Shield();
            GiveAndEquip(sword);
            GiveAndEquip(shield);
            _armament.OnDowned();
            for (int i = 0; i < _inventory.Count; i++) // the player loots the shield only
                if (_inventory.Items[i].Item == shield) { _inventory.RemoveItem(i); break; }

            _armament.OnWokeUp();

            Assert.AreSame(sword, _equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.IsNull(_equipment.EquippedShield);
        }

        [Test]
        public void OnDowned_ShieldOnly_DropsAndReEquipsOnWake()
        {
            ShieldSO shield = Shield();
            GiveAndEquip(shield);

            _armament.OnDowned();
            Assert.AreEqual(1, _inventory.CountOf(shield));

            ExpectShieldPlaceholderError();
            _armament.OnWokeUp();
            Assert.AreSame(shield, _equipment.EquippedShield);
            Assert.AreEqual(0, _inventory.Count);
        }
    }
}
