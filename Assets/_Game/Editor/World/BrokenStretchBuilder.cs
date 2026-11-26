using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Dresses CampEdge/BrokenStretch: the collapsed 8 m of the north palisade and its repair works, from the model and
    /// parts file that build_broken_stretch.py writes (look and layout rules: BROKEN_STRETCH_CONCEPTS.md). Every part
    /// that blocks has a collider of its own shape; nothing invisible stops the player. Agents are kept out of the
    /// gap by a Not Walkable volume. It writes no asset, so a hand-edit scan can run it.
    /// </summary>
    public static class BrokenStretchBuilder
    {
        internal const string MODEL = "Assets/_Game/Art/Environment/Camp/models/broken stretch/BrokenStretch.fbx";
        public const string PARTS = "Assets/_Game/Art/Environment/Camp/models/broken stretch/BrokenStretch.json";
        public const int VERSION = 1;   // of the parts file: build_broken_stretch.py's VERSION

        private const string MATERIALS = "Assets/_Game/Art/Environment/Camp/Materials/";
        private static readonly Dictionary<string, string> MaterialOf = new Dictionary<string, string>
        {
            { "old", "M_Camp_PalisadeLog" }, { "new", "M_Camp_NewLog" }, { "pole", "M_Camp_PoleWood" }, { "rope", "M_Camp_Rope" },
            { "stone", "M_Camp_Rubble" }, { "iron", "M_Camp_Iron" }, { "rag", "M_Camp_Rag" }, { "board", "M_Camp_HutShell" },
        };

        private const string TAG = "[BrokenStretchBuilder]";
        private const string ROCK_PREFIX = "SM_Rock";       // the camp's hand-placed rocks: parts may rest on them
        private const string BLOCKOUT_ROOT = "Area1_Blockout";
        internal const string GROUND_MARK = "docs/World/Areas/area-001/broken-stretch-ground.json";
        private const float RAY_FROM = 60f;
        private const float RAY_LENGTH = 120f;
        private const float NOT_WALKABLE_HEIGHT = 6f;
        private const int NOT_WALKABLE_AREA = 1;
        internal const string NOT_WALKABLE = "NotWalkable";

        // The slip scar: bare earth and a shallow hollow on the shelf just outside the old wall line. World offsets from
        // the break point, for a wall that runs east-west with the river to the north, as it does.
        private static readonly Vector2 ScarOffset = new Vector2(0.3f, 1.6f);
        private static readonly Vector2 ScarSize = new Vector2(6.4f, 2.6f);
        private static readonly Vector2 HollowSize = new Vector2(4.6f, 1.2f);
        private const float SCAR_EDGE = 1f;
        private const float HOLLOW_EDGE = 1.6f;
        private const float HOLLOW_DEPTH = 0.3f;
        /// <summary>Where <see cref="SinkGround"/> lowered the terrain. A break point moved in the layout leaves the hollow here.</summary>
        internal static readonly Vector2 SunkAt = new Vector2(3f, 52.4f);
        internal const float SUNK_TOLERANCE = 0.5f;
        /// <summary>The scar's middle. Fixed with the hollow: neither follows a break point moved in the layout.</summary>
        public static Vector2 ScarCentre => SunkAt + ScarOffset;

        /// <summary>How much of the ground at <paramref name="p"/> is the scar's bare earth, 0 to 1.</summary>
        public static float ScarWeight(Vector2 p, Vector2 scarCentre) =>
            1f - Area1TerrainMath.Ramp(Area1TerrainMath.RectDistance(p, scarCentre, ScarSize), 0f, SCAR_EDGE);

        /// <summary>How far the ground at <paramref name="p"/> sinks, in metres.</summary>
        public static float HollowDepth(Vector2 p, Vector2 scarCentre) =>
            HOLLOW_DEPTH * Mathf.SmoothStep(1f, 0f, Area1TerrainMath.Ramp(Area1TerrainMath.RectDistance(p, scarCentre, HollowSize), 0f, HOLLOW_EDGE));

        /// <summary>
        /// One-time ground work: sinks the hollow into the live terrain and paints the scar. Capture the sculpt afterwards
        /// (later terrain runs then keep the hollow, and paint the scar themselves), rerun Camp Edge, and rebake.
        /// </summary>
        [MenuItem("Tools/World/Sink Area 1 Broken Stretch Ground")]
        public static void SinkGround()
        {
            Terrain terrain = Terrain.activeTerrain;
            GameObject blockout = GameObject.Find(BLOCKOUT_ROOT);
            if (EditorApplication.isPlaying || terrain == null || blockout == null || blockout.scene != terrain.gameObject.scene)
            {
                GameLog.Error(TAG, $"Needs edit mode and the Area 1 scene open with its terrain active ({BLOCKOUT_ROOT} beside it); nothing changed.");
                return;
            }

            TerrainData data = terrain.terrainData;
            Vector2 centre = ScarCentre;
            // Heights cannot say whether the hollow is already there (the shelf's edge is 3 m away), so a file beside the
            // layout records that it was sunk: while it exists, a run only repaints.
            string mark = Path.GetFullPath(Path.Combine(Application.dataPath, "..", GROUND_MARK));
            bool sunk = File.Exists(mark);

            Vector3 origin = terrain.transform.position;
            Undo.RegisterCompleteObjectUndo(data, "Sink Broken Stretch Ground");
            Undo.RegisterCompleteObjectUndo(data.alphamapTextures, "Sink Broken Stretch Ground");
            if (!sunk)
            {
                int res = data.heightmapResolution;
                float step = data.size.x / (res - 1);
                Vector2 reach = HollowSize * 0.5f + Vector2.one * (HOLLOW_EDGE + step);
                int x0 = Mathf.Clamp(Mathf.FloorToInt((centre.x - reach.x - origin.x) / step), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((centre.x + reach.x - origin.x) / step), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((centre.y - reach.y - origin.z) / step), 0, res - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((centre.y + reach.y - origin.z) / step), 0, res - 1);
                float[,] heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        heights[z - z0, x - x0] -= HollowDepth(new Vector2(origin.x + x * step, origin.z + z * step), centre) / data.size.y;
                data.SetHeights(x0, z0, heights);
                WriteGroundMark(mark);
            }

            int alphaRes = data.alphamapResolution;
            float cell = data.size.x / alphaRes;
            Vector2 paintReach = ScarSize * 0.5f + Vector2.one * (SCAR_EDGE + cell);
            int ax0 = Mathf.Clamp(Mathf.FloorToInt((centre.x - paintReach.x - origin.x) / cell), 0, alphaRes - 1);
            int ax1 = Mathf.Clamp(Mathf.CeilToInt((centre.x + paintReach.x - origin.x) / cell), 0, alphaRes - 1);
            int az0 = Mathf.Clamp(Mathf.FloorToInt((centre.y - paintReach.y - origin.z) / cell), 0, alphaRes - 1);
            int az1 = Mathf.Clamp(Mathf.CeilToInt((centre.y + paintReach.y - origin.z) / cell), 0, alphaRes - 1);
            float[,,] alpha = data.GetAlphamaps(ax0, az0, ax1 - ax0 + 1, az1 - az0 + 1);
            int layers = alpha.GetLength(2);
            bool painted = layers > Area1TerrainBuilder.MUD_LAYER;
            if (painted)
            {
                var weights = new float[layers];
                for (int z = az0; z <= az1; z++)
                {
                    for (int x = ax0; x <= ax1; x++)
                    {
                        float scar = ScarWeight(new Vector2(origin.x + (x + 0.5f) * cell, origin.z + (z + 0.5f) * cell), centre);
                        if (scar <= 0f) continue;
                        for (int l = 0; l < layers; l++) weights[l] = alpha[z - az0, x - ax0, l];
                        Area1TerrainMath.BlendLayer(weights, Area1TerrainBuilder.MUD_LAYER, scar);
                        float total = weights.Sum();
                        for (int l = 0; l < layers; l++) alpha[z - az0, x - ax0, l] = weights[l] / total;
                    }
                }
                data.SetAlphamaps(ax0, az0, alpha);
            }

            terrain.Flush();
            EditorUtility.SetDirty(data);
            GameLog.Info(TAG, (sunk ? $"The hollow was already sunk ({GROUND_MARK}); heights left alone." : $"Hollow sunk {HOLLOW_DEPTH} m.")
                + (painted ? " Scar painted." : " The terrain has no Mud layer: nothing painted.")
                + " Capture the terrain sculpt, rerun Camp Edge, save and rebake.");
        }

        private static void WriteGroundMark(string path)
        {
            var mark = new JObject
            {
                ["sunkAt"] = new JArray(SunkAt.x, SunkAt.y),
                ["depth"] = HOLLOW_DEPTH,
                ["note"] = "Written by Tools/World/Sink Area 1 Broken Stretch Ground. While this file exists the command only repaints.",
            };
            File.WriteAllText(path, mark.ToString() + "\n");
        }

        internal sealed class Kit
        {
            public StretchPart[] Parts;
            public Dictionary<string, Mesh> Meshes;
            public Dictionary<string, Material[]> Materials;
            public Vector2 NotWalkableCentre, NotWalkableSize;
        }

        internal static bool TryLoad(out Kit kit, out string problem)
        {
            try
            {
                return TryRead(out kit, out problem);
            }
            catch (System.Exception e) // a value of the wrong shape somewhere in the file: the caller falls back to the graybox
            {
                kit = null;
                problem = $"{PARTS} cannot be used: {e.Message}";
                return false;
            }
        }

        private static bool TryRead(out Kit kit, out string problem)
        {
            kit = null;
            string partsPath = Path.GetFullPath(PARTS);
            if (!File.Exists(partsPath))
            {
                problem = $"No parts file at {PARTS} (run build_broken_stretch.py in Blender)";
                return false;
            }

            Dictionary<string, Mesh> meshes = AssetDatabase.LoadAllAssetsAtPath(MODEL).OfType<Mesh>()
                .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
            if (meshes.Count == 0)
            {
                problem = $"No model at {MODEL} (run build_broken_stretch.py in Blender)";
                return false;
            }

            JObject file;
            try
            {
                file = JObject.Parse(File.ReadAllText(partsPath));
            }
            catch (System.Exception e) when (e is Newtonsoft.Json.JsonException || e is IOException)
            {
                problem = $"{PARTS} cannot be read: {e.Message}";
                return false;
            }

            if (file["version"] == null || file["version"].Type != JTokenType.Integer || (int)file["version"] != VERSION)
            {
                problem = $"{PARTS} is version {file["version"]}, the builder reads version {VERSION}";
                return false;
            }

            if (!BrokenStretchLayout.TryParse(file, out StretchPart[] parts, out problem))
            {
                problem = $"{PARTS}: {problem}";
                return false;
            }

            var materials = new Dictionary<string, Material[]>();
            foreach (StretchPart part in parts)
            {
                if (materials.ContainsKey(part.Mesh)) continue;
                if (!meshes.TryGetValue(part.Mesh, out Mesh mesh) || !(file["meshes"]?[part.Mesh]?["materials"] is JArray slots))
                {
                    problem = $"{PARTS}: part '{part.Name}' uses mesh '{part.Mesh}', which the model or the file's 'meshes' does not have";
                    return false;
                }

                long triangles = 0;
                for (int s = 0; s < mesh.subMeshCount; s++) triangles += mesh.GetIndexCount(s) / 3;
                JToken wanted = file["meshes"][part.Mesh]["triangles"];
                if (slots.Count != mesh.subMeshCount || wanted == null || wanted.Type != JTokenType.Integer || (long)wanted != triangles)
                {
                    problem = $"Mesh '{part.Mesh}' has {mesh.subMeshCount} materials and {triangles} triangles in the model, "
                        + $"{slots.Count} and {wanted} in {PARTS}: the two are a version apart (run build_broken_stretch.py, let Unity import)";
                    return false;
                }

                var found = new Material[slots.Count];
                for (int i = 0; i < found.Length; i++)
                {
                    string slot = slots[i].Type == JTokenType.String ? (string)slots[i] : null;
                    found[i] = slot != null && MaterialOf.TryGetValue(slot, out string name)
                        ? AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + name + ".mat") : null;
                    if (found[i] != null) continue;
                    problem = $"No material for slot '{slot}' of mesh '{part.Mesh}' under {MATERIALS}";
                    return false;
                }
                materials[part.Mesh] = found;
            }

            JToken strip = file["notWalkable"];
            if (!TryPair(strip?["centre"], out Vector2 centre) || !TryPair(strip?["size"], out Vector2 size) || size.x <= 0f || size.y <= 0f)
            {
                problem = $"{PARTS} has no usable 'notWalkable' strip";
                return false;
            }

            kit = new Kit { Parts = parts, Meshes = meshes, Materials = materials, NotWalkableCentre = centre, NotWalkableSize = size };
            problem = null;
            return true;
        }

        /// <summary>Fills <paramref name="stretch"/> with the parts, fitted to the ground as it is now.</summary>
        internal static void Build(Kit kit, Transform stretch, StretchFrame frame, Terrain terrain)
        {
            Physics.SyncTransforms(); // edit-mode rays only see transforms that were synced
            System.Func<Vector2, float> ground = p => Area1BlockoutBuilder.Ground(terrain, p);
            System.Func<Vector2, float> surface = p => Surface(p, ground(p));
            Quaternion yaw = Area1BlockoutBuilder.Yaw(frame.Along);

            foreach (StretchPart part in kit.Parts)
            {
                StretchPose pose = BrokenStretchLayout.Place(part, frame, ground, surface);
                var go = new GameObject(part.Name) { isStatic = true };
                go.transform.SetParent(stretch, false);
                go.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                go.transform.localScale = pose.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = kit.Meshes[part.Mesh];
                go.AddComponent<MeshRenderer>().sharedMaterials = kit.Materials[part.Mesh];

                for (int i = 0; i < part.Colliders.Length; i++) AddCollider(go.transform, part.Colliders[i], i, pose.Scale.z);
            }

            // Solid parts cut themselves out of a bake, but the gap between them is walkable ground: close it for agents.
            Vector2 middle = frame.ToWorld(kit.NotWalkableCentre.x, kit.NotWalkableCentre.y);
            var volume = new GameObject(NOT_WALKABLE).transform;
            volume.SetParent(stretch, false);
            volume.SetPositionAndRotation(new Vector3(middle.x, ground(middle), middle.y), yaw);
            var notWalkable = volume.gameObject.AddComponent<NavMeshModifierVolume>();
            notWalkable.center = Vector3.zero; // the package default sits 1 m up
            notWalkable.size = new Vector3(kit.NotWalkableSize.y, NOT_WALKABLE_HEIGHT, kit.NotWalkableSize.x); // the yaw's z runs along the wall
            notWalkable.area = NOT_WALKABLE_AREA;
        }

        /// <param name="stretch">The part's scale along its z: a strut is a unit pole stretched to its length.</param>
        private static void AddCollider(Transform part, StretchCollider shape, int index, float stretch)
        {
            var holder = new GameObject($"Col_{index}") { isStatic = true };
            holder.transform.SetParent(part, false);
            if (shape.Capsule)
            {
                Vector3 axis = shape.B - shape.A;
                holder.transform.localPosition = (shape.A + shape.B) * 0.5f;
                holder.transform.localRotation = Quaternion.LookRotation(axis.normalized, Mathf.Abs(axis.normalized.y) > 0.99f ? Vector3.right : Vector3.up);
                var capsule = holder.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = shape.Radius;
                capsule.height = axis.magnitude + shape.Radius * 2f / Mathf.Max(stretch, 1e-3f); // the end caps are not stretched with the pole
            }
            else
            {
                holder.transform.localPosition = shape.A;
                holder.transform.localRotation = Quaternion.Euler(0f, shape.Yaw, 0f);
                holder.AddComponent<BoxCollider>().size = shape.B;
            }
        }

        /// <summary>
        /// The highest of the terrain and the camp's hand-placed rocks. Only those rocks count: anything a build tool
        /// owns may be standing twice during a hand-edit scan, and a character may be standing there.
        /// </summary>
        private static float Surface(Vector2 at, float ground)
        {
            float best = ground;
            var from = new Vector3(at.x, ground + RAY_FROM, at.y);
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, RAY_LENGTH, ~0, QueryTriggerInteraction.Ignore))
                if (hit.point.y > best && IsCampRock(hit.collider.transform)) best = hit.point.y;
            return best;
        }

        /// <summary>A hand-placed rock, alone or grouped under a parent, but never one inside a build tool's root.</summary>
        private static bool IsCampRock(Transform t)
        {
            bool rock = false;
            for (; t != null; t = t.parent)
            {
                if (t.name.StartsWith(BLOCKOUT_ROOT, System.StringComparison.Ordinal)) return false; // the root, or a scan's clone of it
                rock |= t.name.StartsWith(ROCK_PREFIX, System.StringComparison.Ordinal);
            }
            return rock;
        }

        private static bool TryPair(JToken token, out Vector2 value)
        {
            value = default;
            if (!(token is JArray a) || a.Count != 2 || !Area1BlockoutBuilder.IsNumber(a[0]) || !Area1BlockoutBuilder.IsNumber(a[1])) return false;
            value = new Vector2((float)a[0], (float)a[1]);
            return true;
        }
    }
}
