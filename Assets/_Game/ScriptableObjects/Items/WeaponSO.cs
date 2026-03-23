using UnityEngine;

namespace Game.Inventory
{
    public abstract class WeaponSO : EquipableItemSO
    {
        [Header("Combat")]
        public float damageBonus;

        [Header("Combo")]
        [Tooltip("Total number of attacks in the combo chain (e.g. 2 = Attack_1 → finisher).")]
        public int comboSteps = 2;

        [Header("Animation")]
        public AnimatorOverrideController animatorOverrideController; // Optional. Applied to player Animator on equip. Null = keep default controller.

        public override bool CanEquip() => true;
    }
}
