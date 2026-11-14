using System.Collections.Generic;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Quest items (<see cref="ItemSO.isQuestItem"/>): category, filter tab, the part rule and the action-bar refusal,
    /// plus the dialogue choice's required-item label (<see cref="DialogueOptionFormat.ChoiceCostLabel"/>).
    /// </summary>
    public class QuestItemTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        private ItemSO QuestItem()
        {
            var item = Create<ItemSO>();
            item.itemName = "Notice for Overseer Dace";
            item.isQuestItem = true;
            return item;
        }

        [Test]
        public void Category_QuestItem_IsQuestItem() =>
            Assert.AreEqual("Quest Item", ItemDetailFormatter.GetCategory(QuestItem()));

        [Test]
        public void Category_QuestFlagWinsOverIngredient()
        {
            var item = QuestItem();
            item.isIngredient = true;
            Assert.AreEqual("Quest Item", ItemDetailFormatter.GetCategory(item));
        }

        [Test]
        public void Filter_QuestItem_IsMisc() =>
            Assert.AreEqual(ItemFilter.Misc, ItemFilterRules.Classify(QuestItem()));

        [Test]
        public void CanPlayerPart_QuestItem_False() => Assert.IsFalse(ItemSO.CanPlayerPart(QuestItem()));

        [Test]
        public void CanPlayerPart_RegularItem_True() => Assert.IsTrue(ItemSO.CanPlayerPart(Create<ItemSO>()));

        [Test]
        public void CanPlayerPart_Null_False() => Assert.IsFalse(ItemSO.CanPlayerPart(null));

        [Test]
        public void Cooking_QuestIngredient_IsNotMaterial()
        {
            var item = QuestItem();
            item.isIngredient = true;
            item.isCraftingMaterial = true;
            Assert.IsFalse(Game.Crafting.CookingRules.IsMaterialFor(Game.Crafting.CraftingKind.Cooking, item));
            Assert.IsFalse(Game.Crafting.CookingRules.IsMaterialFor(Game.Crafting.CraftingKind.Smithing, item));
        }

        [Test]
        public void ActionBar_Assign_RefusesQuestItem()
        {
            var go = new GameObject("ActionBarQuestItemTestGO");
            try
            {
                var bar = go.AddComponent<ActionBarSystem>();
                bar.Assign(1, 0, Create<ItemSO>());
                Assert.IsTrue(bar.GetSlot(1).HasValue, "control: a regular item binds");
                bar.Assign(0, 0, QuestItem());
                Assert.IsFalse(bar.GetSlot(0).HasValue);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── DialogueSystem required item ─────────────────────────────────────

        [Test]
        public void SelectChoice_MissingRequiredItem_IsRefused()
        {
            var go = new GameObject("DialogueRequiredItemTestGO");
            try
            {
                var inventory = go.AddComponent<InventorySystem>();
                var dialogue = go.AddComponent<Game.World.DialogueSystem>();
                typeof(Game.World.DialogueSystem)
                    .GetField("_playerInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(dialogue, inventory);
                var notice = QuestItem();
                var choice = new Game.Dialogue.ChoiceOption { text = "Hand over", requiredItem = notice };

                Assert.IsFalse(dialogue.HasChoiceItem(notice));
                Assert.IsFalse(dialogue.SelectChoice(choice));

                inventory.AddItem(notice);
                Assert.IsTrue(dialogue.HasChoiceItem(notice));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── Choice cost label ────────────────────────────────────────────────

        [Test]
        public void ChoiceCostLabel_HeldItem_NotRed() =>
            Assert.AreEqual(" (Notice)", DialogueOptionFormat.ChoiceCostLabel(0, true, "Notice", true));

        [Test]
        public void ChoiceCostLabel_MissingItem_Red() =>
            Assert.AreEqual(" (<color=#A8473B>Notice</color>)", DialogueOptionFormat.ChoiceCostLabel(0, true, "Notice", false));

        [Test]
        public void ChoiceCostLabel_GoldAndItem_BothParts() =>
            Assert.AreEqual(" (<color=#A8473B>10g</color>, Notice)",
                DialogueOptionFormat.ChoiceCostLabel(10, false, "Notice", true));

        [Test]
        public void ChoiceCostLabel_FreeNoItem_Empty() =>
            Assert.IsEmpty(DialogueOptionFormat.ChoiceCostLabel(0, true, null, true));

        [Test]
        public void GoldCostLabel_Unchanged() =>
            Assert.AreEqual(" (10g)", DialogueOptionFormat.GoldCostLabel(10, true));
    }
}
