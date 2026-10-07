using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Inventory;
using Game.Player;
using Game.Progression;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Save/load restore APIs: each sets state directly and raises only its value-changed event —
    /// never level-up, skill-learned or XP-gained (they would grant LP, toasts or kill counts).
    /// Edit Mode: Awake/OnEnable don't run on AddComponent, so dependencies are wired by reflection.
    /// </summary>
    public class PlayerRestoreTests
    {
        private GameObject _go;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPlayer");
            _cleanup.Add(_go);
        }

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

        private sealed class Counter<T>
        {
            public int Count;
            public T Last;
            public Counter(GameEventSO<T> evt) => evt.AddListener(v => { Count++; Last = v; });
        }

        // ── Stats / health / stamina ──────────────────────────────────────────

        [Test]
        public void RestoreBaseStats_SetsBaseValues_RaisesStatsChanged()
        {
            var stats = _go.AddComponent<PlayerStats>();
            var changed = SO<GameEventSO_Void>();
            Set(stats, "_onStatsChanged", changed);
            var counter = new Counter<bool>(changed);

            stats.RestoreBaseStats(11, 12, 13, 14);

            Assert.AreEqual(11, stats.GetBaseStat(StatType.Strength));
            Assert.AreEqual(12, stats.GetBaseStat(StatType.Dexterity));
            Assert.AreEqual(13, stats.GetBaseStat(StatType.Endurance));
            Assert.AreEqual(14, stats.GetBaseStat(StatType.Intelligence));
            Assert.AreEqual(1, counter.Count);
        }

        [Test]
        public void RestoreHealth_ClampsToOneAndMax_RevivesAndRaisesChanged()
        {
            var health = _go.AddComponent<PlayerHealth>();
            var config = SO<CombatConfigSO>();
            config.baseHealth = 100f;
            Set(health, "_config", config);
            var changed = SO<GameEventSO_Float>();
            Set(health, "_onPlayerHealthChanged", changed);
            var counter = new Counter<float>(changed);

            health.RestoreHealth(0f);
            Assert.AreEqual(1f, health.CurrentHealth);
            health.RestoreHealth(500f);
            Assert.AreEqual(100f, health.CurrentHealth);
            health.RestoreHealth(42f);

            Assert.AreEqual(42f, health.CurrentHealth);
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(3, counter.Count);
        }

        [Test]
        public void RefillToMax_FillsPool_RaisesChanged()
        {
            var stamina = _go.AddComponent<StaminaSystem>();
            var config = SO<CombatConfigSO>();
            config.baseStaminaPool = 80f;
            Set(stamina, "_config", config);
            var changed = SO<GameEventSO_Float>();
            Set(stamina, "_onPlayerStaminaChanged", changed);
            var counter = new Counter<float>(changed);

            stamina.RefillToMax();

            Assert.AreEqual(80f, stamina.CurrentStamina);
            Assert.AreEqual(1, counter.Count);
            Assert.AreEqual(1f, counter.Last);
        }

        // ── Progression ───────────────────────────────────────────────────────

        [Test]
        public void RestoreXPAndLevel_RaiseNoXPGainedOrLevelUp_RaiseProgress()
        {
            var xp = _go.AddComponent<XPSystem>();
            var level = _go.AddComponent<LevelSystem>();
            var config = SO<ProgressionConfigSO>();
            config.xpPerLevel = new[] { 100, 250, 500 };
            var xpGained = SO<GameEventSO_Int>();
            var levelUp = SO<GameEventSO_Int>();
            var progress = SO<GameEventSO_Float>();
            Set(xp, "_onXPGained", xpGained);
            Set(level, "_config", config);
            Set(level, "_xpSystem", xp);
            Set(level, "_onXPGained", xpGained);
            Set(level, "_onLevelUp", levelUp);
            Set(level, "_onPlayerXPProgressChanged", progress);
            var gained = new Counter<int>(xpGained);
            var ups = new Counter<int>(levelUp);
            var prog = new Counter<float>(progress);

            xp.RestoreState(300, 7);
            level.RestoreLevel(3);

            Assert.AreEqual(300, xp.CurrentXP);
            Assert.AreEqual(7, xp.TotalKills);
            Assert.AreEqual(3, level.CurrentLevel);
            Assert.AreEqual(0, gained.Count);
            Assert.AreEqual(0, ups.Count);
            Assert.AreEqual(1, prog.Count);
            Assert.AreEqual(0.2f, prog.Last, 0.0001f); // (300-250)/(500-250)
        }

        [Test]
        public void RestoreLevel_ClampsToMaxLevel()
        {
            var xp = _go.AddComponent<XPSystem>();
            var level = _go.AddComponent<LevelSystem>();
            var config = SO<ProgressionConfigSO>();
            config.xpPerLevel = new[] { 100, 250 };
            Set(level, "_config", config);
            Set(level, "_xpSystem", xp);

            level.RestoreLevel(99);
            Assert.AreEqual(3, level.CurrentLevel);
            level.RestoreLevel(-5);
            Assert.AreEqual(1, level.CurrentLevel);
        }

        [Test]
        public void RestoreLP_ClampsAndRaisesLPChanged()
        {
            var lp = _go.AddComponent<LearningPointSystem>();
            var changed = SO<GameEventSO_Int>();
            Set(lp, "_onLPChanged", changed);
            var counter = new Counter<int>(changed);

            lp.RestoreLP(-3);
            Assert.AreEqual(0, lp.CurrentLP);
            lp.RestoreLP(4);

            Assert.AreEqual(4, lp.CurrentLP);
            Assert.AreEqual(2, counter.Count);
            Assert.AreEqual(4, counter.Last);
        }

        [Test]
        public void RestoreSkills_ReplacesSet_NoSkillLearnedEvent_NoLPSpent()
        {
            var lp = _go.AddComponent<LearningPointSystem>();
            var skills = _go.AddComponent<PlayerSkills>();
            var learned = SO<GameEventSO_String>();
            Set(skills, "_onSkillLearned", learned);
            Set(skills, "_lpSystem", lp);
            lp.RestoreLP(5);
            var counter = new Counter<string>(learned);

            skills.RestoreSkills(new[] { "lockpick", "", "swim" });
            skills.RestoreSkills(new[] { "lockpick", "climb" });

            Assert.IsTrue(skills.HasSkill("lockpick"));
            Assert.IsTrue(skills.HasSkill("climb"));
            Assert.IsFalse(skills.HasSkill("swim"));
            Assert.AreEqual(2, skills.GetLearnedSkillIds().Count);
            Assert.AreEqual(0, counter.Count);
            Assert.AreEqual(5, lp.CurrentLP);
        }

        // ── Inventory / equipment / action bar / gold ─────────────────────────

        private ItemSO Item(string name, int maxStacks = 1)
        {
            var item = SO<ItemSO>();
            item.itemName = name;
            item.maxStacks = maxStacks;
            return item;
        }

        [Test]
        public void InventoryRestoreSlots_ReplacesStartingItems_KeepsOrder_NoRestack()
        {
            var inventory = _go.AddComponent<InventorySystem>();
            var potion = Item("Potion", maxStacks: 10);
            var key = Item("Key");
            inventory.AddItem(key);

            inventory.RestoreSlots(new List<(ItemSO, int)> { (potion, 3), (key, 1), (potion, 2), (null, 1) });

            Assert.AreEqual(3, inventory.Count);
            Assert.AreSame(potion, inventory.Items[0].Item);
            Assert.AreEqual(3, inventory.Items[0].Count);
            Assert.AreSame(key, inventory.Items[1].Item);
            Assert.AreEqual(2, inventory.Items[2].Count, "separate stacks are not merged");
        }

        [Test]
        public void RestoreEquipped_DoesNotTouchInventory_AppliesBonuses_RaisesChanged()
        {
            var inventory = _go.AddComponent<InventorySystem>();
            var equipment = _go.AddComponent<EquipmentSystem>();
            var stats = _go.AddComponent<PlayerStats>();
            var changed = SO<GameEventSO_Void>();
            Set(equipment, "_inventorySystem", inventory);
            Set(equipment, "_playerStats", stats);
            Set(equipment, "_onEquipmentChanged", changed);
            var counter = new Counter<bool>(changed);

            var sword = SO<SwordSO>();
            sword.itemName = "Sword";
            sword.strengthBonus = 3;
            var potion = Item("Potion");
            inventory.AddItem(potion);

            equipment.RestoreEquipped(new Dictionary<EquipmentSlot, ItemSO>
            {
                { EquipmentSlot.Weapon, sword },
                { EquipmentSlot.Ring1, potion }, // not equippable → skipped
                { EquipmentSlot.Ring2, sword },  // equippable but wrong slot → skipped
            });

            Assert.AreSame(sword, equipment.GetEquipped(EquipmentSlot.Weapon));
            Assert.IsNull(equipment.GetEquipped(EquipmentSlot.Ring1));
            Assert.IsNull(equipment.GetEquipped(EquipmentSlot.Ring2));
            Assert.AreEqual(1, equipment.Equipped.Count);
            Assert.AreEqual(1, inventory.Count, "inventory untouched");
            Assert.AreEqual(3, stats.Strength - stats.GetBaseStat(StatType.Strength));
            Assert.AreEqual(1, counter.Count);
        }

        [Test]
        public void ActionBarRestoreSlots_AssignsValidatesAndRaisesRefresh()
        {
            var inventory = _go.AddComponent<InventorySystem>();
            var bar = _go.AddComponent<ActionBarSystem>();
            var used = SO<GameEventSO_Int>();
            Set(bar, "_inventorySystem", inventory);
            Set(bar, "_onActionBarUsed", used);
            var counter = new Counter<int>(used);
            var potion = Item("Potion");
            var gone = Item("Gone");
            inventory.RestoreSlots(new List<(ItemSO, int)> { (Item("Other"), 1), (potion, 1) });
            bar.Assign(5, 0, potion);

            bar.RestoreSlots(new List<(int, int, ItemSO)> { (0, 0, potion), (2, 3, gone) });

            Assert.IsNull(bar.GetSlot(5), "previous assignments are cleared");
            Assert.AreEqual(1, bar.GetSlot(0).Value.InventoryIndex, "index re-validated against inventory");
            Assert.IsNull(bar.GetSlot(2), "item no longer in inventory is cleared");
            Assert.AreEqual(1, counter.Count);
            Assert.AreEqual(-1, counter.Last);
        }

        [Test]
        public void RestoreGold_ClampsAndRaisesChanged()
        {
            var gold = _go.AddComponent<GoldSystem>();
            var changed = SO<GameEventSO_Int>();
            Set(gold, "_onGoldChanged", changed);
            var counter = new Counter<int>(changed);

            gold.RestoreGold(-10);
            Assert.AreEqual(0, gold.Gold);
            gold.RestoreGold(250);

            Assert.AreEqual(250, gold.Gold);
            Assert.AreEqual(2, counter.Count);
        }

        // ── World objects ─────────────────────────────────────────────────────

        [Test]
        public void LockableRestoreLocked_CanRelock()
        {
            var lockable = _go.AddComponent<Lockable>();
            lockable.RestoreLocked(true);
            Assert.IsTrue(lockable.IsLocked);
            lockable.RestoreLocked(false);
            Assert.IsFalse(lockable.IsLocked);
        }

        [Test]
        public void DoorSetOpenImmediate_SnapsVisual()
        {
            var door = _go.AddComponent<DoorInteractable>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(_go.transform, false);
            Set(door, "_visual", visual);

            door.SetOpenImmediate(true);
            Assert.IsTrue(door.IsOpen);
            Assert.AreEqual(90f, visual.localEulerAngles.y, 0.01f);

            door.SetOpenImmediate(false);
            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, visual.localRotation), 0.01f);
        }
    }
}
