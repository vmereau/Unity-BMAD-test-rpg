using UnityEngine;

namespace Game.Inventory
{
    public abstract class WeaponSO : EquipableItemSO
    {
        public const int DEFAULT_COMBO_STEPS = 2;

        [Header("Archetype")]
        [Tooltip("Shared grip/sheath/animation preset for the weapon family.")]
        public WeaponArchetypeSO archetype;

        [Header("Combat")]
        public float damageBonus;

        [Header("Combo")]
        [Tooltip("Total number of attacks in the combo chain (e.g. 2 = Attack_1 → finisher). 0 = use archetype default")]
        [Min(0)] public int comboSteps = 0;

        [Header("Animation")]
        [Tooltip("Optional per-weapon override. Null = use archetype.")]
        public AnimatorOverrideController animatorOverrideController; // Applied to player Animator on equip.

        /// <summary>Weapon comboSteps if &gt; 0, else archetype default (min 1), else DEFAULT_COMBO_STEPS.</summary>
        public int ResolvedComboSteps => comboSteps > 0 ? comboSteps
            : archetype != null ? Mathf.Max(1, archetype.defaultComboSteps)
            : DEFAULT_COMBO_STEPS;

        /// <summary>Weapon override if set, else archetype's, else null (default controller).</summary>
        public AnimatorOverrideController ResolvedAnimatorOverride => animatorOverrideController != null
            ? animatorOverrideController
            : archetype != null ? archetype.animatorOverrideController : null;

        public override bool CanEquip() => true;
    }
}
