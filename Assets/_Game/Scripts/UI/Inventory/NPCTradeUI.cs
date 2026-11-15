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
        [SerializeField] private TradeDetailActions _tradeActions;

        [Header("Context Menu")]
        [SerializeField] private GameObject _contextMenuPrefab;
        [SerializeField] private Canvas _canvas;

        [Header("Economy")]
        [SerializeField] private GoldSystem _goldSystem;
        [SerializeField] private GameEventSO_Int _onGoldChanged;
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private TMP_Text _npcGoldText;

        [Header("Knockout")]
        [Tooltip("Player knocked out (OnPlayerKnockedOut): the trade window closes. Listened to while open (GO active).")]
        [SerializeField] private GameEventSO_Void _onPlayerKnockedOut;

        private GoldSystem _npcGoldSystem;

        private GameObject _activeContextMenu;
        private GameObject _contextMenuBlocker;
        private int _contextMenuSlotIndex = -1;

        private ItemSlotUI _selectedSlotUI;
        private int _selectedIndex = -1;
        private TradeSide _selectedSide;
        private bool _bulkHeld; // Shift: buy / sell up to BulkTransferRules.BulkSize affordable units

        private InputSystem_Actions _input;

        private void Awake()
        {
            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _onPlayerKnockedOut?.AddListener(HandlePlayerKnockedOut);
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _onGoldChanged?.AddListener(HandleGoldChanged);
        }

        private void OnDisable()
        {
            _onPlayerKnockedOut?.RemoveListener(HandlePlayerKnockedOut);
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
            _onGoldChanged?.RemoveListener(HandleGoldChanged);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        // Shift is polled (no callback on hand-added UI actions); repaint the selection's label + price on change.
        private void Update()
        {
            if (_input == null) return;
            bool bulk = _input.UI.BulkModifier.IsPressed();
            if (bulk == _bulkHeld) return;
            _bulkHeld = bulk;
            RepaintSelection();
        }

        private void HandleGoldChanged(int newGold)
        {
            UpdateGoldDisplay();
            RepaintSelection(); // the affordable batch follows the player's gold
        }

        private void HandlePlayerKnockedOut(bool _)
        {
            if (gameObject.activeInHierarchy) Close();
        }

        private void HandleCancel(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (gameObject.activeInHierarchy)
                Close();
        }

        /// <summary>Opens the trade UI with the given NPC inventory and gold system.</summary>
        public void Open(InventorySystem npcInventory, GoldSystem npcGoldSystem)
        {
            _npcInventory = npcInventory;
            _npcGoldSystem = npcGoldSystem;
            gameObject.SetActive(true); // enables the UI map (OnEnable)
            _bulkHeld = _input != null && _input.UI.BulkModifier.IsPressed();
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
            if (_npcGoldText != null && _npcGoldSystem != null)
                _npcGoldText.text = $"Gold: {_npcGoldSystem.Gold}g";
        }

        public void OnSlotSelected(int index, TradeSide side)
        {
            InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
            if (inv == null || index < 0 || index >= inv.Count) return;

            Transform root = (side == TradeSide.NPC) ? _npcContentRoot : _playerContentRoot;
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotUI = root.GetChild(index).GetComponent<ItemSlotUI>();
            _selectedSlotUI?.SetSelected(true);
            _selectedIndex = index;
            _selectedSide = side;

            UpdateDetailPanel(inv.Items[index].Item, index, side);
        }

        public void OnSlotRightClicked(int index, Vector2 pos, TradeSide side)
        {
            ShowContextMenu(index, pos, side);
        }

        public void OnSlotDoubleClicked(int index, TradeSide side)
        {
            if (side == TradeSide.NPC) BuyBatch(index);
            else SellBatch(index);
        }

        /// <summary>
        /// Units one buy / sell on this slot moves right now: 1, or with Shift up to 10 the buyer can afford
        /// (player gold for NPC-side items, NPC gold for player-side items). Never below 1 while the slot exists.
        /// </summary>
        public int BatchSize(int index, TradeSide side)
        {
            InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
            if (inv == null || index < 0 || index >= inv.Count || inv.Items[index].Item == null) return 0;
            ItemSO item = inv.Items[index].Item;
            bool buying = side == TradeSide.NPC;
            GoldSystem buyer = buying ? _goldSystem : _npcGoldSystem;
            int unitPrice = buying ? item.buyValue : item.sellValue;
            // GoldSystem.TrySpend refuses 0g, so a free item can't be traded in bulk: never advertise a batch.
            bool bulk = _bulkHeld && unitPrice > 0;
            return BulkTransferRules.Quantity(bulk, inv.Items[index].Count, unitPrice, buyer != null ? buyer.Gold : 0);
        }

        public void BuyBatch(int index) => BuyItems(index, BatchSize(index, TradeSide.NPC));
        public void SellBatch(int index) => SellItems(index, BatchSize(index, TradeSide.Player));

        private void UpdateDetailPanel(ItemSO item, int slotIndex, TradeSide side)
        {
            if (_detailPanelUI == null) return;
            int qty = BatchSize(slotIndex, side);
            _detailPanelUI.Show(item, side == TradeSide.NPC ? ItemPriceContext.Buy : ItemPriceContext.Sell, qty);
            _tradeActions?.Bind(this, slotIndex, item, side, _goldSystem, _npcGoldSystem, qty);
        }

        // Shift pressed / released: repaint the selected slot's button label and price row.
        private void RepaintSelection()
        {
            if (_selectedIndex < 0) return;
            InventorySystem inv = (_selectedSide == TradeSide.NPC) ? _npcInventory : _playerInventory;
            if (inv == null || _selectedIndex >= inv.Count) return;
            UpdateDetailPanel(inv.Items[_selectedIndex].Item, _selectedIndex, _selectedSide);
        }

        private void ClearSelection()
        {
            _selectedSlotUI?.SetSelected(false);
            _selectedSlotUI = null;
            _selectedIndex = -1;
            _detailPanelUI?.Hide();
        }

        private void RefreshGrids()
        {
            _selectedSlotUI = null;
            _selectedIndex = -1;
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

            int qty = BatchSize(slotIndex, side); // captured at open: the label is what the click does
            if (side == TradeSide.NPC)
            {
                if (buyBtn != null)
                {
                    int total = item.buyValue * qty;
                    buyBtn.gameObject.SetActive(true);
                    buyBtn.GetComponentInChildren<TMP_Text>().text = $"{BulkTransferRules.ActionLabel("Buy", qty)} ({total}g)";
                    buyBtn.interactable = _goldSystem != null && _goldSystem.Gold >= total;
                    buyBtn.onClick.AddListener(() => { BuyItems(_contextMenuSlotIndex, qty); HideContextMenu(); });
                }
                sellBtn?.gameObject.SetActive(false);
            }
            else
            {
                if (sellBtn != null && !ItemSO.CanPlayerPart(item))
                    sellBtn.gameObject.SetActive(false); // quest items can't be sold
                else if (sellBtn != null)
                {
                    int total = item.sellValue * qty;
                    sellBtn.gameObject.SetActive(true);
                    sellBtn.GetComponentInChildren<TMP_Text>().text = $"{BulkTransferRules.ActionLabel("Sell", qty)} ({total}g)";
                    sellBtn.interactable = _npcGoldSystem != null && _npcGoldSystem.Gold >= total;
                    sellBtn.onClick.AddListener(() => { SellItems(_contextMenuSlotIndex, qty); HideContextMenu(); });
                }
                buyBtn?.gameObject.SetActive(false);
            }
        }

        private void HideContextMenu()
        {
            if (_activeContextMenu != null) { Destroy(_activeContextMenu); _activeContextMenu = null; }
            if (_contextMenuBlocker != null) { Destroy(_contextMenuBlocker); _contextMenuBlocker = null; }
        }

        public void BuyItem(int index) => BuyItems(index, 1);
        public void SellItem(int index) => SellItems(index, 1);

        /// <summary>Buys up to <paramref name="quantity"/> units, one at a time, stopping when gold runs out.</summary>
        public void BuyItems(int index, int quantity)
        {
            if (_npcInventory == null || _goldSystem == null || index < 0 || index >= _npcInventory.Count) return;
            ItemSO item = _npcInventory.Items[index].Item;
            if (item == null) return;
            int units = TradeUnits(_npcInventory, _playerInventory, _goldSystem, _npcGoldSystem, item, index,
                                   item.buyValue, quantity);
            if (units == 0) return;
            FinishTrade(item, index, TradeSide.NPC, $"Bought {units} x {item.itemName} for {units * item.buyValue}g");
        }

        /// <summary>Sells up to <paramref name="quantity"/> units, one at a time, stopping when the NPC runs out of gold.</summary>
        public void SellItems(int index, int quantity)
        {
            if (_playerInventory == null || index < 0 || index >= _playerInventory.Count) return;
            ItemSO item = _playerInventory.Items[index].Item;
            if (!ItemSO.CanPlayerPart(item))
            {
                GameLog.Warn(TAG, $"SellItem blocked: {item?.itemName} is a quest item");
                return;
            }
            if (_npcGoldSystem == null)
            {
                GameLog.Warn(TAG, $"SellItem blocked: NPC has no gold system ({item.itemName})");
                return;
            }
            int units = TradeUnits(_playerInventory, _npcInventory, _npcGoldSystem, _goldSystem, item, index,
                                   item.sellValue, quantity);
            if (units == 0)
            {
                GameLog.Warn(TAG, $"SellItem blocked: NPC cannot afford {item.itemName} ({item.sellValue}g)");
                return;
            }
            FinishTrade(item, index, TradeSide.Player, $"Sold {units} x {item.itemName} for {units * item.sellValue}g");
        }

        // Moves units one at a time with the one-unit rules (payer spends, receiver gets paid, item changes hands).
        private static int TradeUnits(InventorySystem fromInv, InventorySystem toInv, GoldSystem payer,
                                      GoldSystem receiverGold, ItemSO item, int index, int unitPrice, int quantity)
        {
            int units = 0;
            while (units < quantity && index < fromInv.Count && fromInv.Items[index].Item == item &&
                   payer.TrySpend(unitPrice))
            {
                receiverGold?.Add(unitPrice);
                fromInv.DecrementStack(index);
                toInv?.AddItem(item);
                units++;
            }
            return units;
        }

        private void FinishTrade(ItemSO item, int index, TradeSide fromSide, string logMessage)
        {
            RefreshGrids();
            UpdateGoldDisplay();
            RefreshDetailPanelAfterTrade(item, index, fromSide);
            GameLog.Info(TAG, logMessage);
        }

        private void RefreshDetailPanelAfterTrade(ItemSO item, int index, TradeSide side)
        {
            InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
            if (inv != null && index < inv.Count && inv.Items[index].Item == item)
                OnSlotSelected(index, side);
            else
                ClearSelection();
        }
    }
}
