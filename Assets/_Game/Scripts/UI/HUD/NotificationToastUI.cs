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
    /// A message identical to the newest visible toast restarts that toast's hold instead of stacking a new one.
    /// </summary>
    public class NotificationToastUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Int _onXPGained;
        [SerializeField] private GameEventSO_Int _onLevelUp;
        [SerializeField] private GameEventSO_String _onLockUnlocked;
        [SerializeField] private GameEventSO_String _onSaveNotification;
        [SerializeField] private GameEventSO_String _onLockpickFailed;
        [SerializeField] private GameEventSO_String _onRecipeLearned;
        [SerializeField] private GameEventSO_String _onMealBonusGained;
        [SerializeField] private GameEventSO_Int _onRewardGoldGained;
        [SerializeField] private GameEventSO_String _onRewardItemReceived;
        [SerializeField] private GameEventSO_String _onSleepRefused;

        [Header("UI References")]
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject _toastEntryPrefab;

        [Header("Behaviour")]
        [SerializeField] private int _maxVisible = 5;
        [SerializeField] private float _holdSeconds = 3f;
        [SerializeField] private float _fadeInSeconds = 0.15f;
        [SerializeField] private float _fadeOutSeconds = 0.3f;

        private readonly List<ActiveToast> _active = new List<ActiveToast>();

        private class ActiveToast
        {
            public GameObject Go;
            public Coroutine Routine;
            public string Message;
            public CanvasGroup Group;
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
            if (_onSaveNotification == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onSaveNotification not assigned — save / load toasts disabled");
            if (_onLockpickFailed == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onLockpickFailed not assigned — lockpick failure toasts disabled");
            if (_onRecipeLearned == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onRecipeLearned not assigned — recipe toasts disabled");
            if (_onMealBonusGained == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onMealBonusGained not assigned — first-meal bonus toasts disabled");
            if (_onRewardGoldGained == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onRewardGoldGained not assigned — reward gold toasts disabled");
            if (_onRewardItemReceived == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onRewardItemReceived not assigned — reward item toasts disabled");
            if (_onSleepRefused == null)
                GameLog.Warn(TAG, "NotificationToastUI: _onSleepRefused not assigned — sleep refusal toasts disabled");

            // Clamp so the FIFO eviction loop can never index an empty list (a stack of 0 is nonsensical).
            if (_maxVisible < 1) _maxVisible = 1;
        }

        private void OnEnable()
        {
            _onXPGained?.AddListener(HandleXPGained);
            _onLevelUp?.AddListener(HandleLevelUp);
            _onLockUnlocked?.AddListener(HandleUnlocked);
            _onSaveNotification?.AddListener(Show);
            _onLockpickFailed?.AddListener(Show);
            _onRecipeLearned?.AddListener(HandleRecipeLearned);
            _onMealBonusGained?.AddListener(HandleMealBonusGained);
            _onRewardGoldGained?.AddListener(HandleRewardGoldGained);
            _onRewardItemReceived?.AddListener(Show);
            _onSleepRefused?.AddListener(Show);
        }

        private void OnDisable()
        {
            _onXPGained?.RemoveListener(HandleXPGained);
            _onLevelUp?.RemoveListener(HandleLevelUp);
            _onLockUnlocked?.RemoveListener(HandleUnlocked);
            _onSaveNotification?.RemoveListener(Show);
            _onLockpickFailed?.RemoveListener(Show);
            _onRecipeLearned?.RemoveListener(HandleRecipeLearned);
            _onMealBonusGained?.RemoveListener(HandleMealBonusGained);
            _onRewardGoldGained?.RemoveListener(HandleRewardGoldGained);
            _onRewardItemReceived?.RemoveListener(Show);
            _onSleepRefused?.RemoveListener(Show);

            // Disabling kills the coroutines — drop their toasts too, or they'd stay frozen on screen (and a matching
            // message would revive one through the dedup).
            foreach (var toast in _active)
                if (toast.Go != null) Destroy(toast.Go);
            _active.Clear();
        }

        private void HandleXPGained(int amount) => Show($"Experience +{amount}");

        private void HandleLevelUp(int _) => Show("Level up!");

        private void HandleUnlocked(string noun) => Show($"{noun} unlocked!");

        private void HandleRecipeLearned(string dishName) => Show($"Recipe learned: {dishName}");

        private void HandleMealBonusGained(string bonusText) => Show(FormatMealBonus(bonusText));

        private void HandleRewardGoldGained(int amount) => Show(FormatGold(amount));

        /// <summary>Toast text for reward gold, e.g. "Gold +100".</summary>
        public static string FormatGold(int amount) => $"Gold +{amount}";

        /// <summary>Toast text for a first-meal bonus, e.g. "Permanent bonus: +5 Max HP".</summary>
        public static string FormatMealBonus(string bonusText) => $"Permanent bonus: {bonusText}";

        private void Show(string message)
        {
            // Coroutines silently stop while inactive — don't queue toasts we can't animate.
            if (!gameObject.activeInHierarchy) return;

            // Dedup: same text as the newest visible toast → keep it on screen and restart its hold.
            if (_active.Count > 0)
            {
                ActiveToast newest = _active[_active.Count - 1];
                if (newest.Go != null && newest.Message == message)
                {
                    if (newest.Routine != null) StopCoroutine(newest.Routine);
                    if (newest.Group != null) newest.Group.alpha = 1f;
                    newest.Routine = StartCoroutine(RunToast(newest, newest.Group, skipFadeIn: true));
                    return;
                }
            }

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

            var toast = new ActiveToast { Go = go, Message = message, Group = canvasGroup };
            _active.Add(toast);
            toast.Routine = StartCoroutine(RunToast(toast, canvasGroup, skipFadeIn: false));
        }

        private IEnumerator RunToast(ActiveToast toast, CanvasGroup canvasGroup, bool skipFadeIn)
        {
            // Fade in
            if (!skipFadeIn && canvasGroup != null && _fadeInSeconds > 0f)
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

            // Hold — unscaled timer (not a shared WaitForSecondsRealtime: its deadline would be shared by every
            // toast and survive a dedup restart), so toasts still expire while paused (timeScale = 0).
            float held = 0f;
            while (held < _holdSeconds)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

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
