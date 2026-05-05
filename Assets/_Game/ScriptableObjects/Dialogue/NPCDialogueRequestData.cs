using Game.AI;
using Game.Inventory;

namespace Game.Core
{
    [System.Serializable]
    public struct NPCDialogueRequestData
    {
        public string npcName;
        public NPCMemoryComponent memories;    // null-safe — DialogueSystem guards
        public NPCDialogueGraphComponent graph; // null-safe — null means no graph, show only Farewell
        public InventorySystem npcInventory;    // Optional: for shop/trade system
    }
}
