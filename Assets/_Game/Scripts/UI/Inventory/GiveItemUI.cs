using System;
using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One-item Give window opened over the dialogue by a <c>ChoiceOption.requiredCategory</c> choice
    /// (<c>DialogueSystem.SelectChoice</c>). Lists only the player's items matching the category; the selected item is
    /// shown in the detail panel (the give slot) and handed over on Give, through the callback. Cancel / Escape closes
    /// the window with nothing given, back to the dialogue's choices. Not an <see cref="IScreenPanel"/>: the dialogue
    /// stays open behind it and owns the cursor, so this window never locks or unlocks it.
    /// </summary>
    public class GiveItemUI : MonoBehaviour, IItemSlotContainer
    {
        private const string TAG = "[GiveItemUI]";

        [Header("Grid")]
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;

        [Header("Give slot")]
        [SerializeField] private ItemDetailPanelUI _detailPanelUI;
        [Tooltip("Shown in place of the detail panel while nothing is selected.")]
        [SerializeField] private TMP_Text _emptyHintText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _giveButton;
        [SerializeField] private Button _cancelButton;

        private InventorySystem _playerInventory;
        private GiveItemCategory _category;
        private Action<ItemSO> _onGive;
        private ItemSO _selectedItem;
        private ItemSlotUI _selectedSlotUI;
        private InputSystem_Actions _input;
        private int _closedFrame = -1;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// Open, or closed during this frame: the Escape that closed the window may still reach other Cancel listeners
        /// (DialogueUI) in the same frame, in any order.
        /// </summary>
        public bool IsOpenOrJustClosed => IsOpen || _closedFrame == Time.frameCount;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            if (_giveButton != null) _giveButton.onClick.AddListener(Give);
            else GameLog.Warn(TAG, "_giveButton not assigned — the window can only be cancelled");
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void HandleCancel(InputAction.CallbackContext _) => Close();

        /// <summary>Opens the window on <paramref name="inventory"/>'s items of <paramref name="category"/>.</summary>
        public void Open(string npcName, GiveItemCategory category, InventorySystem inventory, Action<ItemSO> onGive)
        {
            if (inventory == null)
            {
                GameLog.Error(TAG, "Open: no player inventory — window not opened");
                return;
            }
            _playerInventory = inventory;
            _category = category;
            _onGive = onGive;
            if (_titleText != null)
                _titleText.text = string.IsNullOrEmpty(npcName) ? "Give" : $"Give to {npcName}";
            gameObject.SetActive(true);
            RefreshGrid();
            ClearSelection();
            GameLog.Info(TAG, $"Opened for {category}");
        }

        /// <summary>Closes the window with nothing given.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            ClearSelection();
            _onGive = null;
            _closedFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        private void Give()
        {
            if (_selectedItem == null) return;
            ItemSO item = _selectedItem;
            Action<ItemSO> onGive = _onGive;
            Close();
            onGive?.Invoke(item);
        }

        private void RefreshGrid()
        {
            if (_contentRoot == null || _itemSlotPrefab == null)
            {
                GameLog.Warn(TAG, "RefreshGrid: _contentRoot or _itemSlotPrefab not assigned");
                return;
            }
            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);
            _selectedSlotUI = null;

            var items = _playerInventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (!GiveItemRules.Matches(items[i].Item, _category)) continue;
                var slotUI = Instantiate(_itemSlotPrefab, _contentRoot).GetComponent<ItemSlotUI>();
                slotUI?.Bind(items[i].Item, i, items[i].Count);
            }
        }

        private void ClearSelection()
        {
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotUI = null;
            _selectedItem = null;
            _detailPanelUI?.Hide();
            if (_emptyHintText != null) _emptyHintText.gameObject.SetActive(true);
            if (_giveButton != null) _giveButton.interactable = false;
        }

        // ── IItemSlotContainer (slot index = inventory index) ─────────────────
        public void SelectSlot(int slotIndex, IItemSlotContainer source)
        {
            if (_playerInventory == null || _contentRoot == null || slotIndex < 0 || slotIndex >= _playerInventory.Count) return;
            ItemSO item = _playerInventory.Items[slotIndex].Item;

            ItemSlotUI clicked = null;
            foreach (Transform child in _contentRoot)
            {
                var slot = child.GetComponent<ItemSlotUI>();
                if (slot != null && slot.SlotIndex == slotIndex) { clicked = slot; break; }
            }
            // The inventory changed since the grid was built (indexes shifted): rebuild instead of giving the wrong item.
            if (clicked == null || clicked.Item != item || !GiveItemRules.Matches(item, _category))
            {
                RefreshGrid();
                ClearSelection();
                return;
            }

            _selectedSlotUI?.SetSelected(false);
            _selectedSlotUI = clicked;
            _selectedSlotUI.SetSelected(true);
            _selectedItem = item;
            _detailPanelUI?.Show(item, ItemPriceContext.None);
            if (_emptyHintText != null) _emptyHintText.gameObject.SetActive(false);
            if (_giveButton != null) _giveButton.interactable = true;
        }

        // Double-click only selects: handing an item over always needs the Give click.
        public void PrimaryAction(int slotIndex, IItemSlotContainer source) => SelectSlot(slotIndex, source);

        public void ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source) => SelectSlot(slotIndex, source);

        public void SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source) { }
    }
}
