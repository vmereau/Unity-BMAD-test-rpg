using UnityEngine;

namespace Game.Inventory
{
    public abstract class UsableItemSO : ItemSO
    {
        [Tooltip("If true, the item is removed from inventory after use.")]
        public bool consumable;

        /// <summary>Called when the player uses this item from the inventory context menu.</summary>
        /// <param name="user">The player GameObject — use GetComponent to access player systems.</param>
        /// <returns>True if the item was successfully used; false if the use was rejected (e.g. insufficient LP, already learned).</returns>
        public abstract bool OnUse(GameObject user);
    }
}
