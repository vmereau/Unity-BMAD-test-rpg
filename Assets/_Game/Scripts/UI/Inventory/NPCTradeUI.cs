using Game.Core;
using Game.Economy;
using Game.Inventory;
using Game.World;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Manages the NPC shop interface. Handles buying and selling items between the player and NPC.
    /// </summary>
    public class NPCTradeUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[NPCTradeUI]";

        [Header("Inventories")]
        [SerializeField] private InventorySystem _playerInventory;
        private InventorySystem _npcInventory;

        [Header("Grids")]
        [SerializeField] private Transform _npcContentRoot;
        [SerializeField] private Transform _playerContentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;

        [Header("Detail Panel")]
        [SerializeField] private ItemDetailPanelUI _detailPanelUI;

        [Header("Context Menu")]
        [SerializeField] private GameObject _contextMenuPrefab;
        [SerializeField] private Canvas _canvas;

        [Header("Economy")]
        [SerializeField] private GoldSystem _goldSystem;
        [SerializeField] private GameEventSO_Int _onGoldChanged;
        [SerializeField] private TMP_Text _goldText;

        private GameObject _activeContextMenu;
        private GameObject _contextMenuBlocker;
        private int _contextMenuSlotIndex = -1;

        private InputSystem_Actions _input;

        private void Awake()
        {
            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _onGoldChanged?.AddListener(HandleGoldChanged);
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
            _onGoldChanged?.RemoveListener(HandleGoldChanged);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void HandleGoldChanged(int newGold) => UpdateGoldDisplay();

        private void HandleCancel(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (gameObject.activeInHierarchy)
                Close();
        }

        /// <summary>Opens the trade UI with the given NPC inventory.</summary>
        public void Open(InventorySystem npcInventory)
        {
            _npcInventory = npcInventory;
            gameObject.SetActive(true);
            OnScreenOpen();
        }

        public void Close()
        {
            OnScreenClose();
            gameObject.SetActive(false);
            
            // Exit dialogue if we are in one
            var dialogueSystem = FindFirstObjectByType<DialogueSystem>();
            if (dialogueSystem != null && dialogueSystem.IsOpen)
                dialogueSystem.Close();
        }

        public void OnScreenOpen()
        {
            RefreshGrids();
            _detailPanelUI?.Hide();
            UpdateGoldDisplay();
            CursorManager.Unlock();
            GameLog.Info(TAG, "Trade UI opened");
        }

        public void OnScreenClose()
        {
            HideContextMenu();
            ClearSelection();
            CursorManager.Lock();
            GameLog.Info(TAG, "Trade UI closed");
        }

        private void UpdateGoldDisplay()
        {
            if (_goldText != null && _goldSystem != null)
                _goldText.text = $"Gold: {_goldSystem.Gold}g";
        }

        public void OnSlotSelected(int index, TradeSide side)
        {
            InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
            if (inv != null && index >= 0 && index < inv.Count)
            {
                ItemSO item = inv.Items[index].Item;
                UpdateDetailPanel(item, side);
            }
        }

        public void OnSlotRightClicked(int index, Vector2 pos, TradeSide side)
        {
            ShowContextMenu(index, pos, side);
        }

        public void OnSlotDoubleClicked(int index, TradeSide side)
        {
            if (side == TradeSide.NPC) BuyItem(index);
            else SellItem(index);
        }

        private void UpdateDetailPanel(ItemSO item, TradeSide side)
        {
            if (_detailPanelUI == null) return;
            // No Use/Drop/Equip buttons in Trade UI
            _detailPanelUI.Show(item); 
        }

        private void ClearSelection()
        {
            _detailPanelUI?.Hide();
        }

        private void RefreshGrids()
        {
            RefreshGrid(_npcContentRoot, _npcInventory);
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

        private void ShowContextMenu(int slotIndex, Vector2 screenPos, TradeSide side)
        {
            HideContextMenu();
            _contextMenuSlotIndex = slotIndex;

            InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
            if (inv == null || slotIndex < 0 || slotIndex >= inv.Count) return;
            ItemSO item = inv.Items[slotIndex].Item;

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

            var buyBtn = _activeContextMenu.transform.Find("BuyButton")?.GetComponent<Button>();
            var sellBtn = _activeContextMenu.transform.Find("SellButton")?.GetComponent<Button>();

            if (buyBtn == null) GameLog.Warn(TAG, "ShowContextMenu: 'BuyButton' not found in context menu prefab");
            if (sellBtn == null) GameLog.Warn(TAG, "ShowContextMenu: 'SellButton' not found in context menu prefab");

            if (side == TradeSide.NPC)
            {
                if (buyBtn != null)
                {
                    buyBtn.gameObject.SetActive(true);
                    buyBtn.GetComponentInChildren<TMP_Text>().text = $"Buy ({item.buyValue}g)";
                    buyBtn.interactable = _goldSystem != null && _goldSystem.Gold >= item.buyValue;
                    buyBtn.onClick.AddListener(() => { BuyItem(_contextMenuSlotIndex); HideContextMenu(); });
                }
                sellBtn?.gameObject.SetActive(false);
            }
            else
            {
                if (sellBtn != null)
                {
                    sellBtn.gameObject.SetActive(true);
                    sellBtn.GetComponentInChildren<TMP_Text>().text = $"Sell ({item.sellValue}g)";
                    sellBtn.onClick.AddListener(() => { SellItem(_contextMenuSlotIndex); HideContextMenu(); });
                }
                buyBtn?.gameObject.SetActive(false);
            }
        }

        private void HideContextMenu()
        {
            if (_activeContextMenu != null) { Destroy(_activeContextMenu); _activeContextMenu = null; }
            if (_contextMenuBlocker != null) { Destroy(_contextMenuBlocker); _contextMenuBlocker = null; }
        }

        private void BuyItem(int index)
        {
            if (_npcInventory == null || index < 0 || index >= _npcInventory.Count) return;
            ItemSO item = _npcInventory.Items[index].Item;

            if (_goldSystem != null && _goldSystem.TrySpend(item.buyValue))
            {
                _npcInventory.DecrementStack(index);
                _playerInventory?.AddItem(item);
                RefreshGrids();
                UpdateGoldDisplay();
                GameLog.Info(TAG, $"Bought {item.itemName} for {item.buyValue}g");
            }
        }

        private void SellItem(int index)
        {
            if (_playerInventory == null || index < 0 || index >= _playerInventory.Count) return;
            ItemSO item = _playerInventory.Items[index].Item;

            _playerInventory.DecrementStack(index);
            if (_goldSystem != null) _goldSystem.Add(item.sellValue);
            _npcInventory?.AddItem(item);
            RefreshGrids();
            UpdateGoldDisplay();
            GameLog.Info(TAG, $"Sold {item.itemName} for {item.sellValue}g");
        }
    }
}
