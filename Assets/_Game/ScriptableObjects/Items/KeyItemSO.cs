using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// A key: opens the <c>Lockable</c> whose <c>_keyItem</c> it is. The player never parts with it (no drop, sale,
    /// container or action bar: <see cref="ItemSO.CanPlayerPart"/>) and it is worth nothing. Copies of one key stack.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Key", fileName = "Key_")]
    public class KeyItemSO : ItemSO
    {
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (buyValue != 0 || sellValue != 0)
            {
                buyValue = 0;
                sellValue = 0;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            base.OnValidate();
        }
#endif
    }
}
