namespace Game.Combat
{
    /// <summary>
    /// Optional companion of <see cref="IDamageable"/> on a knockout victim: true once the get-up clip has started, while
    /// <see cref="IDamageable.IsUnconscious"/> is still true. Lets a robber tell "still lying" from "getting up"
    /// (spec-knockout-gold-looting).
    /// </summary>
    public interface IKnockoutRecovery
    {
        bool IsGettingUp { get; }
    }
}
