namespace Game.Combat
{
    /// <summary>
    /// On the Animator GameObject: told by <see cref="SMB_BowAimState"/> when the bow aim pose is entered (arrow nocked).
    /// The player's <c>AnimationEventReceiver</c> and NPCs' <c>EntityAnimationEventReceiver</c> implement it
    /// (spec-ai-ranged-combat-style).
    /// </summary>
    public interface IBowNockListener
    {
        void NotifyBowAimEntered();
    }
}
