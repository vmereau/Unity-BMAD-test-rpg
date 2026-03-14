using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Item", fileName = "Item_")]
    public class ItemSO : ScriptableObject
    {
        public string itemName;
        public string description;
        public Sprite icon;
        public bool isStackable;
    }
}
