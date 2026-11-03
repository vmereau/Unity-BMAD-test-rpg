using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Root of a skinned body-part visual prefab (e.g. an armor's equipVisualPrefab). Holds the bone names the
    /// mesh was baked against, so the prefab carries no skeleton of its own; <see cref="EquipmentVisuals"/> binds
    /// it to the wearer's bones by name with <see cref="SkinnedMeshRebinder"/>. Written by the editor Skinned Mesh Baker.
    /// </summary>
    public class SkinnedBodyPart : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer _renderer;
        [SerializeField] private string[] _boneNames;
        [SerializeField] private string _rootBoneName = SkinnedMeshRebinder.DefaultRootBone;

        public SkinnedMeshRenderer Renderer => _renderer;
        public IReadOnlyList<string> BoneNames => _boneNames;
        public string RootBoneName => _rootBoneName;

#if UNITY_EDITOR
        /// <summary>Editor-only setup used by the baker when it writes the prefab.</summary>
        public void EditorSetup(SkinnedMeshRenderer renderer, string[] boneNames, string rootBoneName)
        {
            _renderer = renderer;
            _boneNames = boneNames;
            _rootBoneName = rootBoneName;
        }
#endif
    }
}
