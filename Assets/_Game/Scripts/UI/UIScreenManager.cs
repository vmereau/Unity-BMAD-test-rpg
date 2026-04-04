using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Options = 3 }

    public class UIScreenManager : MonoBehaviour
    {
        private const string TAG = "[UIScreenManager]";

        [SerializeField] private GameObject _tabBar;
        [SerializeField] private GameObject[] _tabPanelRoots; // indexed by ScreenTab
        [SerializeField] private Button[] _tabButtons;        // indexed by ScreenTab

        private InputSystem_Actions _input;
        private ScreenTab? _activeTab = null;

        private void Awake()
        {
            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _input.Player.Enable();
            _input.UI.Enable();
            _input.Player.InventoryToggle.performed += HandleInventoryToggle;
            _input.Player.CharacterStatsToggle.performed += HandleCharacterStatsToggle;
            _input.UI.Cancel.performed += HandleCancel;
            WireTabButtons();
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.InventoryToggle.performed -= HandleInventoryToggle;
            _input.Player.CharacterStatsToggle.performed -= HandleCharacterStatsToggle;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.Player.Disable();
            _input.UI.Disable();
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

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_activeTab.HasValue)
                CloseAll();
        }
    }
}
