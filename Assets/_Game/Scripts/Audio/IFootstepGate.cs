namespace Game.Audio
{
    /// <summary>
    /// Optional component next to a <see cref="FootstepEmitter"/>: true while the body moves without walking
    /// (dodge roll, attack lunge, stagger, death) so no steps are emitted. Implemented by <c>PlayerStateManager</c>.
    /// </summary>
    public interface IFootstepGate
    {
        bool SuppressFootsteps { get; }
    }
}
