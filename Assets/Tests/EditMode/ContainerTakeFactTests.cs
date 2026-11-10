using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Inventory;
using Game.UI;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ContainerTakeFactTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(asset);
            return asset;
        }
        private GameObject Go()
        {
            var go = new GameObject("ContainerTakeTest");
            go.SetActive(false);
            _cleanup.Add(go);
            return go;
        }

        [TearDown]
        public void Cleanup()
        {
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            foreach (var obj in _cleanup) if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        [Test]
        public void MatchRequiresExactSourceAndAllowedItem()
        {
            var source = Go().AddComponent<InventorySystem>();
            var other = Go().AddComponent<InventorySystem>();
            var bread = Asset<ItemSO>();
            var unrelated = Asset<ItemSO>();
            Assert.IsTrue(ContainerTakeFact.Matches(source, new[] { bread }, new ContainerItemTakenData { sourceInventory = source, item = bread }));
            Assert.IsFalse(ContainerTakeFact.Matches(source, new[] { bread }, new ContainerItemTakenData { sourceInventory = other, item = bread }));
            Assert.IsFalse(ContainerTakeFact.Matches(source, new[] { bread }, new ContainerItemTakenData { sourceInventory = source, item = unrelated }));
            Assert.IsFalse(ContainerTakeFact.Matches(source, new[] { bread }, default));
            Assert.IsFalse(ContainerTakeFact.Matches(null, new[] { bread }, default));
        }

        [Test]
        public void TakingWithoutDestinationPreservesSourceAndEmitsNothing()
        {
            var source = Go().AddComponent<InventorySystem>();
            var bread = Asset<ItemSO>();
            source.AddItem(bread);
            var channel = Asset<GameEventSO_ContainerItemTaken>();
            int notifications = 0;
            channel.AddListener(_ => notifications++);
            var ui = Go().AddComponent<ContainerUI>();
            Set(ui, "_containerInventory", source);
            Set(ui, "_onItemTaken", channel);
            ui.TakeItem(0);
            Assert.AreEqual(1, source.CountOf(bread));
            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void RepeatedTakeNotificationSetsFactOnlyOnceAndRestoreDoesNotSetIt()
        {
            var state = Go().AddComponent<WorldStateManager>();
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, state);
            var factEvent = Asset<GameEventSO_Fact>();
            Set(state, "_onFactChanged", factEvent);
            int writes = 0;
            factEvent.AddListener(_ => writes++);
            var fact = Asset<WorldFact>().Init("test.stash");
            var bread = Asset<ItemSO>();
            var go = Go();
            var inventory = go.AddComponent<InventorySystem>();
            var listener = go.AddComponent<ContainerTakeFact>();
            var channel = Asset<GameEventSO_ContainerItemTaken>();
            Set(listener, "_onItemTaken", channel);
            Set(listener, "_fact", fact);
            Set(listener, "_items", new[] { bread });
            Set(listener, "_inventory", inventory);
            typeof(ContainerTakeFact).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(listener, null);
            inventory.RestoreSlots(new[] { (bread, 5) });
            Assert.IsFalse(state.GetFact(fact));
            channel.Raise(new ContainerItemTakenData { sourceInventory = inventory, item = bread });
            channel.Raise(new ContainerItemTakenData { sourceInventory = inventory, item = bread });
            Assert.IsTrue(state.GetFact(fact));
            Assert.AreEqual(1, writes);
        }
    }
}
