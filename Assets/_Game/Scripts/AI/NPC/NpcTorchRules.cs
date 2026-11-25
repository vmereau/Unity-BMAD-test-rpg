using Game.Core;

namespace Game.AI
{
    /// <summary>
    /// Pure night-torch rule for <see cref="NpcRoutine"/> (spec-npc-night-torch). Tested by <c>NpcNightTorchTests</c>.
    /// </summary>
    public static class NpcTorchRules
    {
        /// <summary>Hours used when no <see cref="DayNightConfigSO"/> is wired (same as the shipped config).</summary>
        public const float DEFAULT_SUNRISE = 6f;
        public const float DEFAULT_SUNSET = 22f;

        /// <summary>
        /// The NPC wants its torch: the routine flag is on, it is night (outside sunrise → sunset, wrapping midnight) and it
        /// is not asleep. An invalid day (sunrise not before sunset) → never.
        /// </summary>
        public static bool WantsTorch(bool torchAtNight, float hour, float sunrise, float sunset, bool asleep) =>
            torchAtNight && !asleep && DayNightMath.IsValidDay(sunrise, sunset) && !DayNightMath.IsDay(hour, sunrise, sunset);
    }
}
