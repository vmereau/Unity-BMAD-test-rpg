using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Start menu (MainMenu scene): New Game (origin pick) / Continue (newest valid save) / Load Game (shared slot list) / Quit.
    /// New Game opens <see cref="OriginPickUI"/>; Begin hands the origin to Core through <see cref="GameSession"/>.
    /// Continue and Load Game hand the slot the same way. Esc: confirm dialog → slot list / origin pick → nothing.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private const string TAG = "[MainMenu]";
        private const string NO_SAVE_TEXT = "No saved game";

        [SerializeField] private GameObject _mainButtons;
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _continueDetailsText;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _quitButton;

        [Header("Load Game")]
        [SerializeField] private SaveSlotListUI _saveSlotList;
        [SerializeField] private Button _backButton;
        [SerializeField] private ConfirmDialogUI _confirmDialog;

        [Header("New Game")]
        [SerializeField] private OriginPickUI _originPick;

        private InputSystem_Actions _input;
        private string _continueSlot;
        private bool _starting;

        private void Awake()
        {
            GameMenuUI.Wire(_newGameButton, ShowOriginPick);
            GameMenuUI.Wire(_continueButton, HandleContinue);
            GameMenuUI.Wire(_loadButton, ShowLoadList);
            GameMenuUI.Wire(_quitButton, GameMenuUI.QuitGame);
            GameMenuUI.Wire(_backButton, ShowMain);
            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed += HandleCancel;
            _input.UI.Enable();
        }

        private void OnDisable()
        {
            if (_input == null) return; // Awake may disable before OnEnable runs
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
        }

        private void OnDestroy() => _input?.Dispose();

        private void Start()
        {
            Time.timeScale = 1f; // never inherit a paused scale
            CursorManager.Unlock();
            if (_confirmDialog != null) _confirmDialog.Hide();
            ShowMain();
        }

        private void ShowMain()
        {
            if (_confirmDialog != null) _confirmDialog.Hide();
            if (_saveSlotList != null) _saveSlotList.Hide();
            if (_originPick != null) _originPick.Hide();
            if (_mainButtons != null) _mainButtons.SetActive(true);
            if (_backButton != null) _backButton.gameObject.SetActive(false);
            RefreshContinue();
        }

        private void ShowLoadList()
        {
            if (_saveSlotList == null) return;
            if (_mainButtons != null) _mainButtons.SetActive(false);
            if (_backButton != null) _backButton.gameObject.SetActive(true);
            _saveSlotList.Show(SaveSlotListUI.Mode.Load, LoadSlot);
        }

        private void ShowOriginPick()
        {
            if (_originPick == null)
            {
                GameLog.Warn(TAG, "OriginPickUI not assigned — starting with the default origin");
                StartNewGame(null);
                return;
            }
            if (_mainButtons != null) _mainButtons.SetActive(false);
            if (_backButton != null) _backButton.gameObject.SetActive(true);
            _originPick.Show(StartNewGame);
        }

        private void RefreshContinue()
        {
            var newest = SaveFileStore.MostRecentValid();
            _continueSlot = newest?.slotId;
            if (_continueButton != null) _continueButton.interactable = newest != null;
            if (_continueDetailsText != null)
                _continueDetailsText.text = newest != null
                    ? $"{SaveSlotListUI.FormatSlotLabel(newest.Value.slotId)} — {SaveSlotListUI.FormatDetails(newest.Value)}"
                    : NO_SAVE_TEXT;
        }

        private void HandleContinue()
        {
            if (!string.IsNullOrEmpty(_continueSlot)) LoadSlot(_continueSlot);
        }

        private void StartNewGame(Game.Player.OriginSO origin)
        {
            if (_starting) return;
            GameSession.SetOrigin(origin != null ? origin.originId : null);
            GameLog.Info(TAG, $"Origin: {(origin != null ? origin.displayName : "default")}");
            LoadCore(null);
        }

        private void LoadSlot(string slotId) => LoadCore(slotId);

        private void LoadCore(string slotId)
        {
            if (_starting) return; // a double click must not queue two loads
            _starting = true;
            GameSession.RequestLoad(slotId);
            GameLog.Info(TAG, slotId == null ? "New Game" : $"Loading save '{slotId}'");
            SceneManager.LoadScene(GameConstants.CORE_SCENE_NAME, LoadSceneMode.Single);
        }

        private void HandleCancel(InputAction.CallbackContext _)
        {
            if (_confirmDialog != null && _confirmDialog.IsOpen) _confirmDialog.Cancel();
            else if (_saveSlotList != null && _saveSlotList.gameObject.activeSelf) ShowMain();
            else if (_originPick != null && _originPick.gameObject.activeSelf) ShowMain();
        }
    }
}
