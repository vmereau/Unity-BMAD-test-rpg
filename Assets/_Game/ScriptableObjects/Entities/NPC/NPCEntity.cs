using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using UnityEngine;

namespace Game.NPC
{
    [CreateAssetMenu(menuName = "Game/NPC/NPC Data", fileName = "NPC_")]
    public class NPCEntity : Entity
    {
        [Header("NPC properties")]
        public NPCState dayState = NPCState.Working;
        public NPCState nightState = NPCState.Sleeping;
        public GameObject prefab;

        public List<NPCMemoryEntrySO>  memories;
    }
}
