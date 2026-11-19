using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// HUD time of day (<c>UICanvas/Game/Clock</c>): a half-circle dial where the sun travels sunrise → sunset and the moon
    /// sunset → sunrise (same <see cref="DayNightMath"/> progress and <see cref="DayNightConfigSO"/> hours as the lighting),
    /// plus an "HH:MM" label. Polls <see cref="GameClock.Active"/> in <c>LateUpdate</c> (UICanvas lives in Player.prefab and
    /// can't reference Core) and only touches the UI when the shown minute changes, so sleep skips and loads show on the
    /// next frame. Hidden (CanvasGroup alpha 0) while no clock runs.
    /// </summary>
    public class HudClockUI : MonoBehaviour
    {
        private const string TAG = "[HudClockUI]";

        [SerializeField] private DayNightConfigSO _config;
        [Tooltip("Shown / hidden as a whole (alpha) depending on whether a clock runs.")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _sunIcon;
        [SerializeField] private RectTransform _moonIcon;
        [SerializeField] private TMP_Text _timeText;
        [Tooltip("Arc radius in pixels, from the dial's base centre (the icons' parent pivot).")]
        [Min(0f)] [SerializeField] private float _radius = 40f;

        private int _shownMinute = -1;
        private int _clockState = -1; // −1 unknown (forces the first show / hide), 0 no clock, 1 clock
        private bool _warnedNoClock;
        private bool _warnedInvalidDay;

        private void Awake()
        {
            if (_config == null) GameLog.Warn(TAG, "DayNightConfigSO not assigned — the dial uses a 6–22 day");
            if (_group == null) GameLog.Warn(TAG, "_group not assigned — the clock can't hide without a GameClock");
            if (_sunIcon == null || _moonIcon == null) GameLog.Warn(TAG, "Sun / moon icon not assigned — the dial shows the time only");
            if (_timeText == null) GameLog.Warn(TAG, "_timeText not assigned — the dial shows no time");
        }

        private void OnEnable()
        {
            _shownMinute = -1; // repaint after any time spent disabled
        }

        private void LateUpdate()
        {
            GameClock clock = GameClock.Active;
            SetHasClock(clock != null);
            if (clock == null) return;

            double now = clock.Now;
            int minute = GameTime.HourOf(now) * 60 + GameTime.MinuteOf(now);
            if (minute == _shownMinute) return;
            _shownMinute = minute;

            float hour = DayNightMath.HourOfDay(now);
            float sunrise = _config != null ? _config.sunriseHour : 6f;
            float sunset = _config != null ? _config.sunsetHour : 22f;
            if (!DayNightMath.IsValidDay(sunrise, sunset))
            {
                if (!_warnedInvalidDay)
                {
                    _warnedInvalidDay = true;
                    GameLog.Warn(TAG, $"Sunrise {sunrise} must be before sunset {sunset} — dial uses a 6–22 day");
                }
                sunrise = 6f;
                sunset = 22f;
            }
            bool isDay = DayNightMath.IsDay(hour, sunrise, sunset);
            float progress = isDay
                ? DayNightMath.DayProgress(hour, sunrise, sunset)
                : DayNightMath.NightProgress(hour, sunrise, sunset);
            PlaceIcon(_sunIcon, isDay, progress);
            PlaceIcon(_moonIcon, !isDay, progress);
            if (_timeText != null) _timeText.text = $"{GameTime.HourOf(now):00}:{GameTime.MinuteOf(now):00}";
        }

        private void PlaceIcon(RectTransform icon, bool shown, float progress)
        {
            if (icon == null) return;
            if (icon.gameObject.activeSelf != shown) icon.gameObject.SetActive(shown);
            if (shown) icon.anchoredPosition = HudClockMath.DialPosition(progress, _radius);
        }

        private void SetHasClock(bool hasClock)
        {
            int state = hasClock ? 1 : 0;
            if (state == _clockState) return;
            _clockState = state;
            if (_group != null) _group.alpha = hasClock ? 1f : 0f;
            if (hasClock) _shownMinute = -1;
            else if (!_warnedNoClock)
            {
                _warnedNoClock = true;
                GameLog.Warn(TAG, "No running GameClock — clock hidden");
            }
        }
    }
}
