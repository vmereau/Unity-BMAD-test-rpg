using System.Collections.Generic;
using Game.Crafting;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure cooking rules (order-free recipe matching + ingredient availability).
    /// </summary>
    public class CookingRulesTests
    {
        private ItemSO _apple;
        private ItemSO _bread;
        private ItemSO _mushroom;
        private ItemSO _sandwich;
        private RecipeSO _sandwichRecipe;
        private readonly Dictionary<ItemSO, int> _owned = new Dictionary<ItemSO, int>();

        private int CountOf(ItemSO item) => item != null && _owned.TryGetValue(item, out int n) ? n : 0;

        [SetUp]
        public void SetUp()
        {
            _apple = ScriptableObject.CreateInstance<ItemSO>();
            _bread = ScriptableObject.CreateInstance<ItemSO>();
            _mushroom = ScriptableObject.CreateInstance<ItemSO>();
            _sandwich = ScriptableObject.CreateInstance<ItemSO>();
            _sandwichRecipe = MakeRecipe(_sandwich, _apple, _bread, _mushroom);
            _owned.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_apple);
            Object.DestroyImmediate(_bread);
            Object.DestroyImmediate(_mushroom);
            Object.DestroyImmediate(_sandwich);
            Object.DestroyImmediate(_sandwichRecipe);
        }

        private static RecipeSO MakeRecipe(ItemSO output, params ItemSO[] ingredients)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeSO>();
            recipe.output = output;
            recipe.ingredients = ingredients;
            return recipe;
        }

        [Test]
        public void FindRecipe_ExactMatchAnyOrder_ReturnsRecipe()
        {
            var recipes = new[] { _sandwichRecipe };
            Assert.AreSame(_sandwichRecipe, CookingRules.FindRecipe(recipes, new[] { _mushroom, _apple, _bread }));
            Assert.AreSame(_sandwichRecipe, CookingRules.FindRecipe(recipes, new[] { _bread, null, _apple, _mushroom }));
        }

        [Test]
        public void FindRecipe_PartialOrExtra_ReturnsNull()
        {
            var recipes = new[] { _sandwichRecipe };
            Assert.IsNull(CookingRules.FindRecipe(recipes, new[] { _apple, _bread, null }));
            Assert.IsNull(CookingRules.FindRecipe(recipes, new[] { _apple, _bread, _mushroom, _apple }));
        }

        [Test]
        public void FindRecipe_DuplicatesMustMatchCounts()
        {
            var twoApples = MakeRecipe(_sandwich, _apple, _apple);
            try
            {
                var recipes = new[] { twoApples };
                Assert.AreSame(twoApples, CookingRules.FindRecipe(recipes, new[] { _apple, null, _apple }));
                Assert.IsNull(CookingRules.FindRecipe(recipes, new[] { _apple }));
                Assert.IsNull(CookingRules.FindRecipe(recipes, new[] { _apple, _bread }));
            }
            finally { Object.DestroyImmediate(twoApples); }
        }

        [Test]
        public void FindRecipe_SkipsNullAndIncompleteRecipes()
        {
            var noOutput = MakeRecipe(null, _apple);
            var tooMany = MakeRecipe(_sandwich, _apple, _apple, _apple, _apple);
            var nullIngredient = MakeRecipe(_sandwich, _apple, null);
            var apple = MakeRecipe(_sandwich, _apple);
            try
            {
                Assert.AreSame(apple, CookingRules.FindRecipe(new[] { null, noOutput, tooMany, nullIngredient, apple }, new[] { _apple }));
                Assert.IsNull(CookingRules.FindRecipe(new[] { tooMany }, new[] { _apple, _apple, _apple, _apple }));
            }
            finally
            {
                Object.DestroyImmediate(noOutput);
                Object.DestroyImmediate(tooMany);
                Object.DestroyImmediate(nullIngredient);
                Object.DestroyImmediate(apple);
            }
        }

        [Test]
        public void FindRecipe_NullListOrEmptySlots_ReturnsNull()
        {
            Assert.IsNull(CookingRules.FindRecipe(null, new[] { _apple }));
            Assert.IsNull(CookingRules.FindRecipe(new[] { _sandwichRecipe }, new ItemSO[] { null, null, null }));
            Assert.IsNull(CookingRules.FindRecipe(new[] { _sandwichRecipe }, null));
        }

        [Test]
        public void HasIngredients_CountsDuplicates()
        {
            _owned[_apple] = 1;
            Assert.IsTrue(CookingRules.HasIngredients(new[] { _apple }, CountOf));
            Assert.IsFalse(CookingRules.HasIngredients(new[] { _apple, _apple }, CountOf));
            _owned[_apple] = 2;
            Assert.IsTrue(CookingRules.HasIngredients(new[] { _apple, null, _apple }, CountOf));
        }

        [Test]
        public void HasIngredients_MissingItem_IsFalse()
        {
            _owned[_apple] = 1;
            _owned[_bread] = 1;
            Assert.IsFalse(CookingRules.HasIngredients(_sandwichRecipe.ingredients, CountOf));
            _owned[_mushroom] = 1;
            Assert.IsTrue(CookingRules.HasIngredients(_sandwichRecipe.ingredients, CountOf));
        }

        [Test]
        public void EvaluateExperiment_States()
        {
            _owned[_apple] = 1;
            Assert.AreEqual(ExperimentState.Empty, CookingRules.EvaluateExperiment(new ItemSO[] { null, null, null }, CountOf));
            Assert.AreEqual(ExperimentState.Ready, CookingRules.EvaluateExperiment(new[] { _apple, null, null }, CountOf));
            Assert.AreEqual(ExperimentState.NotEnough, CookingRules.EvaluateExperiment(new[] { _apple, _apple, null }, CountOf));
            Assert.AreEqual(ExperimentState.NotEnough, CookingRules.EvaluateExperiment(new[] { _bread, null, null }, CountOf));
        }

        [Test]
        public void IsMaterialFor_CookingTakesIngredients_SmithingTakesCraftingMaterials()
        {
            _apple.isIngredient = true;
            var ingot = ScriptableObject.CreateInstance<ItemSO>();
            ingot.isCraftingMaterial = true;
            try
            {
                Assert.IsTrue(CookingRules.IsMaterialFor(CraftingKind.Cooking, _apple));
                Assert.IsFalse(CookingRules.IsMaterialFor(CraftingKind.Smithing, _apple));
                Assert.IsTrue(CookingRules.IsMaterialFor(CraftingKind.Smithing, ingot));
                Assert.IsFalse(CookingRules.IsMaterialFor(CraftingKind.Cooking, ingot));
                Assert.IsFalse(CookingRules.IsMaterialFor(CraftingKind.Cooking, _bread)); // plain item
                Assert.IsFalse(CookingRules.IsMaterialFor(CraftingKind.Smithing, null));
            }
            finally
            {
                Object.DestroyImmediate(ingot);
            }
        }
    }
}
