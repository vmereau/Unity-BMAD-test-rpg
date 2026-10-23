using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.Crafting
{
    /// <summary>
    /// A cooking recipe: 1–<see cref="MAX_INGREDIENTS"/> <see cref="ingredients"/> (order-free, duplicates allowed)
    /// become one <see cref="output"/> item. Stations list the recipes they can cook (<see cref="CookingStation"/>);
    /// the player knows a recipe once its <see cref="knownFact"/> is set (first discovery in the Experiment tab).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Crafting/Recipe", fileName = "Recipe_")]
    public class RecipeSO : ScriptableObject
    {
        public const int MAX_INGREDIENTS = 3;

        [Tooltip("Ingredients consumed by one craft (1–3, order-free; repeat an item to require several).")]
        public ItemSO[] ingredients;
        [Tooltip("Item produced by one craft.")]
        public ItemSO output;
        [Tooltip("Stored fact set when the player discovers this recipe. Null = never learned or listed.")]
        public RecipeFact knownFact;

        public bool IsComplete
        {
            get
            {
                if (output == null || ingredients == null) return false;
                if (ingredients.Length < 1 || ingredients.Length > MAX_INGREDIENTS) return false;
                foreach (ItemSO item in ingredients)
                    if (item == null) return false;
                return true;
            }
        }
    }
}
