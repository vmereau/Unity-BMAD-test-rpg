using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Game.Core;
using Game.Editor.Save;
using Game.Inventory;
using Game.World;

namespace Game.Editor
{
    /// <summary>Values for one generic item built from a Meshy model folder (see <see cref="ItemAssetBuilder"/>).</summary>
    public class ItemBuildRequest
    {
        /// <summary>Folder under Assets/ holding one FBX, the Meshy PNG maps and optionally one icon PNG.</summary>
        public string SourceFolder;
        /// <summary>PascalCase asset name (e.g. "Lockpick") — used for every generated file name.</summary>
        public string Name;
        public string DisplayName;
        public string Description;
        public int MaxStacks = 1;
        public int BuyValue = 1;
        public int SellValue = 1;
        /// <summary>Largest dimension of the world model, in metres.</summary>
        public float TargetSize = 0.3f;
    }

    public class ItemBuildResult
    {
        public bool Success;
        public string Error;
        public readonly List<string> Warnings = new();
        public string ItemPath;
        public string PrefabPath;
        public string MaterialPath;
        public string ModelFolder;
        public string IconPath;
        /// <summary>True when no icon PNG was found — run Tools/Items/Generate Missing Icons.</summary>
        public bool IconMissing;

        public override string ToString()
        {
            if (!Success) return $"FAILED: {Error}";
            var sb = new StringBuilder("OK\n");
            sb.AppendLine($"Item: {ItemPath}");
            sb.AppendLine($"Prefab: {PrefabPath}");
            sb.AppendLine($"Material: {MaterialPath}");
            sb.AppendLine($"Model folder: {ModelFolder}");
            sb.AppendLine(IconMissing ? "Icon: MISSING" : $"Icon: {IconPath}");
            foreach (string w in Warnings)
                sb.AppendLine($"Warning: {w}");
            return sb.ToString();
        }
    }

    /// <summary>Source files of a Meshy download, sorted by role. Built by <see cref="ItemAssetBuilder.ClassifySourceFiles"/>.</summary>
    public class ItemSourceFiles
    {
        public readonly List<string> Fbx = new();
        public string BaseMap;
        public string NormalMap;
        public string MetallicMap;
        public string RoughnessMap;
        public readonly List<string> IconCandidates = new();
        public readonly List<string> Errors = new();
    }

    /// <summary>
    /// Builds a generic pickable item from a Meshy folder: organizes the model / maps / icon, creates a URP Lit
    /// material, the world prefab (ItemPickup + Rigidbody + BoxCollider + InteractionHighlight) and the ItemSO.
    /// Driven by the /items:create Claude command through execute_code.
    /// </summary>
    public static class ItemAssetBuilder
    {
        private const string TAG = "[ItemBuilder]";
        private const string ART_ROOT = "Assets/_Game/Art/Items";
        private const string ICONS_PATH = "Assets/_Game/Art/UI/Items";
        private const string ITEMS_PATH = "Assets/_Game/Data/Items";
        private const string PREFABS_PATH = "Assets/_Game/Prefabs/Items";
        private const string LIT_SHADER = "Universal Render Pipeline/Lit";
        private const float DEFAULT_SMOOTHNESS = 0.3f;

        private const string BASE_SUFFIX = "_texture.png";
        private const string NORMAL_SUFFIX = "_texture_normal.png";
        private const string METALLIC_SUFFIX = "_texture_metallic.png";
        private const string ROUGHNESS_SUFFIX = "_texture_roughness.png";

        private static readonly Regex ValidName = new(@"^[A-Z][A-Za-z0-9]*$");

