using System.Collections;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// "You died" overlay. Listens to OnPlayerDied and, after a realtime delay, pauses the world, unlocks the
    /// cursor and offers Load last save (newest valid slot) / Load quicksave / Restart (only without any valid
    /// save — restores the in-memory start-of-session snapshot) / Quit.
    /// The component sits on an always-active root and toggles <c>_panel</c>. Hides on load started; if a load
    /// finishes with the player still dead (failed load), it shows again.
    /// </summary>
    public class DeathScreenUI : MonoBehaviour
    {
        private const string TAG = "[DeathScreen]";

        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _loadLastButton;
        [SerializeField] private Button _loadQuickButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private float _showDelaySeconds = 2f;

        // TODO(save-load prototype exception): cross-system ref to re-check death after a failed load.
        [SerializeField] private PlayerStateManager _playerStateManager;

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        [SerializeField] private GameEventSO_Void _onLoadStarted;
        [SerializeField] private GameEventSO_Void _onLoadFinished;

        private WaitForSecondsRealtime _showDelay;
        private Coroutine _showRoutine;
        private string _lastValidSlotId;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "_panel not assigned — death screen disabled");
                enabled = false;
                return;
            }
            if (_onPlayerDied == null) GameLog.Warn(TAG, "_onPlayerDied not assigned — death screen will never show");

            _showDelay = new WaitForSecondsRealtime(_showDelaySeconds);

            GameMenuUI.Wire(_loadLastButton, HandleLoadLast);
            GameMenuUI.Wire(_loadQuickButton, HandleLoadQuick);
            GameMenuUI.Wire(_restartButton, HandleRestart);
            GameMenuUI.Wire(_quitButton, GameMenuUI.QuitGame);

            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            _onPlayerDied?.AddListener(HandlePlayerDied);
            _onLoadStarted?.AddListener(HandleLoadStarted);
            _onLoadFinished?.AddListener(HandleLoadFinished);
        }

        private void OnDisable()
        {
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
            _onLoadStarted?.RemoveListener(HandleLoadStarted);
            _onLoadFinished?.RemoveListener(HandleLoadFinished);
        }

        private void HandlePlayerDied(bool _)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(ShowAfterDelayCoroutine());
        }

        private IEnumerator ShowAfterDelayCoroutine()
        {
            yield return _showDelay;
            _showRoutine = null;
            Show();
        }

        private void Show()
        {
            var mostRecent = SaveFileStore.MostRecentValid();
            _lastValidSlotId = mostRecent?.slotId;

            bool hasQuick = false;
            foreach (var slot in SaveFileStore.ListSlots())
            {
                if (slot.slotId == GameConstants.SAVE_SLOT_QUICK && slot.isValid) hasQuick = true;
            }

            bool hasAnySave = _lastValidSlotId != null;
            SetVisible(_loadLastButton, hasAnySave);
            SetVisible(_loadQuickButton, hasQuick);
            SetVisible(_restartButton, !hasAnySave && SaveSystem.Instance != null && SaveSystem.Instance.HasNewGameSnapshot);

            _panel.SetActive(true);
            Time.timeScale = 0f;
            CursorManager.Unlock();
            GameLog.Info(TAG, "Death screen shown");
        }

        private void Hide()
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
            _panel.SetActive(false);
        }

        private void HandleLoadLast()
        {
            if (_lastValidSlotId != null) SaveSystem.Instance?.Load(_lastValidSlotId);
        }

        private void HandleLoadQuick() => SaveSystem.Instance?.Load(GameConstants.SAVE_SLOT_QUICK);

        private void HandleRestart() => SaveSystem.Instance?.RestartNewGame();

        private static void SetVisible(Button button, bool visible)
        {
            if (button != null) button.gameObject.SetActive(visible);
        }

        // Load resets timeScale / cursor itself when it finishes.
        private void HandleLoadStarted(bool _) => Hide();

        private void HandleLoadFinished(bool _)
        {
            if (_playerStateManager != null && _playerStateManager.IsDead) Show();
        }
    }
}
