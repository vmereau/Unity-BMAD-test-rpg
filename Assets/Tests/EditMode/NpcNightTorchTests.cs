using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>spec-npc-night-torch: the night rule, the torch / shield rule and the NpcArmament offhand swaps.</summary>
    public class NpcNightTorchTests
    {
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;

        private GameObject _go;
        private InventorySystem _inventory;
        private EquipmentSystem _equipment;
        private NpcArmament _armament;
        private TorchSO _torch;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            IgnorePlaceholderErrors();
            _go = new GameObject("TestNpc");
            _inventory = _go.AddComponent<InventorySystem>();
            _equipment = _go.AddComponent<EquipmentSystem>();
            var visuals = _go.AddComponent<EquipmentVisuals>();
            _go.AddComponent<EntityMeleeAttacker>();
            _armament = _go.AddComponent<NpcArmament>();

            _torch = Create<TorchSO>("Torch");
            _torch.itemName = "Torch";
            Set(_equipment, "_inventorySystem", _inventory);
            Set(visuals, "_equipmentSystem", _equipment);
            Set(_armament, "_equipment", _equipment);
            Set(_armament, "_visuals", visuals);
            Set(_armament, "_inventory", _inventory);
            Set(_armament, "_nightTorch", _torch);
            typeof(NpcArmament).GetMethod("Awake", PRIVATE).Invoke(_armament, null);
            Set(_armament, "_started", true); // loadout in place (Start not run in EditMode)
        }

        [TearDown]
        public void TearDown()
        {
            IgnorePlaceholderErrors();
            Object.DestroyImmediate(_go);
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // Placeholder visuals (no prefab on the test items) log edit-mode-only errors (Destroy, renderer.material). The flag is
        // reset at the start of every test, so each swap test sets it in its body.
        private static void IgnorePlaceholderErrors() => LogAssert.ignoreFailingMessages = true;

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PRIVATE).SetValue(target, value);

        private T Create<T>(string name) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            so.name = name;
            _created.Add(so);
            return so;
        }

        private ShieldSO GiveShield()
        {
            var shield = Create<ShieldSO>("Shield");
            shield.itemName = "Shield";
            _inventory.AddItem(shield);
            _equipment.Equip(shield);
            return shield;
        }

        private SwordSO GiveSword()
        {
            var sword = Create<SwordSO>("Sword");
            sword.itemName = "Sword";
            _inventory.AddItem(sword);
            _equipment.Equip(sword);
            return sword;
        }

        // A draw / sheathe clip reaching its WeaponSwap event.
        private void CompleteSwitch(bool draw)
        {
            Set(_armament, "_isSwitching", true);
            Set(_armament, "_pendingDraw", draw);
            _armament.CompleteWeaponSwitch(draw);
        }

        private void RemoveFromInventory(ItemSO item)
        {
            for (int i = 0; i < _inventory.Count; i++)
                if (_inventory.Items[i].Item == item) { _inventory.RemoveItem(i); return; }
        }

        // --- NpcTorchRules.WantsTorch ---

        [TestCase(23f, true)]
        [TestCase(22f, true)]   // sunset hour itself is night
        [TestCase(2f, true)]    // wraps midnight
        [TestCase(5.9f, true)]
        [TestCase(6f, false)]   // sunrise
        [TestCase(12f, false)]
        [TestCase(21.9f, false)]
        public void WantsTorch_FollowsNight(float hour, bool expected) =>
            Assert.AreEqual(expected, NpcTorchRules.WantsTorch(true, hour, 6f, 22f, asleep: false));

        [Test]
        public void WantsTorch_Asleep_Never() => Assert.IsFalse(NpcTorchRules.WantsTorch(true, 23f, 6f, 22f, asleep: true));

        [Test]
        public void WantsTorch_FlagOff_Never() => Assert.IsFalse(NpcTorchRules.WantsTorch(false, 23f, 6f, 22f, asleep: false));

        [Test]
        public void WantsTorch_InvalidDay_Never() => Assert.IsFalse(NpcTorchRules.WantsTorch(true, 23f, 22f, 6f, asleep: false));

        // --- NpcArmament.ShouldHoldTorch ---

        [TestCase(false, false, false, false)]
        [TestCase(true, false, true, true)]    // night, out of combat: torch even with a shield
        [TestCase(true, true, true, false)]    // weapon drawn in a fight with an own shield: the shield
        [TestCase(true, true, false, true)]    // fighting without a shield: keeps the torch
        [TestCase(false, true, true, false)]
        public void ShouldHoldTorch_Rule(bool wanted, bool combatOffhand, bool hasOwnShield, bool expected) =>
            Assert.AreEqual(expected, NpcArmament.ShouldHoldTorch(wanted, combatOffhand, hasOwnShield));

        // --- NpcArmament swaps ---

        [Test]
        public void Dusk_GrantsTorch_ShieldGoesToInventory()
        {
            IgnorePlaceholderErrors();
            ShieldSO shield = GiveShield();

            _armament.SetTorchWanted(true);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.CountOf(shield));
            Assert.AreEqual(0, _inventory.CountOf(_torch), "the held torch is not in the inventory");
        }

        [Test]
        public void Dusk_TorchAlreadyCarried_NotGrantedAgain()
        {
            IgnorePlaceholderErrors();
            _inventory.AddItem(_torch);

            _armament.SetTorchWanted(true);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void Dawn_TorchToInventory_ShieldBack()
        {
            IgnorePlaceholderErrors();
            ShieldSO shield = GiveShield();
            _armament.SetTorchWanted(true);

            _armament.SetTorchWanted(false);

            Assert.AreSame(shield, _equipment.EquippedShield);
            Assert.AreEqual(1, _inventory.CountOf(_torch));
            Assert.AreEqual(1, _inventory.Count);
        }

        [Test]
        public void SeveralNights_StillOneTorch()
        {
            IgnorePlaceholderErrors();
            GiveShield();
            for (int i = 0; i < 3; i++)
            {
                _armament.SetTorchWanted(true);
                _armament.SetTorchWanted(false);
            }
            Assert.AreEqual(1, _inventory.CountOf(_torch));
        }

        [Test]
        public void UnarmedFight_WithShield_KeepsTorch()
        {
            IgnorePlaceholderErrors();
            GiveShield();
            _armament.SetTorchWanted(true);

            _armament.OnCombatStateChanged(true);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand), "a shield is only held next to a drawn weapon");
        }

        [Test]
        public void ArmedFight_ShieldAtDraw_KeptThroughWeaponChange_TorchBackAtSheathe()
        {
            IgnorePlaceholderErrors();
            SwordSO sword = GiveSword();
            ShieldSO shield = GiveShield();
            _armament.SetTorchWanted(true);
            Set(_armament, "_wielded", sword);
            Set(_armament, "_inCombat", true);

            CompleteSwitch(draw: true);
            Assert.AreSame(shield, _equipment.EquippedShield, "draw WeaponSwap: the shield replaces the torch");
            Assert.AreEqual(1, _inventory.CountOf(_torch));

            CompleteSwitch(draw: false); // mid-fight melee → bow switch: sheathed, still in combat
            Assert.AreSame(shield, _equipment.EquippedShield, "no torch flash between the switch clips");

            CompleteSwitch(draw: true);
            Set(_armament, "_inCombat", false);
            CompleteSwitch(draw: false); // fight over, sheathe WeaponSwap
            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.CountOf(shield));
        }

        [Test]
        public void ArmedFight_TurnsToFists_TorchBack()
        {
            IgnorePlaceholderErrors();
            SwordSO sword = GiveSword();
            GiveShield();
            _armament.SetTorchWanted(true);
            Set(_armament, "_wielded", sword);
            Set(_armament, "_inCombat", true);
            CompleteSwitch(draw: true);

            _armament.SetFists(true);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
        }

        [Test]
        public void ShieldReEquipPath_LeavesHeldTorch()
        {
            IgnorePlaceholderErrors();
            ShieldSO shield = GiveShield();
            _armament.SetTorchWanted(true);

            typeof(NpcArmament).GetMethod("EquipCarriedShield", PRIVATE).Invoke(_armament, null);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.CountOf(shield));
        }

        [Test]
        public void Restore_TorchSavedAtNight_StaysHeld()
        {
            IgnorePlaceholderErrors();
            _armament.SetTorchWanted(true);
            var saved = new Dictionary<EquipmentSlot, ItemSO> { { EquipmentSlot.Offhand, _torch } };

            _armament.RestoreLoadout(saved, null, killed: false);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(0, _inventory.CountOf(_torch), "no second torch granted");
        }

        [Test]
        public void AuthoredTorch_NotTakenOffByDay()
        {
            IgnorePlaceholderErrors();
            var own = Create<TorchSO>("OwnTorch");
            own.itemName = "Own Torch";
            _inventory.AddItem(own);
            _equipment.Equip(own);

            _armament.SetTorchWanted(true);
            _armament.SetTorchWanted(false);

            Assert.AreSame(own, _equipment.GetEquipped(EquipmentSlot.Offhand));
        }

        [Test]
        public void Fight_NoShield_KeepsTorch()
        {
            IgnorePlaceholderErrors();
            _armament.SetTorchWanted(true);

            _armament.OnCombatStateChanged(true);

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
        }

        [Test]
        public void Down_TorchGoesToInventory()
        {
            IgnorePlaceholderErrors();
            SwordSO sword = GiveSword();
            ShieldSO shield = GiveShield();
            _armament.SetTorchWanted(true);

            _armament.OnDowned();

            Assert.IsNull(_equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.CountOf(_torch), "the torch is lootable");
            Assert.AreEqual(1, _inventory.CountOf(sword), "with the weapon");
            Assert.AreEqual(1, _inventory.CountOf(shield), "and the shield");
        }

        [Test]
        public void WakeAtNight_TorchBack_NotTheShield()
        {
            IgnorePlaceholderErrors();
            ShieldSO shield = GiveShield();
            _armament.SetTorchWanted(true);
            _armament.OnDowned();

            _armament.OnWokeUp();

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
            Assert.AreEqual(1, _inventory.CountOf(shield));
            Assert.AreEqual(0, _inventory.CountOf(_torch));
        }

        [Test]
        public void WakeAtNight_TorchLooted_NewOneGranted()
        {
            IgnorePlaceholderErrors();
            _armament.SetTorchWanted(true);
            _armament.OnDowned();
            RemoveFromInventory(_torch); // the player loots it

            _armament.OnWokeUp();

            Assert.AreSame(_torch, _equipment.GetEquipped(EquipmentSlot.Offhand));
        }

        [Test]
        public void WakeByDay_ShieldBack_TorchStaysInInventory()
        {
            IgnorePlaceholderErrors();
            ShieldSO shield = GiveShield();
            _armament.SetTorchWanted(true);
            _armament.OnDowned();
            _armament.SetTorchWanted(false); // dawn while down: stored only

            _armament.OnWokeUp();

            Assert.AreSame(shield, _equipment.EquippedShield);
            Assert.AreEqual(1, _inventory.CountOf(_torch));
        }

        [Test]
        public void NoTorchItem_WarnsAndKeepsShield()
        {
            IgnorePlaceholderErrors();
            Set(_armament, "_nightTorch", null);
            ShieldSO shield = GiveShield();

            _armament.SetTorchWanted(true);

            Assert.AreSame(shield, _equipment.EquippedShield);
        }
    }
}
