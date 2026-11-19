using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Computed fact: true while the game hour (<see cref="GameClock.Active"/>) is in [<see cref="StartHour"/>,
    /// <see cref="EndHour"/>): start inclusive, end exclusive, wraps midnight, start == end = all day. No clock → false.
    /// NOT stored in <c>_worldFacts</c> and raises no change event, so use it only in readers that evaluate on demand
    /// (memory topic conditions, choice gates). Never in quest parts or talk openers. Prefix intentionally not set.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Hour Window Fact", fileName = "HourWindowFact_")]
    public class HourWindowFact : Fact
    {
        [Tooltip("First hour of the window (inclusive).")]
        [Range(0, 23)] [SerializeField] private int _startHour;
        [Tooltip("Hour the window ends (exclusive). Wraps midnight; equal to Start = all day.")]
        [Range(0, 23)] [SerializeField] private int _endHour;

        public int StartHour => _startHour;
        public int EndHour => _endHour;

        /// <summary>Reads the active clock now.</summary>
        public bool IsTrueNow()
        {
            GameClock clock = GameClock.Active;
            return HourWindowFactRules.IsTrue(clock != null, clock != null ? clock.Hour : 0, _startHour, _endHour);
        }

        /// <summary>For debugging only — not used as a world-fact dictionary key.</summary>
        public override string ToString() => $"Hour.{_startHour}-{_endHour}";
    }

    /// <summary>Pure rule for <see cref="HourWindowFact"/>. Tested by <c>HourWindowFactTests</c>.</summary>
    public static class HourWindowFactRules
    {
        /// <summary>True when a clock runs and <paramref name="hour"/> is in the window (<see cref="GameTime.IsHourInWindow"/>).</summary>
        public static bool IsTrue(bool hasClock, int hour, int startHour, int endHour) =>
            hasClock && GameTime.IsHourInWindow(startHour, endHour, hour);
    }
}
