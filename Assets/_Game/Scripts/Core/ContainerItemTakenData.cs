using Game.Inventory;

namespace Game.Core
{
    /// <summary>Runtime notification after one item has moved from a container to the player.</summary>
    public struct ContainerItemTakenData
    {
        public InventorySystem sourceInventory;
        public ItemSO item;
    }
}
