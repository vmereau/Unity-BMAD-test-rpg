namespace Game.Stealth
{
    /// <summary>
    /// How loud a stealth target's movement is, for AI hearing (<c>EntityPerception</c>). Classified from the
    /// target's actual speed by <see cref="StealthDetection.ClassifyNoise"/>.
    /// </summary>
    public enum MovementNoise
    {
        /// <summary>Standing still, barely moving or sneaking — never heard.</summary>
        Silent,
        /// <summary>Walking (or swimming) — heard within an entity's walk hearing radius.</summary>
        Walk,
        /// <summary>Running (or swim-sprinting) — heard within an entity's run hearing radius.</summary>
        Run
    }
}
