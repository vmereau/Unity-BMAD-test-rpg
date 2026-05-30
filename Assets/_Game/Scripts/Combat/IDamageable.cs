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
        void TakeDamage(float amount);
        HitResult TryReceiveHit(GameObject attacker);
    }
}
