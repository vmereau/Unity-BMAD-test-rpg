using Game.Crafting;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure cooking rules (recipe lookup + ingredient slot state).
    /// </summary>
    public class CookingRulesTests
    {
        private ItemSO _plant;
        private ItemSO _potion;
        private ItemSO _lockpick;
        private RecipeSO _recipe;

        [SetUp]
        public void SetUp()
        {
            _plant = ScriptableObject.CreateInstance<ItemSO>();
            _potion = ScriptableObject.CreateInstance<ItemSO>();
            _lockpick = ScriptableObject.CreateInstance<ItemSO>();
            _recipe = ScriptableObject.CreateInstance<RecipeSO>();
            _recipe.input = _plant;
            _recipe.output = _potion;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_plant);
            Object.DestroyImmediate(_potion);
            Object.DestroyImmediate(_lockpick);
            Object.DestroyImmediate(_recipe);
        }

        [Test]
        public void FindRecipe_MatchingInput_ReturnsRecipe()
        {
            Assert.AreSame(_recipe, CookingRules.FindRecipe(new[] { _recipe }, _plant));
        }

        [Test]
        public void FindRecipe_NoMatch_ReturnsNull()
        {
            Assert.IsNull(CookingRules.FindRecipe(new[] { _recipe }, _lockpick));
        }

        [Test]
        public void FindRecipe_SkipsNullAndIncompleteRecipes()
        {
            var incomplete = ScriptableObject.CreateInstance<RecipeSO>();
            incomplete.input = _plant; // no output
            try { Assert.AreSame(_recipe, CookingRules.FindRecipe(new[] { null, incomplete, _recipe }, _plant)); }
            finally { Object.DestroyImmediate(incomplete); }
        }

        [Test]
        public void FindRecipe_NullOrEmptyList_ReturnsNull()
        {
            Assert.IsNull(CookingRules.FindRecipe(null, _plant));
            Assert.IsNull(CookingRules.FindRecipe(new RecipeSO[0], _plant));
        }

        [Test]
        public void Evaluate_Ready_WhenRecipeAndCount()
        {
            Assert.AreEqual(CookingState.Ready, CookingRules.Evaluate(new[] { _recipe }, _plant, 3, out RecipeSO recipe));
            Assert.AreSame(_recipe, recipe);
        }

        [Test]
        public void Evaluate_NoRecipe_WhenNoMatch()
        {
            Assert.AreEqual(CookingState.NoRecipe, CookingRules.Evaluate(new[] { _recipe }, _lockpick, 1, out RecipeSO recipe));
            Assert.IsNull(recipe);
        }

        [Test]
        public void Evaluate_Empty_WhenNoIngredientOrNoneLeft()
        {
            Assert.AreEqual(CookingState.Empty, CookingRules.Evaluate(new[] { _recipe }, null, 0, out _));
            Assert.AreEqual(CookingState.Empty, CookingRules.Evaluate(new[] { _recipe }, _plant, 0, out _));
        }

        [Test]
        public void Evaluate_NoRecipesOnStation_IsNoRecipe()
        {
            Assert.AreEqual(CookingState.NoRecipe, CookingRules.Evaluate(null, _plant, 2, out _));
        }
    }
}
