using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>Equipped slots ↔ save form (slot name → itemId), shared by the player and NPCs (spec-npc-equipment-save).</summary>
    public class EquipmentSaveConverterTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private ItemCatalogSO _catalog;
        private SwordSO _sword;
        private ArmorSO _armor;

        [SetUp]
        public void SetUp()
        {
            _sword = SO<SwordSO>();
            _sword.itemId = _sword.itemName = "sword";
            _armor = SO<ArmorSO>();
            _armor.itemId = _armor.itemName = "armor";
            _armor.slot = EquipmentSlot.Armor;
            _catalog = SO<ItemCatalogSO>();
            typeof(ItemCatalogSO).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_catalog, new List<ItemSO> { _sword, _armor });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        private T SO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        [Test]
        public void RoundTrip_KeepsEverySlot()
        {
            var equipped = new Dictionary<EquipmentSlot, ItemSO>
            {
                { EquipmentSlot.Weapon, _sword },
                { EquipmentSlot.Armor, _armor }
            };

            var saved = EquipmentSaveConverter.ToSave(equipped);
            Assert.AreEqual("sword", saved["Weapon"]);
            Assert.AreEqual("armor", saved["Armor"]);

            var restored = EquipmentSaveConverter.FromSave(saved, _catalog);
            Assert.AreEqual(2, restored.Count);
            Assert.AreSame(_sword, restored[EquipmentSlot.Weapon]);
            Assert.AreSame(_armor, restored[EquipmentSlot.Armor]);
        }

        [Test]
        public void ToSave_SkipsNullItemsAndMissingIds()
        {
            var noId = SO<SwordSO>();
            noId.name = "noId";
            LogAssert.Expect(LogType.Warning, new Regex("has no itemId"));

            var saved = EquipmentSaveConverter.ToSave(new Dictionary<EquipmentSlot, ItemSO>
            {
                { EquipmentSlot.Weapon, noId },
                { EquipmentSlot.Armor, null }
            });

            Assert.AreEqual(0, saved.Count);
        }

        [Test]
        public void FromSave_UnknownItemId_IsSkipped()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Unknown itemId 'ghost'"));

            var restored = EquipmentSaveConverter.FromSave(
                new Dictionary<string, string> { { "Weapon", "ghost" }, { "Armor", "armor" } }, _catalog);

            Assert.AreEqual(1, restored.Count);
            Assert.AreSame(_armor, restored[EquipmentSlot.Armor]);
        }

        [Test]
        public void FromSave_UnknownSlot_IsSkipped()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Unknown equipment slot 'Tail'"));
            LogAssert.Expect(LogType.Warning, new Regex("Unknown equipment slot '999'"));

            var restored = EquipmentSaveConverter.FromSave(
                new Dictionary<string, string> { { "Tail", "sword" }, { "999", "sword" }, { "Weapon", "sword" } }, _catalog);

            Assert.AreEqual(1, restored.Count);
            Assert.AreSame(_sword, restored[EquipmentSlot.Weapon]);
        }

        [Test]
        public void FromSave_NullInput_IsEmpty()
        {
            Assert.AreEqual(0, EquipmentSaveConverter.FromSave(null, _catalog).Count);
            Assert.AreEqual(0, EquipmentSaveConverter.ToSave(null).Count);
        }

        [Test]
        public void FromSave_NoCatalog_IsEmptyAndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("No ItemCatalog"));
            var restored = EquipmentSaveConverter.FromSave(new Dictionary<string, string> { { "Weapon", "sword" } }, null);
            Assert.AreEqual(0, restored.Count);
        }
    }
}
