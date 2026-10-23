using System.Collections.Generic;
using Game.Inventory;

namespace Game.Crafting
{
    /// <summary>What the cooking screen shows for the current ingredient slot.</summary>
    public enum CookingState { Empty, NoRecipe, Ready }

    /// <summary>
    /// Static, pure cooking rules: recipe lookup and slot state. No Unity lifecycle — tested by <c>CookingRulesTests</c>.
    /// </summary>
    public static class CookingRules
    {
        /// <summary>First complete recipe whose input is <paramref name="ingredient"/>; null entries and incomplete recipes are skipped.</summary>
        public static RecipeSO FindRecipe(IReadOnlyList<RecipeSO> recipes, ItemSO ingredient)
        {
            if (recipes == null || ingredient == null) return null;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeSO recipe = recipes[i];
                if (recipe != null && recipe.IsComplete && recipe.input == ingredient)
                    return recipe;
            }
            return null;
        }

        /// <summary>
        /// Slot state: <see cref="CookingState.Empty"/> when no ingredient or none left in the inventory,
        /// <see cref="CookingState.NoRecipe"/> when nothing matches, otherwise <see cref="CookingState.Ready"/>.
        /// </summary>
        public static CookingState Evaluate(IReadOnlyList<RecipeSO> recipes, ItemSO ingredient, int ingredientCount, out RecipeSO recipe)
        {
            recipe = null;
            if (ingredient == null || ingredientCount <= 0) return CookingState.Empty;
            recipe = FindRecipe(recipes, ingredient);
            return recipe != null ? CookingState.Ready : CookingState.NoRecipe;
        }
    }
}
