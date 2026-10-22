namespace Game.World
{
    public enum LockpickResult
    {
        Success,
        MissingSkill,
        MissingLockpick
    }

    /// <summary>
    /// Static, pure. The single lockpicking rule shared by DoorSystem and ContainerSystem:
    /// a locked Lockable needs the required skill AND at least one lockpick. The skill is checked
    /// first, so a player missing both only hears about the skill.
    /// </summary>
    public static class LockpickRules
    {
        public const string MissingSkillMessage = "You lack the skill to pick this lock";
        public const string MissingLockpickMessage = "You need a lockpick";

        public static LockpickResult Evaluate(bool hasSkill, int lockpickCount)
        {
            if (!hasSkill) return LockpickResult.MissingSkill;
            if (lockpickCount < 1) return LockpickResult.MissingLockpick;
            return LockpickResult.Success;
        }

        /// <summary>Toast text for a failed attempt; null for Success.</summary>
        public static string FailureMessage(LockpickResult result)
        {
            switch (result)
            {
                case LockpickResult.MissingSkill: return MissingSkillMessage;
                case LockpickResult.MissingLockpick: return MissingLockpickMessage;
                default: return null;
            }
        }
    }
}
