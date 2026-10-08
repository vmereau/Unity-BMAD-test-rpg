using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Esc Game Menu (Resume / Save Game / Load Game / Options / Quit). Opened and routed by
    /// <see cref="UIScreenManager"/> (Esc closes the confirm dialog, then a sub-panel, then the menu).
    /// Pauses the world (<c>Time.timeScale = 0</c>) and unlocks the cursor while open; Close restores both.
    /// The component sits on an always-active root and toggles <c>_panel</c>, so it hears the load-started event:
    /// a load hides the menu without touching timeScale / cursor (SaveSystem.FinishLoad restores them).
    /// </summary>
    public class GameMenuUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[GameMenu]";

        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _mainButtons;

        [Header("Main Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private TMP_Text _saveBlockedText;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _quitButton;

        [Header("Sub-panels")]
        [SerializeField] private SaveSlotListUI _saveSlotList;
        [SerializeField] private OptionsUI _optionsPanel;
        [SerializeField] private Button _backButton;
        [SerializeField] private ConfirmDialogUI _confirmDialog;

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Void _onLoadStarted;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "_panel not assigned — Game Menu disabled");
                enabled = false;
                return;
            }
            if (_onLoadStarted == null) GameLog.Warn(TAG, "_onLoadStarted not assigned — the menu won't hide on load");

            Wire(_resumeButton, Close);
            Wire(_saveButton, () => ShowSlotList(SaveSlotListUI.Mode.Save));
            Wire(_loadButton, () => ShowSlotList(SaveSlotListUI.Mode.Load));
            Wire(_optionsButton, ShowOptions);
            Wire(_quitButton, HandleQuitClicked);
            Wire(_backButton, ShowMain);

            _panel.SetActive(false);
        }

        private void OnEnable() => _onLoadStarted?.AddListener(HandleLoadStarted);

        private void OnDisable() => _onLoadStarted?.RemoveListener(HandleLoadStarted);

        public void Open()
        {
            if (_panel == null || IsOpen) return;
            _panel.SetActive(true);
            ShowMain();
            Time.timeScale = 0f;
            OnScreenOpen();
            GameLog.Info(TAG, "Game Menu opened");
        }

        public void Close()
        {
            if (!IsOpen) return;
            HideAll();
            Time.timeScale = 1f;
            OnScreenClose();
            GameLog.Info(TAG, "Game Menu closed");
        }

        /// <summary>Esc: confirm dialog → sub-panel → menu (= Resume).</summary>
        public void HandleBack()
        {
            if (!IsOpen) return;
            if (_confirmDialog != null && _confirmDialog.IsOpen) _confirmDialog.Cancel();
            else if (_mainButtons != null && !_mainButtons.activeSelf) ShowMain();
            else Close();
        }

        /// <summary>Adds a click listener; warns instead of throwing when the button isn't wired.</summary>
        internal static void Wire(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
            else GameLog.Warn(TAG, $"Button for '{action.Method.Name}' not assigned");
        }

        /// <summary>Quits the game (stops Play Mode in the editor). Shared with the death screen.</summary>
        internal static void QuitGame()
        {
            GameLog.Info(TAG, "Quit requested");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnScreenOpen() => CursorManager.Unlock();

        public void OnScreenClose() => CursorManager.Lock();

        private void ShowMain()
        {
            HideSubPanels();
            SetMainVisible(true);
            RefreshSaveButton();
        }

        private void ShowSlotList(SaveSlotListUI.Mode mode)
        {
            if (_saveSlotList == null) return;
            SetMainVisible(false);
            _saveSlotList.Show(mode);
        }

        private void ShowOptions()
        {
            if (_optionsPanel == null) return;
            SetMainVisible(false);
            _optionsPanel.gameObject.SetActive(true);
            _optionsPanel.OnScreenOpen();
        }

        // Main buttons and the Back button are mutually exclusive.
        private void SetMainVisible(bool visible)
        {
            if (_mainButtons != null) _mainButtons.SetActive(visible);
            if (_backButton != null) _backButton.gameObject.SetActive(!visible);
        }

        private void HideSubPanels()
        {
            if (_confirmDialog != null) _confirmDialog.Hide();
            if (_saveSlotList != null) _saveSlotList.Hide();
            if (_optionsPanel != null && _optionsPanel.gameObject.activeSelf)
            {
                _optionsPanel.OnScreenClose();
                _optionsPanel.gameObject.SetActive(false);
            }
        }

        private void RefreshSaveButton()
        {
            string reason = "Saving is unavailable";
            bool canSave = SaveSystem.Instance != null && SaveSystem.Instance.CanSave(out reason);
            if (_saveButton != null) _saveButton.interactable = canSave;
            if (_saveBlockedText != null)
            {
                _saveBlockedText.gameObject.SetActive(!canSave);
                _saveBlockedText.text = canSave ? string.Empty : reason;
            }
        }

        private void HandleQuitClicked()
        {
            if (_confirmDialog != null) _confirmDialog.Show("Quit without saving?", QuitGame);
            else QuitGame();
        }

        private void HideAll()
        {
            HideSubPanels();
            _panel.SetActive(false);
        }

        // A load replaces the world: just hide; SaveSystem restores timeScale and the cursor when it finishes.
        private void HandleLoadStarted(bool _)
        {
            if (IsOpen) HideAll();
        }
    }
}
