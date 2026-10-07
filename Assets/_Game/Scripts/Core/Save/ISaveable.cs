namespace Game.Core
{
    /// <summary>A world object whose state is captured into / restored from a save.</summary>
    public interface ISaveable
    {
        /// <summary>Stable key across sessions. Empty = not saveable (misconfigured).</summary>
        string SaveKey { get; }
        ObjectSaveData Capture();
        /// <summary>Writes state directly — must never raise gameplay events (XP, kills, rewards).</summary>
        void Restore(ObjectSaveData data);
    }
}
