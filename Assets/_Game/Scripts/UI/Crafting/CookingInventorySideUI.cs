using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// <see cref="IItemSlotContainer"/> adapter for the cooking screen's player grid: double-click sets the ingredient;
    /// selection, context menu and reorder are not used here.
    /// </summary>
    public class CookingInventorySideUI : MonoBehaviour, IItemSlotContainer
    {
        private CookingUI _owner;

        private void Awake()
        {
            _owner = GetComponentInParent<CookingUI>();
        }

        public void SelectSlot(int slotIndex, IItemSlotContainer source) { }

        public void ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source) { }

        public void PrimaryAction(int slotIndex, IItemSlotContainer source) => _owner?.SetIngredientFromInventory(slotIndex);

        public void SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source) { }
    }
}
