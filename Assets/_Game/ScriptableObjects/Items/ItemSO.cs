using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Item", fileName = "Item_")]
    public class ItemSO : ScriptableObject
    {
        public string itemName;
        public string description;
        public Sprite icon;
        public int maxStacks = 1;
        public bool IsStackable => maxStacks > 1;
        public GameObject worldItemPrefab;
        public int buyValue = 1;
        public int sellValue = 1;
    }
}
