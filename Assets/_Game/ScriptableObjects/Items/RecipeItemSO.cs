using Game.Core;
using Game.Crafting;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// A recipe the player reads to learn a cooking recipe: sets <see cref="RecipeSO.knownFact"/> on use.
    /// Rejected (not consumed) when the recipe is already known — it is then only worth its sell value.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Recipe Item", fileName = "Item_Recipe_")]
    public class RecipeItemSO : UsableItemSO
    {
        private const string TAG = "[RecipeItemSO]";

        [SerializeField] private RecipeSO _recipe;
        [Tooltip("Raised with the recipe output name when the recipe is learned (toast).")]
        [SerializeField] private GameEventSO_String _onRecipeLearned;

        public RecipeSO Recipe => _recipe;

        /// <summary>
        /// True when the recipe is complete (output + ingredients) and its known fact has a recipe id — otherwise the
        /// learned recipe could never be listed, or would share the empty key with every other such recipe.
        /// </summary>
        public bool IsConfigured =>
            _recipe != null && _recipe.IsComplete && _recipe.knownFact != null && _recipe.knownFact.HasRecipeId;

        public override bool CanUse() => IsConfigured && WorldStateManager.Instance != null && !IsKnown();

        public override bool OnUse(GameObject user)
        {
            if (!IsConfigured)
            {
                GameLog.Warn(TAG, $"OnUse: {name} needs a complete recipe whose known fact has a recipe id");
                return false;
            }
            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, "OnUse: no WorldStateManager (Core not loaded?) — recipe can't be learned");
                return false;
            }
            if (IsKnown())
            {
                GameLog.Info(TAG, $"OnUse: {_recipe.output.itemName} is already known — {name} not used");
                return false;
            }

            WorldStateManager.Instance.SetRecipeKnown(_recipe.knownFact);
            _onRecipeLearned?.Raise(_recipe.output.itemName);
            GameLog.Info(TAG, $"Learned recipe {_recipe.name} from {name}");
            return true;
        }

        private bool IsKnown() => WorldStateManager.Instance.IsRecipeKnown(_recipe.knownFact);
    }
}
