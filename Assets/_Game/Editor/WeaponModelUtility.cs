using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Game.Core;
using Game.Inventory;

namespace Game.Editor
{
    /// <summary>
    /// Editor helpers for turning raw art-pack weapon models into equip-ready meshes:
    /// strip physics/LODs, normalize the Mesh child into the weapon frame, fit hitboxes.
    /// </summary>
    public static class WeaponModelUtility
    {
        private const string TAG = "[WeaponCreator]";
        private static readonly Regex HigherLodName = new(@"_LOD[1-9]\d*$");

        /// <summary>
        /// Destroys every Collider, Rigidbody and LODGroup in the hierarchy, every renderer the LODGroups
        /// list only in LOD1+, plus every child whose name ends in _LOD1+ (LOD0 and non-LOD renderers are kept).
        /// </summary>
        public static void StripForEquip(GameObject model)
        {
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(rb);

            var toDestroy = new List<GameObject>();

            // LODGroup membership first — catches higher LODs whatever their naming convention.
            foreach (var group in model.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                var keep = new HashSet<Renderer>();
                if (lods.Length > 0)
                {
                    foreach (var r in lods[0].renderers)
                        if (r != null) keep.Add(r);
                }
                for (int i = 1; i < lods.Length; i++)
                {
                    foreach (var r in lods[i].renderers)
                    {
                        if (r == null || keep.Contains(r)) continue;
                        if (r.transform != model.transform && r.transform.childCount == 0)
                            toDestroy.Add(r.gameObject);
                        else
                            RemoveRenderer(r); // GO carries children or is the model root — drop the renderer only
                    }
                }
                Object.DestroyImmediate(group);
            }

            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t != model.transform && HigherLodName.IsMatch(t.name) && !toDestroy.Contains(t.gameObject))
                    toDestroy.Add(t.gameObject);
            }
            foreach (var go in toDestroy)
            {
                if (go != null) // may already be gone with a destroyed ancestor
                    Object.DestroyImmediate(go);
            }
        }

        private static void RemoveRenderer(Renderer r)
        {
            var filter = r.GetComponent<MeshFilter>();
            Object.DestroyImmediate(r);
            if (filter != null)
                Object.DestroyImmediate(filter);
        }

        /// <summary>
        /// Sets the Mesh child's local transform so its grip lands at the parent origin, blade +Y, edge +Z.
        /// The Mesh child's own localScale (e.g. from the source prefab root) is preserved and accounted for.
        /// </summary>
        public static void Normalize(Transform meshChild, Vector3 gripMeshLocal, SignedAxis blade, SignedAxis edge)
        {
            Vector3 scaledGrip = Vector3.Scale(meshChild.localScale, gripMeshLocal);
            WeaponGripMath.GetMeshChildLocal(scaledGrip, blade, edge, out var pos, out var rot);
            meshChild.localPosition = pos;
            meshChild.localRotation = rot;
        }

        /// <summary>Fits the box to the bounds of every Renderer under its transform, in the box's local space.</summary>
        public static void FitBoxToRenderers(BoxCollider box)
        {
            if (!TryGetRendererBounds(box.transform, box.transform, out var bounds))
            {
                GameLog.Warn(TAG, $"FitBoxToRenderers: no renderers under '{box.name}' — box left unchanged.");
                return;
            }
            box.center = bounds.center;
            box.size = bounds.size;
        }

        /// <summary>
        /// Encapsulates the 8 corners of each Renderer.localBounds under <paramref name="root"/>,
        /// expressed in <paramref name="space"/> local coordinates.
        /// </summary>
        public static bool TryGetRendererBounds(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds lb = r.localBounds;
                Matrix4x4 m = toSpace * r.transform.localToWorldMatrix;
                Vector3 min = lb.min, max = lb.max;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }
    }
}
