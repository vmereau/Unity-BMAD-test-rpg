using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Editor;
using Game.Inventory;

namespace Tests.EditMode
{
    public class NpcShopEditorTests
    {
        private ItemSO _lockpick;
        private ItemSO _ale;

        [SetUp]
        public void SetUp()
        {
            _lockpick = ScriptableObject.CreateInstance<ItemSO>();
            _lockpick.name = "Item_Lockpick";
            _ale = ScriptableObject.CreateInstance<ItemSO>();
            _ale.name = "Item_Ale";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_lockpick);
            Object.DestroyImmediate(_ale);
        }

        private static StartingItem Entry(ItemSO item, int count) => new() { item = item, count = count };

        [Test]
        public void ApplyStockChange_NewItem_IsAppended()
        {
            var stock = new List<StartingItem> { Entry(_ale, 5) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, 10);

            Assert.AreEqual(StockChange.Added, result.Change);
            Assert.AreEqual(2, stock.Count);
            Assert.AreEqual(_lockpick, stock[1].item);
            Assert.AreEqual(10, stock[1].count);
        }

        [Test]
        public void ApplyStockChange_ExistingItem_SetsCountWithoutDuplicate()
        {
            var stock = new List<StartingItem> { Entry(_lockpick, 3), Entry(_ale, 5), Entry(_lockpick, 1) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, 10);

            Assert.AreEqual(StockChange.Updated, result.Change);
            Assert.AreEqual(2, stock.Count);
            Assert.AreEqual(_lockpick, stock[0].item);
            Assert.AreEqual(10, stock[0].count);
            Assert.AreEqual(_ale, stock[1].item);
            Assert.AreEqual(5, stock[1].count);
        }

        [Test]
        public void ApplyStockChange_SameCount_IsUnchangedAndNotModified()
        {
            var stock = new List<StartingItem> { Entry(_lockpick, 10) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, 10);

            Assert.AreEqual(StockChange.Unchanged, result.Change);
            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Modified);
            Assert.AreEqual(1, stock.Count);
        }

        [Test]
        public void ApplyStockChange_CountZeroWithDuplicates_RemovesAllEntries()
        {
            var stock = new List<StartingItem> { Entry(_lockpick, 3), Entry(_ale, 5), Entry(_lockpick, 1) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, 0);

            Assert.AreEqual(StockChange.Removed, result.Change);
            Assert.AreEqual(1, stock.Count);
            Assert.AreEqual(_ale, stock[0].item);
        }

        [Test]
        public void ApplyStockChange_CountAboveCap_IsError()
        {
            var stock = new List<StartingItem>();

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, NpcShopEditor.MAX_COUNT + 1);

            Assert.AreEqual(StockChange.Error, result.Change);
            Assert.IsEmpty(stock);
        }

        [Test]
        public void ApplyStockChange_CountZero_RemovesItem()
        {
            var stock = new List<StartingItem> { Entry(_lockpick, 3), Entry(_ale, 5) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, 0);

            Assert.AreEqual(StockChange.Removed, result.Change);
            Assert.AreEqual(1, stock.Count);
            Assert.AreEqual(_ale, stock[0].item);
        }

        [Test]
        public void ApplyStockChange_RemoveMissing_FlagsNotFoundAndKeepsStock()
        {
            var stock = new List<StartingItem> { Entry(_ale, 5) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _lockpick, 0);

            Assert.AreEqual(StockChange.NotFound, result.Change);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, stock.Count);
        }

        [Test]
        public void ApplyStockChange_NegativeCount_IsErrorAndKeepsStock()
        {
            var stock = new List<StartingItem> { Entry(_ale, 5) };

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, _ale, -1);

            Assert.AreEqual(StockChange.Error, result.Change);
            Assert.AreEqual(5, stock[0].count);
        }

        [Test]
        public void ApplyStockChange_NullItem_IsError()
        {
            var stock = new List<StartingItem>();

            StockChangeResult result = NpcShopEditor.ApplyStockChange(stock, null, 3);

            Assert.AreEqual(StockChange.Error, result.Change);
            Assert.IsEmpty(stock);
        }
    }
}
