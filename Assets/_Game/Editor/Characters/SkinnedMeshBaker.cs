using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Inventory;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Bakes an imported skinned mesh (body part, armor) into the mesh space and bind poses of the player's base body,
    /// so it can be bound to the player skeleton by bone name at runtime (<see cref="SkinnedMeshRebinder"/>).
    /// Absorbs the axis / scale / bone-orientation drift between Mixamo, Blender and Unity imports: vertices are
    /// re-expressed through one bind-pose transform (most-weighted shared bone), refused unless every weighted shared bone agrees with it, weights are remapped by bone name.
    /// </summary>
    public static class SkinnedMeshBaker
    {
        /// <summary>Allowed vertical drift against the reference mesh, as a fraction of its height.</summary>
        public const float SizeTolerance = 0.03f;
        private const float WeightEpsilon = 1e-4f;
        /// <summary>Max disagreement between shared bones' bind-pose transforms (matrix units ~ metres) before the bake is refused.</summary>
        private const float BindPoseTolerance = 0.002f;
        private const int MaxUvChannels = 8;

        /// <summary>
        /// Returns the baked mesh, or null with <paramref name="error"/> set. <paramref name="reference"/> (optional) is the
        /// mesh whose vertical extent the result must match within <see cref="SizeTolerance"/> (e.g. the baked base body for an armor).
        /// </summary>
        public static Mesh Bake(SkinnedMeshRenderer source, SkinnedMeshRenderer target, Mesh reference, string meshName, out string error)
        {
            error = null;
            if (source == null || source.sharedMesh == null || target == null || target.sharedMesh == null)
            {
                error = "Source and target need a SkinnedMeshRenderer with a mesh.";
                return null;
            }
            var src = source.sharedMesh;
            var tgt = target.sharedMesh;
            if (src.bindposes.Length != source.bones.Length || tgt.bindposes.Length != target.bones.Length)
            {
                error = $"Bone / bind-pose count mismatch (source {source.bones.Length}/{src.bindposes.Length}, target {target.bones.Length}/{tgt.bindposes.Length}).";
                return null;
            }
            if (src.blendShapeCount > 0)
            {
                error = $"Source has {src.blendShapeCount} blend shapes — not supported by the baker.";
                return null;
            }

            // Bone index remap by name.
            var targetIndex = new Dictionary<string, int>();
            for (int i = 0; i < target.bones.Length; i++)
                if (target.bones[i] != null && !targetIndex.ContainsKey(target.bones[i].name)) targetIndex.Add(target.bones[i].name, i);
            var remap = new int[source.bones.Length];
            for (int i = 0; i < remap.Length; i++)
                remap[i] = source.bones[i] != null && targetIndex.TryGetValue(source.bones[i].name, out var t) ? t : -1;

            var bonesPerVertex = src.GetBonesPerVertex();
            var weights = src.GetAllBoneWeights();
            var weightSum = new float[remap.Length];
            foreach (var w in weights)
            {
                if (w.boneIndex < 0 || w.boneIndex >= weightSum.Length)
                {
                    error = $"Source weight references bone index {w.boneIndex} outside its {weightSum.Length} bones.";
                    return null;
                }
                weightSum[w.boneIndex] += w.weight;
            }

            var foreign = new List<string>();
            int anchor = -1;
            for (int i = 0; i < remap.Length; i++)
            {
                if (remap[i] < 0 && weightSum[i] > WeightEpsilon) foreign.Add(source.bones[i] != null ? source.bones[i].name : $"#{i}");
                if (remap[i] >= 0 && (anchor < 0 || weightSum[i] > weightSum[anchor])) anchor = i;
            }
            if (foreign.Count > 0)
            {
                error = "Source is weighted to bones the target skeleton lacks: " + string.Join(", ", foreign);
                return null;
            }
            if (anchor < 0)
            {
                error = "Source shares no weighted bone with the target.";
                return null;
            }

            // Bone-local space is shared at bind: v_bone = srcBindpose · v_src = tgtBindpose · v_tgt.
            // Uses bind poses only — an FBX's rest pose can differ from its bind pose (Blender re-export of the split did, by ~9 mm).
            Matrix4x4 m = tgt.bindposes[remap[anchor]].inverse * src.bindposes[anchor];
            if (m.determinant < 0f)
            {
                error = "Source → target transform is mirrored (negative determinant) — fix the export axes.";
                return null;
            }
            // One transform is only valid if every weighted shared bone was bound in the same place (same skeleton at bind).
            var disagree = new List<string>();
            for (int i = 0; i < remap.Length; i++)
            {
                if (remap[i] < 0 || weightSum[i] <= WeightEpsilon) continue;
                Matrix4x4 mi = tgt.bindposes[remap[i]].inverse * src.bindposes[i];
                float d = 0f;
                for (int k = 0; k < 16; k++) d = Mathf.Max(d, Mathf.Abs(mi[k] - m[k]));
                if (d > BindPoseTolerance) disagree.Add($"{source.bones[i].name} ({d:F4})");
            }
            if (disagree.Count > 0)
            {
                error = "Source was not bound to the target skeleton (bind poses disagree): " + string.Join(", ", disagree) +
                        ". Fit and re-bind it to the base armature in Blender first.";
                return null;
            }
            Matrix4x4 n = m.inverse.transpose;

            var verts = src.vertices;
            var normals = src.normals;
            var tangents = src.tangents;
            for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(verts[i]);
            for (int i = 0; i < normals.Length; i++) normals[i] = n.MultiplyVector(normals[i]).normalized;
            for (int i = 0; i < tangents.Length; i++)
            {
                var d = m.MultiplyVector(tangents[i]).normalized;
                tangents[i] = new Vector4(d.x, d.y, d.z, tangents[i].w);
            }

            var outMesh = new Mesh { name = meshName, indexFormat = src.indexFormat };
            outMesh.vertices = verts;
            if (normals.Length == verts.Length) outMesh.normals = normals;
            if (tangents.Length == verts.Length) outMesh.tangents = tangents;
            var uv = new List<Vector4>();
            for (int ch = 0; ch < MaxUvChannels; ch++)
            {
                if (!src.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + ch)) continue;
                src.GetUVs(ch, uv);
                outMesh.SetUVs(ch, uv);
            }
            if (src.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)) outMesh.colors = src.colors;
            outMesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) outMesh.SetIndices(src.GetIndices(s), src.GetTopology(s), s);

            // Weights remapped to the target bone order (unweighted foreign bones dropped, order kept = still sorted).
            var newCounts = new NativeArray<byte>(verts.Length, Allocator.Temp);
            var newWeights = new List<BoneWeight1>(weights.Length);
            int cursor = 0;
            int unweighted = 0;
            for (int v = 0; v < verts.Length; v++)
            {
                int count = bonesPerVertex[v];
                float total = 0f;
                int start = newWeights.Count;
                for (int k = 0; k < count; k++)
                {
                    var w = weights[cursor + k];
                    int mapped = remap[w.boneIndex];
                    if (mapped < 0 || w.weight <= 0f) continue;
                    newWeights.Add(new BoneWeight1 { boneIndex = mapped, weight = w.weight });
                    total += w.weight;
                }
                cursor += count;
                if (total > 0f)
                    for (int k = start; k < newWeights.Count; k++)
                    {
                        var w = newWeights[k];
                        w.weight /= total;
                        newWeights[k] = w;
                    }
                newCounts[v] = (byte)(newWeights.Count - start);
                if (newCounts[v] == 0) unweighted++;
            }
            if (unweighted > 0)
            {
                newCounts.Dispose();
                Object.DestroyImmediate(outMesh);
                error = $"{unweighted} vertices have no weight on a target bone after remapping — they would collapse to the root.";
                return null;
            }
            var weightArray = new NativeArray<BoneWeight1>(newWeights.ToArray(), Allocator.Temp);
            outMesh.SetBoneWeights(newCounts, weightArray);
            newCounts.Dispose();
            weightArray.Dispose();
            outMesh.bindposes = tgt.bindposes;
            outMesh.RecalculateBounds();

            if (reference != null)
            {
                var rb = reference.bounds;
                var ob = outMesh.bounds;
                float tol = rb.size.y * SizeTolerance;
                if (Mathf.Abs(ob.min.y - rb.min.y) > tol || Mathf.Abs(ob.max.y - rb.max.y) > tol)
                {
                    error = $"Size check failed: baked Y {ob.min.y:F3}..{ob.max.y:F3} m, reference '{reference.name}' Y {rb.min.y:F3}..{rb.max.y:F3} m (±{tol:F3} m).";
                    Object.DestroyImmediate(outMesh);
                    return null;
                }
            }
            return outMesh;
        }

        /// <summary>Saves <paramref name="mesh"/> at <paramref name="path"/>, overwriting an existing asset in place (keeps its GUID).</summary>
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            CopyInto(mesh, existing); // CopySerialized does not carry vertex data — copy through the Mesh API
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void CopyInto(Mesh src, Mesh dst)
        {
            dst.Clear();
            dst.indexFormat = src.indexFormat;
            dst.vertices = src.vertices;
            dst.normals = src.normals;
            dst.tangents = src.tangents;
            var uv = new List<Vector4>();
            for (int ch = 0; ch < MaxUvChannels; ch++)
            {
                if (!src.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + ch)) continue;
                src.GetUVs(ch, uv);
                dst.SetUVs(ch, uv);
            }
            if (src.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)) dst.colors = src.colors;
            dst.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) dst.SetIndices(src.GetIndices(s), src.GetTopology(s), s);
            dst.SetBoneWeights(src.GetBonesPerVertex(), src.GetAllBoneWeights());
            dst.bindposes = src.bindposes;
            dst.bounds = src.bounds;
        }

        /// <summary>Writes a skeleton-less body-part prefab (SMR + <see cref="SkinnedBodyPart"/> holding the target's bone names).</summary>
        public static GameObject CreatePartPrefab(Mesh mesh, Material material, SkinnedMeshRenderer target, string path)
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            smr.sharedMaterials = mats;
            var names = new string[target.bones.Length];
            int rootIndex = -1;
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = target.bones[i] != null ? target.bones[i].name : null;
                if (target.bones[i] != null && target.bones[i] == target.rootBone) rootIndex = i;
            }
            // localBounds live in the root bone's space at bind: target bounds grown to contain this mesh (bulky armor).
            var bounds = target.localBounds;
            if (rootIndex >= 0)
            {
                var bp = mesh.bindposes[rootIndex];
                var b = mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f);
                    bounds.Encapsulate(bp.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, corner)));
                }
            }
            smr.localBounds = bounds;
            go.AddComponent<SkinnedBodyPart>().EditorSetup(smr, names, target.rootBone != null ? target.rootBone.name : SkinnedMeshRebinder.DefaultRootBone);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }

    /// <summary>Tools/Characters/Skinned Mesh Baker — UI over <see cref="SkinnedMeshBaker"/>.</summary>
    public class SkinnedMeshBakerWindow : EditorWindow
    {
        private const string TAG = "[SkinnedMeshBaker]";
        private SkinnedMeshRenderer _source;
        private SkinnedMeshRenderer _target;
        private Mesh _reference;
        private Material _material;
        private bool _createPrefab;
        private string _prefabFolder = "Assets/_Game/Prefabs/Items/Armors";

        [MenuItem("Tools/Characters/Skinned Mesh Baker")]
        private static void Open() => GetWindow<SkinnedMeshBakerWindow>("Skinned Mesh Baker");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Bakes Source into Target's mesh space + bind poses (bones matched by name). " +
                                    "Target = the player's base body SMR. Reference (optional) = mesh the result must match in height (±3%).", MessageType.Info);
            _source = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source", _source, typeof(SkinnedMeshRenderer), true);
            _target = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target (base body)", _target, typeof(SkinnedMeshRenderer), true);
            _reference = (Mesh)EditorGUILayout.ObjectField("Size reference", _reference, typeof(Mesh), false);
            _createPrefab = EditorGUILayout.Toggle("Create part prefab", _createPrefab);
            if (_createPrefab)
            {
                _material = (Material)EditorGUILayout.ObjectField("Material", _material, typeof(Material), false);
                _prefabFolder = EditorGUILayout.TextField("Prefab folder", _prefabFolder);
            }
            using (new EditorGUI.DisabledScope(_source == null || _target == null))
                if (GUILayout.Button("Bake")) Run();
        }

        private void Run()
        {
            var srcPath = _source.sharedMesh != null ? AssetDatabase.GetAssetPath(_source.sharedMesh) : null;
            if (string.IsNullOrEmpty(srcPath))
            {
                Fail("Source mesh is not an asset (pick the SMR inside an imported model).");
                return;
            }
            if (_createPrefab && (_material == null || _reference == null))
            {
                Fail("A part prefab (armor) needs a Material and a Size reference (the baked base body).");
                return;
            }
            if (_createPrefab && !AssetDatabase.IsValidFolder(_prefabFolder))
            {
                Fail($"Prefab folder '{_prefabFolder}' does not exist.");
                return;
            }
            if (_reference == null) GameLog.Warn(TAG, "No size reference — height check skipped");
            var name = _source.name;
            var meshPath = (Path.GetDirectoryName(srcPath) + "/" + name + "_baked.asset").Replace('\\', '/');
            var mesh = SkinnedMeshBaker.Bake(_source, _target, _reference, name + "_baked", out var error);
            if (mesh == null)
            {
                Fail(error);
                return;
            }
            mesh = SkinnedMeshBaker.SaveMesh(mesh, meshPath);
            GameLog.Info(TAG, $"Baked {meshPath}");
            if (_createPrefab)
            {
                var prefabPath = $"{_prefabFolder}/{name}_Visual.prefab";
                var prefab = SkinnedMeshBaker.CreatePartPrefab(mesh, _material, _target, prefabPath);
                if (prefab == null) Fail($"Could not save prefab {prefabPath}");
                else GameLog.Info(TAG, $"Prefab {prefabPath}");
            }
        }

        private static void Fail(string message)
        {
            GameLog.Error(TAG, message);
            EditorUtility.DisplayDialog("Skinned Mesh Baker", message, "OK");
        }
    }
}
