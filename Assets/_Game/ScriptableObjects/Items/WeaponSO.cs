using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Weapon", fileName = "Weapon_")]
    public class WeaponSO : EquipableItemSO
    {
        [Header("Combat")]
        public float damageBonus;

        [Header("Animation")]
        public AnimatorOverrideController animatorOverrideController; // Optional. Applied to player Animator on equip. Null = keep default controller.

        public override bool CanEquip() => true;
    }
}
