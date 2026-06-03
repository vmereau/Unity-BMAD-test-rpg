using Game.Core;
using Game.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class ContainerUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[ContainerUI]";

        [SerializeField] private InventorySystem _playerInventory;
        private InventorySystem _containerInventory;
        private bool _takeOnly;

        [SerializeField] private Transform _containerContentRoot;
        [SerializeField] private Transform _playerContentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;

        [SerializeField] private ItemDetailPanelUI _detailPanelUI;
        [SerializeField] private ContainerDetailActions _containerActions;

        [SerializeField] private GameObject _contextMenuPrefab;
        [SerializeField] private Canvas _canvas;

        private GameObject _activeContextMenu;
        private GameObject _contextMenuBlocker;
        private int _contextMenuSlotIndex = -1;

        private ItemSlotUI _selectedSlotUI;
        private ContainerSide _selectedSide;
        private InputSystem_Actions _input;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _input = new InputSystem_Actions();
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

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (gameObject.activeInHierarchy)
                Close();
        }

        public void Open(InventorySystem containerInventory, bool takeOnly = false)
        {
            _containerInventory = containerInventory;
            _takeOnly = takeOnly;
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

            UpdateDetailPanel(inv.Items[index].Item, index, side);
        }

        public void OnSlotRightClicked(int index, Vector2 pos, ContainerSide side)
        {
            ShowContextMenu(index, pos, side);
        }

        public void OnSlotDoubleClicked(int index, ContainerSide side)
        {
            if (side == ContainerSide.Container) TakeItem(index);
            else if (!_takeOnly) PutItem(index);
        }

        public void TakeItem(int index)
        {
            if (_containerInventory == null || index < 0 || index >= _containerInventory.Count) return;
            ItemSO item = _containerInventory.Items[index].Item;
            _containerInventory.DecrementStack(index);
            _playerInventory.AddItem(item);
            RefreshGrids();
            RefreshDetailPanelAfterAction(item, index, ContainerSide.Container);
            GameLog.Info(TAG, $"Took {item.itemName} from container");
        }

        public void PutItem(int index)
        {
            if (_takeOnly) return; // loot is take-only
            if (_playerInventory == null || index < 0 || index >= _playerInventory.Count) return;
            ItemSO item = _playerInventory.Items[index].Item;
            _playerInventory.DecrementStack(index);
            _containerInventory.AddItem(item);
            RefreshGrids();
            RefreshDetailPanelAfterAction(item, index, ContainerSide.Player);
            GameLog.Info(TAG, $"Put {item.itemName} into container");
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

            if (side == ContainerSide.Container)
            {
                if (takeBtn != null)
                {
                    takeBtn.gameObject.SetActive(true);
                    takeBtn.onClick.AddListener(() => { TakeItem(_contextMenuSlotIndex); HideContextMenu(); });
                }
                putBtn?.gameObject.SetActive(false);
            }
            else
            {
                if (putBtn != null)
                {
                    putBtn.gameObject.SetActive(true);
                    putBtn.onClick.AddListener(() => { PutItem(_contextMenuSlotIndex); HideContextMenu(); });
                }
                takeBtn?.gameObject.SetActive(false);
            }
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
            RefreshGrid(_containerContentRoot, _containerInventory);
            RefreshGrid(_playerContentRoot, _playerInventory);
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
            _detailPanelUI?.Hide();
        }

        private void UpdateDetailPanel(ItemSO item, int slotIndex, ContainerSide side)
        {
            _detailPanelUI?.Show(item);
            _containerActions?.Bind(this, slotIndex, item, side, _takeOnly);
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
