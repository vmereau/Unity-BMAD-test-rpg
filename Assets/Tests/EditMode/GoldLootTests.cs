using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Economy;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class GoldLootTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, PRIVATE).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, PRIVATE).Invoke(target, null);

        private GameObject Go()
        {
            var go = new GameObject("GoldLootTest");
            go.SetActive(false);
            _cleanup.Add(go);
            return go;
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(asset);
            return asset;
        }

        private (InventorySystem inventory, GoldSystem gold, GameEventSO_GoldLootRequest channel) Player(int balance = 10)
        {
            var go = Go();
            var inventory = go.AddComponent<InventorySystem>();
            var gold = go.AddComponent<GoldSystem>();
            var channel = Asset<GameEventSO_GoldLootRequest>();
            Set(inventory, "_collectGoldAsCurrency", true);
            Set(inventory, "_onGoldLootRequested", channel);
            Set(gold, "_onGoldLootRequested", channel);
            gold.RestoreGold(balance);
            Invoke(gold, "OnEnable");
            return (inventory, gold, channel);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in _cleanup)
                if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        [Test]
        public void PlayerCollectsOneGoldWithoutItemSlotAndNotifiesBalance()
        {
            var player = Player();
            var changed = Asset<GameEventSO_Int>();
            Set(player.gold, "_onGoldChanged", changed);
            int observed = -1;
            changed.AddListener(value => observed = value);
            Assert.IsTrue(player.inventory.AddItem(Asset<GoldItemSO>()));
            Assert.AreEqual(11, player.gold.Gold);
            Assert.AreEqual(11, observed);
            Assert.AreEqual(0, player.inventory.Count);
        }

        [Test]
        public void OrdinaryItemsStillEnterPlayerInventory()
        {
            var player = Player();
            Assert.IsTrue(player.inventory.AddItem(Asset<ItemSO>()));
            Assert.AreEqual(1, player.inventory.Count);
            Assert.AreEqual(10, player.gold.Gold);
        }

        [Test]
        public void NonPlayerCoinsStackWithoutRequestingCurrency()
        {
            var inventory = Go().AddComponent<InventorySystem>();
            var channel = Asset<GameEventSO_GoldLootRequest>();
            Set(inventory, "_onGoldLootRequested", channel);
            int requests = 0;
            channel.AddListener(_ => requests++);
            var coin = Asset<GoldItemSO>();
            coin.maxStacks = 100;
            for (int i = 0; i < 25; i++) Assert.IsTrue(inventory.AddItem(coin));
            Assert.AreEqual(1, inventory.Count);
            Assert.AreEqual(25, inventory.CountOf(coin));
            Assert.AreEqual(0, requests);
        }

        [Test]
        public void MissingChannelOrReceiverRejectsWithoutCreatingPlayerCoins()
        {
            var player = Player();
            var coin = Asset<GoldItemSO>();
            Invoke(player.gold, "OnDisable");
            Assert.IsFalse(player.inventory.AddItem(coin));
            Set(player.inventory, "_onGoldLootRequested", null);
            Assert.IsFalse(player.inventory.AddItem(coin));
            Assert.AreEqual(10, player.gold.Gold);
            Assert.AreEqual(0, player.inventory.Count);
        }

        [Test]
        public void OverflowRejectsWithoutBalanceChange()
        {
            var player = Player(int.MaxValue);
            Assert.IsFalse(player.inventory.AddItem(Asset<GoldItemSO>()));
            Assert.AreEqual(int.MaxValue, player.gold.Gold);
            Assert.AreEqual(0, player.inventory.Count);
        }

        [Test]
        public void RepeatedRequestAndReentrantNotificationPayOnlyOnce()
        {
            var player = Player();
            var request = new GoldLootRequest(player.inventory.gameObject, 1);
            var changed = Asset<GameEventSO_Int>();
            Set(player.gold, "_onGoldChanged", changed);
            int notifications = 0;
            changed.AddListener(_ => { notifications++; player.channel.Raise(request); });
            player.channel.Raise(request);
            player.channel.Raise(request);
            Assert.IsTrue(request.Accepted);
            Assert.AreEqual(11, player.gold.Gold);
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void OtherRecipientCannotReceivePlayerCurrency()
        {
            var player = Player();
            var request = new GoldLootRequest(Go(), 1);
            player.channel.Raise(request);
            Assert.IsFalse(request.Accepted);
            Assert.AreEqual(10, player.gold.Gold);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidRequestCannotBeAccepted(int amount)
        {
            var player = Player();
            var request = new GoldLootRequest(player.inventory.gameObject, amount);
            player.channel.Raise(request);
            Assert.IsFalse(request.Accepted);
            Assert.AreEqual(10, player.gold.Gold);
        }

        [Test]
        public void RestoreAndNonPlayerStartingStockNeverPayCurrency()
        {
            var player = Player();
            int requests = 0;
            player.channel.AddListener(_ => requests++);
            var coin = Asset<GoldItemSO>();
            coin.maxStacks = 100;
            var source = Go().AddComponent<InventorySystem>();
            Set(source, "_onGoldLootRequested", player.channel);
            Set(source, "_startingItems", new List<StartingItem> { new StartingItem { item = coin, count = 25 } });
            Invoke(source, "Awake");
            Assert.AreEqual(25, source.CountOf(coin));
            source.RestoreSlots(new[] { ((ItemSO)coin, 24) });
            Assert.AreEqual(24, source.CountOf(coin));
            player.gold.RestoreGold(11);
            Assert.AreEqual(11, player.gold.Gold);
            Assert.AreEqual(0, requests);
        }

        [Test]
        public void PlayerStartingCoinsAreIgnoredWithoutPayoutOrSlots()
        {
            var player = Player();
            Set(player.inventory, "_startingItems", new List<StartingItem>
            {
                new StartingItem { item = Asset<GoldItemSO>(), count = 2 }
            });
            Invoke(player.inventory, "Awake");
            Assert.AreEqual(10, player.gold.Gold);
            Assert.AreEqual(0, player.inventory.Count);
        }

        [Test]
        public void SuccessfulContainerTakeCreditsAndEmitsSourceItemNotification()
        {
            var player = Player();
            var source = Go().AddComponent<InventorySystem>();
            var coin = Asset<GoldItemSO>();
            source.AddItem(coin);
            var taken = Asset<GameEventSO_ContainerItemTaken>();
            ContainerItemTakenData observed = default;
            int notifications = 0;
            taken.AddListener(data => { observed = data; notifications++; });
            var ui = Go().AddComponent<ContainerUI>();
            Set(ui, "_containerInventory", source);
            Set(ui, "_playerInventory", player.inventory);
            Set(ui, "_onItemTaken", taken);
            ui.TakeItem(0);
            Assert.AreEqual(11, player.gold.Gold);
            Assert.AreEqual(0, player.inventory.Count);
            Assert.AreEqual(0, source.Count);
            Assert.AreEqual(1, notifications);
            Assert.AreSame(source, observed.sourceInventory);
            Assert.AreSame(coin, observed.item);
        }

        [Test]
        public void RejectedContainerTakePreservesCoinsAndEmitsNothing()
        {
            var player = Player(int.MaxValue);
            var source = Go().AddComponent<InventorySystem>();
            var coin = Asset<GoldItemSO>();
            coin.maxStacks = 100;
            source.RestoreSlots(new[] { ((ItemSO)coin, 25) });
            var taken = Asset<GameEventSO_ContainerItemTaken>();
            int notifications = 0;
            taken.AddListener(_ => notifications++);
            var ui = Go().AddComponent<ContainerUI>();
            Set(ui, "_containerInventory", source);
            Set(ui, "_playerInventory", player.inventory);
            Set(ui, "_onItemTaken", taken);
            ui.TakeItem(0);
            Assert.AreEqual(25, source.CountOf(coin));
            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void RejectedWorldPickupRemainsAvailable()
        {
            var player = Player(int.MaxValue);
            var pickup = Go().AddComponent<ItemPickup>();
            pickup.Configure(Asset<GoldItemSO>());
            Set(pickup, "_inventory", player.inventory);
            pickup.Interact();
            Assert.IsTrue(pickup != null);
            Assert.IsTrue(pickup.CanInteract);
            Assert.AreEqual(int.MaxValue, player.gold.Gold);
        }
    }
}
