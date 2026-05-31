namespace Game.AI
{
    /// <summary>
    /// Read-only capability: "is this entity currently in combat?".
    /// Implemented by EntityBrain. Consumers (e.g. NPCPresence) POLL IsInCombat via
    /// GetComponent&lt;ICombatStateProvider&gt;() — do NOT add a C# event here:
    /// project-context.md (lines 50, 216) forbids `event Action` across system boundaries;
    /// use a GameEventSO channel if push notification is ever needed.
    /// </summary>
    public interface ICombatStateProvider
    {
        bool IsInCombat { get; }
    }
}
