using Game.AI;

namespace Game.Core
{
    [System.Serializable]
    public struct NPCDialogueRequestData
    {
        public string npcName;
        public NPCMemoryComponent memories; // null-safe — DialogueSystem guards
    }
}
