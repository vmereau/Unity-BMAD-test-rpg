using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Gives a fitted outfit the base hero v2 hands (spec-base-body-regen). Outfit meshes come from Meshy with their
    /// own bare hands (fused or misplaced fingers), weighted to the old three-finger rig. The graft drops the outfit's
    /// hand skin past the cuff, appends the v2 body's hand as the last submesh (body material) from just inside the cuff,
    /// Bare-hand outfits only: "hand skin" is Hand + finger weight, so gloves or gauntlets past the wrist would be cut too.
    /// and returns the mesh in the v2 bone order and bind poses, so outfit hands bend exactly like the body's.
    /// </summary>
    public static class OutfitHandGraft
    {
        /// <summary>Hand + finger weight from which an outfit vertex counts as hand skin (the outfit skin masks use 0.3-0.6).</summary>
        private const float SkinWeight = .45f;
        /// <summary>Hand skin lies past this |x| (metres, rest pose); the wrist joint is at ~0.725.</summary>
        private const float HandMinX = .70f;
        /// <summary>The body hand starts this far inside the outfit's cut, tucked under the cuff.</summary>
        private const float Tuck = .008f;

        /// <summary>Material for submesh 1 (the grafted hands).</summary>
        public static Material HandMaterial => AssetDatabase.LoadAssetAtPath<Material>(BaseHeroV2Applier.MATERIAL)
            ?? throw new InvalidOperationException($"{BaseHeroV2Applier.MATERIAL} is missing: run Apply Base Hero V2 Body first.");

        /// <summary>
        /// <paramref name="outfit"/> is baked onto the old canonical rig (<paramref name="outfitBones"/> = its bone
        /// names, in bind-pose order). Returns a new mesh (caller owns it) and the v2 bone names it is bound to.
        /// </summary>
        public static Mesh Graft(Mesh outfit, IReadOnlyList<string> outfitBones, out string[] boneNames)
        {
            var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(BaseHeroV2Applier.V2_FBX).GetComponentInChildren<SkinnedMeshRenderer>();
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BaseHeroV2Applier.BODY_ASSET);
            if (v2 == null || body == null) throw new InvalidOperationException("Run Apply Base Hero V2 Body first.");
            boneNames = Array.ConvertAll(v2.bones, b => b.name);
            var index = new Dictionary<string, int>();
            for (int i = 0; i < boneNames.Length; i++) index[boneNames[i]] = i;
            // base_hero_body_baked holds the old canonical body until the applier has run: grafting from it would mix
            // the old bone order into v2 names silently.
            if (body.bindposes.Length != boneNames.Length || !index.ContainsKey("mixamorig:LeftHandRing1"))
                throw new InvalidOperationException($"{BaseHeroV2Applier.BODY_ASSET} is not the v2 body: run Apply Base Hero V2 Body first.");
            if (outfit.uv.Length != outfit.vertexCount || outfit.normals.Length != outfit.vertexCount)
                throw new InvalidOperationException($"{outfit.name} needs UV0 and normals.");
            int leftHand = index["mixamorig:LeftHand"], rightHand = index["mixamorig:RightHand"];

            // Outfit weights -> v2 indices. Old finger bones have other bind poses: their weight joins the hand.
            var map = new int[outfitBones.Count];
            var handLike = new bool[outfitBones.Count];
            for (int i = 0; i < map.Length; i++)
            {
                string name = outfitBones[i];
                handLike[i] = name.EndsWith("Hand") || BaseHeroV2Applier.IsFingerBone(name);
                if (BaseHeroV2Applier.IsFingerBone(name)) map[i] = name.Contains("Left") ? leftHand : rightHand;
                else if (!index.TryGetValue(name, out map[i])) throw new InvalidOperationException($"v2 rig lacks {name}.");
            }
            var oPos = outfit.vertices;
            var oCounts = outfit.GetBonesPerVertex();
            var oWeights = outfit.GetAllBoneWeights();
            var oStart = new int[oPos.Length];
            var skin = new bool[oPos.Length];
            for (int v = 0, c = 0; v < oPos.Length; v++)
            {
                oStart[v] = c;
                float hand = 0;
                for (int k = 0; k < oCounts[v]; k++, c++) if (handLike[oWeights[c].boneIndex]) hand += oWeights[c].weight;
                skin[v] = hand >= SkinWeight && Mathf.Abs(oPos[v].x) > HandMinX;
            }

            // Drop outfit triangles made only of hand skin; the cut per side is the innermost dropped vertex. Each outfit
            // submesh stays its own submesh (its own material); the hands are appended as the last one.
            var outfitSubs = new List<List<int>>();
            float cutL = float.PositiveInfinity, cutR = float.PositiveInfinity;
            for (int s = 0; s < outfit.subMeshCount; s++)
            {
                var outfitTris = new List<int>();
                outfitSubs.Add(outfitTris);
                var tris = outfit.GetTriangles(s);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    if (skin[a] && skin[b] && skin[c])
                    {
                        foreach (int v in new[] { a, b, c })
                            if (oPos[v].x > 0) cutR = Mathf.Min(cutR, oPos[v].x);
                            else cutL = Mathf.Min(cutL, -oPos[v].x);
                        continue;
                    }
                    outfitTris.Add(a); outfitTris.Add(b); outfitTris.Add(c);
                }
            }
            if (float.IsInfinity(cutL) || float.IsInfinity(cutR)) throw new InvalidOperationException("No outfit hand skin found on one side.");

            // Body hand triangles (submesh 0 only: the neck band is submesh 1) past the cut minus the tuck.
            var bPos = body.vertices;
            var bodyTris = new List<int>();
            var bt = body.GetTriangles(0);
            for (int t = 0; t < bt.Length; t += 3)
            {
                bool inHand = true;
                for (int j = 0; j < 3 && inHand; j++)
                {
                    var p = bPos[bt[t + j]];
                    inHand = p.x > 0 ? p.x > cutR - Tuck : -p.x > cutL - Tuck;
                }
                if (inHand) { bodyTris.Add(bt[t]); bodyTris.Add(bt[t + 1]); bodyTris.Add(bt[t + 2]); }
            }

            var mesh = new Mesh { name = outfit.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var tan = new List<Vector4>(); var uv = new List<Vector2>();
            var counts = new List<byte>(); var weights = new List<BoneWeight1>();
            var oN = outfit.normals; var oT = outfit.tangents; var oUv = outfit.uv;
            var bN = body.normals; var bT = body.tangents; var bUv = body.uv;
            var bCounts = body.GetBonesPerVertex(); var bWeights = body.GetAllBoneWeights();
            var bStart = new int[bPos.Length];
            for (int v = 0, c = 0; v < bPos.Length; v++) { bStart[v] = c; c += bCounts[v]; }

            int[] Compact(List<int> tris, Dictionary<int, int> remap, Func<int, int> add)
            {
                var result = new int[tris.Count];
                for (int i = 0; i < tris.Count; i++)
                {
                    if (!remap.TryGetValue(tris[i], out int m)) { m = add(tris[i]); remap.Add(tris[i], m); }
                    result[i] = m;
                }
                return result;
            }

            void AddWeights(IEnumerable<BoneWeight1> source)
            {
                var merged = new Dictionary<int, float>();
                foreach (var w in source) merged[w.boneIndex] = (merged.TryGetValue(w.boneIndex, out var x) ? x : 0) + w.weight;
                var list = new List<BoneWeight1>();
                foreach (var kv in merged) list.Add(new BoneWeight1 { boneIndex = kv.Key, weight = kv.Value });
                list.Sort((a, b) => b.weight.CompareTo(a.weight)); // Unity requires descending weights
                if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
                float sum = 0;
                foreach (var w in list) sum += w.weight;
                if (sum <= 0f) throw new InvalidOperationException("A grafted vertex has no bone weight.");
                for (int i = 0; i < list.Count; i++) list[i] = new BoneWeight1 { boneIndex = list[i].boneIndex, weight = list[i].weight / sum };
                counts.Add((byte)list.Count);
                weights.AddRange(list);
            }

            var outfitRemap = new Dictionary<int, int>(); // shared across outfit submeshes
            var outfitIdx = outfitSubs.ConvertAll(tris => Compact(tris, outfitRemap, v =>
            {
                pos.Add(oPos[v]); nrm.Add(oN[v]); tan.Add(oT.Length == oPos.Length ? oT[v] : new Vector4(1, 0, 0, 1)); uv.Add(oUv[v]);
                var src = new List<BoneWeight1>();
                for (int k = 0; k < oCounts[v]; k++)
                {
                    var w = oWeights[oStart[v] + k];
                    src.Add(new BoneWeight1 { boneIndex = map[w.boneIndex], weight = w.weight });
                }
                AddWeights(src);
                return pos.Count - 1;
            }));
            var handIdx = Compact(bodyTris, new Dictionary<int, int>(), v =>
            {
                pos.Add(bPos[v]); nrm.Add(bN[v]); tan.Add(bT.Length == bPos.Length ? bT[v] : new Vector4(1, 0, 0, 1)); uv.Add(bUv[v]);
                var src = new List<BoneWeight1>();
                for (int k = 0; k < bCounts[v]; k++) src.Add(bWeights[bStart[v] + k]);
                AddWeights(src);
                return pos.Count - 1;
            });

            mesh.SetVertices(pos); mesh.SetNormals(nrm); mesh.SetTangents(tan); mesh.SetUVs(0, uv);
            mesh.subMeshCount = outfitIdx.Count + 1;
            for (int s = 0; s < outfitIdx.Count; s++) mesh.SetTriangles(outfitIdx[s], s, false);
            mesh.SetTriangles(handIdx, outfitIdx.Count, false);
            using (var c = new NativeArray<byte>(counts.ToArray(), Allocator.Temp))
            using (var w = new NativeArray<BoneWeight1>(weights.ToArray(), Allocator.Temp))
                mesh.SetBoneWeights(c, w);
            mesh.bindposes = body.bindposes; // v2 order; non-finger poses equal the old rig's (BaseHeroV2Applier)
            if (oT.Length != oPos.Length || bT.Length != bPos.Length) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Points an outfit visual prefab at a grafted mesh: v2 bone names, outfit + hand materials.</summary>
        public static void ApplyToVisual(string prefabPath, Mesh mesh, string[] boneNames)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var part = root.GetComponentInChildren<Game.Inventory.SkinnedBodyPart>(true);
                if (part == null || part.Renderer == null)
                    throw new InvalidOperationException($"{prefabPath}: no SkinnedBodyPart with a renderer.");
                var renderer = part.Renderer;
                // Outfit materials (one per outfit submesh, a re-graft drops the previous hand slot), then the hands.
                var materials = new List<Material>(renderer.sharedMaterials);
                int outfitSlots = mesh.subMeshCount - 1;
                if (materials.Count < outfitSlots)
                    throw new InvalidOperationException($"{prefabPath}: {materials.Count} materials for {outfitSlots} outfit submeshes.");
                materials.RemoveRange(outfitSlots, materials.Count - outfitSlots);
                materials.Add(HandMaterial);
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = materials.ToArray();
                part.EditorSetup(renderer, boneNames, part.RootBoneName);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
