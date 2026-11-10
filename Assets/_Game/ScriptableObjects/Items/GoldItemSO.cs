using UnityEngine;

namespace Game.Inventory
{
    /// <summary>One collectible coin, converted to currency by the player's inventory.</summary>
    [CreateAssetMenu(menuName = "Items/Gold", fileName = "Item_Gold")]
    public class GoldItemSO : ItemSO
    {
        public int GoldValue => 1;
    }
}
