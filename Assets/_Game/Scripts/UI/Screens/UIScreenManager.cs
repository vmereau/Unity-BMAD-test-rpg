using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Skills = 3 }

    /// <summary>
    /// Opens / closes the character tabs (I / J / C / K, tab buttons) and routes Esc:
    /// open tab → close it; Game Menu open → <see cref="GameMenuUI.HandleBack"/>; plain gameplay → open the Game Menu.
    /// "Plain gameplay" is sampled in LateUpdate (previous frame), so the Esc press that closes a dialogue /
    /// container / trade window (whose own Cancel handlers re-lock the cursor this frame) never also opens the menu.
    /// Closes every tab when a load starts or the player dies.
    /// </summary>
    public class UIScreenManager : MonoBehaviour
    {
        private const string TAG = "[UIScreenManager]";

        [SerializeField] private GameObject _tabBar;
        [SerializeField] private GameObject[] _tabPanelRoots; // indexed by ScreenTab
        [SerializeField] private Button[] _tabButtons;        // indexed by ScreenTab
        [SerializeField] private PlayerStateManager _playerStateManager;
        [SerializeField] private GameMenuUI _gameMenu;

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Void _onLoadStarted;
        [SerializeField] private GameEventSO_Void _onPlayerDied;

        private InputSystem_Actions _input;
        private ScreenTab? _activeTab = null;
        private bool _wasGameplayLastFrame;

        private bool IsGameMenuOpen => _gameMenu != null && _gameMenu.IsOpen;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            if (_gameMenu == null) GameLog.Warn(TAG, "_gameMenu not assigned — Esc won't open the Game Menu");
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.Player.Enable();
            _input.UI.Enable();
            _input.Player.InventoryToggle.performed += HandleInventoryToggle;
            _input.Player.CharacterStatsToggle.performed += HandleCharacterStatsToggle;
            _input.Player.QuestLogToggle.performed += HandleQuestLogToggle;
            _input.Player.SkillsToggle.performed += HandleSkillsToggle;
            _input.UI.Cancel.performed += HandleCancel;
            WireTabButtons();
            _onLoadStarted?.AddListener(HandleLoadStarted);
            _onPlayerDied?.AddListener(HandlePlayerDied);
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.InventoryToggle.performed -= HandleInventoryToggle;
            _input.Player.CharacterStatsToggle.performed -= HandleCharacterStatsToggle;
            _input.Player.QuestLogToggle.performed -= HandleQuestLogToggle;
            _input.Player.SkillsToggle.performed -= HandleSkillsToggle;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.Player.Disable();
            _input.UI.Disable();
            _onLoadStarted?.RemoveListener(HandleLoadStarted);
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
        }

        private void LateUpdate()
        {
            _wasGameplayLastFrame = CursorManager.IsLocked
                && (_playerStateManager == null || !_playerStateManager.IsDead)
                && (SaveSystem.Instance == null || !SaveSystem.Instance.IsLoading);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void WireTabButtons()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int tabIndex = i; // capture for closure
                _tabButtons[i].onClick.RemoveAllListeners();
                _tabButtons[i].onClick.AddListener(() => OnTabButtonClicked((ScreenTab)tabIndex));
            }
        }

        private void OnTabButtonClicked(ScreenTab tab)
        {
            if (_activeTab == tab)
                CloseAll();
            else
                OpenTab(tab);
        }

        public void OpenTab(ScreenTab tab)
        {
            if (_playerStateManager != null && (_playerStateManager.IsInDialogue || _playerStateManager.IsDead)) return;
            if (IsGameMenuOpen) return;
            if (_activeTab == tab) return;

            // Close current tab content if switching
            if (_activeTab.HasValue && _activeTab.Value != tab)
                CloseTabContent(_activeTab.Value);

            _activeTab = tab;

            // Show tab bar
            _tabBar.SetActive(true);

            // Show requested panel
            int idx = (int)tab;
            if (idx < _tabPanelRoots.Length)
            {
                _tabPanelRoots[idx].SetActive(true);
                var panel = _tabPanelRoots[idx].GetComponent<IScreenPanel>();
                panel?.OnScreenOpen();
            }

            // Update tab button states
            UpdateTabButtonStates();

            CursorManager.Unlock();
            GameLog.Info(TAG, $"Opened tab: {tab}");
        }

        public void CloseAll()
        {
            if (!_activeTab.HasValue) return;

            CloseTabContent(_activeTab.Value);
            _activeTab = null;
            _tabBar.SetActive(false);
            UpdateTabButtonStates();

            CursorManager.Lock();
            GameLog.Info(TAG, "All screens closed");
        }

        private void CloseTabContent(ScreenTab tab)
        {
            int idx = (int)tab;
            if (idx < _tabPanelRoots.Length)
            {
                var panel = _tabPanelRoots[idx].GetComponent<IScreenPanel>();
                panel?.OnScreenClose();
                _tabPanelRoots[idx].SetActive(false);
            }
        }

        private void UpdateTabButtonStates()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                // Visual feedback: interactable=false on the active tab button
                _tabButtons[i].interactable = !(_activeTab.HasValue && (int)_activeTab.Value == i);
            }
        }

        private void HandleInventoryToggle(InputAction.CallbackContext ctx)
        {
            if (_activeTab == ScreenTab.Inventory)
                CloseAll();
            else
                OpenTab(ScreenTab.Inventory);
        }

        private void HandleCharacterStatsToggle(InputAction.CallbackContext ctx)
        {
            if (_activeTab == ScreenTab.CharacterStats)
                CloseAll();
            else
                OpenTab(ScreenTab.CharacterStats);
        }

        private void HandleQuestLogToggle(InputAction.CallbackContext ctx)
        {
            if (_activeTab == ScreenTab.QuestLog)
                CloseAll();
            else
                OpenTab(ScreenTab.QuestLog);
        }

        private void HandleSkillsToggle(InputAction.CallbackContext ctx)
        {
            if (_activeTab == ScreenTab.Skills)
                CloseAll();
            else
                OpenTab(ScreenTab.Skills);
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_activeTab.HasValue)
                CloseAll();
            else if (IsGameMenuOpen)
                _gameMenu.HandleBack();
            else if (_wasGameplayLastFrame && _gameMenu != null)
                _gameMenu.Open();
        }

        private void HandleLoadStarted(bool _) => CloseAll();

        // Close tabs on death so a later Esc can't re-lock the cursor under the death screen (tabs can't reopen while dead).
        private void HandlePlayerDied(bool _) => CloseAll();
    }
}
