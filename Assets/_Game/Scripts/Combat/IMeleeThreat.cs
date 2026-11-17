namespace Game.Combat
{
    /// <summary>
    /// A melee attacker whose swing a defender can read (spec-ai-npc-blocking): the player (<c>PlayerCombat</c>, melee
    /// only) and AI entities (<c>EntityMeleeAttacker</c>). Read by <c>EntityGuard</c> on the defender's current target.
    /// Keeps <c>Game.AI</c> free of player deps, like <see cref="IStanceTarget"/>.
    /// </summary>
    public interface IMeleeThreat
    {
        /// <summary>A melee swing is in progress (wind-up through recovery).</summary>
        bool IsMeleeAttacking { get; }

        /// <summary>The current swing step is committed (its hit window opened). Re-opens on every combo step.</summary>
        bool IsMeleeCommitted { get; }
    }
}
