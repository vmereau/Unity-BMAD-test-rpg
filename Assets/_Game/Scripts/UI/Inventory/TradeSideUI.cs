using UnityEngine;

namespace Game.UI
{
    public enum TradeSide { NPC, Player }

    /// <summary>
    /// Helper component to distinguish between NPC and Player sides in the Trade UI.
    /// Implements IItemSlotContainer to catch events from ItemSlotUI and forward them to NPCTradeUI.
    /// </summary>
    public class TradeSideUI : MonoBehaviour, IItemSlotContainer
    {
        [SerializeField] private TradeSide _side;
        private NPCTradeUI _tradeUI;

        private void Awake()
        {
            _tradeUI = GetComponentInParent<NPCTradeUI>();
        }

        public void SelectSlot(int index, IItemSlotContainer source) => _tradeUI.OnSlotSelected(index, _side);
        public void ShowContextMenu(int index, Vector2 pos, IItemSlotContainer source) => _tradeUI.OnSlotRightClicked(index, pos, _side);
        public void PrimaryAction(int index, IItemSlotContainer source) => _tradeUI.OnSlotDoubleClicked(index, _side);
        public void SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source) 
        {
            // Drag and drop within the same side is not implemented for trade for now.
        }
    }
}
