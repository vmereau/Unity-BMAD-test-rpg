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
    /// </summary>
    internal static class WarehouseBuilder
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
            if (parts == null || model == null || lantern == null || dayNight == null || glow == null)
            {
                GameLog.Error(TAG, $"Need {MODEL_PATH}, {PARTS_PATH} (run build_warehouse.py), {LANTERN_PATH}, {DAY_NIGHT} and {GLOW}; {PREFAB_PATH} left as it was.");
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

        /// <summary>A kit lantern on the model's bracket, with a clock-driven light and glow (dark by day).</summary>
        private static void BuildLantern(Transform root, JObject point, GameObject kit, DayNightConfigSO dayNight, Material glowMaterial)
        {
            var lantern = (GameObject)PrefabUtility.InstantiatePrefab(kit, root);
            PrefabUtility.UnpackPrefabInstance(lantern, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            lantern.name = (string)point["name"];
            lantern.transform.localPosition = Vector(point["position"]);
            foreach (Collider collider in lantern.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            lantern.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

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
