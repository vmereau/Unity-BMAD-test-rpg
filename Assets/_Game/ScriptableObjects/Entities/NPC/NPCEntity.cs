using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using Game.Core;
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

        [Header("Identity")]
        [Tooltip("This NPC's KilledFact (same asset as its PersistentID). Owned objects become free to take once it is set.")]
        [SerializeField] private KilledFact _killedFact;

        public KilledFact KilledFact => _killedFact;

        [Header("Witness")]
        [Tooltip("Reaction to a sneaking, non-hostile player (WitnessProfile_Humanoid for every humanoid). Swap the profile to tune one NPC. Null = never reacts.")]
        [SerializeField] private WitnessProfileSO _witnessProfile;

        public override WitnessProfileSO WitnessProfile => _witnessProfile;

#if UNITY_EDITOR
        private const string DEFAULT_WITNESS_PROFILE = "WitnessProfile_Humanoid";

        // New NPC assets start with the shared humanoid witness profile.
        private void Reset()
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"{DEFAULT_WITNESS_PROFILE} t:{nameof(WitnessProfileSO)}"))
            {
                var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<WitnessProfileSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null || profile.name != DEFAULT_WITNESS_PROFILE) continue;
                _witnessProfile = profile;
                return;
            }
        }
#endif
    }
}
