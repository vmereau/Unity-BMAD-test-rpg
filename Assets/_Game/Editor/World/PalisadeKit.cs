using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Reads the palisade kit (the model and the sizes file its Blender script writes beside it) and keeps the wall
    /// meshes Camp Edge builds from it in one asset, so the scene references assets instead of embedding 160 meshes.
    /// </summary>
    internal static class PalisadeKit
    {
        internal const string MODEL = "Assets/_Game/Art/Environment/Camp/models/palisade/Palisade.fbx";
        internal const string SIZES = "Assets/_Game/Art/Environment/Camp/models/palisade/Palisade.json";
        internal const string MESHES = "Assets/_Game/Art/Environment/Camp/Generated/Palisade.asset";

        private const string MATERIALS = "Assets/_Game/Art/Environment/Camp/Materials/";
        // In PalisadeMesh's submesh order: weathered wood, the damp foot, rope.
        private static readonly string[] MaterialNames = { "M_Camp_PalisadeLog", "M_Camp_PalisadeFoot", "M_Camp_Rope" };
        private const string HOLDER = "Palisade";

        internal static bool TryLoad(out PalisadeKitData kit, out Material[] materials, out string problem)
        {
            kit = null;
            materials = null;

            string sizesPath = Path.GetFullPath(SIZES);
            if (!File.Exists(sizesPath))
            {
                problem = $"No palisade sizes file at {SIZES} (run build_palisade.py in Blender)";
                return false;
            }

            Dictionary<string, Mesh> meshes = AssetDatabase.LoadAllAssetsAtPath(MODEL).OfType<Mesh>()
                .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
            if (meshes.Count == 0)
            {
                problem = $"No palisade model at {MODEL} (run build_palisade.py in Blender)";
                return false;
            }

            JObject sizes;
            try
            {
                sizes = JObject.Parse(File.ReadAllText(sizesPath));
            }
            catch (System.Exception e) when (e is Newtonsoft.Json.JsonException || e is IOException)
            {
                problem = $"{SIZES} cannot be read: {e.Message}";
                return false;
            }

            foreach (string field in new[] { "head", "tile", "railOffset", "postOffset" })
            {
                if (Area1BlockoutBuilder.IsNumber(sizes[field])) continue;
                problem = $"{SIZES} has no number '{field}'";
                return false;
            }

            if ((float)sizes["head"] <= 0f || (float)sizes["tile"] <= 0f)
            {
                problem = $"{SIZES}: 'head' and 'tile' must be above 0"; // a log with no shaft, or a grain of infinite scale
                return false;
            }

            var data = new PalisadeKitData
            {
                Head = (float)sizes["head"],
                Tile = (float)sizes["tile"],
                RailOffset = (float)sizes["railOffset"],
                PostOffset = (float)sizes["postOffset"],
            };

            if (!TryPieces(sizes["logs"], meshes, data.Head, "logs", out data.Logs, out problem)) return false;
            if (!TryPieces(sizes["posts"], meshes, data.Head, "posts", out data.Posts, out problem)) return false;
            foreach (PalisadePiece log in data.Logs)
            {
                if (log.Shoulder > 0f && log.Shoulder < data.Head) continue;
                problem = $"{SIZES}: every log needs a 'shoulder' between 0 and 'head' (where its point starts)";
                return false;
            }
            if (!TryPiece(sizes["rail"], meshes, 0f, out data.Rail, out problem)) return false;
            if (!TryPiece(sizes["lashing"], meshes, 0f, out data.Lashing, out problem)) return false;

            var found = new Material[MaterialNames.Length];
            for (int i = 0; i < found.Length; i++)
            {
                found[i] = AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + MaterialNames[i] + ".mat");
                if (found[i] != null) continue;
                problem = $"No material {MATERIALS}{MaterialNames[i]}.mat";
                return false;
            }

            kit = data;
            materials = found;
            problem = null;
            return true;
        }

        private static bool TryPieces(JToken list, Dictionary<string, Mesh> meshes, float head, string field, out PalisadePiece[] pieces, out string problem)
        {
            pieces = null;
            if (!(list is JArray array) || array.Count == 0)
            {
                problem = $"{SIZES} has no list '{field}'";
                return false;
            }

            var found = new List<PalisadePiece>();
            foreach (JToken entry in array)
            {
                if (!TryPiece(entry, meshes, head, out PalisadePiece piece, out problem)) return false;
                found.Add(piece);
            }

            pieces = found.ToArray();
            problem = null;
            return true;
        }

        /// <summary>A <paramref name="head"/> above 0 means the piece is a log or post head and must end in an open ring there.</summary>
        private static bool TryPiece(JToken entry, Dictionary<string, Mesh> meshes, float head, out PalisadePiece piece, out string problem)
        {
            piece = null;
            string name = entry is JObject ? (string)entry["name"] : null;
            if (string.IsNullOrEmpty(name) || !meshes.TryGetValue(name, out Mesh mesh))
            {
                problem = $"{MODEL} has no mesh '{name}' named in {SIZES}";
                return false;
            }

            var read = new PalisadePiece
            {
                Vertices = mesh.vertices,
                Normals = mesh.normals,
                Uvs = mesh.uv,
                Triangles = mesh.triangles,
                Radius = Area1BlockoutBuilder.IsNumber(entry["radius"]) ? (float)entry["radius"] : 0f,
                Shoulder = Area1BlockoutBuilder.IsNumber(entry["shoulder"]) ? (float)entry["shoulder"] : 0f,
            };
            if (read.Normals.Length != read.Vertices.Length || read.Uvs.Length != read.Vertices.Length)
            {
                problem = $"Mesh '{name}' in {MODEL} has no normals or no UVs";
                return false;
            }

            if (head > 0f)
            {
                read.Ring = PalisadeMesh.FindRing(read.Vertices, head);
                int sides = Area1BlockoutBuilder.IsNumber(entry["sides"]) ? (int)entry["sides"] : 0;
                if (read.Radius <= 0f || read.Ring.Length < 3 || (sides > 0 && read.Ring.Length != sides))
                {
                    problem = $"Mesh '{name}' in {MODEL} does not end in an open ring of {sides} points {head} m under its top "
                        + $"(found {read.Ring.Length}); the model and {SIZES} are a version apart";
                    return false;
                }
            }

            piece = read;
            problem = null;
            return true;
        }

        /// <summary>
        /// Writes the wall meshes into <see cref="MESHES"/> by name. A mesh already there is refilled, so its id and the
        /// scene's reference to it survive a rebuild; one no panel asked for is removed, unless
        /// <paramref name="keepOthers"/> (a look test on one line must not strip the other lines). Returns the saved meshes.
        /// </summary>
        internal static Dictionary<string, Mesh> Save(Dictionary<string, Mesh> built, bool keepOthers)
        {
            string folder = Path.GetDirectoryName(MESHES).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));

            // Every object in the file is a mesh, so the holder is found by its name, never by being the main object.
            List<Mesh> stored = AssetDatabase.LoadAllAssetsAtPath(MESHES).OfType<Mesh>().ToList();
            Mesh holder = stored.FirstOrDefault(m => m.name == HOLDER);
            if (holder == null)
            {
                holder = new Mesh { name = HOLDER }; // an empty main object: the wall meshes hang under it
                if (stored.Count == 0) AssetDatabase.CreateAsset(holder, MESHES);
                else AssetDatabase.AddObjectToAsset(holder, MESHES);
            }
            AssetDatabase.SetMainObject(holder, MESHES);

            Dictionary<string, Mesh> existing = stored
                .Where(m => m != holder).GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());

            var saved = new Dictionary<string, Mesh>();
            foreach (KeyValuePair<string, Mesh> pair in built)
            {
                if (existing.TryGetValue(pair.Key, out Mesh kept))
                {
                    kept.Clear();
                    EditorUtility.CopySerialized(pair.Value, kept);
                    kept.name = pair.Key;
                    EditorUtility.SetDirty(kept);
                    Object.DestroyImmediate(pair.Value);
                    saved[pair.Key] = kept;
                    existing.Remove(pair.Key);
                    continue;
                }

                pair.Value.name = pair.Key;
                AssetDatabase.AddObjectToAsset(pair.Value, holder);
                saved[pair.Key] = pair.Value;
            }

            if (!keepOthers)
                foreach (Mesh orphan in existing.Values) Object.DestroyImmediate(orphan, true);

            EditorUtility.SetDirty(holder);
            // Written now: the scene is saved against these meshes, and a stale file on disk would ship the old wall.
            AssetDatabase.SaveAssetIfDirty(holder);
            return saved;
        }
    }
}
