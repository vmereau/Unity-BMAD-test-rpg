using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Computed fact: true once the recipe identified by <see cref="Recipe"/> was cooked / forged at least once
    /// (<see cref="WorldStateManager.GetRecipeCraftCount"/> &gt; 0; a matching Experiment cook counts). NOT stored in
    /// <c>_worldFacts</c>; <c>AddRecipeCraft</c> raises <c>OnFactChanged</c> with <see cref="ToString"/> on the first
    /// craft (0 → 1). Prefix intentionally not set.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Crafted Fact", fileName = "CraftedFact_")]
    public class CraftedFact : Fact
    {
        [Tooltip("Known-recipe fact of the recipe (RecipeSO.knownFact): its craft count keys this fact.")]
        [SerializeField] private RecipeFact _recipe;

        public RecipeFact Recipe => _recipe;

        /// <summary>Runtime/test initialiser.</summary>
        public CraftedFact Init(RecipeFact recipe)
        {
            _recipe = recipe;
            return this;
        }

        /// <summary>Change-event key for a recipe id (debug only, not a stored key).</summary>
        public static string KeyFor(string recipeId) => $"{WorldFactPrefix.Crafted}.{recipeId}";

        /// <summary>Why this fact can never become true, or null when it is fine (shared with Quest Explorer V19).</summary>
        public static string Problem(RecipeFact recipe)
        {
            if (recipe == null) return "has no recipe fact — always false";
            if (!recipe.HasRecipeId) return $"recipe fact '{recipe.name}' has no recipe id — always false";
            return null;
        }

        /// <summary>For debugging / change events only — not a stored key.</summary>
        public override string ToString() => KeyFor(_recipe != null ? _recipe.RecipeId : "null");

#if UNITY_EDITOR
        private void OnValidate()
        {
            string problem = Problem(_recipe);
            if (problem != null) GameLog.Warn("[CraftedFact]", $"{name} {problem}");
        }
#endif
    }
}
