using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Contract for any GameObject that can receive damage from AI attacks
    /// or player weapons. Implemented by EntityHealth (AI) and PlayerHealth (Player).
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }

        /// <summary>
        /// Knocked out by a non-lethal blow (spec-unconscious-knockout): lying on the ground until it gets back up.
        /// Not dead — stance and humanoid hits are ignored meanwhile, lethal hits still kill.
        /// </summary>
        bool IsUnconscious { get; }

        /// <summary>Damage from an unknown source — always lethal at 0 HP.</summary>
        void TakeDamage(float amount);

        /// <summary>
        /// Damage from <paramref name="attacker"/>. The victim decides whether a blow reaching 0 HP kills or knocks out
        /// (<see cref="KnockoutRules"/>). Null attacker = <see cref="TakeDamage(float)"/>.
        /// </summary>
        void TakeDamage(float amount, GameObject attacker);

        HitResult TryReceiveHit(GameObject attacker);
    }
}
