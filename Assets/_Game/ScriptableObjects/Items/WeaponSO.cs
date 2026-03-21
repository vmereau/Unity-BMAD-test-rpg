using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Weapon", fileName = "Weapon_")]
    public class WeaponSO : EquipableItemSO
    {
        [Header("Combat")]
        public float damageBonus;

        public override bool CanEquip() => true;
    }
}
