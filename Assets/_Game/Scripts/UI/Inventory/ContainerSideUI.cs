using UnityEngine;

namespace Game.UI
{
    public enum ContainerSide { Container, Player }

    public class ContainerSideUI : MonoBehaviour, IItemSlotContainer
    {
        [SerializeField] private ContainerSide _side;
        private ContainerUI _owner;

        private void Awake()
        {
            _owner = GetComponentInParent<ContainerUI>();
        }

        public void SelectSlot(int slotIndex, IItemSlotContainer source) =>
            _owner.OnSlotSelected(slotIndex, _side);

        public void ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source) =>
            _owner.OnSlotRightClicked(slotIndex, screenPos, _side);

        public void PrimaryAction(int slotIndex, IItemSlotContainer source) =>
            _owner.OnSlotDoubleClicked(slotIndex, _side);

        public void SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source)
        {
            if (source == this) return;

            if (_side == ContainerSide.Player)
                _owner.TakeItem(fromIndex);
            else
                _owner.PutItem(fromIndex);
        }
    }
}
