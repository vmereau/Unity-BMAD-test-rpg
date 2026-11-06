using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Rebuilds the camp's graybox palisade under Area1_Blockout/CampEdge from docs/World/Areas/area-001/layout.json.
    /// Same layout and terrain, same result. Rebake the NavMesh after a run.
    /// </summary>
    public static class Area1BlockoutBuilder
    {
        private const string TAG = "[Area1BlockoutBuilder]";

        private const string BLOCKOUT_ROOT = "Area1_Blockout";
        private const string CAMP_EDGE = "CampEdge";
        private const string TIMBER = "Assets/_Game/Art/Materials/Blockout_Timber.mat";

        private static readonly string[] FenceIds =
        {
            "outer-fence", "outer-fence-2", "outer-fence-3", "outer-fence-4", "outer-fence-5", "outer-fence-6",
            "inner-fence-west", "inner-fence-east", "dock-fence-upper", "dock-fence-lower",
            "compound-fence-upper", "compound-fence-lower", "lake-fence", // keep new lines last: panels draw from one seeded sequence
        };

        private const string WAREHOUSE_FEATURE = "warehouse";
        private const string SHORE_RAIL_FEATURE = "shore-rail";

        private const string BLOCKER_LAYER = "Ignore Raycast";

        private const float PANEL_LENGTH = 3f;
        private const float PANEL_THICKNESS = 0.6f;
        private const float PANEL_OVERLAP = 0.3f;     // past each end, so no gap opens at a corner
        private const float PANEL_HEIGHT = 5f;
        private const float PANEL_MIN_CLEARANCE = 3f; // above the highest ground under a panel, for steep rims
        private const float PANEL_BURY = 1.5f;
        private const float PANEL_JITTER = 0.3f;
        private const int GROUND_SAMPLES = 7;
        private const int SEED = 1337;

        private const float TOWER_WIDTH = 2.5f;
        private const float TOWER_HEIGHT = 7f;

        private const float BREAK_HALF_WIDTH = 4f;
        private const float RAIL_HEIGHT = 1.1f;
        private const float RAIL_THICKNESS = 0.12f;
        private const float POST_HEIGHT = 1.3f;
        private const float POST_WIDTH = 0.3f;
        private const float BLOCKER_HEIGHT = 6f;
        private const float BLOCKER_BURY = 1f;
        private const float BLOCKER_DEPTH = 1f;
        private const float NOT_WALKABLE_DEPTH = 2f;
        private const int NOT_WALKABLE_AREA = 1;

        private const float SHORE_RAIL_PIECE = 3f;
        private const int SHORE_RAIL_GAP_EVERY = 3;       // every third piece is missing; each line divides evenly, so pieces run up to 3 m
        private const float SHORE_RAIL_POST_HEIGHT = 0.7f;
        private const float SHORE_RAIL_POST_WIDTH = 0.14f;
        private const float SHORE_RAIL_BAR = 0.08f;
        private const float SHORE_RAIL_POST_BURY = 0.4f;
        private const float SHORE_RAIL_SAG = 0.12f;       // ends sit at different heights, so it reads as neglected
        private const float SHORE_RAIL_NAV_MARGIN = 0.35f; // agent radius: Not Walkable areas aren't eroded

        [MenuItem("Tools/World/Build Area 1 Camp Edge")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                GameLog.Error(TAG, "Leave play mode first; nothing changed.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            GameObject root = GameObject.Find(BLOCKOUT_ROOT);
            Material timber = AssetDatabase.LoadAssetAtPath<Material>(TIMBER);
            if (terrain == null || root == null || timber == null)
            {
                GameLog.Error(TAG, $"Need an active terrain, {BLOCKOUT_ROOT} and {TIMBER}; nothing changed.");
                return;
            }

            if (!TryLoadFeatures(out Dictionary<string, JObject> features, out string problem))
            {
                GameLog.Error(TAG, $"{problem}; nothing changed.");
                return;
            }

            Transform old = root.transform.Find(CAMP_EDGE);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var edge = new GameObject(CAMP_EDGE).transform;
            edge.SetParent(root.transform, false);

            Vector2 breakPoint = Position(features["fence-break"]);
            int panels = BuildPalisade(features, terrain, timber, edge, breakPoint);
            BuildArrivalGate(features, terrain, timber, edge);
            BuildBrokenStretch(features["outer-fence-3"], terrain, timber, edge, breakPoint);
            BuildShoreRail(features[SHORE_RAIL_FEATURE], terrain, timber, edge);

            EditorSceneManager.MarkSceneDirty(root.scene);
            GameLog.Info(TAG, $"Done. {panels} palisade panels, position hash {PositionHash(edge)}. Rebake the NavMesh.");
        }

        private static int BuildPalisade(Dictionary<string, JObject> features, Terrain terrain, Material timber, Transform edge, Vector2 breakPoint)
        {
            var random = new System.Random(SEED);
            int count = 0;
            JObject warehouse = features[WAREHOUSE_FEATURE];
            Vector2 warehouseAt = Position(warehouse);
            var warehouseSize = new Vector2((float)warehouse["w"], (float)warehouse["d"]);
            foreach (string id in FenceIds)
            {
                var group = new GameObject(id).transform;
                group.SetParent(edge, false);

                foreach ((Vector2 start, Vector2 end) in Area1TerrainMath.CutPanels(Points(features[id]), PANEL_LENGTH, breakPoint, BREAK_HALF_WIDTH))
                {
                    float jitter = ((float)random.NextDouble() * 2f - 1f) * PANEL_JITTER; // drawn first, so a skipped panel leaves the rest alone
                    Vector2 a = start, b = end;
                    if (!Area1TerrainMath.ClipOutsideRect(ref a, ref b, warehouseAt, warehouseSize)) continue; // its walls are the fence here

                    GroundUnder(terrain, a, b, out float low, out float mean, out float high);
                    float top = Mathf.Max(mean + PANEL_HEIGHT + jitter, high + PANEL_MIN_CLEARANCE);
                    float bottom = low - PANEL_BURY;

                    Vector2 middle = (a + b) * 0.5f;
                    Vector2 along = b - a;
                    Transform panel = Box($"Panel_{count:000}", group, timber,
                        new Vector3(middle.x, (top + bottom) * 0.5f, middle.y),
                        new Vector3(PANEL_THICKNESS, top - bottom, along.magnitude + PANEL_OVERLAP * 2f));
                    panel.rotation = Yaw(along);
                    count++;
                }
            }
            return count;
        }

        /// <summary>Two towers cap the fence ends either side of the road. No leaves or blocker: the pass blocker is the story lock.</summary>
        private static void BuildArrivalGate(Dictionary<string, JObject> features, Terrain terrain, Material timber, Transform edge)
        {
            var gate = new GameObject("ArrivalGate").transform;
            gate.SetParent(edge, false);

            Vector2[] west = Points(features["outer-fence-2"]);
            Vector2 left = west[west.Length - 1];
            Vector2 right = Points(features["outer-fence-3"])[0];

            GateTower("Tower_L", gate, terrain, timber, left, right - left);
            GateTower("Tower_R", gate, terrain, timber, right, right - left);
        }

        private static void GateTower(string name, Transform parent, Terrain terrain, Material timber, Vector2 at, Vector2 across)
        {
            Vector2 half = across.normalized * (TOWER_WIDTH * 0.5f);
            GroundUnder(terrain, at - half, at + half, out float low, out float mean, out _);
            float top = mean + TOWER_HEIGHT;
            float bottom = low - PANEL_BURY;
            Transform tower = Box(name, parent, timber, new Vector3(at.x, (top + bottom) * 0.5f, at.y),
                new Vector3(TOWER_WIDTH, top - bottom, TOWER_WIDTH));
            tower.rotation = Yaw(across);
        }

        /// <summary>Marks a line the Company doesn't defend: knee-high, with a walkable gap after every stretch.</summary>
        private static void BuildShoreRail(JObject feature, Terrain terrain, Material timber, Transform edge)
        {
            var rail = new GameObject("ShoreRail").transform;
            rail.SetParent(edge, false);

            var random = new System.Random(SEED);
            int index = 0;
            foreach ((Vector2 a, Vector2 b) in Area1TerrainMath.CutPanels(Points(feature), SHORE_RAIL_PIECE, Vector2.zero, 0f))
            {
                float sag = (float)random.NextDouble() * SHORE_RAIL_SAG;
                if (++index % SHORE_RAIL_GAP_EVERY == 0) continue;

                float groundA = Ground(terrain, a), groundB = Ground(terrain, b);
                foreach ((Vector2 at, float ground, string end) in new[] { (a, groundA, "A"), (b, groundB, "B") })
                {
                    float bottom = ground - SHORE_RAIL_POST_BURY;
                    float height = SHORE_RAIL_POST_HEIGHT + SHORE_RAIL_POST_BURY;
                    Box($"Post_{index:00}_{end}", rail, timber, new Vector3(at.x, bottom + height * 0.5f, at.y),
                        new Vector3(SHORE_RAIL_POST_WIDTH, height, SHORE_RAIL_POST_WIDTH));
                }

                var from = new Vector3(a.x, groundA + SHORE_RAIL_POST_HEIGHT - sag, a.y);
                var to = new Vector3(b.x, groundB + SHORE_RAIL_POST_HEIGHT, b.y);
                Transform bar = Box($"Bar_{index:00}", rail, timber, (from + to) * 0.5f, new Vector3(SHORE_RAIL_BAR, SHORE_RAIL_BAR, Vector3.Distance(from, to)));
                bar.rotation = Quaternion.LookRotation(to - from);

                // The bar and posts are too thin for a bake to see, so the NavMesh would run straight through them.
                var volume = new GameObject($"NotWalkable_{index:00}").transform;
                volume.SetParent(rail, false);
                volume.SetPositionAndRotation((from + to) * 0.5f, Yaw(b - a));
                var notWalkable = volume.gameObject.AddComponent<NavMeshModifierVolume>();
                notWalkable.center = Vector3.zero;
                notWalkable.size = new Vector3(SHORE_RAIL_POST_WIDTH + SHORE_RAIL_NAV_MARGIN * 2f, BLOCKER_HEIGHT, Vector2.Distance(a, b) + SHORE_RAIL_NAV_MARGIN * 2f);
                notWalkable.area = NOT_WALKABLE_AREA;
            }
        }

        /// <summary>
        /// Open to sight, closed to walking. The rail sits above the tents-to-fire rays. The blocker is on Ignore Raycast so
        /// sight tests and the camera skip it, and bakes collect render meshes, so the NavMesh needs its own volume.
        /// </summary>
        private static void BuildBrokenStretch(JObject fence, Terrain terrain, Material timber, Transform edge, Vector2 at)
        {
            var stretch = new GameObject("BrokenStretch").transform;
            stretch.SetParent(edge, false);

            Vector2 direction = NearestSegmentDirection(Points(fence), at);
            Quaternion yaw = Yaw(direction);
            float ground = Ground(terrain, at);
            float width = BREAK_HALF_WIDTH * 2f;

            Transform rail = Box("Rail", stretch, timber, new Vector3(at.x, ground + RAIL_HEIGHT, at.y),
                new Vector3(RAIL_THICKNESS, RAIL_THICKNESS, width));
            rail.rotation = yaw;
            UnityEngine.Object.DestroyImmediate(rail.GetComponent<Collider>());

            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 p = at + direction * (side * (BREAK_HALF_WIDTH - PANEL_OVERLAP - POST_WIDTH));
                Transform post = Box(side < 0f ? "Post_A" : "Post_B", stretch, timber,
                    new Vector3(p.x, ground + POST_HEIGHT * 0.5f, p.y), new Vector3(POST_WIDTH, POST_HEIGHT, POST_WIDTH));
                post.rotation = yaw;
                UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
            }

            var blocker = new GameObject("Blocker").transform;
            blocker.SetParent(stretch, false);
            blocker.SetPositionAndRotation(new Vector3(at.x, ground + BLOCKER_HEIGHT * 0.5f - BLOCKER_BURY, at.y), yaw);
            blocker.gameObject.layer = LayerMask.NameToLayer(BLOCKER_LAYER);

            var wall = blocker.gameObject.AddComponent<BoxCollider>();
            wall.size = new Vector3(BLOCKER_DEPTH, BLOCKER_HEIGHT, width);

            var notWalkable = blocker.gameObject.AddComponent<NavMeshModifierVolume>();
            notWalkable.center = Vector3.zero; // the package default sits 1 m up, which would lift the volume off the ground
            notWalkable.size = new Vector3(NOT_WALKABLE_DEPTH, BLOCKER_HEIGHT, width);
            notWalkable.area = NOT_WALKABLE_AREA;
        }

        private static Vector2 NearestSegmentDirection(Vector2[] line, Vector2 point)
        {
            Vector2 best = Vector2.right;
            float closest = float.PositiveInfinity;
            for (int i = 1; i < line.Length; i++)
            {
                float distance = Area1TerrainMath.PolylineDistance(point, new[] { line[i - 1], line[i] }, out _);
                if (distance >= closest) continue;
                closest = distance;
                best = (line[i] - line[i - 1]).normalized;
            }
            return best;
        }

        internal static Transform Box(string name, Transform parent, Material material, Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.isStatic = true;
            return box.transform;
        }

        /// <summary>Yaw whose local Z runs along <paramref name="direction"/>.</summary>
        internal static Quaternion Yaw(Vector2 direction) =>
            Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);

        internal static void GroundUnder(Terrain terrain, Vector2 a, Vector2 b, out float low, out float mean, out float high)
        {
            low = float.PositiveInfinity;
            high = float.NegativeInfinity;
            float sum = 0f;
            for (int i = 0; i < GROUND_SAMPLES; i++)
            {
                float height = Ground(terrain, Vector2.Lerp(a, b, i / (GROUND_SAMPLES - 1f)));
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
                sum += height;
            }
            mean = sum / GROUND_SAMPLES;
        }

        internal static float Ground(Terrain terrain, Vector2 p) =>
            terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;

        /// <summary>FNV-1a over child positions, for comparing two runs.</summary>
        private static uint PositionHash(Transform root)
        {
            uint hash = 2166136261;
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
            {
                Vector3 p = child.position;
                foreach (float value in new[] { p.x, p.y, p.z })
                {
                    hash ^= (uint)Mathf.RoundToInt(value * 100f);
                    hash *= 16777619;
                }
            }
            return hash;
        }

        internal static bool TryReadLayout(out Dictionary<string, JObject> features, out string problem)
        {
            features = null;
            problem = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs/World/Areas/area-001/layout.json"));
            if (!File.Exists(path))
            {
                problem = $"Layout not found at {path}";
                return false;
            }

            features = JObject.Parse(File.ReadAllText(path))["features"].Cast<JObject>().ToDictionary(f => (string)f["id"]);
            return true;
        }

        private static bool TryLoadFeatures(out Dictionary<string, JObject> features, out string problem)
        {
            features = null;
            if (!TryReadLayout(out Dictionary<string, JObject> found, out problem)) return false;

            string missing = FenceIds.FirstOrDefault(id => !found.TryGetValue(id, out JObject fence) || !HasPoints(fence))
                ?? new[] { "fence-break", "arrival-gate" }.FirstOrDefault(id => !found.TryGetValue(id, out JObject point) || !HasPosition(point))
                ?? (found.TryGetValue(SHORE_RAIL_FEATURE, out JObject rail) && HasPoints(rail) ? null : SHORE_RAIL_FEATURE);
            if (missing != null)
            {
                problem = $"Layout has no usable feature '{missing}'";
                return false;
            }

            if (!found.TryGetValue(WAREHOUSE_FEATURE, out JObject warehouse) || !HasPosition(warehouse) || !IsNumber(warehouse["w"]) || !IsNumber(warehouse["d"]))
            {
                problem = $"Layout has no usable feature '{WAREHOUSE_FEATURE}'";
                return false;
            }

            features = found;
            return true;
        }

        internal static bool HasPoints(JObject feature) =>
            feature["points"] is JArray points && points.Count >= 2
            && points.All(p => p is JArray pair && pair.Count >= 2 && IsNumber(pair[0]) && IsNumber(pair[1]));

        internal static bool HasPosition(JObject feature) => IsNumber(feature["x"]) && IsNumber(feature["z"]);

        internal static bool IsNumber(JToken token) => token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);

        internal static Vector2[] Points(JObject feature) =>
            feature["points"].Select(p => new Vector2((float)p[0], (float)p[1])).ToArray();

        internal static Vector2 Position(JObject feature) => new Vector2((float)feature["x"], (float)feature["z"]);
    }
}
