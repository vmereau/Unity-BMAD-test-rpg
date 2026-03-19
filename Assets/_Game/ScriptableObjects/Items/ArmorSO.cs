using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Armor", fileName = "Armor_")]
    public class ArmorSO : ItemSO
    {
        /// <summary>Which equipment slot this armor occupies. Ring items authored as Ring1; EquipmentSystem handles overflow to Ring2.</summary>
        public EquipmentSlot slot;
    }
}
