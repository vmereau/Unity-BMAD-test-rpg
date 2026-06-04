using System.Collections;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// HUD toast/notification controller. Subscribes to gameplay event channels, formats a short
    /// message per event, and shows a vertical stack of fading toasts (FIFO, capped at _maxVisible).
    /// Lives on the UICanvas Game/HUD layer alongside HealthBarUI / StaminaBarUI.
    /// Each toast fades in, holds for _holdSeconds, then fades out and is destroyed.
    /// </summary>
    public class NotificationToastUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Int _onXPGained;
        [SerializeField] private GameEventSO_Int _onLevelUp;
        [SerializeField] private GameEventSO_String _onLockUnlocked;

        [Header("UI References")]
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject _toastEntryPrefab;

        [Header("Behaviour")]
        [SerializeField] private int _maxVisible = 5;
        [SerializeField] private float _holdSeconds = 3f;
        [SerializeField] private float _fadeInSeconds = 0.15f;
        [SerializeField] private float _fadeOutSeconds = 0.3f;

        private readonly List<ActiveToast> _active = new List<ActiveToast>();
        private WaitForSecondsRealtime _holdWait;

        private class ActiveToast
        {
            public GameObject Go;
            public Coroutine Routine;
        }

        private void Awake()
        {
            // The controller lives on the container by convention — default to our own
            // RectTransform when left unassigned so toasts parent under this object.
            if (_container == null)
                _container = transform as RectTransform;
            if (_container == null)
            {
                GameLog.Error(TAG, "NotificationToastUI: _container not assigned and this GameObject has no RectTransform");
                enabled = false;
                return;
            }
            if (_toastEntryPrefab == null)
            {
                GameLog.Error(TAG, "NotificationToastUI: _toastEntryPrefab not assigned");
                enabled = false;
                return;
            }
            if (_onXPGained == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onXPGained not assigned — XP toasts disabled");
            if (_onLevelUp == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onLevelUp not assigned — level-up toasts disabled");
            if (_onLockUnlocked == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onLockUnlocked not assigned — unlock toasts disabled");

            // Clamp so the FIFO eviction loop can never index an empty list (a stack of 0 is nonsensical).
            if (_maxVisible < 1) _maxVisible = 1;
            // Realtime hold so toasts still expire while the game is paused (timeScale = 0).
            _holdWait = new WaitForSecondsRealtime(_holdSeconds);
        }

        private void OnEnable()
        {
            _onXPGained?.AddListener(HandleXPGained);
            _onLevelUp?.AddListener(HandleLevelUp);
            _onLockUnlocked?.AddListener(HandleUnlocked);
        }

        private void OnDisable()
        {
            _onXPGained?.RemoveListener(HandleXPGained);
            _onLevelUp?.RemoveListener(HandleLevelUp);
            _onLockUnlocked?.RemoveListener(HandleUnlocked);
        }

        private void HandleXPGained(int amount) => Show($"Experience +{amount}");

        private void HandleLevelUp(int _) => Show("Level up!");

        private void HandleUnlocked(string noun) => Show($"{noun} unlocked!");

        private void Show(string message)
        {
            // Coroutines silently stop while inactive — don't queue toasts we can't animate.
            if (!gameObject.activeInHierarchy) return;

            // FIFO eviction: drop the oldest toast immediately when at capacity.
            while (_active.Count >= _maxVisible)
            {
                ActiveToast oldest = _active[0];
                _active.RemoveAt(0);
                if (oldest.Routine != null) StopCoroutine(oldest.Routine);
                if (oldest.Go != null) Destroy(oldest.Go);
            }

            GameObject go = Instantiate(_toastEntryPrefab, _container, false);

            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = message;

            var canvasGroup = go.GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            var toast = new ActiveToast { Go = go };
            _active.Add(toast);
            toast.Routine = StartCoroutine(RunToast(toast, canvasGroup));
        }

        private IEnumerator RunToast(ActiveToast toast, CanvasGroup canvasGroup)
        {
            // Fade in
            if (canvasGroup != null && _fadeInSeconds > 0f)
            {
                float t = 0f;
                while (t < _fadeInSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(t / _fadeInSeconds);
                    yield return null;
                }
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // Hold
            yield return _holdWait;

            // Fade out
            if (canvasGroup != null && _fadeOutSeconds > 0f)
            {
                float t = 0f;
                while (t < _fadeOutSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(1f - t / _fadeOutSeconds);
                    yield return null;
                }
            }

            _active.Remove(toast);
            if (toast.Go != null) Destroy(toast.Go);
        }
    }
}
