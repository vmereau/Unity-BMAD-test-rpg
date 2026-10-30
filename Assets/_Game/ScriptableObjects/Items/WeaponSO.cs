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

        [Header("Forging")]
        [Tooltip("Standard counterpart of a forged weapon (detail category \"Weapon · Forged\"). Null = not forged.")]
        public WeaponSO baseWeapon;

        /// <summary>True for a forged weapon (crafted at an anvil) — it names its standard counterpart.</summary>
        public bool IsForged => baseWeapon != null && baseWeapon != this;

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

        /// <summary>Per-weapon knockdown override: Archetype = use the archetype's <c>canKnockDown</c>.</summary>
        public enum KnockDownOverride { Archetype, Yes, No }

        [Header("Stance")]
        [Tooltip("Stance damage per hit. 0 = use archetype.")]
        [Min(0f)] public float stanceDamage = 0f;
        [Tooltip("Whether hits can knock down (take stance to 0). Archetype = use the archetype's flag.")]
        public KnockDownOverride knockDown = KnockDownOverride.Archetype;

        /// <summary>Weapon stanceDamage if &gt; 0, else archetype's, else 0.</summary>
        public float ResolvedStanceDamage => stanceDamage > 0f ? stanceDamage
            : archetype != null ? archetype.stanceDamage
            : 0f;

        /// <summary>Weapon override if set, else archetype's flag, else false.</summary>
        public bool ResolvedCanKnockDown => knockDown == KnockDownOverride.Yes
            || (knockDown == KnockDownOverride.Archetype && archetype != null && archetype.canKnockDown);

        /// <summary>Archetype's melee attack stamina multiplier (floored at 0.1 — [Min] is Inspector-only), else 1.</summary>
        public float ResolvedAttackStaminaMultiplier => archetype != null ? Mathf.Max(0.1f, archetype.attackStaminaMultiplier) : 1f;

        /// <summary>Archetype's full-body attack flag (player swings root the player), else false.</summary>
        public bool ResolvedFullBodyAttacks => archetype != null && archetype.fullBodyAttacks;

        /// <summary>Weapon override if set, else archetype's, else null (default controller).</summary>
        public AnimatorOverrideController ResolvedAnimatorOverride => animatorOverrideController != null
            ? animatorOverrideController
            : archetype != null ? archetype.animatorOverrideController : null;

        public override bool CanEquip() => true;
    }
}
