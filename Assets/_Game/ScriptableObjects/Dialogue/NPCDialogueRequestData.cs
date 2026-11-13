using Game.AI;
using Game.Economy;
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
        public GoldSystem npcGoldSystem;        // Optional: null if NPC has no economy component
        public string forcedLine;               // non-empty → NPC-initiated: show only this line, the player closes it (no topics)
        public Game.Dialogue.StartDialogueNode forcedStartNode; // non-null → NPC-initiated topic: opens straight into it (full graph context)
        public bool forcedTopicThenTopics;      // with forcedStartNode: the chain's end shows the NPC's normal topic list instead of closing
        public NPCPresence requester;           // the NPC that raised the request — only its close request closes this dialogue
    }
}
