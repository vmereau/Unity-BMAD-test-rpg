using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// A humanoid head variant (spec-npc-head-variants): a baked head mesh on the canonical base-hero skeleton and neck
    /// interface, plus its skin material. Referenced from <see cref="NPCEntity"/>; applied at spawn by
    /// <c>Game.AI.NpcHeadAppearance</c>. Built by <c>Tools/Characters/Bake Head Variants</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Head Appearance", fileName = "HeadAppearance_")]
    public class HeadAppearanceSO : ScriptableObject
    {
        [Tooltip("Baked head mesh (Head_<Name>_baked). Must share the canonical head's bone count and bind poses.")]
        [SerializeField] private Mesh _mesh;

        [Tooltip("Skin material for this head (M_Head_<Name>). Empty = keep the renderer's material.")]
        [SerializeField] private Material _material;

        public Mesh Mesh => _mesh;
        public Material Material => _material;

#if UNITY_EDITOR
        /// <summary>Editor-only setter used by the head baker.</summary>
        public void EditorSet(Mesh mesh, Material material)
        {
            _mesh = mesh;
            _material = material;
        }
#endif
    }
}
