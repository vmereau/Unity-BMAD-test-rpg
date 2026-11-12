using Game.Core;

namespace Game.AI
{
    /// <summary>How the player answered the toll (from the last dialogue signal; closing without one = refused).</summary>
    public enum TollOutcome { Paid, Deferred, Refused }

    /// <summary>State of a toll brawl, checked every frame by the gate.</summary>
    public enum TollBrawlResult { Ongoing, PlayerLost, CollectorLost, Ended }

    /// <summary>
    /// <b>Static, pure.</b> Rules behind <see cref="TollGate"/>. Tested by <c>TollGateRulesTests</c>.
    /// </summary>
    public static class TollGateRules
    {
        /// <summary>Gate hours: inside the window (start incl. / end excl., wraps midnight); always without a clock.</summary>
        public static bool IsCollectingHour(bool hasClock, int hour, int startHour, int endHour) =>
            !hasClock || GameTime.IsHourInWindow(startHour, endHour, hour);

        /// <summary>
        /// The gate stops the player: in hours, this gate unpaid, the collector never beaten, re-armed since the last
        /// encounter (the player left the zone) and the collector on duty at the post.
        /// </summary>
        public static bool CanCollect(bool inHours, bool paid, bool beaten, bool armed, bool onDuty) =>
            inHours && !paid && !beaten && armed && onDuty;

        /// <summary>The mocking "already paid over there" opener when any other gate of the racket was paid.</summary>
        public static bool UseOtherGateOpener(bool anyOtherGatePaid) => anyOtherGatePaid;

        /// <summary>Pay wins over defer, defer over refuse; no answer (Escape, closed) = refused.</summary>
        public static TollOutcome ResolveOutcome(bool paid, bool deferred) =>
            paid ? TollOutcome.Paid : deferred ? TollOutcome.Deferred : TollOutcome.Refused;

        /// <summary>
        /// A toll brawl: the player down first counts (the collector then robs them), then the collector down, then the
        /// collector no longer fighting the player (fled / disengaged).
        /// </summary>
        public static TollBrawlResult EvaluateBrawl(bool playerUnconscious, bool collectorUnconscious, bool collectorFightingPlayer)
        {
            if (playerUnconscious) return TollBrawlResult.PlayerLost;
            if (collectorUnconscious) return TollBrawlResult.CollectorLost;
            return collectorFightingPlayer ? TollBrawlResult.Ongoing : TollBrawlResult.Ended;
        }
    }
}
