using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Binds a skinned body part (armor, head) to an existing skeleton by bone name.
    /// The part's mesh must already use the skeleton's bind poses (see the editor Skinned Mesh Baker),
    /// so only the <c>bones[]</c> array changes — never the mesh.
    /// </summary>
    public static class SkinnedMeshRebinder
    {
        /// <summary>Root bone of the Mixamo humanoid skeleton every body part is baked against.</summary>
        public const string DefaultRootBone = "mixamorig:Hips";

        /// <summary>Indexes the given bones (plus <paramref name="root"/>) by name — a renderer's own bone list, so sockets and
        /// attached weapon children can never shadow a bone.</summary>
        public static Dictionary<string, Transform> IndexBones(IReadOnlyList<Transform> bones, Transform root)
        {
            var map = new Dictionary<string, Transform>();
            if (root != null) map[root.name] = root;
            if (bones == null) return map;
            foreach (var t in bones)
                if (t != null && !map.ContainsKey(t.name)) map.Add(t.name, t);
            return map;
        }

        /// <summary>Indexes every transform under <paramref name="root"/> (root included) by name. First match wins on duplicates.</summary>
        public static Dictionary<string, Transform> IndexSkeleton(Transform root)
        {
            var map = new Dictionary<string, Transform>();
            if (root == null) return map;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!map.ContainsKey(t.name)) map.Add(t.name, t);
            return map;
        }

        /// <summary>
        /// Resolves <paramref name="boneNames"/> against <paramref name="skeleton"/>, in order.
        /// Returns null (and fills <paramref name="missing"/>) when any name is absent — a partial bind would tear the mesh.
        /// </summary>
        public static Transform[] ResolveBones(IReadOnlyList<string> boneNames, IReadOnlyDictionary<string, Transform> skeleton, List<string> missing)
        {
            missing?.Clear();
            if (boneNames == null || skeleton == null) return null;
            var bones = new Transform[boneNames.Count];
            bool ok = true;
            for (int i = 0; i < boneNames.Count; i++)
            {
                if (boneNames[i] != null && skeleton.TryGetValue(boneNames[i], out var t) && t != null)
                {
                    bones[i] = t;
                    continue;
                }
                ok = false;
                missing?.Add(boneNames[i] ?? "<null>");
            }
            return ok ? bones : null;
        }

        /// <summary>
        /// Applies the resolved bones and root bone to <paramref name="renderer"/>. Returns false (renderer untouched)
        /// when a bone is missing or the name count differs from the mesh's bind poses (Unity would not skin it);
        /// the root bone falls back to <paramref name="fallbackRoot"/> when its name is absent.
        /// </summary>
        public static bool TryBind(SkinnedMeshRenderer renderer, IReadOnlyList<string> boneNames, string rootBoneName,
                                   IReadOnlyDictionary<string, Transform> skeleton, Transform fallbackRoot, List<string> missing)
        {
            if (renderer == null || renderer.sharedMesh == null || boneNames == null) return false;
            if (boneNames.Count != renderer.sharedMesh.bindposes.Length)
            {
                missing?.Clear();
                missing?.Add($"<{boneNames.Count} names for {renderer.sharedMesh.bindposes.Length} bind poses>");
                return false;
            }
            var bones = ResolveBones(boneNames, skeleton, missing);
            if (bones == null) return false;
            renderer.bones = bones;
            renderer.rootBone = rootBoneName != null && skeleton.TryGetValue(rootBoneName, out var root) ? root : fallbackRoot;
            return true;
        }
    }
}
