using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core;
using Game.World;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The Area 1 warehouse prefab, built round the Blender model (<c>Art/Environment/Camp/models/warehouse</c>).
    /// The model script fits posts, trestles and rubble to a ground capture taken here, and writes a parts file
    /// (colliders, walk surfaces, Not Walkable volumes, lantern points) that this builder turns into objects, so
    /// no size is written twice. Local frame: x east, y up, z north (the river), origin on the floor's top at the
    /// hall's centre. <see cref="Area1CampInterior.BuildWarehouse"/> places the prefab.
    /// The cellar under the landing and the hall is part of the model; its door is the game's door prefab, placed here. The room
    /// exists because the terrain has a hole over it: <see cref="PlaceCellar"/> cuts it in the scene and
    /// <see cref="CutCellarHole"/> keeps it through a terrain run.
    /// </summary>
    public static class WarehouseBuilder
    {
        private const string TAG = "[WarehouseBuilder]";
        internal const string PREFAB_PATH = "Assets/_Game/Prefabs/World/Warehouse/Warehouse.prefab";
        private const string MODEL_FOLDER = "Assets/_Game/Art/Environment/Camp/models/warehouse/";
        private const string MODEL_PATH = MODEL_FOLDER + "Warehouse.fbx";
        private const string PARTS_PATH = MODEL_FOLDER + "Warehouse.json";
        private const string GROUND_PATH = MODEL_FOLDER + "Source~/warehouse_ground.json";
        private const string LANTERN_PATH = "Assets/Hivemind/HDRP(Default)/Medieval_Docks/Art/Prefabs/SM_Lantern_Hanging.prefab";
        private const string DAY_NIGHT = "Assets/_Game/Data/Config/DayNightConfig.asset";
        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";
        private const string SCENE_ROOT = "Area1_Blockout/Warehouse";
        private const string DOOR_PATH = "Assets/_Game/Prefabs/World/Doors/Poor small door.prefab";
        private const string CELLAR_DOOR = "CellarDoor";
        private const string CELLAR_DOOR_SAVE_ID = "area1-warehouse-cellar-door"; // fixed, so a rebuild keeps the saved door state
        private const string AREAS = "RestrictedAreas";
        internal const string CELLAR_AREA = "RA_WarehouseCellar";
        private const string HALL_AREA = "RA_Warehouse";
        private const string GUARD = "Assets/_Game/Data/NPCs/WarehouseGuard/NPC_WarehouseGuard.asset";
        private const float FIT_TOLERANCE = 0.05f;
        private const string MATERIAL_PREFIX = "M_Camp_";
        /// <summary>The camp ramp's walk surface: <c>HomeYardDresser</c> ties a tarp to this object's faces.</summary>
        internal const string RAMP_CAMP = "Ramp_Camp";
        private const string COLLIDERS = "Colliders";
        private const string MODEL = "Model";
        private const string NAVMESH = "NavMesh";
        private static readonly string[] NotWalkableParts = { "Roof", "Hoist" };
        private static readonly string[] PartLists = { "objects", "walk", "colliders", "notWalkable", "lanterns" };
        // Packed under the floor boards: marked Not Walkable it would cut the floor above it out of the NavMesh.
        private const string RUBBLE = "Rubble";

        // Ground capture: a grid in the prefab's frame, wide enough for both ramps' feet and the landing's posts.
        private const float GRID_MIN_X = -14f, GRID_MAX_X = 11f, GRID_MIN_Z = -19f, GRID_MAX_Z = 10f;
        private const float GRID_STEP = 0.25f;
        private const float RAY_TOP = 30f;
        private const string ROCK_PREFIX = "SM_Rock";

        // Tuned in Play at 23:00: a pool on the doorstep, the hall's wall readable beside it.
        private static readonly Color LanternColor = new Color(1f, 0.7f, 0.42f);
        private static readonly Color LanternGlow = new Color(2.4f, 1.2f, 0.45f, 1f);
        private const float LANTERN_INTENSITY = 5f;
        private const float LANTERN_RANGE = 7f;
        // The cellar has no window: its inside lantern burns at all hours, low.
        private const float ALWAYS_INTENSITY = 2.4f;
        private const float ALWAYS_RANGE = 9f;
        private const int NOT_WALKABLE = 1;

        /// <summary>
        /// Where the model was fitted: the floor's centre (world x, y, z). False when the parts file is missing or
        /// does not say.
        /// </summary>
        internal static bool TryGetFit(out Vector3 fitted)
        {
            fitted = Vector3.zero;
            JObject parts = ReadParts();
            if (parts == null || !IsNumber(parts["floorWorldY"]) || !(parts["centre"] is JArray centre) || centre.Count != 2
                || !IsNumber(centre[0]) || !IsNumber(centre[1])) return false;
            fitted = new Vector3((float)centre[0], (float)parts["floorWorldY"], (float)centre[1]);
            return true;
        }

        /// <summary>Where the hoist's rope hangs, in the prefab's frame (x, z). False when the parts file does not say.</summary>
        internal static bool TryGetHoistDrop(out Vector2 drop)
        {
            drop = Vector2.zero;
            JObject parts = ReadParts();
            if (parts == null || !(parts["hoistDrop"] is JArray at) || at.Count != 2 || !IsNumber(at[0]) || !IsNumber(at[1])) return false;
            drop = new Vector2((float)at[0], (float)at[1]);
            return true;
        }

        private static bool IsNumber(JToken token) => token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);

        /// <summary>What the parts file says about the cellar, in the prefab's frame.</summary>
        public struct CellarFacts
        {
            /// <summary>Terrain hole cells must lie wholly inside this (x, z).</summary>
            public Rect Hole;
            /// <summary>The room's inside faces (x, z).</summary>
            public Rect Inside;
            public float FloorY;
            public Vector3 DoorPosition;
            public float DoorYaw, DoorScaleY, DoorOpenAngle;
            public Vector3 AreaCentre, AreaSize;
        }

        /// <summary>The cellar as the parts file on disk describes it. False when it is missing or malformed.</summary>
        internal static bool TryGetCellar(out CellarFacts cellar) => TryReadCellar(ReadParts(), out cellar, out _);

        /// <summary>Reads <c>cellar</c> from a parts file. False, with what is wrong, when a field is missing or malformed.</summary>
        public static bool TryReadCellar(JObject parts, out CellarFacts cellar, out string problem)
        {
            cellar = default;
            if (parts == null || !(parts["cellar"] is JObject data))
            {
                problem = "no 'cellar' object";
                return false;
            }
            if (!TryFloats(data["hole"], 4, out float[] hole) || hole[2] <= hole[0] || hole[3] <= hole[1])
            {
                problem = "'cellar.hole' must be [min x, min z, max x, max z]";
                return false;
            }
            if (!TryFloats(data["inside"], 4, out float[] inside) || inside[2] <= inside[0] || inside[3] <= inside[1])
            {
                problem = "'cellar.inside' must be [min x, min z, max x, max z]";
                return false;
            }
            if (!IsNumber(data["floorY"]))
            {
                problem = "'cellar.floorY' must be a number";
                return false;
            }
            if (!(data["door"] is JObject door) || !TryFloats(door["position"], 3, out float[] at) || !IsNumber(door["yaw"])
                || !IsNumber(door["scaleY"]) || !IsNumber(door["openAngle"]) || (float)door["scaleY"] <= 0f)
            {
                problem = "'cellar.door' needs position [x, y, z], yaw, scaleY (above 0) and openAngle";
                return false;
            }
            if (!(data["restricted"] is JObject area) || !TryFloats(area["centre"], 3, out float[] centre)
                || !TryFloats(area["size"], 3, out float[] size) || size[0] <= 0f || size[1] <= 0f || size[2] <= 0f)
            {
                problem = "'cellar.restricted' needs centre [x, y, z] and size [x, y, z] above 0";
                return false;
            }
            cellar = new CellarFacts
            {
                Hole = Rect.MinMaxRect(hole[0], hole[1], hole[2], hole[3]),
                Inside = Rect.MinMaxRect(inside[0], inside[1], inside[2], inside[3]),
                FloorY = (float)data["floorY"],
                DoorPosition = new Vector3(at[0], at[1], at[2]),
                DoorYaw = (float)door["yaw"],
                DoorScaleY = (float)door["scaleY"],
                DoorOpenAngle = (float)door["openAngle"],
                AreaCentre = new Vector3(centre[0], centre[1], centre[2]),
                AreaSize = new Vector3(size[0], size[1], size[2]),
            };
            problem = null;
            return true;
        }

        private static bool TryFloats(JToken token, int count, out float[] values)
        {
            values = null;
            if (!(token is JArray array) || array.Count != count) return false;
            var read = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!IsNumber(array[i])) return false;
                read[i] = (float)array[i];
            }
            values = read;
            return true;
        }

        /// <summary>
        /// The terrain hole cells that lie wholly inside a world rectangle (x, z): x, y = first cell, width, height =
        /// counts. Empty when no cell fits. A cell is <c>size / resolution</c> wide, counted from the terrain's corner.
        /// </summary>
        public static RectInt HoleCells(Rect world, Vector2 terrainMin, float size, int resolution)
        {
            if (size <= 0f || resolution <= 0) return new RectInt(0, 0, 0, 0);
            float cell = size / resolution;
            int x0 = Mathf.Max(0, Mathf.CeilToInt((world.xMin - terrainMin.x) / cell - 1e-4f));
            int z0 = Mathf.Max(0, Mathf.CeilToInt((world.yMin - terrainMin.y) / cell - 1e-4f));
            int x1 = Mathf.Min(resolution, Mathf.FloorToInt((world.xMax - terrainMin.x) / cell + 1e-4f));
            int z1 = Mathf.Min(resolution, Mathf.FloorToInt((world.yMax - terrainMin.y) / cell + 1e-4f));
            return new RectInt(x0, z0, Mathf.Max(0, x1 - x0), Mathf.Max(0, z1 - z0));
        }

        /// <summary>The cellar's hole rectangle in the world (x, z), from the parts file alone. False, with an error, when it does not say.</summary>
        private static bool TryGetCellarHoleWorld(out Rect world)
        {
            world = default;
            JObject parts = ReadParts();
            if (!TryReadCellar(parts, out CellarFacts cellar, out string problem) || !TryGetFit(out Vector3 fitted))
            {
                GameLog.Error(TAG, $"{PARTS_PATH}: {problem ?? "no 'floorWorldY' / 'centre'"} (run build_warehouse.py); the warehouse cellar's terrain hole was not cut.");
                return false;
            }
            world = new Rect(fitted.x + cellar.Hole.xMin, fitted.z + cellar.Hole.yMin, cellar.Hole.width, cellar.Hole.height);
            return true;
        }

        /// <summary>
        /// Opens the cellar's hole in a hole map (true = solid ground), for the terrain tool: every run rewrites the
        /// holes from the source asset. False, with an error, when the parts file does not say where; the map is
        /// then left as it was and the run goes on.
        /// </summary>
        public static bool CutCellarHole(bool[,] holes, Vector2 terrainMin, float size)
        {
            if (holes == null || !TryGetCellarHoleWorld(out Rect world)) return false;
            int resolution = holes.GetLength(0);
            RectInt cells = HoleCells(world, terrainMin, size, resolution);
            if (cells.width == 0 || cells.height == 0)
            {
                GameLog.Error(TAG, "The warehouse cellar's hole holds no terrain cell; nothing cut.");
                return false;
            }
            for (int z = cells.yMin; z < cells.yMax; z++)
                for (int x = cells.xMin; x < cells.xMax; x++)
                    holes[z, x] = false;
            return true;
        }

        [MenuItem("Tools/World/Place Warehouse Cellar")]
        private static void PlaceCellarMenu() => PlaceCellar();

        /// <summary>
        /// One-shot authoring for the cellar, with StartingTown open: cuts the terrain hole over the room and
        /// rebuilds <c>RestrictedAreas/RA_WarehouseCellar</c> (the warehouse guard's, at all hours). Rerunnable.
        /// It never saves and never bakes; a missing input stops it before anything changes.
        /// </summary>
        internal static bool PlaceCellar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return false;
            }
            JObject parts = ReadParts();
            if (!TryReadCellar(parts, out CellarFacts cellar, out string problem) || !TryGetFit(out Vector3 fitted))
            {
                GameLog.Error(TAG, $"{PARTS_PATH}: {problem ?? "no 'floorWorldY' / 'centre'"} (run build_warehouse.py); nothing changed.");
                return false;
            }
            GameObject root = GameObject.Find(SCENE_ROOT);
            GameObject areas = GameObject.Find(AREAS);
            Terrain terrain = Terrain.activeTerrain;
            var guard = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GUARD);
            if (root == null || areas == null || terrain == null || guard == null || LayerMask.NameToLayer("Ignore Raycast") < 0)
            {
                GameLog.Error(TAG, $"Need StartingTown open with {SCENE_ROOT}, the root '{AREAS}' and a terrain, and {GUARD}; nothing changed.");
                return false;
            }
            if ((root.transform.position - fitted).magnitude > FIT_TOLERANCE)
            {
                GameLog.Error(TAG, $"{SCENE_ROOT} stands at {root.transform.position:F2} but the model was fitted at {fitted:F2}: capture the ground and rebuild first; nothing changed.");
                return false;
            }
            TerrainData data = terrain.terrainData;
            Vector3 corner = terrain.transform.position;
            var world = new Rect(fitted.x + cellar.Hole.xMin, fitted.z + cellar.Hole.yMin, cellar.Hole.width, cellar.Hole.height);
            RectInt cells = HoleCells(world, new Vector2(corner.x, corner.z), data.size.x, data.holesResolution);
            if (cells.width == 0 || cells.height == 0)
            {
                GameLog.Error(TAG, "The cellar's hole holds no terrain cell; nothing changed.");
                return false;
            }

            data.SetHoles(cells.xMin, cells.yMin, new bool[cells.height, cells.width]); // all false: open
            EditorUtility.SetDirty(data);
            BuildCellarArea(areas.transform, fitted, cellar, guard);
            EditorSceneManager.MarkSceneDirty(root.scene);
            GameLog.Info(TAG, $"Cellar placed: terrain hole over cells x {cells.xMin}..{cells.xMax - 1}, z {cells.yMin}..{cells.yMax - 1}, {AREAS}/{CELLAR_AREA} rebuilt. Rebake the NavMesh and save.");
            return true;
        }

        /// <summary>The cellar as a restricted area, owned by the warehouse guard at all hours, like the hall above.</summary>
        private static void BuildCellarArea(Transform areas, Vector3 origin, CellarFacts cellar, ScriptableObject guard)
        {
            int warnings = -1;
            Transform hall = areas.Find(HALL_AREA);
            if (hall != null && hall.TryGetComponent(out RestrictedArea hallArea))
                warnings = new SerializedObject(hallArea).FindProperty("_warningsBeforeFight").intValue;
            Transform old = areas.Find(CELLAR_AREA);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var area = new GameObject(CELLAR_AREA) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            area.transform.SetParent(areas, false);
            area.transform.position = origin + cellar.AreaCentre;
            var collider = area.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = cellar.AreaSize;
            var so = new SerializedObject(area.AddComponent<RestrictedArea>());
            SerializedProperty owners = so.FindProperty("_ownerEntities");
            owners.arraySize = 1;
            owners.GetArrayElementAtIndex(0).objectReferenceValue = guard;
            so.FindProperty("_useHours").boolValue = false;
            if (warnings >= 0) so.FindProperty("_warningsBeforeFight").intValue = warnings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/World/Capture Warehouse Ground")]
        private static void CaptureMenu() => CaptureGround();

        /// <summary>
        /// Heights of the terrain and the hand-placed rocks round the warehouse, relative to its floor, on a grid in
        /// the prefab's frame. Run after moving a rock or sculpting there, then rerun the model script.
        /// </summary>
        internal static bool CaptureGround()
        {
            GameObject root = GameObject.Find(SCENE_ROOT);
            if (root == null)
            {
                GameLog.Error(TAG, $"Open StartingTown first: no active {SCENE_ROOT}; nothing written.");
                return false;
            }
            Physics.SyncTransforms();
            Vector3 origin = root.transform.position;
            int columns = Mathf.RoundToInt((GRID_MAX_X - GRID_MIN_X) / GRID_STEP) + 1;
            int rows = Mathf.RoundToInt((GRID_MAX_Z - GRID_MIN_Z) / GRID_STEP) + 1;
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append(Invariant($"  \"floorWorldY\": {origin.y:F4},\n  \"centre\": [{origin.x:F3}, {origin.z:F3}],\n"));
            text.Append(Invariant($"  \"minX\": {GRID_MIN_X}, \"minZ\": {GRID_MIN_Z}, \"step\": {GRID_STEP}, \"columns\": {columns}, \"rows\": {rows},\n"));
            text.Append("  \"heights\": [\n");
            int missed = 0, onRock = 0;
            for (int row = 0; row < rows; row++)
            {
                text.Append("    [");
                for (int column = 0; column < columns; column++)
                {
                    var at = new Vector3(origin.x + GRID_MIN_X + column * GRID_STEP, RAY_TOP, origin.z + GRID_MIN_Z + row * GRID_STEP);
                    if (!TryGround(at, out float height, out bool rock))
                    {
                        missed++;
                        height = origin.y;
                    }
                    if (rock) onRock++;
                    text.Append(Invariant($"{height - origin.y:F3}")).Append(column < columns - 1 ? "," : "");
                }
                text.Append(row < rows - 1 ? "],\n" : "]\n");
            }
            text.Append("  ]\n}\n");
            if (missed > 0)
            {
                GameLog.Error(TAG, $"{missed} grid point(s) hit neither terrain nor rock; nothing written.");
                return false;
            }
            File.WriteAllText(GROUND_PATH, text.ToString());
            GameLog.Info(TAG, $"Wrote {GROUND_PATH} ({columns} × {rows} points, {onRock} on rock, floor at y {origin.y:F3}). Rerun build_warehouse.py.");
            return true;
        }

        /// <summary>
        /// The highest terrain or rock surface under a point; everything else (the building, props, people) is
        /// ignored. Rocks are the active scene roots named SM_Rock*: a rock parented under a group is not seen.
        /// </summary>
        private static bool TryGround(Vector3 from, out float height, out bool rock)
        {
            height = float.NegativeInfinity;
            rock = false;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, RAY_TOP * 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                bool isRock = hit.collider.transform.root.name.StartsWith(ROCK_PREFIX);
                if (!(isRock || hit.collider is TerrainCollider) || hit.point.y <= height) continue;
                height = hit.point.y;
                rock = isRock;
            }
            return !float.IsNegativeInfinity(height);
        }

        [MenuItem("Tools/World/Build Warehouse Prefab")]
        private static void BuildMenu() => Build();

        /// <summary>
        /// Rebuilds the prefab from the model and its parts file. A missing model, a model object the parts file
        /// names but the FBX lacks, or a material that is not a camp material stops before saving, so the prefab
        /// (and the scene instance that follows it) is left as it was.
        /// </summary>
        internal static GameObject Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return null;
            }
            JObject parts = ReadParts();
            // The script has just rewritten the FBX beside the parts file: never pair new parts with the old import.
            if (File.Exists(MODEL_PATH)) AssetDatabase.ImportAsset(MODEL_PATH, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);
            var lantern = AssetDatabase.LoadAssetAtPath<GameObject>(LANTERN_PATH);
            var dayNight = AssetDatabase.LoadAssetAtPath<DayNightConfigSO>(DAY_NIGHT);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GLOW);
            var door = AssetDatabase.LoadAssetAtPath<GameObject>(DOOR_PATH);
            bool doorUsable = door != null && door.GetComponent<DoorInteractable>() != null && door.GetComponent<Lockable>() != null;
            if (parts == null || model == null || lantern == null || dayNight == null || glow == null || !doorUsable)
            {
                GameLog.Error(TAG, $"Need {MODEL_PATH}, {PARTS_PATH} (run build_warehouse.py), {LANTERN_PATH}, {DAY_NIGHT}, {GLOW} and {DOOR_PATH} (with DoorInteractable + Lockable); {PREFAB_PATH} left as it was.");
                return null;
            }
            if (!TryReadCellar(parts, out CellarFacts cellar, out string problem))
            {
                GameLog.Error(TAG, $"{PARTS_PATH}: {problem} (rerun build_warehouse.py); {PREFAB_PATH} left as it was.");
                return null;
            }
            foreach (string list in PartLists)
            {
                if (parts[list] is JArray) continue;
                GameLog.Error(TAG, $"{PARTS_PATH} has no list '{list}' (rerun build_warehouse.py); {PREFAB_PATH} left as it was.");
                return null;
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(PREFAB_PATH));
            try
            {
                if (!BuildModel(root.transform, model, parts) || !BuildBoxes(root.transform, parts)) return null;
                foreach (JToken point in parts["lanterns"]) BuildLantern(root.transform, (JObject)point, lantern, dayNight, glow);
                BuildCellarDoor(root.transform, door, cellar);

                string folder = PREFAB_PATH.Substring(0, PREFAB_PATH.LastIndexOf('/'));
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out bool saved);
                if (saved) GameLog.Info(TAG, $"Saved {PREFAB_PATH} ({parts.Value<int?>("total")} triangles)");
                else GameLog.Error(TAG, $"Could not save {PREFAB_PATH}");
                return saved ? prefab : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The model's objects under <c>Model</c>. Roof and hoist are Not Walkable; the rubble is left out of the bake.</summary>
        private static bool BuildModel(Transform root, GameObject model, JObject parts)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = MODEL;
            instance.transform.localPosition = Vector3.zero;
            bool complete = true;
            foreach (JToken name in parts["objects"])
            {
                Transform part = instance.transform.Find((string)name);
                if (part == null || !part.TryGetComponent(out MeshRenderer renderer))
                {
                    GameLog.Error(TAG, $"'{MODEL_PATH}' has no object '{name}'; {PREFAB_PATH} left as it was.");
                    complete = false;
                    continue;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.name.StartsWith(MATERIAL_PREFIX)) continue;
                    GameLog.Error(TAG, $"'{name}' uses '{(material == null ? "no material" : material.name)}': remap the model's materials to {MATERIAL_PREFIX}* (model importer, Materials tab).");
                    complete = false;
                }
            }
            foreach (string name in NotWalkableParts)
            {
                Transform part = instance.transform.Find(name);
                if (part == null) continue;
                var modifier = part.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = NOT_WALKABLE;
            }
            Transform rubble = instance.transform.Find(RUBBLE);
            if (rubble != null) rubble.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return complete;
        }

        /// <summary>
        /// Walk surfaces (scaled boxes directly under the root, as the graybox had them), solid colliders by group
        /// and Not Walkable volumes along the handrails, all from the parts file.
        /// </summary>
        private static bool BuildBoxes(Transform root, JObject parts)
        {
            bool ramp = false;
            foreach (JToken token in parts["walk"])
            {
                Transform walk = Box(root, (JObject)token, scaled: true);
                ramp |= walk.name == RAMP_CAMP;
            }
            if (!ramp)
            {
                GameLog.Error(TAG, $"{PARTS_PATH} has no walk surface '{RAMP_CAMP}' (the home yard's tarp is tied to it); {PREFAB_PATH} left as it was.");
                return false;
            }

            var colliders = new GameObject(COLLIDERS).transform;
            colliders.SetParent(root, false);
            var groups = new Dictionary<string, Transform>();
            foreach (JToken token in parts["colliders"])
            {
                var box = (JObject)token;
                string group = (string)box["group"] ?? "Other";
                if (!groups.TryGetValue(group, out Transform parent))
                {
                    parent = new GameObject(group).transform;
                    parent.SetParent(colliders, false);
                    groups[group] = parent;
                }
                Box(parent, box, scaled: false);
            }

            var volumes = new GameObject(NAVMESH).transform;
            volumes.SetParent(root, false);
            foreach (JToken token in parts["notWalkable"])
            {
                var box = (JObject)token;
                var holder = new GameObject((string)box["name"]).transform;
                holder.SetParent(volumes, false);
                holder.localPosition = Vector(box["centre"]);
                holder.localRotation = Quaternion.Euler(Vector(box["euler"]));
                var volume = holder.gameObject.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = Vector(box["size"]);
                volume.area = NOT_WALKABLE;
            }
            return true;
        }

        private static Transform Box(Transform parent, JObject box, bool scaled)
        {
            var holder = new GameObject((string)box["name"]).transform;
            holder.SetParent(parent, false);
            holder.localPosition = Vector(box["centre"]);
            holder.localRotation = Quaternion.Euler(Vector(box["euler"]));
            var collider = holder.gameObject.AddComponent<BoxCollider>();
            if (scaled) holder.localScale = Vector(box["size"]);
            else collider.size = Vector(box["size"]);
            return holder;
        }

        /// <summary>
        /// The cellar's dock door: the game's door prefab on the model's east jamb, unlocked, swinging into the room,
        /// with a fixed save id. Left out of the bake, so the shut leaf does not cut the room off the NavMesh.
        /// </summary>
        private static void BuildCellarDoor(Transform root, GameObject prefab, CellarFacts cellar)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            door.name = CELLAR_DOOR;
            door.transform.localPosition = cellar.DoorPosition;
            door.transform.localRotation = Quaternion.Euler(0f, cellar.DoorYaw, 0f);
            door.transform.localScale = new Vector3(1f, cellar.DoorScaleY, 1f);
            door.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var doorSo = new SerializedObject(door.GetComponent<DoorInteractable>());
            doorSo.FindProperty("_openAngle").floatValue = cellar.DoorOpenAngle;
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            var lockSo = new SerializedObject(door.GetComponent<Lockable>());
            lockSo.FindProperty("_isLocked").boolValue = false;
            lockSo.ApplyModifiedPropertiesWithoutUndo();

            var saveable = door.GetComponent<SaveableObject>();
            if (saveable == null) saveable = door.AddComponent<SaveableObject>(); // the prefab has none; scene doors add it
            var saveSo = new SerializedObject(saveable);
            saveSo.FindProperty(Game.Editor.Save.SaveableObjectValidator.SAVE_ID_PROPERTY).stringValue = CELLAR_DOOR_SAVE_ID;
            saveSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A kit lantern on the model's bracket, with a clock-driven light and glow (dark by day). A point marked
        /// <c>always</c> gets a low light that is simply on, and no clock.
        /// </summary>
        private static void BuildLantern(Transform root, JObject point, GameObject kit, DayNightConfigSO dayNight, Material glowMaterial)
        {
            var lantern = (GameObject)PrefabUtility.InstantiatePrefab(kit, root);
            PrefabUtility.UnpackPrefabInstance(lantern, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            lantern.name = (string)point["name"];
            lantern.transform.localPosition = Vector(point["position"]);
            foreach (Collider collider in lantern.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            lantern.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            if (point.Value<bool?>("always") == true)
            {
                var steady = new GameObject("Light");
                steady.transform.SetParent(lantern.transform, false);
                steady.transform.localPosition = new Vector3(0f, -0.05f, 0.25f * (float)point["out"]);
                var burning = steady.AddComponent<Light>();
                burning.type = LightType.Point;
                burning.color = LanternColor;
                burning.intensity = ALWAYS_INTENSITY;
                burning.range = ALWAYS_RANGE;
                // With shadows: a light that is on by day would otherwise show through the walls, on the dock and in the hall.
                burning.shadows = LightShadows.Soft;
                return;
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Glow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(lantern.transform, false);
            quad.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            quad.transform.localScale = new Vector3(0.16f, 0.2f, 1f);
            var glow = quad.GetComponent<MeshRenderer>();
            glow.sharedMaterial = glowMaterial;
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glow.enabled = false; // lit by ClockNightLight; a disabled renderer also stays out of the bake

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(lantern.transform, false);
            // Out from the wall, so the boards behind the lantern are lit rather than cut by the light's own shell.
            lightObject.transform.localPosition = new Vector3(0f, -0.05f, 0.25f * (float)point["out"]);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LanternColor;
            light.intensity = LANTERN_INTENSITY;
            light.range = LANTERN_RANGE;
            light.shadows = LightShadows.None;
            light.enabled = false; // ClockNightLight switches it on at dusk

            var nightLight = lantern.AddComponent<ClockNightLight>();
            var so = new SerializedObject(nightLight);
            so.FindProperty("_config").objectReferenceValue = dayNight;
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_billboard").objectReferenceValue = quad.transform;
            so.FindProperty("_glowColor").colorValue = LanternGlow;
            SerializedProperty renderers = so.FindProperty("_glowRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 Vector(JToken array) => new Vector3((float)array[0], (float)array[1], (float)array[2]);

        private static string Invariant(System.FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

        private static JObject ReadParts()
        {
            if (!File.Exists(PARTS_PATH)) return null;
            try
            {
                return JObject.Parse(File.ReadAllText(PARTS_PATH));
            }
            catch (System.Exception e) when (e is Newtonsoft.Json.JsonException || e is IOException)
            {
                GameLog.Error(TAG, $"{PARTS_PATH} could not be read: {e.Message}");
                return null;
            }
        }
    }
}
