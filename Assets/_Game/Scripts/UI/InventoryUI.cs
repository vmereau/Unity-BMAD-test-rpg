using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class InventoryUI : MonoBehaviour
    {
        private const string TAG = "[InventoryUI]";

        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _playerTransform;

        [SerializeField] private GameObject _contextMenuPrefab;

        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailDescription;

        private bool _isOpen = false;
        private InputSystem_Actions _input;

        private GameObject _activeContextMenu;
        private GameObject _contextMenuBlocker;
        private int _contextMenuSlotIndex = -1;

        private int _selectedSlotIndex = -1;
        private ItemSlotUI _selectedSlotUI = null;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.UI.Enable();

            // Close context menu on any click over the panel background (not a slot)
            var panelClickHandler = _panelRoot.AddComponent<AnyButtonClickListener>();
            panelClickHandler.callback = (_) => HideContextMenu();
        }

        private void OnEnable()
        {
            _input.Player.InventoryToggle.performed += HandleToggle;
            _input.UI.Cancel.performed += HandleClose;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.InventoryToggle.performed -= HandleToggle;
            _input.UI.Cancel.performed -= HandleClose;
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void HandleToggle(InputAction.CallbackContext ctx)
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void HandleClose(InputAction.CallbackContext ctx)
        {
            if (_isOpen)
                Close();
        }

        private void Open()
        {
            RefreshSlots();
            _panelRoot.SetActive(true);
            _isOpen = true;
            CursorManager.Unlock();
            GameLog.Info(TAG, "Inventory opened");
        }

        private void Close()
        {
            HideContextMenu();
            ClearSelection();
            _panelRoot.SetActive(false);
            _isOpen = false;
            CursorManager.Lock();
            GameLog.Info(TAG, "Inventory closed");
        }

        public void DropItem(int slotIndex)
        {
            // Guard before removal — item would be lost if prefab is missing
            if (slotIndex < 0 || slotIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"Drop skipped: slot {slotIndex} out of range");
                return;
            }
            var item = _inventorySystem.Items[slotIndex];
            if (item.worldItemPrefab == null)
            {
                GameLog.Warn(TAG, $"Drop skipped: {item.itemName} has no worldItemPrefab");
                return;
            }

            _inventorySystem.RemoveItem(slotIndex);

            // Item prefabs are active with _item pre-baked; Instantiate triggers Awake immediately
            var dropPos = _playerTransform.position + _playerTransform.forward * 1.5f + Vector3.up * 0.5f;
            var go = Instantiate(item.worldItemPrefab, dropPos, Quaternion.identity);
            go.GetComponent<Rigidbody>().AddForce(_playerTransform.forward * 2f + Vector3.up * 1f, ForceMode.Impulse);
            RefreshSlots();
            GameLog.Info(TAG, $"Dropped: {item.itemName}");
        }

        public void SwapSlots(int a, int b)
        {
            _inventorySystem.MoveItem(a, b);
            RefreshSlots();
        }

        public void ShowContextMenu(int slotIndex, Vector2 screenPos)
        {
            // Same slot already showing — do nothing
            if (_activeContextMenu != null && _contextMenuSlotIndex == slotIndex)
                return;

            HideContextMenu();
            _contextMenuSlotIndex = slotIndex;

            var canvas = GetComponentInParent<Canvas>();

            // Blocker — full-screen transparent overlay positioned BELOW _panelRoot so that
            // clicks on inventory slots still reach their IPointerClickHandler.
            // Any click on the blocker (i.e. outside the inventory panel) dismisses the menu.
            _contextMenuBlocker = new GameObject("ContextMenuBlocker");
            _contextMenuBlocker.transform.SetParent(canvas.transform, false);
            var blockerImg = _contextMenuBlocker.AddComponent<Image>();
            blockerImg.color = Color.clear;
            blockerImg.raycastTarget = true;
            var blockerRect = _contextMenuBlocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.sizeDelta = Vector2.zero;
            _contextMenuBlocker.transform.SetSiblingIndex(_panelRoot.transform.GetSiblingIndex());
            var blockerListener = _contextMenuBlocker.AddComponent<AnyButtonClickListener>();
            blockerListener.callback = (_) => HideContextMenu();

            // Menu panel (topmost)
            _activeContextMenu = Instantiate(_contextMenuPrefab, canvas.transform);
            _activeContextMenu.transform.SetAsLastSibling();

            // Position + clamp to screen bounds
            var rt = _activeContextMenu.GetComponent<RectTransform>();
            rt.position = screenPos;
            var pos = rt.position;
            pos.x = Mathf.Clamp(pos.x, 0, Screen.width - rt.rect.width);
            pos.y = Mathf.Clamp(pos.y, rt.rect.height, Screen.height);
            rt.position = pos;

            // Wire drop item button at runtime
            var btn = _activeContextMenu.GetComponentInChildren<Button>();
            btn.onClick.AddListener(() => { DropItem(_contextMenuSlotIndex); HideContextMenu(); });
        }

        public void HideContextMenu()
        {
            if (_activeContextMenu != null) { Destroy(_activeContextMenu); _activeContextMenu = null; }
            if (_contextMenuBlocker != null) { Destroy(_contextMenuBlocker); _contextMenuBlocker = null; }
            _contextMenuSlotIndex = -1;
        }

        public void SelectSlot(int slotIndex)
        {
            _selectedSlotUI?.SetSelected(false);
            var newSlot = _contentRoot.GetChild(slotIndex).GetComponent<ItemSlotUI>();
            newSlot.SetSelected(true);
            _selectedSlotIndex = slotIndex;
            _selectedSlotUI = newSlot;
            UpdateDetailPanel(_inventorySystem.Items[slotIndex]);
        }

        private void UpdateDetailPanel(ItemSO item)
        {
            _detailIcon.sprite = item.icon;
            _detailIcon.color = item.icon != null ? Color.white : Color.gray;
            _detailName.text = item.itemName;
            _detailDescription.text = item.description;
            _detailPanel.SetActive(true);
        }

        private void ClearSelection()
        {
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotIndex = -1;
            _selectedSlotUI = null;
            _detailPanel?.SetActive(false);
        }

        private void RefreshSlots()
        {
            // Null out stale UI reference before destroying slot GameObjects
            _selectedSlotUI = null;

            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            var items = _inventorySystem.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(_itemSlotPrefab, _contentRoot);
                var slot = go.GetComponent<ItemSlotUI>();
                slot.Bind(items[i], i);
            }

            // Restore selection if selected slot index is still valid after refresh
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < items.Count)
            {
                var slot = _contentRoot.GetChild(_selectedSlotIndex).GetComponent<ItemSlotUI>();
                _selectedSlotUI = slot;
                slot.SetSelected(true);
                UpdateDetailPanel(items[_selectedSlotIndex]);
            }
            else
            {
                _selectedSlotIndex = -1;
                _detailPanel?.SetActive(false);
            }
        }
    }

    /// <summary>Catches any pointer button click and forwards it to a callback.</summary>
    internal class AnyButtonClickListener : MonoBehaviour, IPointerClickHandler
    {
        public System.Action<PointerEventData.InputButton> callback;
        public void OnPointerClick(PointerEventData eventData) => callback?.Invoke(eventData.button);
    }
}
