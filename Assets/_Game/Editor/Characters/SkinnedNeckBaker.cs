using System;
using System.Collections.Generic;
using Unity.Collections;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>Validated import-to-asset step for the sample canonical neck pipeline.</summary>
    public static class SkinnedNeckBaker
    {
        private const string TAG = "[SkinnedNeckBaker]";
        private const string MODELS = "Assets/_Game/Art/Characters/Humanoids/models/";

        [MenuItem("Tools/Characters/Bake Leather Neck Parts")]
        private static void BakeMenu()
        {
            try { GameLog.Info(TAG, BakeLeather()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.Message); }
        }

        public static string BakeLeather()
        {
            string splitPath = MODELS + "test base hero/base hero split.fbx";
            string armorPath = MODELS + "humanoid leather armor/leather armor neck.fbx";
            AssetDatabase.ImportAsset(splitPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(armorPath, ImportAssetOptions.ForceUpdate);
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(MODELS + "test base hero/rigged test hero with textures.fbx")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var parts = AssetDatabase.LoadAssetAtPath<GameObject>(splitPath).GetComponentsInChildren<SkinnedMeshRenderer>();
            var head = Array.Find(parts, part => part.name.Contains("_head"));
            var body = Array.Find(parts, part => part.name.Contains("_body"));
            var armor = AssetDatabase.LoadAssetAtPath<GameObject>(armorPath).GetComponentInChildren<SkinnedMeshRenderer>();
            if (head == null || body == null || armor == null || parts.Length != 2)
                throw new InvalidOperationException("Expected one head, one body and one armor renderer.");
            var meshes = new List<Mesh>();
            try
            {
                foreach (var source in new[] { head, body, armor })
                {
                    var mesh = SkinnedMeshBaker.Bake(source, target, null, source.name, out string error);
                    if (mesh == null) throw new InvalidOperationException(error);
                    meshes.Add(mesh);
                }
                var names = Array.ConvertAll(target.bones, bone => bone.name);
                int repairedCaps = RepairNeckUV(meshes[0], 1.48f, 1.5849f, .17f);
                AlignSeamTangents(meshes[0], meshes[1], 1.585f);
                Require(SkinnedNeckValidator.ValidateExactJoin(meshes[0], names, meshes[1], names, 1.585f));
                Require(SkinnedNeckValidator.ValidateBindSpace(meshes[2], names, target.sharedMesh, names));
                Require(SkinnedNeckValidator.ValidateNeckUV(meshes[0], 1.48f, 1.62f, .17f));
                int repaired = RepairNeckUV(meshes[2], 1.48f, 1.62f, .17f);
                Require(SkinnedNeckValidator.ValidateNeckUV(meshes[2], 1.48f, 1.62f, .17f));
                string[] outputs = {
                    MODELS + "test base hero/base_hero_head_baked.asset",
                    MODELS + "test base hero/base_hero_body_baked.asset",
                    MODELS + "humanoid leather armor/LeatherArmor_baked.asset"
                };
                // Validate the complete batch, then retain rollback copies until
                // all three GUID-preserving saves have succeeded.
                var backups = new Mesh[outputs.Length];
                var existed = new bool[outputs.Length];
                bool saveStarted = false;
                try
                {
                    for (int i = 0; i < outputs.Length; i++)
                    {
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputs[i]);
                        existed[i] = existing != null;
                        if (existing != null) backups[i] = UnityEngine.Object.Instantiate(existing);
                    }
                    saveStarted = true;
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        SkinnedMeshBaker.SaveMesh(meshes[i], outputs[i]);
                        meshes[i] = null; // SaveMesh transfers ownership or destroys the temporary.
                    }
                    AssetDatabase.SaveAssets();
                }
                catch (Exception failure)
                {
                    if (!saveStarted) throw;
                    var failures = new List<Exception> { failure };
                    for (int i = 0; i < outputs.Length; i++)
                        try
                        {
                            if (existed[i]) { SkinnedMeshBaker.SaveMesh(backups[i], outputs[i]); backups[i] = null; }
                            else if (AssetDatabase.LoadMainAssetAtPath(outputs[i]) != null) AssetDatabase.DeleteAsset(outputs[i]);
                        }
                        catch (Exception rollbackFailure) { failures.Add(rollbackFailure); }
                    if (failures.Count > 1) throw new AggregateException("Neck batch save and rollback failed.", failures);
                    throw;
                }
                finally
                {
                    foreach (var backup in backups) if (backup != null) UnityEngine.Object.DestroyImmediate(backup);
                }
                return $"Baked canonical head/body/armor; {repairedCaps} hidden-cap and {repaired} garment UV triangle repairs; all rest checks passed.";
            }
            finally
            {
                foreach (var mesh in meshes) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// Repairs collapsed UVs on a transient generated part mesh only. FBX
        /// import may share a repaired corner with another triangle; validate
        /// again after each pass. Valid triangles outside the neck are untouched.
        /// </summary>
        public static int RepairNeckUV(Mesh mesh, float minY, float maxY, float radius)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (float.IsNaN(minY) || float.IsInfinity(minY) || float.IsNaN(maxY) || float.IsInfinity(maxY) ||
                float.IsNaN(radius) || float.IsInfinity(radius) || minY >= maxY || radius <= 0)
                throw new ArgumentException("Provide finite ordered heights and a positive radius.");
            if (mesh.blendShapeCount != 0) throw new NotSupportedException("UV repair requires a generated mesh without blend shapes.");
            int originalCount = mesh.vertexCount;
            var vertices = new List<Vector3>(mesh.vertices);
            var normals = new List<Vector3>(mesh.normals);
            var oldTangents = mesh.tangents;
            var colors = new List<Color>(mesh.colors);
            var channels = new List<Vector4>[8];
            for (int channel = 0; channel < channels.Length; channel++)
            {
                channels[channel] = new List<Vector4>();
                mesh.GetUVs(channel, channels[channel]);
            }
            if (channels[0].Count != originalCount || normals.Count != originalCount)
                throw new InvalidOperationException("Part needs complete UV0 and normals.");
            var counts = mesh.GetBonesPerVertex();
            var allWeights = mesh.GetAllBoneWeights();
            var newCounts = new List<byte>();
            var newWeights = new List<BoneWeight1>();
            var starts = new int[originalCount];
            int cursor = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                starts[i] = cursor;
                newCounts.Add(counts[i]);
                for (int j = 0; j < counts[i]; j++) newWeights.Add(allWeights[cursor++]);
            }
            var indices = new int[mesh.subMeshCount][];
            int repaired = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    throw new NotSupportedException("UV repair requires triangle submeshes.");
                var triangles = mesh.GetTriangles(submesh);
                indices[submesh] = triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    var center = (vertices[a] + vertices[b] + vertices[c]) / 3;
                    if (center.y < minY || center.y > maxY || center.x * center.x + center.z * center.z > radius * radius) continue;
                    if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude < 1e-16f) continue;
                    var edge = (Vector2)(channels[0][b] - channels[0][a]);
                    var other = (Vector2)(channels[0][c] - channels[0][a]);
                    if (Mathf.Abs(edge.x * other.y - edge.y * other.x) * .5f >= 1e-10f) continue;
                    // Isolate all three corners: no neighbouring valid face,
                    // including one outside the fitting band, changes UVs.
                    int start = vertices.Count;
                    foreach (int source in new[] { a, b, c })
                    {
                        vertices.Add(vertices[source]);
                        normals.Add(normals[source]);
                        if (colors.Count > 0) colors.Add(colors[source]);
                        foreach (var channel in channels) if (channel.Count > 0) channel.Add(channel[source]);
                        if (counts.Length == originalCount)
                        {
                            newCounts.Add(counts[source]);
                            for (int j = 0; j < counts[source]; j++) newWeights.Add(allWeights[starts[source] + j]);
                        }
                    }
                    var origin = channels[0][a];
                    channels[0][start] = origin;
                    channels[0][start + 1] = origin + new Vector4(.001f, 0, 0, 0);
                    channels[0][start + 2] = origin + new Vector4(0, .001f, 0, 0);
                    triangles[i] = start; triangles[i + 1] = start + 1; triangles[i + 2] = start + 2;
                    repaired++;
                }
            }
            if (repaired == 0) return 0;
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.subMeshCount = indices.Length;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (colors.Count > 0) mesh.SetColors(colors);
            for (int channel = 0; channel < channels.Length; channel++)
                if (channels[channel].Count > 0) mesh.SetUVs(channel, channels[channel]);
            for (int submesh = 0; submesh < indices.Length; submesh++) mesh.SetTriangles(indices[submesh], submesh, false);
            if (counts.Length == originalCount)
            {
                using var countArray = new NativeArray<byte>(newCounts.ToArray(), Allocator.Temp);
                using var weightArray = new NativeArray<BoneWeight1>(newWeights.ToArray(), Allocator.Temp);
                mesh.SetBoneWeights(countArray, weightArray);
            }
            mesh.RecalculateTangents();
            var tangents = mesh.tangents;
            if (oldTangents.Length == originalCount)
            {
                Array.Copy(oldTangents, tangents, originalCount);
                mesh.tangents = tangents;
            }
            return repaired;
        }

        private static void Require(SkinnedNeckValidator.Report report)
        {
            if (!report.IsValid) throw new InvalidOperationException(string.Join("\n", report.Errors));
        }

        /// <summary>Shares the complete normal-map frame at an exposed canonical skin join.</summary>
        public static void AlignSeamTangents(Mesh head, Mesh body, float height)
        {
            var hp = head.vertices; var bp = body.vertices;
            var hu = head.uv; var bu = body.uv;
            var hn = head.normals; var bn = body.normals;
            var ht = head.tangents; var bt = body.tangents;
            if (ht.Length != hp.Length || bt.Length != bp.Length)
                throw new InvalidOperationException("Both skin meshes need complete tangent frames.");
            for (int i = 0; i < hp.Length; i++)
            {
                if (Mathf.Abs(hp[i].y - height) > .00001f) continue;
                int best = -1; float distance = float.PositiveInfinity;
                for (int j = 0; j < bp.Length; j++)
                {
                    float gap = Vector3.Distance(hp[i], bp[j]);
                    if (gap > .0001f || Vector2.Distance(hu[i], bu[j]) > .0001f || Vector3.Angle(hn[i], bn[j]) > 1) continue;
                    if (gap < distance) { best = j; distance = gap; }
                }
                if (best < 0) throw new InvalidOperationException("No canonical tangent-frame counterpart for head ring vertex " + i);
                hn[i] = bn[best];
                ht[i] = bt[best];
            }
            head.normals = hn;
            head.tangents = ht;
        }
    }
}