        /// <summary>Sorts file paths by role. Pure — no AssetDatabase access. .meta files are ignored.</summary>
        public static ItemSourceFiles ClassifySourceFiles(IEnumerable<string> paths)
        {
            var files = new ItemSourceFiles();
            foreach (string path in paths)
            {
                string lower = path.ToLowerInvariant();
                if (lower.EndsWith(".meta")) continue;

                if (lower.EndsWith(".fbx")) files.Fbx.Add(path);
                else if (lower.EndsWith(NORMAL_SUFFIX)) files.NormalMap = SetOnce(files, files.NormalMap, path, "normal map");
                else if (lower.EndsWith(METALLIC_SUFFIX)) files.MetallicMap = SetOnce(files, files.MetallicMap, path, "metallic map");
                else if (lower.EndsWith(ROUGHNESS_SUFFIX)) files.RoughnessMap = SetOnce(files, files.RoughnessMap, path, "roughness map");
                else if (lower.EndsWith(BASE_SUFFIX)) files.BaseMap = SetOnce(files, files.BaseMap, path, "base color map");
                else if (lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")) files.IconCandidates.Add(path);
            }

            if (files.Fbx.Count != 1)
                files.Errors.Add($"Expected exactly 1 FBX, found {files.Fbx.Count}.");
            if (files.BaseMap == null)
                files.Errors.Add($"No base color map (*{BASE_SUFFIX}) found.");
            if (files.IconCandidates.Count > 1)
                files.Errors.Add($"Ambiguous icon — {files.IconCandidates.Count} non-Meshy images: {string.Join(", ", files.IconCandidates)}.");
            return files;
        }

        private static string SetOnce(ItemSourceFiles files, string current, string path, string role)
        {
            if (current == null) return path;
            files.Errors.Add($"Two {role}s found: {current}, {path}.");
            return current;
        }

        public static ItemBuildResult Build(ItemBuildRequest request)
        {
            var result = new ItemBuildResult();
            string folder = request.SourceFolder?.Replace('\\', '/').TrimEnd('/');

            // 1. Validate everything before touching any asset.
            string error = Validate(request, folder, out ItemSourceFiles files);
            if (error != null)
            {
                result.Error = error;
                GameLog.Error(TAG, error);
                return result;
            }

            string name = request.Name;
            result.ModelFolder = $"{ART_ROOT}/{name}";
            result.MaterialPath = $"{result.ModelFolder}/M_{name}.mat";
            result.ItemPath = $"{ITEMS_PATH}/Item_{name}.asset";
            result.PrefabPath = $"{PREFABS_PATH}/Item_{name}.prefab";
            if (request.SellValue > request.BuyValue)
                Warn(result, $"SellValue ({request.SellValue}) > BuyValue ({request.BuyValue}) — buy/sell loop at traders.");

            // Everything moved or created is recorded so a failure can be rolled back (no partial output).
            var moves = new List<(string from, string to)>();
            var created = new List<string>();
            try
            {
                // 2. Move and rename the source assets (MoveAsset keeps GUIDs and .meta files).
                EnsureFolder(ART_ROOT);
                EnsureFolder(result.ModelFolder);
                created.Add(result.ModelFolder);

                string fbxPath = Move(files.Fbx[0], $"{result.ModelFolder}/{name}.fbx", moves);
                string basePath = Move(files.BaseMap, $"{result.ModelFolder}/{name}_BaseColor.png", moves);
                string normalPath = MoveOptional(files.NormalMap, $"{result.ModelFolder}/{name}_Normal.png", "normal", moves, result);
                string metallicPath = MoveOptional(files.MetallicMap, $"{result.ModelFolder}/{name}_Metallic.png", "metallic", moves, result);
                string roughnessPath = MoveOptional(files.RoughnessMap, $"{result.ModelFolder}/{name}_Roughness.png", "roughness", moves, result);
                if (files.IconCandidates.Count == 1)
                    result.IconPath = Move(files.IconCandidates[0], IconTarget(name, files.IconCandidates[0]), moves);

                // 3. Importer settings.
                ConfigureTextures(normalPath, metallicPath, roughnessPath);
                Sprite icon = result.IconPath != null ? ConfigureIcon(result.IconPath) : null;
                if (result.IconPath != null && icon == null)
                    Warn(result, $"'{result.IconPath}' did not import as a Sprite — icon left empty.");
                result.IconMissing = icon == null;

                // 4. Material, remapped onto the FBX's embedded materials.
                Material material = CreateMaterial(result.MaterialPath, basePath, normalPath, metallicPath);
                created.Add(result.MaterialPath);
                ConfigureModel(fbxPath, material, result);

                // 5. ItemSO first, so the prefab's ItemPickup can reference it.
                var item = ScriptableObject.CreateInstance<ItemSO>();
                item.itemId = Guid.NewGuid().ToString("N");
                item.itemName = request.DisplayName;
                item.description = request.Description;
                item.maxStacks = request.MaxStacks;
                item.buyValue = request.BuyValue;
                item.sellValue = request.SellValue;
                item.icon = icon;
                AssetDatabase.CreateAsset(item, result.ItemPath);
                created.Add(result.ItemPath);

                // 6. World prefab, then wire it back.
                GameObject prefab = CreateWorldPrefab(result.PrefabPath, fbxPath, item, request.TargetSize, result);
                created.Add(result.PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("World prefab could not be saved.");
                item.worldItemPrefab = prefab;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Rollback(moves, created);
                result.Error = $"{ex.Message} — all moves and created assets were rolled back.";
                GameLog.Error(TAG, result.Error);
                return result;
            }

            // The postprocessor's sync is deferred to delayCall (needs an editor tick) — sync now instead.
            ItemCatalogAutoSync.SyncItemCatalog();
            DeleteIfEmpty(folder);

            result.Success = true;
            GameLog.Info(TAG, result.ToString());
            return result;
        }

        // Keeps the source extension — MoveAsset can't convert a .jpg into a .png.
        private static string IconTarget(string name, string source) =>
            $"{ICONS_PATH}/Item_{name}_Icon{Path.GetExtension(source).ToLowerInvariant()}";

        private static void Warn(ItemBuildResult result, string message)
        {
            result.Warnings.Add(message);
            GameLog.Warn(TAG, message);
        }

        private static void Rollback(List<(string from, string to)> moves, List<string> created)
        {
            // Generated assets first, then undo the moves, then drop folders left empty.
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (!AssetDatabase.IsValidFolder(created[i]) && File.Exists(created[i]))
                    AssetDatabase.DeleteAsset(created[i]);
            }
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                string error = AssetDatabase.MoveAsset(moves[i].to, moves[i].from);
                if (!string.IsNullOrEmpty(error))
                    GameLog.Error(TAG, $"Rollback: could not move '{moves[i].to}' back to '{moves[i].from}': {error}");
            }
            foreach (string path in created)
            {
                if (AssetDatabase.IsValidFolder(path) && Directory.GetFileSystemEntries(path).Length == 0)
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static string Validate(ItemBuildRequest request, string folder, out ItemSourceFiles files)
        {
            files = null;
            if (request == null) return "Request is null.";
            if (string.IsNullOrEmpty(request.Name) || !ValidName.IsMatch(request.Name))
                return $"Name '{request.Name}' must be PascalCase letters/digits (e.g. Lockpick).";
            if (string.IsNullOrWhiteSpace(request.DisplayName)) return "DisplayName is empty.";
            if (request.MaxStacks < 1) return "MaxStacks must be >= 1.";
            if (request.BuyValue < 0 || request.SellValue < 0) return "Buy/sell values must be >= 0.";
            if (!(request.TargetSize > 0f) || float.IsInfinity(request.TargetSize))
                return "TargetSize must be a finite value > 0.";
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets/") || !AssetDatabase.IsValidFolder(folder))
                return $"Source folder '{request.SourceFolder}' is not an existing folder under Assets/.";

            var paths = new List<string>();
            foreach (string file in Directory.GetFiles(folder))
                paths.Add(file.Replace('\\', '/'));
            files = ClassifySourceFiles(paths);
            if (files.Errors.Count > 0) return string.Join(" ", files.Errors);

            // Files copied in while Unity hasn't refreshed have no asset entry — MoveAsset would fail mid-build.
            foreach (string path in paths)
            {
                if (!path.EndsWith(".meta") && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                    return $"'{path}' is not imported yet — refresh the AssetDatabase and retry.";
            }

            string name = request.Name;
            string[] targets =
            {
                $"{ART_ROOT}/{name}", $"{ICONS_PATH}/Item_{name}_Icon.png",
                $"{ITEMS_PATH}/Item_{name}.asset", $"{PREFABS_PATH}/Item_{name}.prefab",
                files.IconCandidates.Count == 1 ? IconTarget(name, files.IconCandidates[0]) : null,
            };
            foreach (string target in targets)
            {
                if (target != null && (AssetDatabase.IsValidFolder(target) || File.Exists(target)))
                    return $"'{target}' already exists — choose another name or remove it first.";
            }

            if (Shader.Find(LIT_SHADER) == null) return $"Shader '{LIT_SHADER}' not found.";
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Move(string from, string to, List<(string from, string to)> moves)
        {
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Move '{from}' -> '{to}' failed: {error}");
            moves.Add((from, to));
            return to;
        }

        private static string MoveOptional(string from, string to, string role,
            List<(string from, string to)> moves, ItemBuildResult result)
        {
            if (from != null) return Move(from, to, moves);
            Warn(result, $"No {role} map — material built without it.");
            return null;
        }

        private static void ConfigureTextures(string normalPath, string metallicPath, string roughnessPath)
        {
            if (normalPath != null && AssetImporter.GetAtPath(normalPath) is TextureImporter normal)
            {
                normal.textureType = TextureImporterType.NormalMap;
                normal.SaveAndReimport();
            }
            // Data maps, not colors.
            foreach (string linearPath in new[] { metallicPath, roughnessPath })
            {
                if (linearPath != null && AssetImporter.GetAtPath(linearPath) is TextureImporter linear)
                {
                    linear.sRGBTexture = false;
                    linear.SaveAndReimport();
                }
            }
        }

        private static Sprite ConfigureIcon(string iconPath)
        {
            if (AssetImporter.GetAtPath(iconPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        }

        // URP Lit reads smoothness from the metallic map's alpha; Meshy maps have none (alpha = 1),
        // so _Smoothness acts as a constant. The roughness map is kept but unused.
        private static Material CreateMaterial(string path, string basePath, string normalPath, string metallicPath)
        {
            var material = new Material(Shader.Find(LIT_SHADER));
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            material.SetTexture("_BaseMap", baseMap);
            material.mainTexture = baseMap;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", DEFAULT_SMOOTHNESS);

            if (normalPath != null)
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.EnableKeyword("_NORMALMAP");
            }
            if (metallicPath != null)
            {
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureModel(string fbxPath, Material material, ItemBuildResult result)
        {
            if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
                throw new InvalidOperationException($"'{fbxPath}' has no ModelImporter.");
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;

            // Embedded materials, plus source materials already remapped (they no longer appear as sub-assets).
            var sourceNames = new HashSet<string>();
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (sub is Material embedded) sourceNames.Add(embedded.name);
            }
            foreach (var id in importer.GetExternalObjectMap().Keys)
            {
                if (id.type == typeof(Material)) sourceNames.Add(id.name);
            }

            int remapped = 0;
            foreach (string sourceName in sourceNames)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), material);
                remapped++;
            }
            if (remapped == 0)
                Warn(result, $"'{fbxPath}' has no embedded material to remap — assign M_ material on the prefab's Model renderer by hand.");
            importer.SaveAndReimport();
        }

        private static GameObject CreateWorldPrefab(string path, string fbxPath, ItemSO item, float targetSize,
            ItemBuildResult result)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
                throw new InvalidOperationException($"'{fbxPath}' did not import as a model.");
            var root = new GameObject($"Item_{Path.GetFileNameWithoutExtension(fbxPath)}");
            try
            {
                int interactable = LayerMask.NameToLayer("Interactable");
                if (interactable >= 0)
                    root.layer = interactable;
                else
                    Warn(result, "'Interactable' layer not found — prefab left on Default layer.");

                root.AddComponent<ItemPickup>().Configure(item);
                var body = root.AddComponent<Rigidbody>();
                // Small items dropped with an impulse can tunnel through thin ground colliders.
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                root.AddComponent<InteractionHighlight>();

                // Nested model instance (like TestItem_Health_Potion) so FBX reimports flow into the prefab.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";

                // Scale so the largest dimension equals targetSize, then centre the mesh on the root.
                if (WeaponModelUtility.TryGetRendererBounds(instance.transform, root.transform, out Bounds raw))
                {
                    float largest = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
                    if (largest > 0f)
                        instance.transform.localScale *= targetSize / largest;
                    WeaponModelUtility.TryGetRendererBounds(instance.transform, root.transform, out Bounds scaled);
                    instance.transform.localPosition -= scaled.center;
                }
                else
                    Warn(result, "Model has no renderers — not scaled, collider left at default size.");

                var box = root.AddComponent<BoxCollider>();
                if (WeaponModelUtility.TryGetRendererBounds(instance.transform, root.transform, out Bounds fitted))
                {
                    box.center = fitted.center;
                    box.size = fitted.size;
                }

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // Removes the source folder only when every file was moved out — never deletes leftover files.
        private static void DeleteIfEmpty(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                AssetDatabase.DeleteAsset(folder);
        }
    }
}
