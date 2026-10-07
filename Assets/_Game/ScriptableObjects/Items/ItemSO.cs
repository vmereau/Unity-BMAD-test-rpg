using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Item", fileName = "Item_")]
    public class ItemSO : ScriptableObject
    {
        [Tooltip("Stable save ID — auto-generated, never edit. Clear it on a duplicated asset so it regenerates.")]
        public string itemId;
        public string itemName;
        public string description;
        public Sprite icon;
        public int maxStacks = 1;
        public bool IsStackable => maxStacks > 1;
        public GameObject worldItemPrefab;
        public int buyValue = 1;
        public int sellValue = 1;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (!string.IsNullOrEmpty(itemId)) return;
            itemId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
