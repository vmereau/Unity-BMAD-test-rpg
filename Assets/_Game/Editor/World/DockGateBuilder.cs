using System.Collections.Generic;
using System.IO;
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
    /// The Area 1 dock gate prefab, built round the Blender model (<c>Art/Environment/Camp/models/dock gate</c>):
    /// a palisade gateway with two leaves standing open, the guard's lean-to and a fire basket that burns at night.
    /// The model script writes a parts file (colliders, Not Walkable volumes, the fire's light point) that this
    /// builder turns into objects, so no size is written twice. Local frame: x east (out of the camp), y up,
    /// z north; origin on the lane in the middle of the opening. <see cref="Area1SitesBuilder"/> places the prefab
    /// as the <c>DockGate</c> root; without it, the graybox gate. Look: <c>Art/Environment/Camp/QUAY_CONCEPTS.md</c>.
    /// </summary>
    public static class DockGateBuilder
    {
        private const string TAG = "[DockGateBuilder]";
        internal const string PREFAB_PATH = "Assets/_Game/Prefabs/World/DockGate/DockGate.prefab";
        private const string MODEL_FOLDER = "Assets/_Game/Art/Environment/Camp/models/dock gate/";
        private const string MODEL_PATH = MODEL_FOLDER + "DockGate.fbx";
        private const string PARTS_PATH = MODEL_FOLDER + "DockGate.json";
        internal const string WILDERNESS_PREFAB_PATH = "Assets/_Game/Prefabs/World/DockGate/WildernessGate.prefab";
        private const string WILDERNESS_FOLDER = "Assets/_Game/Art/Environment/Camp/models/wilderness gate/";

        /// <summary>One gate model and the prefab built from it. The wilderness gate is the dock gate's gateway alone.</summary>
        private readonly struct Kit
        {
            public readonly string Prefab, Model, Parts, Script, RemapFrom;
            public readonly bool HasFire;

            public Kit(string prefab, string model, string parts, string script, bool hasFire, string remapFrom)
            {
                Prefab = prefab; Model = model; Parts = parts; Script = script; HasFire = hasFire; RemapFrom = remapFrom;
            }
        }

        private static readonly Kit Dock = new Kit(PREFAB_PATH, MODEL_PATH, PARTS_PATH, "build_dock_gate.py", true, null);
        private static readonly Kit Wilderness = new Kit(WILDERNESS_PREFAB_PATH, WILDERNESS_FOLDER + "WildernessGate.fbx",
            WILDERNESS_FOLDER + "WildernessGate.json", "build_wilderness_gate.py", false, MODEL_PATH);
        private const string DAY_NIGHT = "Assets/_Game/Data/Config/DayNightConfig.asset";
        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";
        private const string MATERIAL_PREFIX = "M_Camp_";
        private const string MODEL = "Model";
        private const string COLLIDERS = "Colliders";
        private const string NAVMESH = "NavMesh";
        internal const string FIRE = "BrazierFire";
        internal const int VERSION = 1;
        private const int NOT_WALKABLE = 1;
        private static readonly string[] PartLists = { "objects", "colliders", "notWalkable" };

        // Tuned in Play at 23:00: the guard's corner and the gateway's posts readable, the bank path dark beyond.
        private static readonly Color FireColor = new Color(1f, 0.62f, 0.32f);
        private static readonly Color FireGlow = new Color(2.6f, 1.15f, 0.4f, 1f);
        private const float FIRE_INTENSITY = 6f;
        private const float FIRE_RANGE = 8f;

        /// <summary>What the parts file says, in the prefab's frame.</summary>
        public struct GateFacts
        {
            /// <summary>The clear opening: min z, max z, height under the lintel.</summary>
            public Vector3 Opening;
            public Vector3 Fire;
        }

        /// <summary>Reads the gate's facts from a parts file. False, with what is wrong, when a field is missing or malformed.</summary>
        public static bool TryReadGate(JObject parts, out GateFacts gate, out string problem) => TryReadGate(parts, true, out gate, out problem);

        /// <summary>As above; a gateway without a fire basket (<paramref name="needsFire"/> false) has no 'fire'.</summary>
        public static bool TryReadGate(JObject parts, bool needsFire, out GateFacts gate, out string problem)
        {
            gate = default;
            if (parts == null)
            {
                problem = "no parts file";
                return false;
            }
            if (!IsNumber(parts["version"]) || (int)parts["version"] != VERSION)
            {
                problem = $"'version' must be {VERSION}";
                return false;
            }
            foreach (string list in PartLists)
            {
                if (parts[list] is JArray) continue;
                problem = $"no list '{list}'";
                return false;
            }
            if (!TryFloats(parts["opening"], out Vector3 opening) || opening.y <= opening.x || opening.z <= 0f)
            {
                problem = "'opening' must be [min z, max z, height]";
                return false;
            }
            Vector3 fire = Vector3.zero;
            if (needsFire && !TryFloats(parts["fire"], out fire))
            {
                problem = "'fire' must be [x, y, z]";
                return false;
            }
            foreach (string list in new[] { "colliders", "notWalkable" })
                foreach (JToken token in parts[list])
                    if (!(token is JObject box) || box["name"] == null || box["name"].Type != JTokenType.String
                        || !TryFloats(box["centre"], out _) || !TryFloats(box["size"], out _) || !TryFloats(box["euler"], out _))
                    {
                        problem = $"every '{list}' entry needs name, centre, size and euler [x, y, z]";
                        return false;
                    }
            gate = new GateFacts { Opening = opening, Fire = fire };
            problem = null;
            return true;
        }

        private static bool IsNumber(JToken token) => token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);

        private static bool TryFloats(JToken token, out Vector3 value)
        {
            value = Vector3.zero;
            if (!(token is JArray array) || array.Count != 3 || !IsNumber(array[0]) || !IsNumber(array[1]) || !IsNumber(array[2])) return false;
            value = new Vector3((float)array[0], (float)array[1], (float)array[2]);
            return true;
        }

        [MenuItem("Tools/World/Build Dock Gate Prefab")]
        private static void BuildMenu() => Build();

        [MenuItem("Tools/World/Build Wilderness Gate Prefab")]
        private static void BuildWildernessMenu() => BuildWilderness();

        /// <summary>The gateway by the gang house: the dock gate's posts, lintel and leaves, without lean-to or fire.</summary>
        internal static GameObject BuildWilderness() => Build(Wilderness);

        /// <summary>
        /// Rebuilds the prefab from the model and its parts file. A missing model, a model object the parts file
        /// names but the FBX lacks, a malformed parts file or a material that is not a camp material stops before
        /// saving, so the prefab (and the scene instance that follows it) is left as it was.
        /// </summary>
        internal static GameObject Build() => Build(Dock);

        private static GameObject Build(Kit kit)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return null;
            }
            JObject parts = ReadParts(kit.Parts);
            if (File.Exists(kit.Model))
            {
                AssetDatabase.ImportAsset(kit.Model, ImportAssetOptions.ForceSynchronousImport);
                if (kit.RemapFrom != null) Remap(kit.RemapFrom, kit.Model);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(kit.Model);
            var dayNight = AssetDatabase.LoadAssetAtPath<DayNightConfigSO>(DAY_NIGHT);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GLOW);
            if (parts == null || model == null || dayNight == null || glow == null)
            {
                GameLog.Error(TAG, $"Need {kit.Model}, {kit.Parts} (run {kit.Script}), {DAY_NIGHT} and {GLOW}; {kit.Prefab} left as it was.");
                return null;
            }
            if (!TryReadGate(parts, kit.HasFire, out GateFacts gate, out string problem))
            {
                GameLog.Error(TAG, $"{kit.Parts}: {problem} (rerun {kit.Script}); {kit.Prefab} left as it was.");
                return null;
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(kit.Prefab));
            try
            {
                if (!BuildModel(root.transform, model, parts, kit)) return null;
                BuildBoxes(root.transform, parts);
                if (kit.HasFire) BuildFire(root.transform, gate.Fire, dayNight, glow);

                string folder = kit.Prefab.Substring(0, kit.Prefab.LastIndexOf('/'));
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, kit.Prefab, out bool saved);
                if (saved) GameLog.Info(TAG, $"Saved {kit.Prefab} ({parts.Value<int?>("total")} triangles)");
                else GameLog.Error(TAG, $"Could not save {kit.Prefab}");
                return saved ? prefab : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The gateway's materials (Wh_*) point where the dock gate's do: round logs on the palisade's wood.</summary>
        private static void Remap(string from, string to)
        {
            var source = AssetImporter.GetAtPath(from) as ModelImporter;
            var target = AssetImporter.GetAtPath(to) as ModelImporter;
            if (source == null || target == null) return;
            Dictionary<AssetImporter.SourceAssetIdentifier, Object> have = target.GetExternalObjectMap();
            bool changed = false;
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> pair in source.GetExternalObjectMap())
            {
                if (pair.Key.type != typeof(Material) || pair.Value == null) continue;
                if (have.TryGetValue(pair.Key, out Object current) && current == pair.Value) continue;
                target.AddRemap(pair.Key, pair.Value);
                changed = true;
            }
            if (changed) target.SaveAndReimport();
        }

        /// <summary>The model's objects under <c>Model</c>, all left out of the bake: the lane's NavMesh is the ground's.</summary>
        private static bool BuildModel(Transform root, GameObject model, JObject parts, Kit kit)
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
                    GameLog.Error(TAG, $"'{kit.Model}' has no object '{name}'; {kit.Prefab} left as it was.");
                    complete = false;
                    continue;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.name.StartsWith(MATERIAL_PREFIX)) continue;
                    GameLog.Error(TAG, $"'{name}' uses '{(material == null ? "no material" : material.name)}': remap the model's materials to {MATERIAL_PREFIX}* (model importer, Materials tab).");
                    complete = false;
                }
                // thin logs, planks and bars: a bake would leave slivers on them; the volumes below close what matters
                part.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            return complete;
        }

        private static void BuildBoxes(Transform root, JObject parts)
        {
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
                Transform holder = Holder(parent, box);
                holder.gameObject.AddComponent<BoxCollider>().size = Vector(box["size"]);
            }

            var volumes = new GameObject(NAVMESH).transform;
            volumes.SetParent(root, false);
            foreach (JToken token in parts["notWalkable"])
            {
                var box = (JObject)token;
                var volume = Holder(volumes, box).gameObject.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = Vector(box["size"]);
                volume.area = NOT_WALKABLE;
            }
        }

        private static Transform Holder(Transform parent, JObject box)
        {
            var holder = new GameObject((string)box["name"]).transform;
            holder.SetParent(parent, false);
            holder.localPosition = Vector(box["centre"]);
            holder.localRotation = Quaternion.Euler(Vector(box["euler"]));
            return holder;
        }

        /// <summary>The fire in the basket: a point light and a glow, both off by day (<see cref="ClockNightLight"/>).</summary>
        private static void BuildFire(Transform root, Vector3 at, DayNightConfigSO dayNight, Material glowMaterial)
        {
            var fire = new GameObject(FIRE);
            fire.transform.SetParent(root, false);
            fire.transform.localPosition = at;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Glow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(fire.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            quad.transform.localScale = new Vector3(0.42f, 0.5f, 1f);
            var glow = quad.GetComponent<MeshRenderer>();
            glow.sharedMaterial = glowMaterial;
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glow.enabled = false; // lit by ClockNightLight; a disabled renderer also stays out of the bake

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(fire.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.35f, 0f); // above the basket's rim, so the bars do not cut it
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = FireColor;
            light.intensity = FIRE_INTENSITY;
            light.range = FIRE_RANGE;
            light.shadows = LightShadows.None;
            light.enabled = false; // ClockNightLight switches it on at dusk

            var so = new SerializedObject(fire.AddComponent<ClockNightLight>());
            so.FindProperty("_config").objectReferenceValue = dayNight;
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_billboard").objectReferenceValue = quad.transform;
            so.FindProperty("_glowColor").colorValue = FireGlow;
            SerializedProperty renderers = so.FindProperty("_glowRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 Vector(JToken array) => new Vector3((float)array[0], (float)array[1], (float)array[2]);

        private static JObject ReadParts(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (System.Exception e) when (e is Newtonsoft.Json.JsonException || e is IOException)
            {
                GameLog.Error(TAG, $"{path} could not be read: {e.Message}");
                return null;
            }
        }
    }
}
