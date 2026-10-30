namespace Game.Combat
{
    /// <summary>
    /// Target with a hidden stance meter (AI entities — <c>EntityStance</c>, the player — <c>PlayerStance</c>). Keeps
    /// <c>Game.Combat</c> free of AI deps, like <see cref="ISneakAttackTarget"/>. Applied after <c>TakeDamage</c> on a
    /// landed hit (player and AI attackers), or on a regular block (AI attackers).
    /// </summary>
    public interface IStanceTarget
    {
        /// <param name="canKnockDown">False = the hit can't take stance below the stagger threshold.</param>
        void ApplyStanceDamage(float amount, bool canKnockDown);

        /// <summary>A regular block absorbed the hit: reduced stance damage, never a knockdown.</summary>
        void ApplyBlockedStanceDamage(float amount);
    }
}
