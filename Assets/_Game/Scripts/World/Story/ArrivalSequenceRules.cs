namespace Game.World
{
    /// <summary>Escort decision for the current lane point (spec-area-1-arrival-escort-beating).</summary>
    public enum EscortStep { Walking, WaitForPlayer, Advance, ReachedEnd }

    /// <summary>What the escort does about a player wandering off.</summary>
    public enum StrayAction { None, Warn, Brawl, Clear }

    /// <summary>State of the beating at the stage.</summary>
    public enum BeatingOutcome { Ongoing, VictimDown, Interrupted }

    /// <summary>What the hut briefing does this frame (spec-area-1-arrival-hut-briefing).</summary>
    public enum BriefingStep { Wait, Open, Skip }

    /// <summary>Pure decisions behind <see cref="ArrivalSequence"/> (tested by <c>ArrivalSequenceRulesTests</c>).</summary>
    public static class ArrivalSequenceRules
    {
        /// <summary>
        /// Guards still walking → Walking. Arrived but the player is beyond <paramref name="followDistance"/> → WaitForPlayer.
        /// Arrived with the player close → Advance, or ReachedEnd on the last lane point (the stage, or the hut).
        /// </summary>
        public static EscortStep EvaluateEscort(bool guardsArrived, float playerDistance, float followDistance, bool isLastPoint)
        {
            if (!guardsArrived) return EscortStep.Walking;
            if (playerDistance > followDistance) return EscortStep.WaitForPlayer;
            return isLastPoint ? EscortStep.ReachedEnd : EscortStep.Advance;
        }

        /// <summary>
        /// Player within <paramref name="strayDistance"/> → Clear when a warning was given (else None). Beyond it: first a
        /// warning, then a brawl once they stayed away longer than <paramref name="graceSeconds"/> after it.
        /// A brawl already running → None.
        /// </summary>
        public static StrayAction EvaluateStray(float playerDistance, float strayDistance, bool warned,
            float secondsSinceWarning, float graceSeconds, bool brawling)
        {
            if (brawling) return StrayAction.None;
            if (playerDistance <= strayDistance) return warned ? StrayAction.Clear : StrayAction.None;
            if (!warned) return StrayAction.Warn;
            return secondsSinceWarning > graceSeconds ? StrayAction.Brawl : StrayAction.None;
        }

        /// <summary>
        /// Victim unconscious → VictimDown (checked first: a simultaneous timeout still counts the beating). Beater down /
        /// dead, or <paramref name="elapsed"/> past <paramref name="timeout"/> → Interrupted. Otherwise Ongoing.
        /// </summary>
        public static BeatingOutcome EvaluateBeating(bool victimDown, bool beaterDown, float elapsed, float timeout)
        {
            if (victimDown) return BeatingOutcome.VictimDown;
            if (beaterDown || elapsed > timeout) return BeatingOutcome.Interrupted;
            return BeatingOutcome.Ongoing;
        }

        /// <summary>
        /// Speaker down (or no briefing) → Skip. Waited past <paramref name="timeout"/> (time counted only while it could
        /// open) → Skip. Player within range and nothing blocking (<paramref name="canOpen"/>: calm, no menu, speaker not
        /// fighting) → Open. Otherwise Wait.
        /// </summary>
        public static BriefingStep EvaluateBriefing(bool speakerDown, bool playerInRange, bool canOpen, float waitTime,
            float timeout)
        {
            if (speakerDown || waitTime > timeout) return BriefingStep.Skip;
            return playerInRange && canOpen ? BriefingStep.Open : BriefingStep.Wait;
        }
    }
}
