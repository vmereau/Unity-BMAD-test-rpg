using System.Reflection;
using Game.Dialogue;
using Game.Inventory;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ChoiceItemRulesTests
    {
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;

        // ── Pure rule ────────────────────────────────────────────────────────

        [Test] public void Locate_InBag_IsBag() => Assert.AreEqual(ChoiceItemSource.Bag, ChoiceItemRules.Locate(1, false));
        [Test] public void Locate_InBagAndWorn_BagWins() => Assert.AreEqual(ChoiceItemSource.Bag, ChoiceItemRules.Locate(1, true));
        [Test] public void Locate_WornOnly_IsEquipped() => Assert.AreEqual(ChoiceItemSource.Equipped, ChoiceItemRules.Locate(0, true));
        [Test] public void Locate_Neither_IsNone() => Assert.AreEqual(ChoiceItemSource.None, ChoiceItemRules.Locate(0, false));

        // ── DialogueSystem hand-over ─────────────────────────────────────────

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PRIVATE).SetValue(target, value);

        private static ArmorSO Ring()
        {
            var ring = ScriptableObject.CreateInstance<ArmorSO>();
            ring.itemName = "Ring";
            ring.slot = EquipmentSlot.Ring1;
            return ring;
        }

        [Test]
        public void SelectChoice_WornItem_GiveToNpc_UnequipsAndMovesToNpc()
        {
            var player = new GameObject("ChoiceItemPlayer");
            var npc = new GameObject("ChoiceItemNpc");
            var ring = Ring();
            try
            {
                var bag = player.AddComponent<InventorySystem>();
                player.SetActive(false); // wire before Awake
                var equipment = player.AddComponent<EquipmentSystem>();
                Set(equipment, "_inventorySystem", bag);
                var dialogue = player.AddComponent<DialogueSystem>();
                Set(dialogue, "_playerInventory", bag);
                Set(dialogue, "_playerEquipment", equipment);
                player.SetActive(true);
                var npcBag = npc.AddComponent<InventorySystem>();
                Set(dialogue, "_currentNPCInventory", npcBag);

                bag.AddItem(ring);
                equipment.Equip(ring);
                Assert.AreEqual(0, bag.CountOf(ring), "control: equipping empties the bag");
                Assert.IsTrue(dialogue.HasChoiceItem(ring), "a worn ring counts as held");

                var choice = new ChoiceOption { text = "Here", requiredItem = ring, giveToNpc = true };
                Assert.IsTrue(dialogue.SelectChoice(choice));
                Assert.IsFalse(equipment.IsEquipped(ring));
                Assert.AreEqual(0, bag.CountOf(ring));
                Assert.AreEqual(1, npcBag.CountOf(ring));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(npc);
                Object.DestroyImmediate(ring);
            }
        }

        [Test]
        public void SelectChoice_GiveToNpcOff_ConsumesItem()
        {
            var player = new GameObject("ChoiceItemPlayer");
            var npc = new GameObject("ChoiceItemNpc");
            var ring = Ring();
            try
            {
                var bag = player.AddComponent<InventorySystem>();
                var dialogue = player.AddComponent<DialogueSystem>();
                Set(dialogue, "_playerInventory", bag);
                var npcBag = npc.AddComponent<InventorySystem>();
                Set(dialogue, "_currentNPCInventory", npcBag);
                bag.AddItem(ring);

                Assert.IsTrue(dialogue.SelectChoice(new ChoiceOption { text = "Here", requiredItem = ring }));
                Assert.AreEqual(0, bag.CountOf(ring));
                Assert.AreEqual(0, npcBag.CountOf(ring));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(npc);
                Object.DestroyImmediate(ring);
            }
        }
    }
}
