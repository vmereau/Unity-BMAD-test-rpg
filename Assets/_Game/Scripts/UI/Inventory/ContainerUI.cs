using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class ContainerUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[ContainerUI]";

        [SerializeField] private InventorySystem _playerInventory;
        [SerializeField] private GameEventSO_ContainerItemTaken _onItemTaken;
        private InventorySystem _containerInventory;
        private bool _takeOnly;

        [SerializeField] private Transform _containerContentRoot;
        [SerializeField] private Transform _playerContentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;

        [SerializeField] private ItemDetailPanelUI _detailPanelUI;
        [SerializeField] private ContainerDetailActions _containerActions;
        [Tooltip("Container pane button: takes every unit, then closes the window (also R).")]
        [SerializeField] private Button _takeAllButton;

        [SerializeField] private GameObject _contextMenuPrefab;
        [SerializeField] private Canvas _canvas;

        // Closes on death: an open loot window would block loading and its Esc would re-lock the death screen's cursor.
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        [Tooltip("Player knocked out (OnPlayerKnockedOut): closes like a death.")]
        [SerializeField] private GameEventSO_Void _onPlayerKnockedOut;
        // Closes when the looted body stops being lootable (a knocked-out NPC getting up) — matched by inventory.
        [SerializeField] private GameEventSO_Inventory _onLootSourceLost;

        private GameObject _activeContextMenu;
        private GameObject _contextMenuBlocker;
        private int _contextMenuSlotIndex = -1;

        private ItemSlotUI _selectedSlotUI;
        private ContainerSide _selectedSide;
        private int _selectedIndex = -1;
        private bool _bulkHeld; // Shift: actions move up to BulkTransferRules.BulkSize units
        private InputSystem_Actions _input;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _input = new InputSystem_Actions();
            if (_takeAllButton != null) _takeAllButton.onClick.AddListener(TakeAll);
            else GameLog.Warn(TAG, "Take All button is not assigned");
        }

        // Shift / R are polled (not callbacks) so the Take All close runs after the Player map's R (DrawWeapon)
        // callback already saw an unlocked cursor (PlayerStateManager.IsBusy) and refused to draw.
        private void Update()
        {
            if (!IsOpen || _input == null) return;
            bool bulk = _input.UI.BulkModifier.IsPressed();
            if (bulk != _bulkHeld)
            {
                _bulkHeld = bulk;
                RepaintSelection();
            }
            if (_input.UI.TakeAll.WasPressedThisFrame()) TakeAll();
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _onPlayerDied?.AddListener(HandlePlayerDied);
            _onPlayerKnockedOut?.AddListener(HandlePlayerDied);
            _onLootSourceLost?.AddListener(HandleLootSourceLost);
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
            _onPlayerKnockedOut?.RemoveListener(HandlePlayerDied);
            _onLootSourceLost?.RemoveListener(HandleLootSourceLost);
        }

        private void OnDestroy()
        {
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

        private void HandleLootSourceLost(InventorySystem source)
        {
            if (IsOpen && source != null && source == _containerInventory)
            {
                GameLog.Info(TAG, "Looted body got up — closing loot window");
                Close();
            }
        }

        public void Open(InventorySystem containerInventory, bool takeOnly = false)
        {
            _containerInventory = containerInventory;
            _takeOnly = takeOnly;
            gameObject.SetActive(true); // enables the UI map (OnEnable)
            _bulkHeld = _input != null && _input.UI.BulkModifier.IsPressed();
            OnScreenOpen();
        }

        public void Close()
        {
            OnScreenClose();
            gameObject.SetActive(false);
        }

        public void OnScreenOpen()
        {
            RefreshGrids();
            _detailPanelUI?.Hide();
            CursorManager.Unlock();
            IsOpen = true;
            GameLog.Info(TAG, "Container UI opened");
        }

        public void OnScreenClose()
        {
            HideContextMenu();
            ClearSelection();
            CursorManager.Lock();
            IsOpen = false;
            GameLog.Info(TAG, "Container UI closed");
        }

        public void OnSlotSelected(int index, ContainerSide side)
        {
            InventorySystem inv = (side == ContainerSide.Container) ? _containerInventory : _playerInventory;
            if (inv == null || index < 0 || index >= inv.Count) return;

            Transform root = (side == ContainerSide.Container) ? _containerContentRoot : _playerContentRoot;
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotUI = root.GetChild(index).GetComponent<ItemSlotUI>();
            _selectedSlotUI?.SetSelected(true);
            _selectedSide = side;
            _selectedIndex = index;

            UpdateDetailPanel(inv.Items[index].Item, index, side);
        }

        public void OnSlotRightClicked(int index, Vector2 pos, ContainerSide side)
        {
            ShowContextMenu(index, pos, side);
        }

        public void OnSlotDoubleClicked(int index, ContainerSide side)
        {
            if (side == ContainerSide.Container) TakeBatch(index);
            else if (!_takeOnly) PutBatch(index);
        }

        /// <summary>Units one action on this slot moves right now: 1, or up to 10 while Shift is held.</summary>
        public int BatchSize(int index, ContainerSide side)
        {
            InventorySystem inv = (side == ContainerSide.Container) ? _containerInventory : _playerInventory;
            if (inv == null || index < 0 || index >= inv.Count) return 0;
            return BulkTransferRules.Quantity(_bulkHeld, inv.Items[index].Count, 0, 0);
        }

        public void TakeBatch(int index) => TakeItems(index, BatchSize(index, ContainerSide.Container));
        public void PutBatch(int index) => PutItems(index, BatchSize(index, ContainerSide.Player));

        public void TakeItem(int index) => TakeItems(index, 1);
        public void PutItem(int index) => PutItems(index, 1);

        /// <summary>Takes up to <paramref name="quantity"/> units of the slot's item, one unit at a time.</summary>
        public void TakeItems(int index, int quantity)
        {
            if (_containerInventory == null || _playerInventory == null ||
                _containerInventory == _playerInventory || index < 0 || index >= _containerInventory.Count) return;
            ItemSO item = _containerInventory.Items[index].Item;
            if (item == null) return;
            int taken = 0;
            while (taken < quantity && index < _containerInventory.Count &&
                   _containerInventory.Items[index].Item == item && TakeUnit(index, item))
                taken++;
            if (taken == 0) return;
            RefreshGrids();
            RefreshDetailPanelAfterAction(item, index, ContainerSide.Container);
            GameLog.Info(TAG, $"Took {taken} x {item.itemName} from container");
        }

        /// <summary>Moves every unit to the player (R / Take All button), then closes once the container is empty.</summary>
        public void TakeAll()
        {
            if (!IsOpen || _containerInventory == null || _playerInventory == null ||
                _containerInventory == _playerInventory || _containerInventory.Count == 0) return;
            HideContextMenu();
            int taken = 0;
            int i = 0;
            while (IsOpen && i < _containerInventory.Count)
            {
                ItemSO item = _containerInventory.Items[i].Item;
                int units = _containerInventory.Items[i].Count;
                int moved = 0;
                while (IsOpen && moved < units && item != null && TakeUnit(i, item)) moved++;
                taken += moved;
                // A refused unit (or null item / empty slot) stays: move on to the next slot.
                if (units <= 0 || moved < units) i++;
            }
            if (!IsOpen) return; // a take reaction (quest, loot source lost) already closed the window
            GameLog.Info(TAG, $"Take All: {taken} unit(s) taken, {_containerInventory.Count} slot(s) left");
            if (_containerInventory.Count == 0)
            {
                Close();
                return;
            }
            RefreshGrids();
            ClearSelection();
        }

        // One unit: add to the player first, remove from the container only once accepted.
        private bool TakeUnit(int index, ItemSO item)
        {
            if (!_playerInventory.AddItem(item)) return false;
            _containerInventory.DecrementStack(index);
            _onItemTaken?.Raise(new ContainerItemTakenData { sourceInventory = _containerInventory, item = item });
            return true;
        }

        /// <summary>Puts up to <paramref name="quantity"/> units of the player slot's item into the container.</summary>
        public void PutItems(int index, int quantity)
        {
            if (_takeOnly) return; // loot is take-only
            if (_playerInventory == null || _containerInventory == null ||
                index < 0 || index >= _playerInventory.Count) return;
            ItemSO item = _playerInventory.Items[index].Item;
            if (!ItemSO.CanPlayerPart(item))
            {
                GameLog.Warn(TAG, $"PutItem blocked: {item?.itemName} is a quest item");
                return;
            }
            int put = 0;
            while (put < quantity && index < _playerInventory.Count && _playerInventory.Items[index].Item == item)
            {
                _playerInventory.DecrementStack(index);
                _containerInventory.AddItem(item);
                put++;
            }
            if (put == 0) return;
            RefreshGrids();
            RefreshDetailPanelAfterAction(item, index, ContainerSide.Player);
            GameLog.Info(TAG, $"Put {put} x {item.itemName} into container");
        }

        private void ShowContextMenu(int slotIndex, Vector2 screenPos, ContainerSide side)
        {
            HideContextMenu();
            _contextMenuSlotIndex = slotIndex;

            if (side == ContainerSide.Player && _takeOnly) return; // loot: cannot deposit into a corpse

            InventorySystem inv = (side == ContainerSide.Container) ? _containerInventory : _playerInventory;
            if (inv == null || slotIndex < 0 || slotIndex >= inv.Count) return;

            _contextMenuBlocker = new GameObject("ContextMenuBlocker");
            _contextMenuBlocker.transform.SetParent(_canvas.transform, false);
            var blockerImg = _contextMenuBlocker.AddComponent<Image>();
            blockerImg.color = Color.clear;
            blockerImg.raycastTarget = true;
            var blockerRect = _contextMenuBlocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.sizeDelta = Vector2.zero;
            _contextMenuBlocker.AddComponent<AnyButtonClickListener>().callback = (_) => HideContextMenu();

            _activeContextMenu = Instantiate(_contextMenuPrefab, _canvas.transform);
            _activeContextMenu.transform.SetAsLastSibling();
            var rt = _activeContextMenu.GetComponent<RectTransform>();
            rt.position = screenPos;

            var takeBtn = _activeContextMenu.transform.Find("TakeButton")?.GetComponent<Button>();
            var putBtn = _activeContextMenu.transform.Find("PutButton")?.GetComponent<Button>();

            if (takeBtn == null) GameLog.Warn(TAG, "ShowContextMenu: 'TakeButton' not found in context menu prefab");
            if (putBtn == null) GameLog.Warn(TAG, "ShowContextMenu: 'PutButton' not found in context menu prefab");

            int qty = BatchSize(slotIndex, side); // captured at open: the label is what the click does
            if (side == ContainerSide.Container)
            {
                if (takeBtn != null)
                {
                    takeBtn.gameObject.SetActive(true);
                    SetButtonLabel(takeBtn, BulkTransferRules.ActionLabel("Take", qty));
                    takeBtn.onClick.AddListener(() => { TakeItems(_contextMenuSlotIndex, qty); HideContextMenu(); });
                }
                putBtn?.gameObject.SetActive(false);
            }
            else
            {
                if (putBtn != null && !ItemSO.CanPlayerPart(inv.Items[slotIndex].Item))
                    putBtn.gameObject.SetActive(false); // quest items stay with the player
                else if (putBtn != null)
                {
                    putBtn.gameObject.SetActive(true);
                    SetButtonLabel(putBtn, BulkTransferRules.ActionLabel("Put", qty));
                    putBtn.onClick.AddListener(() => { PutItems(_contextMenuSlotIndex, qty); HideContextMenu(); });
                }
                takeBtn?.gameObject.SetActive(false);
            }
        }

        private static void SetButtonLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        private void HideContextMenu()
        {
            if (_activeContextMenu != null) { Destroy(_activeContextMenu); _activeContextMenu = null; }
            if (_contextMenuBlocker != null) { Destroy(_contextMenuBlocker); _contextMenuBlocker = null; }
            _contextMenuSlotIndex = -1;
        }

        private void RefreshGrids()
        {
            _selectedSlotUI = null;
            _selectedIndex = -1;
            RefreshGrid(_containerContentRoot, _containerInventory);
            RefreshGrid(_playerContentRoot, _playerInventory);
            if (_takeAllButton != null)
                _takeAllButton.interactable = _containerInventory != null && _containerInventory.Count > 0;
        }

        private void RefreshGrid(Transform root, InventorySystem inv)
        {
            if (root == null) return;

            foreach (Transform child in root)
                Destroy(child.gameObject);

            if (inv == null) return;

            var items = inv.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(_itemSlotPrefab, root);
                var slotUI = go.GetComponent<ItemSlotUI>();
                slotUI.Bind(items[i].Item, i, items[i].Count);
            }
        }

        private void ClearSelection()
        {
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotUI = null;
            _selectedIndex = -1;
            _detailPanelUI?.Hide();
        }

        private void UpdateDetailPanel(ItemSO item, int slotIndex, ContainerSide side)
        {
            _detailPanelUI?.Show(item);
            _containerActions?.Bind(this, slotIndex, item, side, _takeOnly, BatchSize(slotIndex, side));
        }

        // Shift pressed / released: repaint the selected slot's button labels.
        private void RepaintSelection()
        {
            if (_selectedIndex < 0) return;
            InventorySystem inv = (_selectedSide == ContainerSide.Container) ? _containerInventory : _playerInventory;
            if (inv == null || _selectedIndex >= inv.Count) return;
            UpdateDetailPanel(inv.Items[_selectedIndex].Item, _selectedIndex, _selectedSide);
        }

        private void RefreshDetailPanelAfterAction(ItemSO item, int index, ContainerSide side)
        {
            InventorySystem inv = (side == ContainerSide.Container) ? _containerInventory : _playerInventory;
            if (inv != null && index < inv.Count && inv.Items[index].Item == item)
                OnSlotSelected(index, side);
            else
                ClearSelection();
        }
    }
}
