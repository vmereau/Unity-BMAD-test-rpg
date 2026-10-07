using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Inventory;
using Game.Player;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// PlayerSaveAdapter capture → restore round-trip, and the death flow (player stays active, actions blocked).
    /// Edit Mode: Awake doesn't run on AddComponent, so every dependency is wired by reflection.
    /// </summary>
    public class PlayerSaveAdapterTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private T SO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private ItemSO Item(string id, int maxStacks = 1)
        {
            var item = SO<ItemSO>();
            item.itemName = item.name = id;
            item.itemId = id;
            item.maxStacks = maxStacks;
            return item;
        }

        private sealed class Player
        {
            public GameObject Go;
            public PlayerSaveAdapter Adapter;
            public PlayerStats Stats;
            public PlayerHealth Health;
            public StaminaSystem Stamina;
            public XPSystem Xp;
            public LevelSystem Level;
            public LearningPointSystem Lp;
            public PlayerSkills Skills;
            public InventorySystem Inventory;
            public EquipmentSystem Equipment;
            public ActionBarSystem ActionBar;
            public GoldSystem Gold;
            public PlayerStateManager State;
        }

        private Player CreatePlayer()
        {
            var p = new Player { Go = new GameObject("Player_Test") };
            _cleanup.Add(p.Go);
            var go = p.Go;
            p.State = go.AddComponent<PlayerStateManager>(); // RequireComponent adds CharacterController + drivers
            p.Stats = go.AddComponent<PlayerStats>();
            p.Health = go.AddComponent<PlayerHealth>();
            p.Stamina = go.AddComponent<StaminaSystem>();
            p.Xp = go.AddComponent<XPSystem>();
            p.Level = go.AddComponent<LevelSystem>();
            p.Lp = go.AddComponent<LearningPointSystem>();
            p.Skills = go.AddComponent<PlayerSkills>();
            p.Inventory = go.AddComponent<InventorySystem>();
            p.Equipment = go.AddComponent<EquipmentSystem>();
            p.ActionBar = go.AddComponent<ActionBarSystem>();
            p.Gold = go.AddComponent<GoldSystem>();
            p.Adapter = go.AddComponent<PlayerSaveAdapter>();

            var combat = SO<CombatConfigSO>();
            combat.baseHealth = 100f;
            combat.baseStaminaPool = 50f;
            var progression = SO<ProgressionConfigSO>();
            progression.xpPerLevel = new[] { 100, 250, 500 };

            Set(p.Health, "_config", combat);
            Set(p.Health, "_playerStateManager", p.State);
            Set(p.Stamina, "_config", combat);
            Set(p.Level, "_config", progression);
            Set(p.Level, "_xpSystem", p.Xp);
            Set(p.Skills, "_lpSystem", p.Lp);
            Set(p.Equipment, "_inventorySystem", p.Inventory);
            Set(p.Equipment, "_playerStats", p.Stats);
            Set(p.ActionBar, "_inventorySystem", p.Inventory);

            Set(p.Adapter, "_stats", p.Stats);
            Set(p.Adapter, "_health", p.Health);
            Set(p.Adapter, "_stamina", p.Stamina);
            Set(p.Adapter, "_xp", p.Xp);
            Set(p.Adapter, "_level", p.Level);
            Set(p.Adapter, "_learningPoints", p.Lp);
            Set(p.Adapter, "_skills", p.Skills);
            Set(p.Adapter, "_inventory", p.Inventory);
            Set(p.Adapter, "_equipment", p.Equipment);
            Set(p.Adapter, "_actionBar", p.ActionBar);
            Set(p.Adapter, "_gold", p.Gold);
            Set(p.Adapter, "_stateManager", p.State);
            Set(p.Adapter, "_characterController", go.GetComponent<CharacterController>());
            p.Health.RestoreHealth(100f);
            return p;
        }

        private ItemCatalogSO Catalog(params ItemSO[] items)
        {
            var catalog = SO<ItemCatalogSO>();
            Set(catalog, "_items", new List<ItemSO>(items));
            return catalog;
        }

        [Test]
        public void CaptureThenRestore_RoundTripsEveryPlayerSystem()
        {
            var p = CreatePlayer();
            var potion = Item("potion", maxStacks: 5);
            var key = Item("key");
            var sword = SO<SwordSO>();
            sword.itemName = sword.itemId = "sword";
            sword.strengthBonus = 2;
            var catalog = Catalog(potion, key, sword);

            // ── state at save time ──
            p.Go.transform.SetPositionAndRotation(new Vector3(3f, 0f, 4f), Quaternion.Euler(0f, 45f, 0f));
            p.Stats.RestoreBaseStats(11, 12, 13, 14);
            p.Xp.RestoreState(300, 6);
            p.Level.RestoreLevel(3);
            p.Lp.RestoreLP(4);
            p.Skills.RestoreSkills(new[] { "lockpick" });
            p.Inventory.RestoreSlots(new List<(ItemSO, int)> { (key, 1), (potion, 3) });
            p.Equipment.RestoreEquipped(new Dictionary<EquipmentSlot, ItemSO> { { EquipmentSlot.Weapon, sword } });
            p.ActionBar.Assign(2, 1, potion);
            p.Gold.RestoreGold(120);
            p.Health.RestoreHealth(42f);

            var saved = p.Adapter.Capture();

            // ── change everything ──
            p.Go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            p.Stats.RestoreBaseStats(1, 1, 1, 1);
            p.Xp.RestoreState(0, 0);
            p.Level.RestoreLevel(1);
            p.Lp.RestoreLP(0);
            p.Skills.RestoreSkills(new string[0]);
            p.Inventory.RestoreSlots(new List<(ItemSO, int)>());
            p.Equipment.RestoreEquipped(new Dictionary<EquipmentSlot, ItemSO>());
            p.ActionBar.RestoreSlots(new List<(int, int, ItemSO)>());
            p.Gold.RestoreGold(0);
            p.Health.RestoreHealth(5f);

            p.Adapter.Restore(saved, catalog);

            Assert.AreEqual(new Vector3(3f, 0f, 4f), p.Go.transform.position);
            Assert.AreEqual(45f, p.Go.transform.eulerAngles.y, 0.01f);
            Assert.AreEqual(11, p.Stats.GetBaseStat(StatType.Strength));
            Assert.AreEqual(14, p.Stats.GetBaseStat(StatType.Intelligence));
            Assert.AreEqual(2, p.Stats.Strength - p.Stats.GetBaseStat(StatType.Strength), "equipment bonus re-applied");
            Assert.AreEqual(300, p.Xp.CurrentXP);
            Assert.AreEqual(6, p.Xp.TotalKills);
            Assert.AreEqual(3, p.Level.CurrentLevel);
            Assert.AreEqual(4, p.Lp.CurrentLP);
            Assert.IsTrue(p.Skills.HasSkill("lockpick"));
            Assert.AreEqual(2, p.Inventory.Count);
            Assert.AreSame(key, p.Inventory.Items[0].Item);
            Assert.AreSame(potion, p.Inventory.Items[1].Item);
            Assert.AreEqual(3, p.Inventory.Items[1].Count);
            Assert.AreSame(sword, p.Equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.AreSame(potion, p.ActionBar.GetSlot(2).Value.Item);
            Assert.AreEqual(1, p.ActionBar.GetSlot(2).Value.InventoryIndex);
            Assert.AreEqual(120, p.Gold.Gold);
            Assert.AreEqual(42f, p.Health.CurrentHealth);
            Assert.AreEqual(p.Stamina.MaxStamina, p.Stamina.CurrentStamina);
        }

        [Test]
        public void Restore_RevivesDeadPlayer()
        {
            var p = CreatePlayer();
            p.Health.TakeDamage(1000f);
            Assert.IsTrue(p.Health.IsDead);

            var saved = p.Adapter.Capture();
            saved.health = 60f;
            p.Adapter.Restore(saved, Catalog());

            Assert.IsFalse(p.Health.IsDead);
            Assert.IsFalse(p.State.IsDead);
            Assert.AreEqual(60f, p.Health.CurrentHealth);
        }

        [Test]
        public void Restore_ResetsFallVelocity()
        {
            var p = CreatePlayer();
            var controller = p.Go.AddComponent<PlayerController>();
            Set(p.Adapter, "_playerController", controller);
            Set(controller, "_verticalVelocity", -80f);

            p.Adapter.Restore(p.Adapter.Capture(), Catalog());

            Assert.AreEqual(-2f, (float)typeof(PlayerController)
                .GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller));
        }

        // ── Death flow (Task 20) ──────────────────────────────────────────────

        [Test]
        public void Die_KeepsPlayerActive_AndBlocksEveryAction()
        {
            var p = CreatePlayer();

            p.Health.TakeDamage(1000f);

            Assert.IsTrue(p.Go.activeSelf, "Player root (hosting the UICanvas) must stay active");
            Assert.IsTrue(p.Health.IsDead);
            Assert.IsTrue(p.State.IsDead);
            Assert.IsTrue(p.State.IsBusy);
            Assert.IsFalse(p.State.CanMove());
            Assert.IsFalse(p.State.CanDodge());
            Assert.IsFalse(p.State.CanJump());
            Assert.IsFalse(p.State.CanAttack());
            Assert.IsFalse(p.State.CanBlock());
        }

        [Test]
        public void SetDead_ClearsActionStates()
        {
            var p = CreatePlayer();
            Set(p.State, "<IsBlocking>k__BackingField", true);

            p.State.SetDead(true);

            Assert.IsFalse(p.State.IsBlocking);
            p.State.SetDead(false);
            Assert.IsFalse(p.State.IsDead);
        }
    }
}
