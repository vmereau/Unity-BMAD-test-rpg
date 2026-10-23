namespace Game.Core
{
    /// <summary>
    /// Canonical prefixes for world fact keys. Use with WorldStateManager's typed setter methods
    /// (RegisterKill, SetWorldEvent, SetDialoguePlayed, SetRecipeKnown) — never construct key strings manually at call sites.
    /// ToString() of each value yields the exact prefix segment used in the key format.
    /// </summary>
    public enum WorldFactPrefix
    {
        Killed,
        Quest,
        World,
        Dialogue,
        Recipe
    }
}
