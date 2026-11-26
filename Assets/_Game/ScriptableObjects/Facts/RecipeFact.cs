using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Key format: Recipe.Known.{recipeId}. Stored — set once when the player discovers the recipe
    /// (<c>RecipeSO.knownFact</c>). Never rename <c>_recipeId</c>: it is the save key.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Recipe Fact", fileName = "RecipeFact_")]
    public class RecipeFact : Fact
    {
        [SerializeField] private string _recipeId;

        public bool HasRecipeId => !string.IsNullOrWhiteSpace(_recipeId);
        /// <summary>Save key id (also keys the craft count and <see cref="CraftedFact"/>).</summary>
        public string RecipeId => _recipeId;

        public RecipeFact Init(string recipeId)
        {
            Prefix = WorldFactPrefix.Recipe;
            _recipeId = recipeId;
            return this;
        }

        private void OnEnable() => Prefix = WorldFactPrefix.Recipe;

        public override string ToString() => $"{WorldFactPrefix.Recipe}.Known.{_recipeId}";
    }
}
