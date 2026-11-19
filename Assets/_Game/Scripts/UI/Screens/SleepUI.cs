using System;
using System.Collections;
using Game.Core;
using Game.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bed sleep panel (GO <c>SleepScreen</c> under UICanvas, always active so the fade coroutine can run): an hour
    /// picker window (<see cref="_panel"/>, header "Day N, HH:MM", − / "Sleep until HH:00 (N h)" / +, Sleep, Cancel —
    /// arrow keys / A-D step the hour) and a full-screen black fade.
    /// Opened by <see cref="SleepSystem"/>; Esc / Cancel / player death close it. <see cref="IsOpen"/> stays true for the
    /// whole fade so save / load and gameplay input stay blocked (cursor unlocked = player busy).
    /// </summary>
    public class SleepUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[SleepUI]";

        [SerializeField] private TimeConfigSO _config;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _targetText;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _plusButton;
        [SerializeField] private Button _sleepButton;
        [SerializeField] private Button _cancelButton;
        [Tooltip("Full-screen black CanvasGroup faded in and out around the time skip.")]
        [SerializeField] private CanvasGroup _fade;

        // Closes on death: an open screen would block loading. Close() locks the cursor; DeathScreenUI unlocks it
        // again when it shows (after its realtime delay), like for CookingUI.
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        [Tooltip("Player knocked out (OnPlayerKnockedOut): closes like a death.")]
        [SerializeField] private GameEventSO_Void _onPlayerKnockedOut;

        private InputSystem_Actions _input;
        private Action<int> _onConfirm;
        private GameClock _clock;
        private int _targetHour;
        // Last label inputs: the label is rebuilt only when the target or the hour count changes (clock runs while open).
        private int _shownTarget = -1;
        private int _shownHours = -1;
        private int _shownMinute = -1;
        // Last horizontal Navigate direction (−1 / 0 / 1): Navigate is a PassThrough Vector2 that performs on every value
        // change (diagonals, stick drift), so the hour steps only when the direction changes from 0.
        private int _navSign;
        private Coroutine _sleepRoutine;
        // Separate from the handle: a zero-length fade finishes inside StartCoroutine, before the handle is assigned.
        private bool _sleeping;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _input = new InputSystem_Actions();
            if (_panel == null) GameLog.Warn(TAG, "_panel not assigned");
            if (_fade == null) GameLog.Warn(TAG, "_fade not assigned — sleep will skip time without a fade");
            if (_config == null) GameLog.Warn(TAG, "TimeConfigSO not assigned — using an instant fade");
            if (_targetText == null) GameLog.Warn(TAG, "_targetText not assigned — the picked hour won't show");
            _minusButton?.onClick.AddListener(StepDown);
            _plusButton?.onClick.AddListener(StepUp);
            _sleepButton?.onClick.AddListener(Confirm);
            _cancelButton?.onClick.AddListener(Cancel);
            SetFade(0f);
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _input.UI.Navigate.performed += HandleNavigate;
            _input.UI.Navigate.canceled += HandleNavigate;
            _onPlayerDied?.AddListener(HandlePlayerDied);
            _onPlayerKnockedOut?.AddListener(HandlePlayerDied);
        }

        private void OnDisable()
        {
            if (IsOpen) Close(); // a disabled screen can't run (or finish) the fade — never leave it half-open
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Navigate.performed -= HandleNavigate;
            _input.UI.Navigate.canceled -= HandleNavigate;
            _input.UI.Disable();
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
            _onPlayerKnockedOut?.RemoveListener(HandlePlayerDied);
        }

        private void OnDestroy()
        {
            _minusButton?.onClick.RemoveListener(StepDown);
            _plusButton?.onClick.RemoveListener(StepUp);
            _sleepButton?.onClick.RemoveListener(Confirm);
            _cancelButton?.onClick.RemoveListener(Cancel);
            _input?.Dispose();
        }

        private void HandleCancel(InputAction.CallbackContext _) => Cancel();

        // Horizontal arrows / A-D step the hour; vertical input is ignored.
        private void HandleNavigate(InputAction.CallbackContext context)
        {
            float x = context.ReadValue<Vector2>().x;
            int sign = x > 0.5f ? 1 : x < -0.5f ? -1 : 0;
            if (sign == _navSign) return;
            _navSign = sign;
            if (sign != 0) Step(sign);
        }

        // The clock keeps running while the panel is open: keep the header and the hour count current.
        private void Update()
        {
            if (IsOpen && !_sleeping) RefreshLabels();
        }

        private void HandlePlayerDied(bool _)
        {
            if (IsOpen) Close();
        }

        /// <summary>
        /// Shows the hour picker on <paramref name="defaultHour"/>. <paramref name="onConfirm"/> runs with the picked hour
        /// when Sleep is clicked. <paramref name="clock"/> feeds the header and the "(N h)" count.
        /// </summary>
        public void Open(GameClock clock, int defaultHour, Action<int> onConfirm)
        {
            _clock = clock;
            _onConfirm = onConfirm;
            _targetHour = SleepRules.StepHour(defaultHour, 0);
            _shownTarget = _shownHours = _shownMinute = -1;
            _navSign = 0;
            RefreshLabels();
            if (_panel != null) _panel.SetActive(true);
            OnScreenOpen();
            SelectSleepButton();
        }

        /// <summary>Esc / Cancel button: closes the window, ignored once the fade has started.</summary>
        public void Cancel()
        {
            if (IsOpen && !_sleeping) Close();
        }

        /// <summary>Hides everything immediately (also aborts a running fade).</summary>
        public void Close()
        {
            if (_sleepRoutine != null)
            {
                StopCoroutine(_sleepRoutine);
                _sleepRoutine = null;
            }
            _sleeping = false;
            _onConfirm = null;
            _clock = null;
            SetFade(0f);
            if (_panel != null) _panel.SetActive(false);
            OnScreenClose();
        }

        /// <summary>Hides the window, fades to black, runs <paramref name="atBlack"/> (the time skip), fades back and closes.</summary>
        public void PlaySleep(Action atBlack)
        {
            if (!IsOpen || _sleeping) return;
            if (_panel != null) _panel.SetActive(false);
            _sleeping = true;
            _sleepRoutine = StartCoroutine(SleepCoroutine(atBlack));
            if (!_sleeping) _sleepRoutine = null; // already finished (zero-length fade)
        }

        public void OnScreenOpen()
        {
            CursorManager.Unlock();
            IsOpen = true;
            GameLog.Info(TAG, "Sleep UI opened");
        }

        public void OnScreenClose()
        {
            CursorManager.Lock();
            IsOpen = false;
            GameLog.Info(TAG, "Sleep UI closed");
        }

        private IEnumerator SleepCoroutine(Action atBlack)
        {
            yield return FadeCoroutine(0f, 1f);
            // A throwing listener must not leave the screen black with save / load blocked.
            try { atBlack?.Invoke(); }
            catch (Exception e) { GameLog.Error(TAG, $"Sleep callback failed: {e}"); }
            yield return FadeCoroutine(1f, 0f);
            _sleepRoutine = null;
            Close();
        }

        // Unscaled: a pause menu can't freeze the screen black.
        private IEnumerator FadeCoroutine(float from, float to)
        {
            float duration = _config != null ? _config.sleepFadeSeconds : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetFade(to);
        }

        private void SetFade(float alpha)
        {
            if (_fade == null) return;
            _fade.alpha = alpha;
            _fade.blocksRaycasts = alpha > 0f;
        }

        private void Confirm()
        {
            if (!IsOpen || _sleeping) return;
            _onConfirm?.Invoke(_targetHour);
        }

        private void StepUp() => Step(1);
        private void StepDown() => Step(-1);

        private void Step(int delta)
        {
            if (!IsOpen || _sleeping) return;
            _targetHour = SleepRules.StepHour(_targetHour, delta);
            RefreshLabels();
            SelectSleepButton();
        }

        // Keyboard: Enter / Space always confirm (the panel buttons have Navigation None, and a clicked − / + would
        // otherwise stay selected and take the Submit).
        private void SelectSleepButton()
        {
            if (_sleepButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_sleepButton.gameObject);
        }

        private void RefreshLabels()
        {
            if (_clock == null) return;
            double now = _clock.Now;
            int minute = GameTime.HourOf(now) * 60 + GameTime.MinuteOf(now);
            if (minute != _shownMinute)
            {
                _shownMinute = minute;
                if (_headerText != null) _headerText.text = GameTime.Format(now);
            }
            int hours = SleepRules.HoursUntil(_targetHour, now); // 0 = under an hour
            if (_targetHour == _shownTarget && hours == _shownHours) return;
            _shownTarget = _targetHour;
            _shownHours = hours;
            if (_targetText != null) _targetText.text = $"Sleep until {_targetHour:00}:00 ({SleepRules.FormatHours(hours)})";
        }
    }
}
