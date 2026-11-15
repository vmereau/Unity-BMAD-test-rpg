using System.Collections.Generic;
using Game.Dialogue;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Which items a dialogue Give choice accepts (<see cref="GiveItemRules"/>) and the choice row's goods label
    /// (<see cref="DialogueOptionFormat.ChoiceGoodsName"/>).
    /// </summary>
    public class GiveItemRulesTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private PotionItemSO Food(ConsumableKind kind, bool readyToEat, bool ingredient = false, bool quest = false)
        {
            var item = ScriptableObject.CreateInstance<PotionItemSO>();
            _created.Add(item);
            item.itemName = "Food";
            item.isIngredient = ingredient;
            item.isQuestItem = quest;
            var so = new SerializedObject(item);
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_readyToEat").boolValue = readyToEat;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        [Test]
        public void ReadyFood_Apple_Matches() =>
            Assert.IsTrue(GiveItemRules.Matches(Food(ConsumableKind.Potion, true, ingredient: true), GiveItemCategory.ReadyFood));

        [Test]
        public void ReadyFood_RawTrout_DoesNotMatch() =>
            Assert.IsFalse(GiveItemRules.Matches(Food(ConsumableKind.Potion, false, ingredient: true), GiveItemCategory.ReadyFood));

        [Test]
        public void ReadyFood_Meal_DoesNotMatch() =>
            Assert.IsFalse(GiveItemRules.Matches(Food(ConsumableKind.Meal, false), GiveItemCategory.ReadyFood));

        [Test]
        public void Meal_CookedMeal_Matches() =>
            Assert.IsTrue(GiveItemRules.Matches(Food(ConsumableKind.Meal, false), GiveItemCategory.Meal));

        [Test]
        public void Meal_Apple_DoesNotMatch() =>
            Assert.IsFalse(GiveItemRules.Matches(Food(ConsumableKind.Potion, true, ingredient: true), GiveItemCategory.Meal));

        [Test]
        public void QuestItem_NeverMatches()
        {
            Assert.IsFalse(GiveItemRules.Matches(Food(ConsumableKind.Potion, true, quest: true), GiveItemCategory.ReadyFood));
            Assert.IsFalse(GiveItemRules.Matches(Food(ConsumableKind.Meal, false, quest: true), GiveItemCategory.Meal));
        }

        [Test]
        public void None_AndNullItem_NeverMatch()
        {
            Assert.IsFalse(GiveItemRules.Matches(Food(ConsumableKind.Potion, true), GiveItemCategory.None));
            Assert.IsFalse(GiveItemRules.Matches(null, GiveItemCategory.ReadyFood));
        }

        [Test]
        public void NonFoodItem_DoesNotMatch()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            _created.Add(item);
            Assert.IsFalse(GiveItemRules.Matches(item, GiveItemCategory.ReadyFood));
        }

        [Test]
        public void GoodsName_Category_UsesLabel()
        {
            Assert.AreEqual("Food", DialogueOptionFormat.ChoiceGoodsName(new ChoiceOption { requiredCategory = GiveItemCategory.ReadyFood }));
            Assert.AreEqual("Meal", DialogueOptionFormat.ChoiceGoodsName(new ChoiceOption { requiredCategory = GiveItemCategory.Meal }));
        }

        [Test]
        public void GoodsName_RequiredItemWinsOverCategory()
        {
            var item = Food(ConsumableKind.Potion, true);
            item.itemName = "Apple";
            Assert.AreEqual("Apple", DialogueOptionFormat.ChoiceGoodsName(
                new ChoiceOption { requiredItem = item, requiredCategory = GiveItemCategory.Meal }));
        }

        [Test]
        public void GoodsName_NoGoods_IsNull() =>
            Assert.IsNull(DialogueOptionFormat.ChoiceGoodsName(new ChoiceOption()));

        [Test]
        public void CostLabel_MissingCategory_IsRed() =>
            Assert.AreEqual(" (<color=#A8473B>Meal</color>)", DialogueOptionFormat.ChoiceCostLabel(0, true, "Meal", false));

        // ── DialogueSystem category choice ───────────────────────────────────

        [Test]
        public void HasChoiceCategory_FollowsInventory_AndSelectRefusesWithoutMatch()
        {
            var go = new GameObject("DialogueGiveCategoryTestGO");
            try
            {
                var inventory = go.AddComponent<InventorySystem>();
                var dialogue = go.AddComponent<Game.World.DialogueSystem>();
                typeof(Game.World.DialogueSystem)
                    .GetField("_playerInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(dialogue, inventory);
                var choice = new ChoiceOption { text = "Here.", requiredCategory = GiveItemCategory.Meal };

                inventory.AddItem(Food(ConsumableKind.Potion, true)); // an apple is not a meal
                Assert.IsFalse(dialogue.HasChoiceCategory(GiveItemCategory.Meal));
                Assert.IsFalse(dialogue.HasChoiceGoods(choice));
                Assert.IsFalse(dialogue.SelectChoice(choice));
                Assert.IsTrue(dialogue.HasChoiceCategory(GiveItemCategory.ReadyFood));

                inventory.AddItem(Food(ConsumableKind.Meal, false));
                Assert.IsTrue(dialogue.HasChoiceGoods(choice));
            }
            finally { Object.DestroyImmediate(go); }
        }

        private const System.Reflection.BindingFlags Private =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // DialogueSystem with a player inventory, an open dialogue and an NPC inventory (no UI, no WorldStateManager).
        private static Game.World.DialogueSystem OpenDialogue(GameObject go, out InventorySystem player, out InventorySystem npc)
        {
            player = go.AddComponent<InventorySystem>();
            npc = new GameObject("NpcInventory").AddComponent<InventorySystem>();
            npc.transform.SetParent(go.transform);
            var dialogue = go.AddComponent<Game.World.DialogueSystem>();
            var type = typeof(Game.World.DialogueSystem);
            type.GetField("_playerInventory", Private).SetValue(dialogue, player);
            type.GetField("_currentNPCInventory", Private).SetValue(dialogue, npc);
            type.GetProperty("IsOpen").SetValue(dialogue, true);
            return dialogue;
        }

        [Test]
        public void CompleteGiveChoice_MovesOneUnitToNpc_KeepsOtherItems()
        {
            var go = new GameObject("DialogueGiveSuccessTestGO");
            try
            {
                var dialogue = OpenDialogue(go, out var player, out var npc);
                var apple = Food(ConsumableKind.Potion, true);
                var meal = Food(ConsumableKind.Meal, false);
                meal.maxStacks = 5;
                player.AddItem(apple);
                player.AddItems(meal, 2);
                var choice = new ChoiceOption { text = "Here.", requiredCategory = GiveItemCategory.Meal };

                Assert.IsTrue(dialogue.CompleteGiveChoice(choice, null, meal));
                Assert.AreEqual(1, player.CountOf(meal));
                Assert.AreEqual(1, npc.CountOf(meal));
                Assert.AreEqual(1, player.CountOf(apple), "the other food stays with the player");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CompleteGiveChoice_WrongCategory_TakesNothing()
        {
            var go = new GameObject("DialogueGiveWrongTestGO");
            try
            {
                var dialogue = OpenDialogue(go, out var player, out var npc);
                var apple = Food(ConsumableKind.Potion, true);
                player.AddItem(apple);
                var choice = new ChoiceOption { text = "Here.", requiredCategory = GiveItemCategory.Meal };

                Assert.IsFalse(dialogue.CompleteGiveChoice(choice, null, apple));
                Assert.AreEqual(1, player.CountOf(apple));
                Assert.AreEqual(0, npc.CountOf(apple));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HideWhenMissing_HidesOnlyFlaggedChoicesWithoutGoods()
        {
            var go = new GameObject("DialogueHideMissingTestGO");
            try
            {
                var dialogue = OpenDialogue(go, out var player, out _);
                player.AddItem(Food(ConsumableKind.Potion, true));
                var food = new ChoiceOption { text = "Food", requiredCategory = GiveItemCategory.ReadyFood };
                var mealHidden = new ChoiceOption { text = "Meal", requiredCategory = GiveItemCategory.Meal, hideWhenMissing = true };
                var mealShown = new ChoiceOption { text = "Meal (red)", requiredCategory = GiveItemCategory.Meal };
                var plain = new ChoiceOption { text = "Not yet." };
                var filter = typeof(Game.World.DialogueSystem).GetMethod("FilterMissingGoodsChoices", Private);

                var kept = (ChoiceOption[])filter.Invoke(dialogue, new object[] { new[] { food, mealHidden, mealShown, plain } });
                CollectionAssert.AreEqual(new[] { food, mealShown, plain }, kept);

                player.AddItem(Food(ConsumableKind.Meal, false));
                kept = (ChoiceOption[])filter.Invoke(dialogue, new object[] { new[] { food, mealHidden, mealShown, plain } });
                CollectionAssert.AreEqual(new[] { food, mealHidden, mealShown, plain }, kept);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("Item_Apple", true)]
        [TestCase("Item_Bread", true)]
        [TestCase("Item_Trout", false)]
        [TestCase("Item_BrownMushroom", false)]
        [TestCase("Item_HealingPlant", false)]
        public void RealAssets_ReadyFood(string asset, bool expected)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>($"Assets/_Game/Data/Items/{asset}.asset");
            Assert.IsNotNull(item, asset);
            Assert.AreEqual(expected, GiveItemRules.Matches(item, GiveItemCategory.ReadyFood));
        }

        [TestCase("Item_AppleTroutSandwich")]
        [TestCase("Item_AppleMushroomSandwich")]
        [TestCase("Item_ReplenishingStew")]
        public void RealAssets_MealsAreMealsOnly(string asset)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>($"Assets/_Game/Data/Items/{asset}.asset");
            Assert.IsNotNull(item, asset);
            Assert.IsTrue(GiveItemRules.Matches(item, GiveItemCategory.Meal));
            Assert.IsFalse(GiveItemRules.Matches(item, GiveItemCategory.ReadyFood));
        }

        [Test]
        public void CompleteGiveChoice_DialogueClosed_IsRefused()
        {
            var go = new GameObject("DialogueGiveClosedTestGO");
            try
            {
                var inventory = go.AddComponent<InventorySystem>();
                var dialogue = go.AddComponent<Game.World.DialogueSystem>();
                typeof(Game.World.DialogueSystem)
                    .GetField("_playerInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(dialogue, inventory);
                var meal = Food(ConsumableKind.Meal, false);
                inventory.AddItem(meal);
                var choice = new ChoiceOption { text = "Here.", requiredCategory = GiveItemCategory.Meal };

                Assert.IsFalse(dialogue.CompleteGiveChoice(choice, null, meal));
                Assert.AreEqual(1, inventory.CountOf(meal), "nothing is taken while no dialogue is open");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
