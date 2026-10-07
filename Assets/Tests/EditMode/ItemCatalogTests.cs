using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>ItemCatalogSO lookup + ItemStackConverter (save ↔ inventory stacks).</summary>
    public class ItemCatalogTests
    {
        private ItemCatalogSO _catalog;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            _cleanup.Add(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        private ItemSO Item(string id, int maxStacks = 1)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.name = item.itemName = "Item_" + id;
            item.itemId = id;
            item.maxStacks = maxStacks;
            _cleanup.Add(item);
            return item;
        }

        private void SetItems(params ItemSO[] items) =>
            typeof(ItemCatalogSO).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_catalog, new List<ItemSO>(items));

        // ── ItemCatalogSO ─────────────────────────────────────────────────────

        [Test]
        public void FindById_Hit_ReturnsItem()
        {
            var a = Item("a");
            SetItems(a, Item("b"));
            Assert.AreSame(a, _catalog.FindById("a"));
        }

        [Test]
        public void FindById_MissOrEmpty_ReturnsNull()
        {
            SetItems(Item("a"));
            Assert.IsNull(_catalog.FindById("zzz"));
            Assert.IsNull(_catalog.FindById(""));
            Assert.IsNull(_catalog.FindById(null));
        }

        [Test]
        public void FindById_SkipsNullEntries()
        {
            var a = Item("a");
            SetItems(null, a);
            Assert.AreSame(a, _catalog.FindById("a"));
        }

        [Test]
        public void FindById_DuplicateId_WarnsAndKeepsFirst()
        {
            var first = Item("dup");
            SetItems(first, Item("dup"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate itemId 'dup'"));
            Assert.AreSame(first, _catalog.FindById("dup"));
        }

        [Test]
        public void FindById_ReindexesWhenListGrows()
        {
            SetItems(Item("a"));
            _catalog.FindById("a");
            var b = Item("b");
            SetItems(Item("a"), b);
            Assert.AreSame(b, _catalog.FindById("b"));
        }

        // ── ItemStackConverter ────────────────────────────────────────────────

        [Test]
        public void FromSave_UnknownIdSkippedWithWarning_OrderPreserved()
        {
            var a = Item("a");
            var b = Item("b");
            SetItems(a, b);
            var saved = new List<ItemStackSaveData>
            {
                new ItemStackSaveData { itemId = "b", count = 2 },
                new ItemStackSaveData { itemId = "gone", count = 1 },
                new ItemStackSaveData { itemId = "a", count = 1 },
            };
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("gone"));

            var result = ItemStackConverter.FromSave(saved, _catalog);

            Assert.AreEqual(2, result.Count);
            Assert.AreSame(b, result[0].item);
            Assert.AreEqual(2, result[0].count);
            Assert.AreSame(a, result[1].item);
        }

        [Test]
        public void FromSave_NonPositiveCountSkipped()
        {
            SetItems(Item("a"));
            var result = ItemStackConverter.FromSave(new List<ItemStackSaveData>
            {
                new ItemStackSaveData { itemId = "a", count = 0 },
                new ItemStackSaveData { itemId = "a", count = -3 },
            }, _catalog);
            Assert.IsEmpty(result);
        }

        [Test]
        public void ToSave_ThenRestore_ClampsCountsToMaxStacks()
        {
            var potion = Item("potion", maxStacks: 5);
            SetItems(potion);
            var go = new GameObject("Inv");
            _cleanup.Add(go);
            var inventory = go.AddComponent<InventorySystem>();

            var saved = new List<ItemStackSaveData> { new ItemStackSaveData { itemId = "potion", count = 99 } };
            inventory.RestoreSlots(ItemStackConverter.FromSave(saved, _catalog));

            Assert.AreEqual(1, inventory.Count);
            Assert.AreEqual(5, inventory.Items[0].Count);
            var back = ItemStackConverter.ToSave(inventory.Items);
            Assert.AreEqual("potion", back[0].itemId);
            Assert.AreEqual(5, back[0].count);
        }
    }
}
