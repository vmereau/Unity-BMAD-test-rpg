namespace Game.UI
{
    /// <summary>
    /// Optional companion of <see cref="IItemSlotContainer"/>: a host that wants to know when one of its
    /// <see cref="ItemSlotUI"/> starts / stops being dragged (InventoryUI → paper-doll valid-target highlight).
    /// </summary>
    public interface IItemDragListener
    {
        void OnItemDragBegin(int slotIndex);
        void OnItemDragEnd();
    }
}
