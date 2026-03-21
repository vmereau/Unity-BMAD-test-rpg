using UnityEngine;

namespace Game.NPC
{
    [CreateAssetMenu(menuName = "Game/NPC/NPC Data", fileName = "NPC_")]
    public class NPCDataSO : ScriptableObject
    {
        public string npcName;
        public NPCState dayState = NPCState.Working;
        public NPCState nightState = NPCState.Sleeping;
        public float walkSpeed = 2f;
    }
}
