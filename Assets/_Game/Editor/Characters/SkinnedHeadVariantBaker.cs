using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.NPC;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Bakes NPC head variants (spec-npc-head-variants). A folder under <c>models/heads/&lt;Name&gt;/</c> holds
    /// <c>Head_&lt;Name&gt;.fbx</c> + <c>T_Head_&lt;Name&gt;_BaseColor.png</c> from <c>Tools/Blender/make_head_variant.py</c>
    /// (a folder without that FBX is skipped with a warning). For each head: apply the FBX import contract (importer
    /// settings are written even if validation later fails - they are the fixed contract, not baked data), bake the head
    /// into the canonical bind space, repair the hidden cap UVs, share the canonical body's seam normals / tangents and
    /// validate the exact skin join + neck UVs + bind space. Only when every head and texture is valid are the outputs
    /// written: <c>Head_&lt;Name&gt;_baked.asset</c> (GUID kept), <c>M_Head_&lt;Name&gt;.mat</c> (base-hero material
    /// with the variant texture) and <c>Data/Characters/Heads/HeadAppearance_&lt;Name&gt;.asset</c>. A failed save
    /// restores or deletes every output already written.
    /// </summary>
    public static class SkinnedHeadVariantBaker
    {
        private const string TAG = "[HeadVariantBaker]";
        private const string MODELS = "Assets/_Game/Art/Characters/Humanoids/models/";
        public const string HEADS = MODELS + "heads";
        public const string APPEARANCES = "Assets/_Game/Data/Characters/Heads";
        private const string TARGET = MODELS + "test base hero/rigged test hero with textures.fbx";
        private const string BODY = MODELS + "test base hero/base_hero_body_baked.asset";
        private const string BASE_MATERIAL = MODELS + "test base hero/M_TestBaseHero.mat";
        // Canonical neck interface (Humanoids/CLAUDE.md): exposed skin seam, hidden stump bottom, neck-UV check top, radius.
        private const float SEAM = 1.585f;
        private const float STUMP_BOTTOM = 1.48f;
        private const float NECK_UV_TOP = 1.62f;
        private const float NECK_RADIUS = .17f;

        private struct Head
        {
            public string Name, Folder;
            public Mesh Mesh;
            public Texture2D Texture;
        }

        [MenuItem("Tools/Characters/Bake Head Variants")]
        private static void BakeMenu()
        {
            try { GameLog.Info(TAG, BakeAll()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.ToString()); }
        }

        public static string BakeAll()
        {
            if (!AssetDatabase.IsValidFolder(HEADS)) throw new InvalidOperationException($"No head folder at {HEADS}.");
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(TARGET)?.GetComponentInChildren<SkinnedMeshRenderer>();
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BODY);
            var baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BASE_MATERIAL);
            if (target == null || body == null || baseMaterial == null)
                throw new InvalidOperationException("Canonical target, baked body or base material is missing.");
            if (Array.Exists(target.bones, bone => bone == null))
                throw new InvalidOperationException("The canonical target has a missing bone.");
            var names = Array.ConvertAll(target.bones, bone => bone.name);

            var heads = new List<Head>();
            try
            {
                foreach (string folder in AssetDatabase.GetSubFolders(HEADS))
                {
                    string name = Path.GetFileName(folder);
                    string fbx = $"{folder}/Head_{name}.fbx";
                    if (AssetDatabase.LoadMainAssetAtPath(fbx) == null)
                    {
                        GameLog.Warn(TAG, $"{folder}: no Head_{name}.fbx - skipped");
                        continue;
                    }
                    string texturePath = $"{folder}/T_Head_{name}_BaseColor.png";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture == null) throw new InvalidOperationException($"{name}: missing {texturePath}.");
                    ConfigureModel(fbx);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                    var source = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                    if (source == null) throw new InvalidOperationException($"{name}: {fbx} has no SkinnedMeshRenderer.");
                    var mesh = SkinnedMeshBaker.Bake(source, target, null, $"Head_{name}_baked", out string error);
                    if (mesh == null) throw new InvalidOperationException($"{name}: {error}");
                    heads.Add(new Head { Name = name, Folder = folder, Mesh = mesh, Texture = texture });
                    SkinnedNeckBaker.RepairNeckUV(mesh, STUMP_BOTTOM, SEAM - .0001f, NECK_RADIUS);
                    SkinnedNeckBaker.AlignSeamTangents(mesh, body, SEAM);
                    Require(name, SkinnedNeckValidator.ValidateExactJoin(mesh, names, body, names, SEAM));
                    Require(name, SkinnedNeckValidator.ValidateNeckUV(mesh, STUMP_BOTTOM, NECK_UV_TOP, NECK_RADIUS));
                    Require(name, SkinnedNeckValidator.ValidateBindSpace(mesh, names, target.sharedMesh, names));
                }
                if (heads.Count == 0) return "No head variants found.";
                Save(heads, baseMaterial);
                return $"Baked {heads.Count} head variants ({string.Join(", ", heads.ConvertAll(h => h.Name))}); " +
                       "exact join, neck UV and bind-space checks passed.";
            }
            finally
            {
                foreach (var head in heads) if (head.Mesh != null) UnityEngine.Object.DestroyImmediate(head.Mesh);
            }
        }

        /// <summary>Same import contract as <c>base hero split.fbx</c>: x100, imported normals, Mikk tangents, Generic, no clips.</summary>
        private static void ConfigureModel(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                throw new InvalidOperationException($"{path} is not imported as a model.");
            bool dirty = false;
            void Set<T>(T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                apply(wanted);
                dirty = true;
            }
            Set(importer.globalScale, 100f, v => importer.globalScale = v);
            Set(importer.useFileScale, true, v => importer.useFileScale = v);
            Set(importer.importNormals, ModelImporterNormals.Import, v => importer.importNormals = v);
            Set(importer.importTangents, ModelImporterTangents.CalculateMikk, v => importer.importTangents = v);
            Set(importer.animationType, ModelImporterAnimationType.Generic, v => importer.animationType = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.None, v => importer.materialImportMode = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>
        /// Writes every head's mesh, material and appearance. Each output path is snapshotted first (a copy of the
        /// existing asset, or "did not exist"); on any failure all snapshots are restored, then the original error is
        /// rethrown (rollback failures are attached, never replace it).
        /// </summary>
        private static void Save(List<Head> heads, Material baseMaterial)
        {
            EnsureFolder(APPEARANCES);
            var snapshots = new List<(string path, UnityEngine.Object backup)>();
            void Snapshot(string path)
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                snapshots.Add((path, existing != null ? UnityEngine.Object.Instantiate(existing) : null));
            }
            try
            {
                for (int i = 0; i < heads.Count; i++)
                {
                    var head = heads[i];
                    string meshPath = $"{head.Folder}/Head_{head.Name}_baked.asset";
                    string materialPath = $"{head.Folder}/M_Head_{head.Name}.mat";
                    string appearancePath = $"{APPEARANCES}/HeadAppearance_{head.Name}.asset";
                    Snapshot(meshPath);
                    Snapshot(materialPath);
                    Snapshot(appearancePath);
                    var saved = SkinnedMeshBaker.SaveMesh(head.Mesh, meshPath);
                    head.Mesh = null; // SaveMesh transfers ownership or destroys the temporary
                    heads[i] = head;
                    var material = BuildMaterial(materialPath, head.Texture, baseMaterial);
                    BuildAppearance(appearancePath, saved, material);
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception failure)
            {
                var rollbackFailures = new List<Exception>();
                for (int i = snapshots.Count - 1; i >= 0; i--)
                {
                    var (path, backup) = snapshots[i];
                    try { Restore(path, backup); }
                    catch (Exception rollback) { rollbackFailures.Add(rollback); }
                }
                try { AssetDatabase.SaveAssets(); }
                catch (Exception rollback) { rollbackFailures.Add(rollback); }
                if (rollbackFailures.Count > 0)
                {
                    rollbackFailures.Insert(0, failure);
                    throw new AggregateException("Head variant save failed and its rollback was incomplete.", rollbackFailures);
                }
                throw;
            }
            finally
            {
                foreach (var (_, backup) in snapshots)
                    if (backup != null && !EditorUtility.IsPersistent(backup)) UnityEngine.Object.DestroyImmediate(backup);
            }
        }

        private static void Restore(string path, UnityEngine.Object backup)
        {
            var current = AssetDatabase.LoadMainAssetAtPath(path);
            if (backup == null)
            {
                if (current != null) AssetDatabase.DeleteAsset(path);
                return;
            }
            if (backup is Mesh mesh)
            {
                SkinnedMeshBaker.SaveMesh(UnityEngine.Object.Instantiate(mesh), path);
                return;
            }
            if (current == null) throw new InvalidOperationException($"Cannot restore deleted {path}.");
            if (current is Material material && backup is Material previous) material.shader = previous.shader;
            EditorUtility.CopySerialized(backup, current);
            EditorUtility.SetDirty(current);
        }

        private static Material BuildMaterial(string path, Texture2D texture, Material baseMaterial)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(baseMaterial);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = baseMaterial.shader; // CopyPropertiesFromMaterial keeps the old shader
                material.CopyPropertiesFromMaterial(baseMaterial);
            }
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            // The base hero's skin mask matches its own hair, not the variant's repainted hair / beard (spec-npc-skin-tones):
            // a variant has no mask until it gets its own, so toned NPCs keep the variant head's skin.
            if (material.HasProperty("_SkinMask")) material.SetTexture("_SkinMask", null);
            if (material.HasProperty("_SkinToneStrength")) material.SetFloat("_SkinToneStrength", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildAppearance(string path, Mesh mesh, Material material)
        {
            var appearance = AssetDatabase.LoadAssetAtPath<HeadAppearanceSO>(path);
            if (appearance == null)
            {
                appearance = ScriptableObject.CreateInstance<HeadAppearanceSO>();
                AssetDatabase.CreateAsset(appearance, path);
            }
            appearance.EditorSet(mesh, material);
            EditorUtility.SetDirty(appearance);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Require(string name, SkinnedNeckValidator.Report report)
        {
            if (!report.IsValid) throw new InvalidOperationException($"{name}: " + string.Join("\n", report.Errors));
        }
    }
}
