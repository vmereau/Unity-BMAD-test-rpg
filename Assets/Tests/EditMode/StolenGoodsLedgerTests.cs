using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class StolenGoodsLedgerTests
    {
        private ItemSO _bread;
        private ItemSO _potion;
        private readonly List<StolenItemCount> _items = new List<StolenItemCount>();

        [SetUp]
        public void SetUp()
        {
            StolenGoodsLedger.ClearAll();
            _bread = ScriptableObject.CreateInstance<ItemSO>();
            _potion = ScriptableObject.CreateInstance<ItemSO>();
        }

        [TearDown]
        public void TearDown()
        {
            StolenGoodsLedger.ClearAll();
            Object.DestroyImmediate(_bread);
            Object.DestroyImmediate(_potion);
        }

        [Test]
        public void Collect_SumsItemsAndGoldAcrossIncidents()
        {
            StolenGoodsLedger.Record(1, _potion);
            StolenGoodsLedger.Record(2, _bread);
            StolenGoodsLedger.Record(2, _bread);
            StolenGoodsLedger.Record(2, _potion);
            StolenGoodsLedger.RecordGold(2, 12);

            int gold = StolenGoodsLedger.Collect(new[] { 1, 2 }, _items);

            Assert.AreEqual(12, gold);
            Assert.AreEqual(2, _items.Count);
            Assert.AreEqual(_potion, _items[0].item);
            Assert.AreEqual(2, _items[0].count);
            Assert.AreEqual(_bread, _items[1].item);
            Assert.AreEqual(2, _items[1].count);
        }

        [Test]
        public void Collect_SkipsUnknownAndDuplicateIds()
        {
            StolenGoodsLedger.Record(5, _bread);
            StolenGoodsLedger.RecordGold(5, 3);

            int gold = StolenGoodsLedger.Collect(new[] { 5, 5, 99 }, _items);

            Assert.AreEqual(3, gold);
            Assert.AreEqual(1, _items.Count);
            Assert.AreEqual(1, _items[0].count);
        }

        [Test]
        public void Record_IgnoresNullItemAndNonPositiveAmounts()
        {
            StolenGoodsLedger.Record(1, null);
            StolenGoodsLedger.Record(1, _bread, 0);
            StolenGoodsLedger.RecordGold(1, 0);

            Assert.IsFalse(StolenGoodsLedger.HasGoods(new[] { 1 }));
        }

        [Test]
        public void HasGoods_TrueForGoldOnlyIncident()
        {
            StolenGoodsLedger.RecordGold(7, 4);
            Assert.IsTrue(StolenGoodsLedger.HasGoods(new[] { 7 }));
            Assert.IsFalse(StolenGoodsLedger.HasGoods(new[] { 8 }));
        }

        [Test]
        public void Clear_ForgetsIncidentsAndRefusals()
        {
            StolenGoodsLedger.Record(1, _bread);
            StolenGoodsLedger.MarkRefused(new[] { 1 });

            StolenGoodsLedger.Clear(new[] { 1 });

            Assert.IsFalse(StolenGoodsLedger.HasGoods(new[] { 1 }));
            Assert.IsFalse(StolenGoodsLedger.HasRefused);
        }

        [Test]
        public void TakeRefused_ReturnsRefusedGoodsOnlyThenClearsThem()
        {
            StolenGoodsLedger.Record(1, _bread);
            StolenGoodsLedger.RecordGold(1, 10);
            StolenGoodsLedger.Record(2, _potion);
            StolenGoodsLedger.MarkRefused(new[] { 1 });

            int gold = StolenGoodsLedger.TakeRefused(_items);

            Assert.AreEqual(10, gold);
            Assert.AreEqual(1, _items.Count);
            Assert.AreEqual(_bread, _items[0].item);
            Assert.IsFalse(StolenGoodsLedger.HasRefused);
            Assert.IsFalse(StolenGoodsLedger.HasGoods(new[] { 1 }));
            Assert.IsTrue(StolenGoodsLedger.HasGoods(new[] { 2 }));
        }

        [Test]
        public void ClearRefused_ForgetsOnlyRefusedIncidents()
        {
            StolenGoodsLedger.Record(1, _bread);
            StolenGoodsLedger.Record(2, _potion);
            StolenGoodsLedger.MarkRefused(new[] { 1 });
            Assert.IsTrue(StolenGoodsLedger.IsRefused(1));

            StolenGoodsLedger.ClearRefused();

            Assert.IsFalse(StolenGoodsLedger.IsRefused(1));
            Assert.IsFalse(StolenGoodsLedger.HasGoods(new[] { 1 }));
            Assert.IsTrue(StolenGoodsLedger.HasGoods(new[] { 2 }));
        }

        [Test]
        public void MarkRefused_IgnoresIncidentsWithoutGoods()
        {
            StolenGoodsLedger.MarkRefused(new[] { 3 });
            Assert.IsFalse(StolenGoodsLedger.HasRefused);
        }

        [Test]
        public void Request_ClampsResultsToRequested()
        {
            var request = new StolenGoodsRequest(new GameObject("victim"), new[] { new StolenItemCount(_bread, 2) }, 5, take: false);
            try
            {
                Assert.IsTrue(request.TryHandleItems(new[] { 9 }));
                Assert.IsTrue(request.TryHandleGold(50));
                Assert.IsFalse(request.TryHandleGold(1));
                Assert.AreEqual(2, request.ItemResult(0));
                Assert.AreEqual(5, request.GoldResult);
                Assert.IsTrue(request.HasAny);
            }
            finally { Object.DestroyImmediate(request.Victim); }
        }

        [Test]
        public void Request_NothingAvailable_HasAnyFalse()
        {
            var request = new StolenGoodsRequest(new GameObject("victim"), new[] { new StolenItemCount(_bread, 2) }, 5, take: false);
            try
            {
                request.TryHandleItems(new[] { 0 });
                request.TryHandleGold(0);
                Assert.IsFalse(request.HasAny);
            }
            finally { Object.DestroyImmediate(request.Victim); }
        }
    }
}
