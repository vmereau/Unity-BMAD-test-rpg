namespace Game.AI
{
    /// <summary>
    /// Pure rules for NPC sleep (spec-npc-routine-sleep): when a sleeper at its bed lies down, and when the player counts
    /// as quiet for a woken one. Tested by <c>NpcSleepRulesTests</c>.
    /// </summary>
    public static class NpcSleepRules
    {
        /// <summary>
        /// Lie down now? A sleeper that was never woken lies down on arrival; a woken one only once the player has been
        /// quiet for <paramref name="resumeDelay"/> seconds.
        /// </summary>
        public static bool ShouldLieDown(bool waitingForQuiet, float quietTime, float resumeDelay) =>
            !waitingForQuiet || quietTime >= resumeDelay;

        /// <summary>
        /// The player leaves a woken sleeper alone: absent (none / dead) or farther (flat) than its walk hearing radius.
        /// </summary>
        public static bool IsQuiet(bool playerAbsent, float flatDistance, float walkHearingRadius) =>
            playerAbsent || flatDistance > walkHearingRadius;
    }
}
