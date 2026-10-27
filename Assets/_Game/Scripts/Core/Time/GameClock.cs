using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The single authoritative world clock (GO <c>GameClock</c> in Core.unity). Holds the absolute world time in game
    /// seconds and advances it by scaled delta time, so pausing menus (<c>timeScale = 0</c>) stop it.
    /// Raises <c>OnHourChanged</c> (hour 0–23) per crossed hour, and on <see cref="SkipTo"/> raises <c>OnTimeSkipped</c>
    /// (skipped seconds) then one <c>OnHourChanged</c> with the final hour. Saved by <c>SaveSystem</c>
    /// (<see cref="Restore"/> raises nothing). Not a singleton: Core readers hold a serialized reference or listen to the
    /// events; region-scene readers (which can't reference Core) read the time through <see cref="Active"/>.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        private const string TAG = "[GameClock]";

        [SerializeField] private TimeConfigSO _config;
        [SerializeField] private GameEventSO_Int _onHourChanged;
        [SerializeField] private GameEventSO_Float _onTimeSkipped;

        private double _rate;

        /// <summary>Read path for region-scene objects. Null when no running clock is loaded (e.g. a region played alone).</summary>
        public static GameClock Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Active = null;

        /// <summary>Absolute world time in game seconds since day 1 00:00.</summary>
        public double Now { get; private set; }
        public int Day => GameTime.DayOf(Now);
        public int Hour => GameTime.HourOf(Now);
        public int Minute => GameTime.MinuteOf(Now);

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "No TimeConfigSO assigned — the clock is stopped");
                return;
            }
            _rate = GameTime.Rate(_config.realMinutesPerGameDay);
            Now = GameTime.StartTime(_config.startHour);
            if (Active != null && Active != this) GameLog.Warn(TAG, "A second GameClock awoke — it replaces the active one");
            Active = this;
            if (_onHourChanged == null) GameLog.Warn(TAG, "_onHourChanged not assigned — hour listeners won't be notified");
            if (_onTimeSkipped == null) GameLog.Warn(TAG, "_onTimeSkipped not assigned — skip listeners won't be notified");
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (_rate <= 0d) return;
            // A load restores the time before its async region reload — don't drift (or raise hours) meanwhile.
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) return;
            double previous = Now;
            Now += Time.deltaTime * _rate;

            // A hitch can cross several hours; one raise each, never more than a day's worth.
            int count = GameTime.CrossedHours(previous, Now, out long first);
            if (count == 0 || _onHourChanged == null) return;
            for (long h = first; h < first + count; h++)
                _onHourChanged.Raise(GameTime.HourOfAbsolute(h));
        }

        /// <summary>Jumps forward to <paramref name="target"/> (sleep / wait). Returns false (and changes nothing) if it isn't in the future.</summary>
        public bool SkipTo(double target)
        {
            if (!(target > Now) || double.IsInfinity(target))
            {
                GameLog.Warn(TAG, $"SkipTo({target}) is not after now ({Now}) — ignored");
                return false;
            }
            double skipped = target - Now;
            Now = target;
            GameLog.Info(TAG, $"Skipped {skipped / GameTime.SECONDS_PER_HOUR:0.##} h → {GameTime.Format(Now)}");
            _onTimeSkipped?.Raise((float)skipped);
            _onHourChanged?.Raise(Hour);
            return true;
        }

        /// <summary>Sets the time from a save (null = save made before the clock existed → new-game start). No events.</summary>
        public void Restore(double? worldSeconds)
        {
            double start = _config != null ? GameTime.StartTime(_config.startHour) : 0d;
            double value = worldSeconds ?? start;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                GameLog.Warn(TAG, $"Invalid saved world time {value} — using the start time");
                value = start;
            }
            Now = value;
            GameLog.Info(TAG, $"Restored {GameTime.Format(Now)}");
        }

        [ContextMenu("Skip 1 hour")]
        private void SkipOneHour() => SkipTo(Now + GameTime.SECONDS_PER_HOUR);
    }
}
