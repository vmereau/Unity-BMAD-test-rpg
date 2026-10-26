using System;
using System.Collections;
using Game.Core;
using Game.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bed sleep panel (GO <c>SleepScreen</c> under UICanvas, always active so the fade coroutine can run): a choice
    /// window (<see cref="_panel"/>, header "Day N, HH:MM", 4 durations + Cancel) and a full-screen black fade.
    /// Opened by <see cref="SleepSystem"/>; Esc / Cancel / player death close it. <see cref="IsOpen"/> stays true for the
    /// whole fade so save / load and gameplay input stay blocked (cursor unlocked = player busy).
    /// </summary>
    public class SleepUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[SleepUI]";

        [SerializeField] private TimeConfigSO _config;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private Button _untilMorningButton;
        [SerializeField] private Button _untilEveningButton;
        [SerializeField] private Button _untilMidnightButton;
        [SerializeField] private Button _oneHourButton;
        [SerializeField] private Button _cancelButton;
        [Tooltip("Full-screen black CanvasGroup faded in and out around the time skip.")]
        [SerializeField] private CanvasGroup _fade;

        // Closes on death: an open screen would block loading. Close() locks the cursor; DeathScreenUI unlocks it
        // again when it shows (after its realtime delay), like for CookingUI.
        [SerializeField] private GameEventSO_Void _onPlayerDied;

        private InputSystem_Actions _input;
        private Action<SleepChoice> _onChoice;
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
            _untilMorningButton?.onClick.AddListener(ChooseUntilMorning);
            _untilEveningButton?.onClick.AddListener(ChooseUntilEvening);
            _untilMidnightButton?.onClick.AddListener(ChooseUntilMidnight);
            _oneHourButton?.onClick.AddListener(ChooseOneHour);
            _cancelButton?.onClick.AddListener(Cancel);
            SetFade(0f);
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _onPlayerDied?.AddListener(HandlePlayerDied);
        }

        private void OnDisable()
        {
            if (IsOpen) Close(); // a disabled screen can't run (or finish) the fade — never leave it half-open
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
        }

        private void OnDestroy()
        {
            _untilMorningButton?.onClick.RemoveListener(ChooseUntilMorning);
            _untilEveningButton?.onClick.RemoveListener(ChooseUntilEvening);
            _untilMidnightButton?.onClick.RemoveListener(ChooseUntilMidnight);
            _oneHourButton?.onClick.RemoveListener(ChooseOneHour);
            _cancelButton?.onClick.RemoveListener(Cancel);
            _input?.Dispose();
        }

        private void HandleCancel(InputAction.CallbackContext _) => Cancel();

        private void HandlePlayerDied(bool _)
        {
            if (IsOpen) Close();
        }

        /// <summary>Shows the choice window. <paramref name="onChoice"/> runs when a duration is clicked.</summary>
        public void Open(string header, Action<SleepChoice> onChoice)
        {
            _onChoice = onChoice;
            if (_headerText != null) _headerText.text = header;
            if (_panel != null) _panel.SetActive(true);
            OnScreenOpen();
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
            _onChoice = null;
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

        private void Choose(SleepChoice choice)
        {
            if (_sleeping) return;
            _onChoice?.Invoke(choice);
        }

        private void ChooseUntilMorning() => Choose(SleepChoice.UntilMorning);
        private void ChooseUntilEvening() => Choose(SleepChoice.UntilEvening);
        private void ChooseUntilMidnight() => Choose(SleepChoice.UntilMidnight);
        private void ChooseOneHour() => Choose(SleepChoice.OneHour);
    }
}
