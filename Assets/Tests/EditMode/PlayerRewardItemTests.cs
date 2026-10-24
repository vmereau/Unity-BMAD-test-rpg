using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Inventory;
using Game.Progression;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Item rewards on <see cref="PlayerRewardSO"/>: added to the inventory by <see cref="PlayerRewards"/>,
    /// one "Received: …" toast per entry, bad entries skipped; reward gold toast text.
    /// </summary>
    public class PlayerRewardItemTests
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<string> _itemToasts = new List<string>();
        private readonly List<int> _goldToasts = new List<int>();
        private PlayerRewards _rewards;
        private InventorySystem _inventory;
        private ItemSO _trout;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("PlayerRewards_Test");
            _cleanup.Add(go);
            _rewards = go.AddComponent<PlayerRewards>();
            _inventory = go.AddComponent<InventorySystem>();

            var onItem = Create<GameEventSO_String>();
            onItem.AddListener(_itemToasts.Add);
            var onGold = Create<GameEventSO_Int>();
            onGold.AddListener(_goldToasts.Add);
            Set(_rewards, "_inventory", _inventory);
            Set(_rewards, "_onRewardItemReceived", onItem);
            Set(_rewards, "_onRewardGoldGained", onGold);

            _trout = Create<ItemSO>();
            _trout.itemName = "Trout";
            _trout.maxStacks = 20;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup)
                if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
            _itemToasts.Clear();
            _goldToasts.Clear();
        }

        [Test]
        public void FormatItemReceived_SingleUnit_HasNoCount()
        {
            Assert.AreEqual("Received: Recipe: Apple Trout Sandwich",
                PlayerRewardSO.FormatItemReceived("Recipe: Apple Trout Sandwich", 1));
        }

        [Test]
        public void FormatItemReceived_SeveralUnits_ShowsCount()
        {
            Assert.AreEqual("Received: Trout ×3", PlayerRewardSO.FormatItemReceived("Trout", 3));
        }

        [Test]
        public void FormatGold_ShowsAmount()
        {
            Assert.AreEqual("Gold +100", NotificationToastUI.FormatGold(100));
        }

        [Test]
        public void ItemReward_AddsCountUnits_AndRaisesOneToast()
        {
            Apply(Reward(new ItemReward { item = _trout, count = 3 }));

            Assert.AreEqual(3, _inventory.CountOf(_trout));
            CollectionAssert.AreEqual(new[] { "Received: Trout ×3" }, _itemToasts);
        }

        [Test]
        public void BadEntries_AreSkipped_OthersStillApplied()
        {
            Apply(Reward(
                new ItemReward { item = null, count = 1 },
                new ItemReward { item = _trout, count = 0 },
                new ItemReward { item = _trout, count = 1 }));

            Assert.AreEqual(1, _inventory.CountOf(_trout));
            CollectionAssert.AreEqual(new[] { "Received: Trout" }, _itemToasts);
        }

        [Test]
        public void NoInventory_SkipsItems_AndRaisesNoToast()
        {
            Set(_rewards, "_inventory", null);

            Apply(Reward(new ItemReward { item = _trout, count = 1 }));

            Assert.AreEqual(0, _inventory.CountOf(_trout));
            Assert.IsEmpty(_itemToasts);
        }

        [Test]
        public void GoldReward_AddsGold_AndRaisesGoldToast()
        {
            var gold = _rewards.gameObject.AddComponent<Game.Economy.GoldSystem>();
            Set(_rewards, "_goldSystem", gold);
            var reward = Reward();
            Set(reward, "_goldReward", 100);

            Apply(reward);

            Assert.AreEqual(100, gold.Gold);
            CollectionAssert.AreEqual(new[] { 100 }, _goldToasts);
        }

        [Test]
        public void NoGoldSystem_RaisesNoGoldToast()
        {
            var reward = Reward();
            Set(reward, "_goldReward", 100);

            Apply(reward);

            Assert.IsEmpty(_goldToasts);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private PlayerRewardSO Reward(params ItemReward[] items)
        {
            var reward = Create<PlayerRewardSO>();
            Set(reward, "_itemRewards", new List<ItemReward>(items));
            return reward;
        }

        private void Apply(PlayerRewardSO reward)
        {
            var method = typeof(PlayerRewards).GetMethod("ApplyRewards", PRIVATE);
            Assert.IsNotNull(method, "PlayerRewards.ApplyRewards not found");
            method.Invoke(_rewards, new object[] { reward });
        }

        private T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private static void Set(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, PRIVATE);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field} not found");
            info.SetValue(target, value);
        }
    }
}
