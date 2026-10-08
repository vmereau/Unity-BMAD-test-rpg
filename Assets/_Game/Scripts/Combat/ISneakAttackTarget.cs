using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Implemented by <c>EntityBrain</c>, resolved via <c>TryGetComponent</c> on the hit target.
    /// <c>PlayerCombat</c> polls <see cref="IsUnawareOf"/> before applying damage and multiplies the hit by
    /// <c>CombatConfigSO.sneakAttackDamageMultiplier</c> when the target is unaware.
    /// Every damage source (<c>PlayerCombat</c>, <c>EntityMeleeAttacker</c>) calls <see cref="NotifyHitBy"/>
    /// right before <c>TakeDamage</c>, so the target's damage reaction knows who hit it.
    /// </summary>
    public interface ISneakAttackTarget
    {
        bool IsUnawareOf(GameObject attacker);

        /// <summary>Records the attacker of the hit about to land (consumed by the same frame's damage reaction).</summary>
        void NotifyHitBy(GameObject attacker);
    }
}
