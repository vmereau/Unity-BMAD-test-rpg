namespace Game.Combat
{
    /// <summary>
    /// Target with a hidden stance meter (AI entities — <c>EntityStance</c>). Keeps <c>Game.Combat</c> free of AI
    /// deps, like <see cref="ISneakAttackTarget"/>. Applied after <c>TakeDamage</c> on a landed hit.
    /// </summary>
    public interface IStanceTarget
    {
        /// <param name="canKnockDown">False = the hit can't take stance below the stagger threshold.</param>
        void ApplyStanceDamage(float amount, bool canKnockDown);
    }
}
