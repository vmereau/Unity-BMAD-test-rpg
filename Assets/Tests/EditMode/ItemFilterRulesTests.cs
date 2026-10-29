using System.Collections.Generic;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    public class ItemFilterRulesTests
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
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        private PotionItemSO CreatePotion(ConsumableKind kind)
        {
            var potion = Create<PotionItemSO>();
            var so = new SerializedObject(potion);
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            return potion;
        }

        [Test]
        public void Classify_Null_IsMisc() => Assert.AreEqual(ItemFilter.Misc, ItemFilterRules.Classify(null));

        [Test]
        public void Classify_PlainItem_IsMisc() => Assert.AreEqual(ItemFilter.Misc, ItemFilterRules.Classify(Create<ItemSO>()));

        [Test]
        public void Classify_Weapon_IsWeapons() => Assert.AreEqual(ItemFilter.Weapons, ItemFilterRules.Classify(Create<SwordSO>()));

        [Test]
        public void Classify_ForgedWeapon_IsWeapons()
        {
            var forged = Create<SwordSO>();
            forged.baseWeapon = Create<SwordSO>();
            Assert.AreEqual(ItemFilter.Weapons, ItemFilterRules.Classify(forged));
        }

        [Test]
        public void Classify_Armor_IsArmor() => Assert.AreEqual(ItemFilter.Armor, ItemFilterRules.Classify(Create<ArmorSO>()));

        [Test]
        public void Classify_Potion_IsPotions() => Assert.AreEqual(ItemFilter.Potions, ItemFilterRules.Classify(CreatePotion(ConsumableKind.Potion)));

        [Test]
        public void Classify_Meal_IsMeals() => Assert.AreEqual(ItemFilter.Meals, ItemFilterRules.Classify(CreatePotion(ConsumableKind.Meal)));

        [Test]
        public void Classify_RawFoodIngredient_IngredientFlagWinsOverPotion()
        {
            var rawFood = CreatePotion(ConsumableKind.Potion);
            rawFood.isIngredient = true;
            Assert.AreEqual(ItemFilter.Ingredients, ItemFilterRules.Classify(rawFood));
        }

        [Test]
        public void Classify_CraftingMaterial_IsCraftingMaterials()
        {
            var ingot = Create<ItemSO>();
            ingot.isCraftingMaterial = true;
            Assert.AreEqual(ItemFilter.CraftingMaterials, ItemFilterRules.Classify(ingot));
        }

        [Test]
        public void Classify_IngredientFlag_WinsOverCraftingFlag()
        {
            var item = Create<ItemSO>();
            item.isIngredient = true;
            item.isCraftingMaterial = true;
            Assert.AreEqual(ItemFilter.Ingredients, ItemFilterRules.Classify(item));
        }

        [Test]
        public void Classify_TomeAndRecipe_AreBooksAndRecipes()
        {
            Assert.AreEqual(ItemFilter.BooksAndRecipes, ItemFilterRules.Classify(Create<SkillItemSO>()));
            Assert.AreEqual(ItemFilter.BooksAndRecipes, ItemFilterRules.Classify(Create<RecipeItemSO>()));
        }

        [Test]
        public void Matches_All_MatchesEveryItem()
        {
            Assert.IsTrue(ItemFilterRules.Matches(Create<SwordSO>(), ItemFilter.All));
            Assert.IsTrue(ItemFilterRules.Matches(Create<ItemSO>(), ItemFilter.All));
            Assert.IsTrue(ItemFilterRules.Matches(null, ItemFilter.All));
        }

        [Test]
        public void Matches_OtherCategory_IsFalse() => Assert.IsFalse(ItemFilterRules.Matches(Create<SwordSO>(), ItemFilter.Armor));

        [Test]
        public void TooltipText_FormatsLabelAndCount()
        {
            Assert.AreEqual("Weapons (3)", ItemFilterRules.TooltipText(ItemFilter.Weapons, 3));
            Assert.AreEqual("Books & Recipes (0)", ItemFilterRules.TooltipText(ItemFilter.BooksAndRecipes, 0));
        }
    }
}
