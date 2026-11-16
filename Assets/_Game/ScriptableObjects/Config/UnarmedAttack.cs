using System;
using Game.Inventory;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// One step of the player's unarmed combo (spec-unarmed-combo-rework): forward step, which hand's hitbox opens,
    /// and the health / stance damage multipliers of that step. Element 0 of
    /// <see cref="CombatConfigSO.unarmedAttacks"/> = Attack_1.
    /// </summary>
    [Serializable]
    public struct UnarmedAttack
    {
        [Tooltip("Forward step of this attack state (distance in metres, start / duration in seconds since the state entered).")]
        public AttackStep step;
        [Tooltip("Hand whose hitbox opens during this step's hit window. The other hand never deals damage.")]
        public WeaponHand hand;
        [Tooltip("Health damage multiplier of this step (finisher > 1).")]
        [Min(0f)] public float damageMultiplier;
        [Tooltip("Stance damage multiplier of this step (finisher > 1). Replaces the global finisherStanceMultiplier for unarmed.")]
        [Min(0f)] public float stanceMultiplier;

        public UnarmedAttack(AttackStep step, WeaponHand hand, float damageMultiplier = 1f, float stanceMultiplier = 1f)
        {
            this.step = step;
            this.hand = hand;
            this.damageMultiplier = damageMultiplier;
            this.stanceMultiplier = stanceMultiplier;
        }

        /// <summary>
        /// Data for attack state <paramref name="attackIndex"/> (1 = Attack_1): <paramref name="attacks"/>[index − 1]
        /// when present, else <paramref name="fallback"/>.
        /// </summary>
        public static UnarmedAttack Resolve(UnarmedAttack[] attacks, int attackIndex, UnarmedAttack fallback) =>
            attacks != null && attackIndex >= 1 && attackIndex <= attacks.Length ? attacks[attackIndex - 1] : fallback;

        /// <summary>Right-hand, ×1 step used when the config lists no entry for an attack state.</summary>
        public static UnarmedAttack Fallback(AttackStep step) => new UnarmedAttack(step, WeaponHand.Right);
    }
}
