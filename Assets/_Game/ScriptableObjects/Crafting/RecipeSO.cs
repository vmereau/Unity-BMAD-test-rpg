using Game.Inventory;
using UnityEngine;

namespace Game.Crafting
{
    /// <summary>
    /// A one-ingredient recipe: one <see cref="input"/> item becomes one <see cref="output"/> item.
    /// Stations list the recipes they know (<see cref="CookingStation"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Crafting/Recipe", fileName = "Recipe_")]
    public class RecipeSO : ScriptableObject
    {
        [Tooltip("Ingredient consumed by one craft.")]
        public ItemSO input;
        [Tooltip("Item produced by one craft.")]
        public ItemSO output;

        public bool IsComplete => input != null && output != null;
    }
}
