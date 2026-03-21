using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for Story 7.3: Equipped Item Stat Effects.
    /// Tests PlayerStats split storage, EquipmentSystem bonus recompute, and PlayerHealth defense reduction.
    /// </summary>
    public class EquipmentStatEffectsTests
    {
        private readonly List<Object> _cleanup = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private T CreateSO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private ProgressionConfigSO CreateProgConfig(int baseStr = 5, int baseDex = 5,
            int baseEnd = 5, int baseMna = 5)
        {
            var cfg = CreateSO<ProgressionConfigSO>();
            cfg.baseStrength  = baseStr;
            cfg.baseDexterity = baseDex;
            cfg.baseEndurance = baseEnd;
            cfg.baseMana      = baseMna;
            return cfg;
        }

        /// <summary>
        /// Creates a PlayerStats, sets fields, then re-invokes Awake via reflection.
        /// Necessary because AddComponent fires Awake immediately (EditMode behaviour),
        /// so we must reinvoke after wiring to get base fields initialized.
        /// </summary>
        private PlayerStats CreatePlayerStats(ProgressionConfigSO config = null,
            GameEventSO_Void onStatsChanged = null)
        {
            config ??= CreateProgConfig();
            var go = new GameObject("PlayerStats_Test");
            _cleanup.Add(go);
            var ps = go.AddComponent<PlayerStats>(); // Awake fires immediately with null config
            SetPrivate(ps, "_config", config);
            if (onStatsChanged != null)
                SetPrivate(ps, "_onStatsChanged", onStatsChanged);
            ps.enabled = true; // Awake disabled it due to null config
            InvokePrivate(ps, "Awake"); // re-run with config wired
            return ps;
        }

        private CombatConfigSO CreateCombatConfig(float baseHealth = 100f)
        {
            var cfg = CreateSO<CombatConfigSO>();
            cfg.baseHealth = baseHealth;
            return cfg;
        }

        private PlayerHealth CreatePlayerHealth(CombatConfigSO config = null, PlayerStats stats = null)
        {
            config ??= CreateCombatConfig();
            var go = new GameObject("PlayerHealth_Test");
            _cleanup.Add(go);
            var ph = go.AddComponent<PlayerHealth>(); // Awake fires immediately with null config
            SetPrivate(ph, "_config", config);
            if (stats != null)
                SetPrivate(ph, "_playerStats", stats);
            ph.enabled = true;
            InvokePrivate(ph, "Awake"); // re-run with config wired to set CurrentHealth
            return ph;
        }

        private (EquipmentSystem equipment, InventorySystem inventory) CreateEquipmentSystem(
            PlayerStats stats = null)
        {
            var go = new GameObject("Equipment_Test");
            _cleanup.Add(go);
            var inv = go.AddComponent<InventorySystem>();
            var eq  = go.AddComponent<EquipmentSystem>();
            // Set fields after Awake (which disabled eq due to null _inventorySystem)
            SetPrivate(eq, "_inventorySystem", inv);
            if (stats != null)
                SetPrivate(eq, "_playerStats", stats);
            return (eq, inv);
        }

        private WeaponSO CreateWeapon(int strBonus = 0, float damageBonus = 0f)
        {
            var w = CreateSO<WeaponSO>();
            w.strengthBonus = strBonus;
            w.damageBonus   = damageBonus;
            return w;
        }

        private ArmorSO CreateArmor(EquipmentSlot slot, int defBonus = 0, int endBonus = 0)
        {
            var a = CreateSO<ArmorSO>();
            a.slot          = slot;
            a.defenseBonus  = defBonus;
            a.enduranceBonus = endBonus;
            return a;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(target, null);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void ApplyEquipmentBonuses_UpdatesEffectiveStrength()
        {
            var ps = CreatePlayerStats(CreateProgConfig(baseStr: 5));

            ps.ApplyEquipmentBonuses(str: 2, dex: 0, end: 0, mna: 0, def: 0);

            Assert.AreEqual(5 + 2, ps.Strength, "Effective Strength should be base + equipment bonus");
        }

        [Test]
        public void ApplyEquipmentBonuses_UpdatesDefense()
        {
            var ps = CreatePlayerStats();

            ps.ApplyEquipmentBonuses(str: 0, dex: 0, end: 0, mna: 0, def: 5);

            Assert.AreEqual(5, ps.Defense, "Defense should equal the equipment bonus");
        }

        [Test]
        public void UpgradeStat_StillIncreasesBaseOnly_EquipmentUnaffected()
        {
            var config = CreateProgConfig(baseStr: 5);
            var ps = CreatePlayerStats(config);
            ps.ApplyEquipmentBonuses(str: 2, dex: 0, end: 0, mna: 0, def: 0);

            ps.UpgradeStat(StatType.Strength, 3);

            // base(5) + upgrade(3) + equip(2) = 10
            Assert.AreEqual(5 + 3 + 2, ps.Strength,
                "Strength should stack base upgrade and equipment bonus independently");
        }

        [Test]
        public void ApplyEquipmentBonuses_RaisesOnStatsChanged()
        {
            bool raised = false;
            var evt = CreateSO<GameEventSO_Void>();
            evt.AddListener(_ => raised = true);
            var ps = CreatePlayerStats(onStatsChanged: evt);

            ps.ApplyEquipmentBonuses(0, 0, 0, 0, 0);

            Assert.IsTrue(raised, "_onStatsChanged should be raised by ApplyEquipmentBonuses");
        }

        [Test]
        public void TakeDamage_ReducedByDefense()
        {
            var ps = CreatePlayerStats();
            ps.ApplyEquipmentBonuses(str: 0, dex: 0, end: 0, mna: 0, def: 4);
            var config = CreateCombatConfig(baseHealth: 100f);
            var ph = CreatePlayerHealth(config, ps);

            ph.TakeDamage(10f);

            Assert.AreEqual(100f - (10f - 4f), ph.CurrentHealth, 0.001f,
                "HP should be reduced by max(1, raw - defense)");
        }

        [Test]
        public void TakeDamage_MinimumOneDamage()
        {
            var ps = CreatePlayerStats();
            ps.ApplyEquipmentBonuses(str: 0, dex: 0, end: 0, mna: 0, def: 10);
            var config = CreateCombatConfig(baseHealth: 100f);
            var ph = CreatePlayerHealth(config, ps);

            ph.TakeDamage(3f); // defense(10) > raw(3) → effective = max(1, -7) = 1

            Assert.AreEqual(100f - 1f, ph.CurrentHealth, 0.001f,
                "Minimum 1 damage should always get through regardless of defense");
        }

        [Test]
        public void TakeDamage_NoPlayerStats_UsesZeroDefense()
        {
            var config = CreateCombatConfig(baseHealth: 100f);
            var ph = CreatePlayerHealth(config, stats: null); // no PlayerStats

            Assert.DoesNotThrow(() => ph.TakeDamage(10f));
            Assert.AreEqual(100f - 10f, ph.CurrentHealth, 0.001f,
                "Without PlayerStats, full damage should be applied");
        }

        [Test]
        public void RecomputeBonuses_SumsAllEquippedItems()
        {
            var config = CreateProgConfig(baseStr: 5);
            var ps = CreatePlayerStats(config);
            var (eq, inv) = CreateEquipmentSystem(ps);

            var sword  = CreateWeapon(strBonus: 2);
            var helmet = CreateArmor(EquipmentSlot.Helmet, defBonus: 3);
            inv.AddItem(sword);
            inv.AddItem(helmet);

            eq.Equip(0); // sword
            eq.Equip(0); // helmet (sword now gone from inv, helmet at index 0)

            Assert.AreEqual(5 + 2, ps.Strength, "Strength should include weapon strengthBonus");
            Assert.AreEqual(3, ps.Defense, "Defense should equal armor defenseBonus");
        }

        [Test]
        public void GetWeaponDamageBonus_WithWeaponEquipped_ReturnsDamageBonus()
        {
            var ps = CreatePlayerStats();
            var (eq, inv) = CreateEquipmentSystem(ps);

            var sword = CreateWeapon(damageBonus: 10f);
            inv.AddItem(sword);
            eq.Equip(0);

            Assert.AreEqual(10f, eq.GetWeaponDamageBonus(), 0.001f,
                "GetWeaponDamageBonus should return the equipped weapon's damageBonus");
        }

        [Test]
        public void GetWeaponDamageBonus_NoWeaponEquipped_ReturnsZero()
        {
            var ps = CreatePlayerStats();
            var (eq, _) = CreateEquipmentSystem(ps);

            Assert.AreEqual(0f, eq.GetWeaponDamageBonus(), 0.001f,
                "GetWeaponDamageBonus should return 0 when no weapon is equipped");
        }

        [Test]
        public void Unequip_RemovesBonusesFromPlayerStats()
        {
            var config = CreateProgConfig(baseStr: 5);
            var ps = CreatePlayerStats(config);
            var (eq, inv) = CreateEquipmentSystem(ps);

            var sword = CreateWeapon(strBonus: 2);
            inv.AddItem(sword);
            eq.Equip(0);
            Assert.AreEqual(5 + 2, ps.Strength, "Pre-condition: strength should include weapon bonus after equip");

            eq.Unequip(EquipmentSlot.Weapon);

            Assert.AreEqual(5, ps.Strength, "Strength should return to base after unequip");
        }
    }
}
