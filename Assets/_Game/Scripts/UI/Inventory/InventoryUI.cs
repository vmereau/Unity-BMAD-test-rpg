using Game.Core;
using Game.Economy;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class InventoryUI : MonoBehaviour, IScreenPanel, IItemSlotContainer
    {
        private const string TAG = "[InventoryUI]";

        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private GoldSystem _goldSystem;
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private GameObject _panelRoot;
[SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _playerTransform;

        [SerializeField] private GameObject _contextMenuPrefab;

        [SerializeField] private ItemDetailPanelUI _detailPanelUI;
        [SerializeField] private ActionBarUI _actionBarUI;
        [SerializeField] private GameEventSO_Int _onGoldChanged;
        [SerializeField] private GameEventSO_Int _onActionBarUsed;

        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private EquipmentUI _equipmentUI;
        [SerializeField] private GameEventSO_Void _onEquipmentChanged;

        private GameObject _activeContextMenu;
        private GameObject _contextMenuBlocker;
        private int _contextMenuSlotIndex = -1;

        private int _selectedSlotIndex = -1;
        private ItemSlotUI _selectedSlotUI = null;

        private void Awake()
        {
            // Close context menu on any click over the panel background (not a slot)
            var panelClickHandler = _panelRoot.AddComponent<AnyButtonClickListener>();
            panelClickHandler.callback = (_) => HideContextMenu();
        }

        private void OnEnable()
        {
            _onActionBarUsed?.AddListener(HandleActionBarUsed);
            _onEquipmentChanged?.AddListener(HandleEquipmentSlotsChange);
            _onGoldChanged?.AddListener(HandleGoldChanged);
            UpdateGoldDisplay();
        }

        private void OnDisable()
        {
            _onActionBarUsed?.RemoveListener(HandleActionBarUsed);
            _onEquipmentChanged?.RemoveListener(HandleEquipmentSlotsChange);
            _onGoldChanged?.RemoveListener(HandleGoldChanged);
        }

        private void HandleGoldChanged(int newGold) => UpdateGoldDisplay();

        public void OnScreenOpen()
        {
            RefreshSlots();
            _equipmentUI?.Refresh();
            _equipmentUI?.gameObject.SetActive(true);
            UpdateGoldDisplay();
            GameLog.Info(TAG, "Inventory opened");
        }

        public void OnScreenClose()
        {
            HideContextMenu();
            ClearSelection();
            _equipmentUI?.gameObject.SetActive(false);
            GameLog.Info(TAG, "Inventory closed");
        }

        private void UpdateGoldDisplay()
        {
            if (_goldText != null && _goldSystem != null)
                _goldText.text = $"{_goldSystem.Gold}g";
        }

        private void HandleActionBarUsed(int _) => RefreshSlots(false);
        private void HandleEquipmentSlotsChange(bool _) => RefreshSlots();

        public void DropItem(int slotIndex)
        {
            // Guard before removal — item would be lost if prefab is missing
            if (slotIndex < 0 || slotIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"Drop skipped: slot {slotIndex} out of range");
                return;
            }
            // Capture slot BEFORE decrement — index may become invalid after removal
            var slot = _inventorySystem.Items[slotIndex];
            var item = slot.Item;
            if (item.worldItemPrefab == null)
            {
                GameLog.Warn(TAG, $"Drop skipped: {item.itemName} has no worldItemPrefab");
                return;
            }

            _inventorySystem.DecrementStack(slotIndex);

            // Item prefabs are active with _item pre-baked; Instantiate triggers Awake immediately
            var dropPos = _playerTransform.position + _playerTransform.forward * 1.5f + Vector3.up * 0.5f;
            var go = Instantiate(item.worldItemPrefab, dropPos, Quaternion.identity);
            go.GetComponent<Rigidbody>().AddForce(_playerTransform.forward * 2f + Vector3.up * 1f, ForceMode.Impulse);
            RefreshSlots();
            GameLog.Info(TAG, $"Dropped: {item.itemName}");
        }

        public void UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"Use skipped: slot {slotIndex} out of range");
                return;
            }
            var slot = _inventorySystem.Items[slotIndex];
            var item = slot.Item;
            if (item is not UsableItemSO usable)
            {
                GameLog.Warn(TAG, $"Use skipped: {item.itemName} is not UsableItemSO");
                return;
            }

            bool used = usable.OnUse(_playerTransform.gameObject);

            if (used && usable.consumable)
            {
                _inventorySystem.DecrementStack(slotIndex);
                RefreshSlots();
                GameLog.Info(TAG, $"Consumed: {item.itemName}");
            }
        }

        public void PrimaryAction(int slotIndex, IItemSlotContainer source)
        {
            if (slotIndex < 0 || slotIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"PrimaryAction: slot {slotIndex} out of range");
                return;
            }
            var item = _inventorySystem.Items[slotIndex].Item;
            if (item == null) return;

            if (item is EquipableItemSO)
            {
                if (_equipmentSystem == null)
                {
                    GameLog.Warn(TAG, "PrimaryAction: _equipmentSystem is not assigned");
                    return;
                }
                _equipmentSystem.Equip(slotIndex);
                RefreshSlots();
            }
            else if (item is UsableItemSO)
            {
                UseItem(slotIndex);
            }
            else
            {
                GameLog.Info(TAG, $"No primary action for {item.itemName}");
            }
        }

        public void SwapSlots(int a, int b, IItemSlotContainer source)
        {
            _inventorySystem.MoveItem(a, b);
            RefreshSlots();
        }

        public void ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source)
        {
            // Same slot already showing — do nothing
            if (_activeContextMenu != null && _contextMenuSlotIndex == slotIndex)
                return;

            HideContextMenu();
            _contextMenuSlotIndex = slotIndex;

            // _canvas is the serialized canvas root; all context menu GOs are parented here
            var canvas = _canvas;

            // Blocker — full-screen transparent overlay positioned BELOW _panelRoot so that
            // clicks on inventory slots still reach their IPointerClickHandler.
            // Any click on the blocker (i.e. outside the inventory panel) dismisses the menu.
            // INVARIANT: _panelRoot must be a direct child of canvas.transform for the
            // sibling-index placement to produce the correct z-order.
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

            var item = _inventorySystem.Items[slotIndex].Item;

            // Wire drop button by name
            var dropBtn = _activeContextMenu.transform.Find("DropButton").GetComponent<Button>();
            dropBtn.onClick.AddListener(() => { DropItem(_contextMenuSlotIndex); HideContextMenu(); });

            // Use button — always present; enabled only for UsableItemSO
            var useBtn = _activeContextMenu.transform.Find("UseButton").GetComponent<Button>();
            if (item is UsableItemSO usable)
            {
                useBtn.gameObject.SetActive(true);
                useBtn.interactable = true;
                useBtn.onClick.AddListener(() => { UseItem(_contextMenuSlotIndex); HideContextMenu(); });
            }
            else
            {
                useBtn.gameObject.SetActive(false);
            }

            // Equip button — visible only when item is equippable and not yet equipped
            var equipBtnTransform = _activeContextMenu.transform.Find("EquipButton");
            if (_equipmentSystem != null)
            {
                if (equipBtnTransform == null)
                {
                    GameLog.Warn(TAG, "ShowContextMenu: 'EquipButton' not found in context menu prefab");
                }
                else
                {
                    var equipBtn = equipBtnTransform.GetComponent<Button>();
                    bool canEquip = _equipmentSystem.IsEquippable(item) && !_equipmentSystem.IsEquipped(item);
                    equipBtnTransform.gameObject.SetActive(canEquip);
                    if (canEquip)
                        equipBtn.onClick.AddListener(() => { _equipmentSystem.Equip(_contextMenuSlotIndex); RefreshSlots(); HideContextMenu(); });
                }
            }
        }

        public void HideContextMenu()
        {
            if (_activeContextMenu != null) { Destroy(_activeContextMenu); _activeContextMenu = null; }
            if (_contextMenuBlocker != null) { Destroy(_contextMenuBlocker); _contextMenuBlocker = null; }
            _contextMenuSlotIndex = -1;
        }

        public void SelectSlot(int slotIndex, IItemSlotContainer source)
        {
            if (slotIndex == _selectedSlotIndex) return;
            _selectedSlotUI?.SetSelected(false);
            var newSlot = _contentRoot.GetChild(slotIndex).GetComponent<ItemSlotUI>();
            newSlot.SetSelected(true);
            _selectedSlotIndex = slotIndex;
            _selectedSlotUI = newSlot;
            UpdateDetailPanel(_inventorySystem.Items[slotIndex].Item, slotIndex);
        }

        private void UpdateDetailPanel(ItemSO item, int slotIndex)
        {
            System.Action onUse = item is UsableItemSO ? () => UseItem(slotIndex) : (System.Action)null;
            _detailPanelUI.Show(item, () => DropItem(slotIndex), onUse);
        }

        private void ClearSelection()
        {
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotIndex = -1;
            _selectedSlotUI = null;
            _detailPanelUI?.Hide();
        }

        private void RefreshSlots(bool refreshActionBar = true)
        {
            // Null out stale UI reference before destroying slot GameObjects
            _selectedSlotUI = null;

            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            var items = _inventorySystem.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(_itemSlotPrefab, _contentRoot);
                var slotUI = go.GetComponent<ItemSlotUI>();
                slotUI.Bind(items[i].Item, i, items[i].Count);
            }

            // Restore selection if selected slot index is still valid after refresh
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < items.Count)
            {
                var slot = _contentRoot.GetChild(_selectedSlotIndex).GetComponent<ItemSlotUI>();
                _selectedSlotUI = slot;
                slot.SetSelected(true);
                UpdateDetailPanel(items[_selectedSlotIndex].Item, _selectedSlotIndex);
            }
            else
            {
                _selectedSlotIndex = -1;
                _detailPanelUI?.Hide();
            }

            // refresh action bar if any item was binded to it.
            if (!refreshActionBar) return;
            _actionBarUI?.Refresh();
            }
            }
            }
