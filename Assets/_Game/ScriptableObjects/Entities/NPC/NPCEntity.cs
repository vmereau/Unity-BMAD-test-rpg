using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using Game.Combat;
using Game.Core;
using Game.Inventory;
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

        [Header("Loadout")]
        [Tooltip("Equipped when the NPC spawns (NpcArmament): armor is worn, a weapon is wielded in every fight unless the " +
                 "caller asks for fists. Empty = unarmed. Needs EquipmentSystem + EquipmentVisuals + NpcArmament on the NPC.")]
        [SerializeField] private List<EquipableItemSO> _startingEquipment = new();

        [Header("Combat")]
        [Tooltip("Stance damage multiplier on the last hit of a multi-hit combo (finisher), like the player's.")]
        [SerializeField, Min(1f)] private float _finisherStanceMultiplier = 1.5f;

        public IReadOnlyList<EquipableItemSO> StartingEquipment => _startingEquipment;

        /// <summary>Humanoid: entity base + wielded weapon stance, × finisher on the last hit of a multi-hit combo.
        /// Knockdown only with a weapon that can knock down (fists never).</summary>
        public override float ResolveAttackStance(int step, int hits, WeaponSO wielded, out bool canKnockDown)
        {
            canKnockDown = wielded != null && wielded.ResolvedCanKnockDown;
            bool isFinisher = hits > 1 && step >= hits;
            return StanceMath.ComputeHumanoidStanceDamage(AttackStanceDamage,
                wielded != null ? wielded.ResolvedStanceDamage : 0f, isFinisher, _finisherStanceMultiplier);
        }

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
