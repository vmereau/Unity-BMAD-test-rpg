using Game.Core;
using Game.Economy;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class TradeDetailActions : MonoBehaviour
    {
        private const string TAG = "[TradeDetailActions]";

        [SerializeField] private Button _buyButton;
        [SerializeField] private Button _sellButton;

        // Cached lazily: Bind can run before Awake while the panel is still inactive.
        private TMP_Text _buyLabel;
        private TMP_Text _sellLabel;

        public void Bind(NPCTradeUI owner, int slotIndex, ItemSO item, TradeSide side,
                         GoldSystem playerGold, GoldSystem npcGold)
        {
            if (item == null)  { GameLog.Warn(TAG, "Bind: item is null");  return; }
            if (owner == null) { GameLog.Warn(TAG, "Bind: owner is null"); return; }

            if (_buyButton  != null) _buyButton.onClick.RemoveAllListeners();
            if (_sellButton != null) _sellButton.onClick.RemoveAllListeners();

            if (side == TradeSide.NPC)
            {
                ShowBuy(owner, slotIndex, item, playerGold);
                if (_sellButton != null) _sellButton.gameObject.SetActive(false);
            }
            else
            {
                ShowSell(owner, slotIndex, item, npcGold);
                if (_buyButton != null) _buyButton.gameObject.SetActive(false);
            }
        }

        private void ShowBuy(NPCTradeUI owner, int slotIndex, ItemSO item, GoldSystem playerGold)
        {
            if (_buyButton == null) { GameLog.Warn(TAG, "Bind: BuyButton is not assigned"); return; }
            _buyButton.gameObject.SetActive(true);
            if (_buyLabel == null) _buyLabel = _buyButton.GetComponentInChildren<TMP_Text>(true);
            if (_buyLabel != null) _buyLabel.text = "Buy";
            _buyButton.interactable = playerGold != null && playerGold.Gold >= item.buyValue;
            _buyButton.onClick.AddListener(() => owner.BuyItem(slotIndex));
        }

        private void ShowSell(NPCTradeUI owner, int slotIndex, ItemSO item, GoldSystem npcGold)
        {
            if (_sellButton == null) { GameLog.Warn(TAG, "Bind: SellButton is not assigned"); return; }
            _sellButton.gameObject.SetActive(true);
            if (_sellLabel == null) _sellLabel = _sellButton.GetComponentInChildren<TMP_Text>(true);
            if (_sellLabel != null) _sellLabel.text = "Sell";
            _sellButton.interactable = npcGold != null && npcGold.Gold >= item.sellValue;
            _sellButton.onClick.AddListener(() => owner.SellItem(slotIndex));
        }
    }
}
