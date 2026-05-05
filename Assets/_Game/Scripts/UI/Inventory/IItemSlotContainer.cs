using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Interface for UI panels that contain and manage item slots (e.g., InventoryUI, NPCTradeUI).
    /// Allows ItemSlotUI to communicate with its parent container without being tightly coupled.
    /// </summary>
    public interface IItemSlotContainer
    {
        void SelectSlot(int slotIndex, IItemSlotContainer source);
        void ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source);
        void PrimaryAction(int slotIndex, IItemSlotContainer source);
        void SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source);
    }
}
