using System;
using System.Collections.Generic;
using Game.Inventory;

namespace Game.Crafting
{
    /// <summary>What the Experiment tab shows for its ingredient slots.</summary>
    public enum ExperimentState { Empty, NotEnough, Ready }

    /// <summary>
    /// Static, pure cooking rules: order-free recipe matching and ingredient availability. Ingredient lists may
    /// contain nulls (empty slots), which are ignored. No Unity lifecycle — tested by <c>CookingRulesTests</c>.
    /// </summary>
    public static class CookingRules
    {
        /// <summary>
        /// First complete recipe whose ingredients exactly match the filled <paramref name="slots"/> (same items,
        /// same counts, any order); null entries and incomplete recipes are skipped. Null when nothing matches.
        /// </summary>
        public static RecipeSO FindRecipe(IReadOnlyList<RecipeSO> recipes, IReadOnlyList<ItemSO> slots)
        {
            if (recipes == null || CountFilled(slots) == 0) return null;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeSO recipe = recipes[i];
                if (recipe != null && recipe.IsComplete && SameIngredients(recipe.ingredients, slots))
                    return recipe;
            }
            return null;
        }

        /// <summary>True when, for every distinct item in <paramref name="needs"/>, <paramref name="countOf"/> holds enough units.</summary>
        public static bool HasIngredients(IReadOnlyList<ItemSO> needs, Func<ItemSO, int> countOf)
        {
            if (needs == null || countOf == null) return false;
            for (int i = 0; i < needs.Count; i++)
            {
                ItemSO item = needs[i];
                if (item != null && countOf(item) < CountIn(needs, item)) return false;
            }
            return true;
        }

        /// <summary>
        /// Experiment state: <see cref="ExperimentState.Empty"/> when no slot is filled,
        /// <see cref="ExperimentState.NotEnough"/> when the slots ask for more units than owned, otherwise Ready.
        /// </summary>
        public static ExperimentState EvaluateExperiment(IReadOnlyList<ItemSO> slots, Func<ItemSO, int> countOf)
        {
            if (CountFilled(slots) == 0) return ExperimentState.Empty;
            return HasIngredients(slots, countOf) ? ExperimentState.Ready : ExperimentState.NotEnough;
        }

        /// <summary>Number of units of <paramref name="item"/> in <paramref name="list"/>.</summary>
        public static int CountIn(IReadOnlyList<ItemSO> list, ItemSO item)
        {
            if (list == null || item == null) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) n++;
            return n;
        }

        private static int CountFilled(IReadOnlyList<ItemSO> slots)
        {
            if (slots == null) return 0;
            int n = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null) n++;
            return n;
        }

        private static bool SameIngredients(IReadOnlyList<ItemSO> needs, IReadOnlyList<ItemSO> slots)
        {
            if (needs.Count != CountFilled(slots)) return false;
            for (int i = 0; i < needs.Count; i++)
                if (CountIn(needs, needs[i]) != CountIn(slots, needs[i])) return false;
            return true;
        }
    }
}
