using Game.Core;
using Game.Crafting;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Cooking screen opened by <see cref="CookingSystem"/>: player inventory grid + one ingredient slot (a reference,
    /// set by drag &amp; drop or double-click), recipe output preview and a Cook button (1 input → 1 output).
    /// Same lifecycle as <see cref="ContainerUI"/>: Esc / player death closes it.
    /// </summary>
    public class CookingUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[CookingUI]";
        private const string EMPTY_HINT = "Place an ingredient";
        private const string NO_RECIPE_HINT = "No recipe";

        [SerializeField] private InventorySystem _playerInventory;
        [SerializeField] private Transform _playerContentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;

        [Header("Station")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private CookingIngredientSlotUI _ingredientSlot;
        [SerializeField] private Image _outputIcon;
        [SerializeField] private TMP_Text _outputText;
        [SerializeField] private Button _cookButton;

        // Closes on death: an open screen would block loading and its Esc would re-lock the death screen's cursor.
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        // Action bar hotkeys stay live while the screen is open (and Cook must refresh the HUD bar): both sides use this channel.
        [SerializeField] private GameEventSO_Int _onActionBarUsed;

        private RecipeSO[] _recipes;
        private ItemSO _ingredient;
        private RecipeSO _matchedRecipe;
        private InputSystem_Actions _input;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _input = new InputSystem_Actions();
            _cookButton?.onClick.AddListener(Cook);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _onPlayerDied?.AddListener(HandlePlayerDied);
            _onActionBarUsed?.AddListener(HandleActionBarUsed);
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
            _onActionBarUsed?.RemoveListener(HandleActionBarUsed);
        }

        private void OnDestroy()
        {
            _cookButton?.onClick.RemoveListener(Cook);
            _input?.Dispose();
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (gameObject.activeInHierarchy)
                Close();
        }

        private void HandlePlayerDied(bool _)
        {
            if (IsOpen) Close();
        }

        private void HandleActionBarUsed(int _)
        {
            if (IsOpen) Refresh(); // a hotkey consumed an item → grid and ingredient count follow
        }

        public void Open(RecipeSO[] recipes, string stationName)
        {
            _recipes = recipes;
            if (_titleText != null) _titleText.text = stationName;
            gameObject.SetActive(true);
            OnScreenOpen();
        }

        public void Close()
        {
            OnScreenClose();
            gameObject.SetActive(false);
        }

        public void OnScreenOpen()
        {
            _ingredient = null; // the slot is a reference — always empty on open
            Refresh();
            CursorManager.Unlock();
            IsOpen = true;
            GameLog.Info(TAG, "Cooking UI opened");
        }

        public void OnScreenClose()
        {
            _ingredient = null;
            CursorManager.Lock();
            IsOpen = false;
            GameLog.Info(TAG, "Cooking UI closed");
        }

        public void SetIngredient(ItemSO item)
        {
            if (item == null) return;
            _ingredient = item;
            RefreshStation();
        }

        public void SetIngredientFromInventory(int index)
        {
            // Read the item the player sees on the clicked slot, not Items[index] — the grid may lag the inventory.
            if (_playerContentRoot == null || index < 0 || index >= _playerContentRoot.childCount) return;
            var slot = _playerContentRoot.GetChild(index).GetComponent<ItemSlotUI>();
            if (slot != null) SetIngredient(slot.Item);
        }

        public void ClearIngredient()
        {
            _ingredient = null;
            RefreshStation();
        }

        public void Cook()
        {
            if (_playerInventory == null) return;
            EventSystem.current?.SetSelectedGameObject(null); // Submit (Enter / Space) must not repeat the cook
            CookingState state = CookingRules.Evaluate(_recipes, _ingredient, _playerInventory.CountOf(_ingredient), out RecipeSO recipe);
            if (state != CookingState.Ready) return;

            if (!_playerInventory.TryConsume(recipe.input))
            {
                GameLog.Warn(TAG, $"Could not consume {recipe.input.itemName} — nothing cooked");
                return;
            }
            if (!_playerInventory.AddItem(recipe.output))
            {
                _playerInventory.AddItem(recipe.input); // refund so the ingredient isn't lost
                GameLog.Warn(TAG, $"Could not add {recipe.output.itemName} — {recipe.input.itemName} refunded");
                Refresh();
                return;
            }

            GameLog.Info(TAG, $"Cooked {recipe.input.itemName} → {recipe.output.itemName}");
            // Inventory slots changed → the HUD action bar re-validates its bindings; our own listener refreshes this screen.
            if (_onActionBarUsed != null) _onActionBarUsed.Raise(ActionBarSystem.REFRESH_ONLY_SLOT);
            else Refresh();
        }

        private void Refresh()
        {
            RefreshGrid();
            RefreshStation();
        }

        private void RefreshGrid()
        {
            if (_playerContentRoot == null) return;

            foreach (Transform child in _playerContentRoot)
                Destroy(child.gameObject);

            if (_playerInventory == null || _itemSlotPrefab == null) return;

            var items = _playerInventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(_itemSlotPrefab, _playerContentRoot);
                go.GetComponent<ItemSlotUI>()?.Bind(items[i].Item, i, items[i].Count);
            }
        }

        private void RefreshStation()
        {
            int count = _playerInventory != null && _ingredient != null ? _playerInventory.CountOf(_ingredient) : 0;
            if (count <= 0) _ingredient = null; // last one cooked (or gone) → slot clears

            CookingState state = CookingRules.Evaluate(_recipes, _ingredient, count, out _matchedRecipe);
            _ingredientSlot?.Bind(_ingredient, count);

            ItemSO output = state == CookingState.Ready ? _matchedRecipe.output : null;
            if (_outputIcon != null)
            {
                _outputIcon.sprite = output != null ? output.icon : null;
                _outputIcon.color = output != null && output.icon == null ? Color.gray : Color.white;
                _outputIcon.enabled = output != null;
            }
            if (_outputText != null)
            {
                _outputText.text = state switch
                {
                    CookingState.Ready => output.itemName,
                    CookingState.NoRecipe => NO_RECIPE_HINT,
                    _ => EMPTY_HINT
                };
            }
            if (_cookButton != null) _cookButton.interactable = state == CookingState.Ready;
        }
    }
}
