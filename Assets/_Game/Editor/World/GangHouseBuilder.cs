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
    /// The Area 1 gang house: the prefab built round the Blender model (<c>Art/Environment/Camp/models/gang house</c>:
    /// the lodge on its rubble base, the camp ramp, the balcony, the deck, the walkway, the pier and the net frame)
    /// and the command that readies its site in StartingTown. The model script writes a parts file (colliders, walk
    /// surfaces, Not Walkable volumes, lights, anchors) that this builder, <see cref="Area1SitesBuilder"/> and the
    /// tests read, so no size is written twice. Local frame: x along the house toward its west end, y up, z toward
    /// the camp; origin on the main floor's top at the house's centre; the root stands turned in the scene.
    /// Look: <c>Art/Environment/Camp/GANG_HOUSE_CONCEPTS.md</c>.
    /// </summary>
    public static class GangHouseBuilder
    {
        private const string TAG = "[GangHouseBuilder]";
        internal const string PREFAB_PATH = "Assets/_Game/Prefabs/World/GangHouse/GangHouse.prefab";
        private const string MODEL_FOLDER = "Assets/_Game/Art/Environment/Camp/models/gang house/";
        private const string MODEL_PATH = MODEL_FOLDER + "GangHouse.fbx";
        internal const string PARTS_PATH = MODEL_FOLDER + "GangHouse.json";
        private const string REMAP_SOURCE = "Assets/_Game/Art/Environment/Camp/models/warehouse/Warehouse.fbx";
        private const string LANTERN_PATH = "Assets/Hivemind/HDRP(Default)/Medieval_Docks/Art/Prefabs/SM_Lantern_Hanging.prefab";
        private const string DAY_NIGHT = "Assets/_Game/Data/Config/DayNightConfig.asset";
        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";
        private const string MATERIAL_PREFIX = "M_Camp_";
        private const string MODEL = "Model";
        private const string COLLIDERS = "Colliders";
        private const string NAVMESH = "NavMesh";
        internal const int VERSION = 2;
        private const string DOOR_PATH = "Assets/_Game/Prefabs/World/Doors/Poor small door.prefab";
        private const string HIDDEN_DOOR = "HiddenDoor";
        private const string HIDDEN_DOOR_SAVE_ID = "area1-gang-house-hidden-door"; // fixed, so a rebuild keeps the saved door state
        internal const string TUNNEL_DOOR = "TunnelDoor_Basement";
        private const string TUNNEL_DOOR_SAVE_ID = "area1-gang-house-tunnel-door";
        private const string WAREHOUSE = "/Area1_Blockout/Warehouse";
        private const string WAREHOUSE_PARTS = "Assets/_Game/Art/Environment/Camp/models/warehouse/Warehouse.json";
        private const int NOT_WALKABLE = 1;
        // Roofs, furniture, the hearth, door leaves, the rubble, piles and rails, the nets: no NavMesh island on them.
        // Frame, walls and iron carry no modifier (it would cut the floor at every post); floor and decks are walked on.
        private static readonly string[] NotWalkableParts = { "Roof", "Shelters", "Furniture", "Hearth", "Doors", "Base", "Piles", "Nets", "BasementWalls", "TunnelTimber" };
        private static readonly string[] PartLists = { "objects", "walk", "colliders", "notWalkable", "lanterns", "lights", "goods" };
        private static readonly (string name, int count)[] Anchors =
        {
            ("campDoor", 4), ("lakeDoor", 4), ("room", 5), ("ramp", 6), ("deck", 5), ("walkway", 5), ("pier", 5), ("boat", 4),
            ("groundCut", 5), ("groundCutInside", 5), ("hiddenDoor", 5), ("basement", 6), ("nets", 4), ("ridge", 1), ("chimneyTop", 3),
            ("well", 5), ("mouth", 5), ("tunnelStart", 3),
        };

        // Scene objects the place command edits (hand-owned, so found by path: this is an authoring command, not a builder).
        private const string TERRAIN = "/Terrain_StartingArea";
        private const string TOLL_GATE = "/TollGates/TollGate_Wilderness";
        private const string BARRICADE_PREFIX = "SM_Barricade_";
        private static readonly Rect BarricadeArea = Rect.MinMaxRect(27f, 49f, 39f, 58f);   // world x, z: the old wilderness opening
        private const float BLEND = 1.2f;                       // the cut's bank rises over this distance...
        private const float BANK = 0.6f;                        // ...by this much
        private const float HEIGHT_STEP = 1f / 32766f;
        private const float POSE_TOLERANCE = 0.05f;

        // Tuned to read as in GANG_HOUSE_CONCEPTS.md -> R8: two lanterns and the hearth, light dying within a few metres.
        private static readonly Color LanternColor = new Color(1f, 0.7f, 0.42f);
        private static readonly Color LanternGlow = new Color(2.4f, 1.2f, 0.45f, 1f);
        private static readonly Color FireColor = new Color(1f, 0.6f, 0.32f);
        private const float LANTERN_INTENSITY = 5f, LANTERN_RANGE = 7f;
        private const float ALWAYS_INTENSITY = 2.2f, ALWAYS_RANGE = 7f;   // the basement's and the tunnel's lanterns: low, always on
        private const float HEARTH_INTENSITY = 6f, HEARTH_RANGE = 7f;

        /// <summary>What the parts file says about where the house stands and what stands round it.</summary>
        public struct HouseFacts
        {
            /// <summary>World x, z of the origin.</summary>
            public Vector2 Centre;
            public float Yaw;
            public float FloorWorldY;
            public JObject Anchors;
            /// <summary>The basement's terrain hole in the prefab's frame: min x, min z, max x, max z.</summary>
            public Vector4 Hole;
            /// <summary>The hidden door: where its hinge stands (prefab frame), its turn, its stretch in height and its swing.</summary>
            public Vector3 DoorPosition;
            public float DoorYaw, DoorScaleY, DoorOpenAngle;
            /// <summary>The tunnel's line on its floor, in the prefab's frame, from the warehouse's side room to the basement.</summary>
            public Vector3[] TunnelNodes;
            public float TunnelWidth, TunnelHeight;
            /// <summary>The tunnel's door at the basement's end, hung on the last frame outside the west wall.</summary>
            public DoorPlacement TunnelDoor;

            public DoorPlacement HiddenDoor => new DoorPlacement { Position = DoorPosition, Yaw = DoorYaw, ScaleY = DoorScaleY, OpenAngle = DoorOpenAngle };

            public Vector3 Origin => new Vector3(Centre.x, FloorWorldY, Centre.y);
            public Quaternion Rotation => Quaternion.Euler(0f, Yaw, 0f);

            /// <summary>A point of the prefab's frame, in the world.</summary>
            public Vector3 World(Vector3 local) => Origin + Rotation * local;

            public float[] Anchor(string name)
            {
                var array = (JArray)Anchors[name];
                var values = new float[array.Count];
                for (int i = 0; i < values.Length; i++) values[i] = (float)array[i];
                return values;
            }
        }

        /// <summary>Where a door of the game's door prefab hangs: its hinge (prefab frame), turn, stretch in height and swing.</summary>
        public struct DoorPlacement
        {
            public Vector3 Position;
            public float Yaw, ScaleY, OpenAngle;
        }

        private static bool TryReadPlacement(JToken token, out DoorPlacement placement)
        {
            placement = default;
            if (!(token is JObject door) || !IsVector(door["position"]) || !IsNumber(door["yaw"]) || !IsNumber(door["scaleY"])
                || !IsNumber(door["openAngle"]) || (float)door["scaleY"] <= 0f) return false;
            placement = new DoorPlacement { Position = Vector(door["position"]), Yaw = (float)door["yaw"], ScaleY = (float)door["scaleY"], OpenAngle = (float)door["openAngle"] };
            return true;
        }

        /// <summary>Reads and checks a parts file. False, with what is wrong, when a field is missing or malformed.</summary>
        public static bool TryReadHouse(JObject parts, out HouseFacts house, out string problem)
        {
            house = default;
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
            if (!(parts["centre"] is JArray centre) || centre.Count != 2 || !IsNumber(centre[0]) || !IsNumber(centre[1]))
            {
                problem = "'centre' must be [x, z]";
                return false;
            }
            if (!IsNumber(parts["yaw"]) || !IsNumber(parts["floorWorldY"]))
            {
                problem = "'yaw' and 'floorWorldY' must be numbers";
                return false;
            }
            foreach (string list in new[] { "walk", "colliders", "notWalkable" })
                foreach (JToken token in parts[list])
                    if (!(token is JObject box) || box["name"] == null || box["name"].Type != JTokenType.String
                        || !IsVector(box["centre"]) || !IsVector(box["size"]) || !IsVector(box["euler"]))
                    {
                        problem = $"every '{list}' entry needs name, centre, size and euler [x, y, z]";
                        return false;
                    }
            foreach (JToken token in parts["lanterns"])
                if (!(token is JObject point) || point["name"] == null || !IsVector(point["position"]) || !IsVector(point["out"]))
                {
                    problem = "every 'lanterns' entry needs name, position and out [x, y, z]";
                    return false;
                }
            foreach (JToken token in parts["lights"])
                if (!(token is JObject point) || point["name"] == null || !IsVector(point["position"]))
                {
                    problem = "every 'lights' entry needs name and position [x, y, z]";
                    return false;
                }
            foreach (JToken token in parts["goods"])
                if (!(token is JObject prop) || prop["kit"] == null || prop["kit"].Type != JTokenType.String || !IsVector(prop["position"])
                    || !IsVector(prop["euler"]) || !IsNumber(prop["scale"]) || prop["solid"] == null || prop["solid"].Type != JTokenType.Boolean)
                {
                    problem = "every 'goods' entry needs kit, position and euler [x, y, z], scale and solid";
                    return false;
                }
            if (!(parts["anchors"] is JObject anchors))
            {
                problem = "no 'anchors'";
                return false;
            }
            foreach ((string name, int count) in Anchors)
            {
                if (anchors[name] is JArray values && values.Count >= count && AllNumbers(values)) continue;
                problem = $"anchor '{name}' needs {count} numbers";
                return false;
            }
            if (!(parts["basement"] is JObject basement) || !(basement["hole"] is JArray hole) || hole.Count != 4 || !AllNumbers(hole)
                || (float)hole[2] <= (float)hole[0] || (float)hole[3] <= (float)hole[1])
            {
                problem = "'basement.hole' must be [min x, min z, max x, max z]";
                return false;
            }
            if (!(basement["door"] is JObject door) || !IsVector(door["position"]) || !IsNumber(door["yaw"]) || !IsNumber(door["scaleY"]) || !IsNumber(door["openAngle"]))
            {
                problem = "'basement.door' needs position [x, y, z], yaw, scaleY and openAngle";
                return false;
            }
            if (!(parts["tunnel"] is JObject tunnel) || !(tunnel["nodes"] is JArray nodes) || nodes.Count < 2 || !IsNumber(tunnel["width"]) || !IsNumber(tunnel["height"]))
            {
                problem = "'tunnel' needs nodes (two or more), width and height";
                return false;
            }
            var line = new Vector3[nodes.Count];
            for (int i = 0; i < line.Length; i++)
            {
                if (!IsVector(nodes[i]))
                {
                    problem = "every 'tunnel.nodes' entry must be [x, y, z]";
                    return false;
                }
                line[i] = Vector(nodes[i]);
            }
            if (!TryReadPlacement(tunnel["door"], out DoorPlacement tunnelDoor))
            {
                problem = "'tunnel.door' needs position [x, y, z], yaw, scaleY (above 0) and openAngle";
                return false;
            }
            house = new HouseFacts
            {
                Centre = new Vector2((float)centre[0], (float)centre[1]), Yaw = (float)parts["yaw"],
                FloorWorldY = (float)parts["floorWorldY"], Anchors = anchors,
                Hole = new Vector4((float)hole[0], (float)hole[1], (float)hole[2], (float)hole[3]),
                DoorPosition = Vector(door["position"]), DoorYaw = (float)door["yaw"], DoorScaleY = (float)door["scaleY"],
                DoorOpenAngle = (float)door["openAngle"], TunnelNodes = line,
                TunnelWidth = (float)tunnel["width"], TunnelHeight = (float)tunnel["height"], TunnelDoor = tunnelDoor,
            };
            problem = null;
            return true;
        }

        private static bool IsNumber(JToken token) => token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);

        private static bool AllNumbers(JArray array)
        {
            foreach (JToken token in array)
                if (!IsNumber(token)) return false;
            return true;
        }

        private static bool IsVector(JToken token) => token is JArray array && array.Count == 3 && AllNumbers(array);

        // ---------------------------------------------------------------------------------- prefab

        [MenuItem("Tools/World/Build Gang House Prefab")]
        private static void BuildMenu() => Build();

        /// <summary>
        /// Rebuilds the prefab from the model and its parts file. A missing model, a model object the parts file
        /// names, a malformed parts file or a material that is not a camp material stops before saving, so the
        /// prefab (and the scene instance that follows it) is left as it was.
        /// </summary>
        internal static GameObject Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return null;
            }
            JObject parts = ReadParts();
            if (File.Exists(MODEL_PATH))
            {
                AssetDatabase.ImportAsset(MODEL_PATH, ImportAssetOptions.ForceSynchronousImport);
                Remap();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);
            var lantern = AssetDatabase.LoadAssetAtPath<GameObject>(LANTERN_PATH);
            var dayNight = AssetDatabase.LoadAssetAtPath<DayNightConfigSO>(DAY_NIGHT);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GLOW);
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DOOR_PATH);
            var absent = new List<string>();
            if (doorPrefab == null || doorPrefab.GetComponent<DoorInteractable>() == null || doorPrefab.GetComponent<Lockable>() == null)
                absent.Add(DOOR_PATH + " (with DoorInteractable and Lockable)");
            if (model == null) absent.Add(MODEL_PATH + " (run build_gang_house.py)");
            if (parts == null) absent.Add(PARTS_PATH + " (run build_gang_house.py)");
            if (lantern == null) absent.Add(LANTERN_PATH);
            if (dayNight == null) absent.Add(DAY_NIGHT);
            if (glow == null) absent.Add(GLOW);
            var (skill, basementKey) = RouteDoorLocks.Load(RouteDoorLocks.KEY_GANG_BASEMENT, absent);
            var (_, tunnelKey) = RouteDoorLocks.Load(RouteDoorLocks.KEY_GANG_TUNNEL, absent);
            if (absent.Count > 0)
            {
                GameLog.Error(TAG, $"Missing: {string.Join(", ", absent)}; {PREFAB_PATH} left as it was.");
                return null;
            }
            if (!TryReadHouse(parts, out HouseFacts facts, out string problem))
            {
                GameLog.Error(TAG, $"{PARTS_PATH}: {problem} (rerun build_gang_house.py); {PREFAB_PATH} left as it was.");
                return null;
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(PREFAB_PATH));
            try
            {
                if (!BuildModel(root.transform, model, parts)) return null;
                BuildBoxes(root.transform, parts);
                foreach (JToken point in parts["lanterns"]) BuildLantern(root.transform, (JObject)point, lantern, dayNight, glow);
                foreach (JToken point in parts["lights"]) BuildFireLight(root.transform, (JObject)point, dayNight);
                RouteDoorLocks.Lock(BuildDoor(root.transform, doorPrefab, HIDDEN_DOOR, HIDDEN_DOOR_SAVE_ID, facts.HiddenDoor), skill, basementKey);
                RouteDoorLocks.Lock(BuildDoor(root.transform, doorPrefab, TUNNEL_DOOR, TUNNEL_DOOR_SAVE_ID, facts.TunnelDoor), skill, tunnelKey);

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

        /// <summary>The model's materials (Wh_*) point at the same camp materials as the warehouse's.</summary>
        private static void Remap()
        {
            var source = AssetImporter.GetAtPath(REMAP_SOURCE) as ModelImporter;
            var target = AssetImporter.GetAtPath(MODEL_PATH) as ModelImporter;
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
                if (instance.transform.Find(name) != null) continue;
                GameLog.Error(TAG, $"'{MODEL_PATH}' has no object '{name}'; {PREFAB_PATH} left as it was.");
                complete = false;
            }
            if (!complete) return false;
            foreach (string name in NotWalkableParts)
            {
                var modifier = instance.transform.Find(name).gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = NOT_WALKABLE;
            }
            return true;
        }

        /// <summary>Walk surfaces (scaled boxes directly under the root), solid colliders by group and Not Walkable volumes.</summary>
        private static void BuildBoxes(Transform root, JObject parts)
        {
            foreach (JToken token in parts["walk"]) Box(root, (JObject)token, scaled: true);
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
        /// A door of the model (the hidden door behind the nets, the tunnel's door at the basement's end): the game's
        /// door prefab where the parts file hangs it, with a fixed save id (a rebuild keeps the saved door state). Left
        /// out of the bake, so the shut leaf does not cut the room off the NavMesh. The caller locks it.
        /// </summary>
        private static GameObject BuildDoor(Transform root, GameObject prefab, string name, string saveId, DoorPlacement placement)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            door.name = name;
            door.transform.localPosition = placement.Position;
            door.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
            door.transform.localScale = new Vector3(1f, placement.ScaleY, 1f);
            door.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var doorSo = new SerializedObject(door.GetComponent<DoorInteractable>());
            doorSo.FindProperty("_openAngle").floatValue = placement.OpenAngle;
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            var saveable = door.GetComponent<SaveableObject>();
            if (saveable == null) saveable = door.AddComponent<SaveableObject>(); // the prefab has none; scene doors add it
            var saveSo = new SerializedObject(saveable);
            saveSo.FindProperty(Game.Editor.Save.SaveableObjectValidator.SAVE_ID_PROPERTY).stringValue = saveId;
            saveSo.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        /// <summary>
        /// A kit lantern on the model's bracket, with a clock-driven light and glow (dark by day). A point marked
        /// <c>always</c> (the basement, the tunnel: no daylight there) gets a low light that is simply on.
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
                // With shadows: unshadowed, a light that is on by day would show through the ground and the walls.
                Light steady = NightLight(lantern.transform, Vector(point["out"]) * 0.25f + new Vector3(0f, -0.05f, 0f),
                    LanternColor, ALWAYS_INTENSITY, ALWAYS_RANGE, LightShadows.Soft);
                steady.enabled = true;
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

            // Out from the wall, so the boards behind the lantern are lit rather than cut by the light's own shell.
            Light light = NightLight(lantern.transform, Vector(point["out"]) * 0.25f + new Vector3(0f, -0.05f, 0f),
                LanternColor, LANTERN_INTENSITY, LANTERN_RANGE, LightShadows.None);
            Wire(lantern.AddComponent<ClockNightLight>(), dayNight, light, quad.transform, glow);
        }

        /// <summary>The hearth's fire: a clock-driven light with shadows, so it shows through the doors and no further.</summary>
        private static void BuildFireLight(Transform root, JObject point, DayNightConfigSO dayNight)
        {
            var holder = new GameObject((string)point["name"]);
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = Vector(point["position"]);
            Light light = NightLight(holder.transform, Vector3.zero, FireColor, HEARTH_INTENSITY, HEARTH_RANGE, LightShadows.Soft);
            Wire(holder.AddComponent<ClockNightLight>(), dayNight, light, null, null);
        }

        private static Light NightLight(Transform parent, Vector3 localPosition, Color color, float intensity, float range, LightShadows shadows)
        {
            var holder = new GameObject("Light");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPosition;
            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            light.enabled = false; // ClockNightLight switches it on at dusk
            return light;
        }

        private static void Wire(ClockNightLight nightLight, DayNightConfigSO dayNight, Light light, Transform billboard, Renderer glow)
        {
            var so = new SerializedObject(nightLight);
            so.FindProperty("_config").objectReferenceValue = dayNight;
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_billboard").objectReferenceValue = billboard;
            so.FindProperty("_glowColor").colorValue = LanternGlow;
            SerializedProperty renderers = so.FindProperty("_glowRenderers");
            renderers.arraySize = glow == null ? 0 : 1;
            if (glow != null) renderers.GetArrayElementAtIndex(0).objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------------------------ Sites

        /// <summary>
        /// Stands a prefab instance where the model was built for, and says where the boat lies. False, with what is
        /// wrong, when the parts file cannot be read or the layout's <c>gang-house</c> is not where the model stands
        /// (the tool then builds the graybox house).
        /// </summary>
        internal static bool TrySeat(Transform root, Vector2 layoutAt, float layoutYaw, out Vector3 boatAt, out Vector2 boatAlong, out string problem)
        {
            boatAt = Vector3.zero;
            boatAlong = Vector2.up;
            if (!TryReadHouse(ReadParts(), out HouseFacts house, out problem))
            {
                problem = $"{PARTS_PATH}: {problem} (rerun build_gang_house.py)";
                return false;
            }
            if (Vector2.Distance(layoutAt, house.Centre) > POSE_TOLERANCE || Mathf.Abs(Mathf.DeltaAngle(layoutYaw, house.Yaw)) > 0.5f)
            {
                problem = $"the layout's gang-house stands at {layoutAt} turned {layoutYaw}, the model at {house.Centre} turned {house.Yaw}: " +
                          "change CENTRE / YAW in build_gang_house.py and rebuild, or move the layout back";
                return false;
            }
            root.SetPositionAndRotation(house.Origin, house.Rotation);
            float[] boat = house.Anchor("boat");
            boatAt = house.World(new Vector3(boat[0], boat[1], boat[2]));
            Vector3 along = Quaternion.Euler(0f, house.Yaw + boat[3], 0f) * Vector3.back;   // the pier runs along local -z
            boatAlong = new Vector2(along.x, along.z);
            return true;
        }

        /// <summary>
        /// Casks, crates and sacks on the deck and in the room, under <c>Goods</c>. The model's script owns where each
        /// stands (<c>goods</c> in the parts file, the prefab's frame): this only maps a kit piece to it. Dressing
        /// stays out of the bake.
        /// </summary>
        internal static void BuildGoods(Transform root)
        {
            JObject parts = ReadParts();
            if (!TryReadHouse(parts, out _, out _)) return;
            var goods = new GameObject("Goods").transform;
            goods.SetParent(root, false);
            foreach (JToken token in parts["goods"])
            {
                string kit = (string)token["kit"];
                Transform prop = WorkerHutKitBuilder.Piece(kit, goods, Vector3.zero);
                prop.SetPositionAndRotation(root.TransformPoint(Vector(token["position"])), root.rotation * Quaternion.Euler(Vector(token["euler"])));
                prop.localScale = Vector3.one * (float)token["scale"];
                if (prop.GetComponentInChildren<Renderer>(true) == null)
                {
                    GameLog.Error(TAG, $"Goods: kit piece '{kit}' not found; an empty '{prop.name}' stands in for it.");
                    continue;
                }
                if ((bool)token["solid"])
                    foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true)) collider.enabled = true;
                prop.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
        }

        // ----------------------------------------------------------------------------------- scene

        [MenuItem("Tools/World/Place Area 1 Gang House")]
        private static void PlaceMenu() => Place();

        /// <summary>
        /// Readies the gang house's site: lowers the bank under the deck (never fills), removes the loose barricades
        /// of the old wilderness opening and moves the toll gate's zone into the new gateway. Rerunning changes
        /// nothing more. It never saves and never bakes: capture the terrain sculpt, run Camp Edge and Sites, rebake.
        /// </summary>
        internal static bool Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return false;
            }
            var missing = new List<string>();
            if (!TryReadHouse(ReadParts(), out HouseFacts house, out string problem)) missing.Add($"{PARTS_PATH}: {problem}");
            GameObject terrainObject = GameObject.Find(TERRAIN);
            Terrain terrain = terrainObject == null ? null : terrainObject.GetComponent<Terrain>();
            if (terrain == null) missing.Add(TERRAIN);
            GameObject toll = GameObject.Find(TOLL_GATE);
            BoxCollider zone = toll == null ? null : toll.GetComponentInChildren<BoxCollider>(true);
            if (zone == null) missing.Add(TOLL_GATE + " (with its zone box)");
            if (!TryReadGateway(out Vector3 gateway, out string gateProblem)) missing.Add(gateProblem);
            // The tunnel starts in the warehouse cellar's side room: both models were built for the warehouse where it stands.
            GameObject warehouse = GameObject.Find(WAREHOUSE);
            if (warehouse == null) missing.Add(WAREHOUSE);
            else if (terrain != null && house.TunnelNodes != null && !TunnelMeetsWarehouse(house, warehouse.transform, out string tunnelProblem)) missing.Add(tunnelProblem);
            if (missing.Count > 0)
            {
                GameLog.Error(TAG, $"Open StartingTown first. Missing: {string.Join("; ", missing)}; nothing changed.");
                return false;
            }

            int lowered = LowerGround(terrain, house, "groundCut", BLEND) + LowerGround(terrain, house, "groundCutInside", 0f);
            int opened = CutHole(terrain.terrainData, terrain.transform.position, house);
            int removed = 0;
            foreach (GameObject top in terrain.gameObject.scene.GetRootGameObjects())
            {
                Vector3 at = top.transform.position;
                if (!top.name.StartsWith(BARRICADE_PREFIX) || !BarricadeArea.Contains(new Vector2(at.x, at.z))) continue;
                Object.DestroyImmediate(top);
                removed++;
            }
            // The zone box keeps its size and height; its middle goes to the gateway's.
            Vector3 centre = zone.transform.TransformPoint(zone.center);
            Vector3 shift = new Vector3(gateway.x - centre.x, 0f, gateway.z - centre.z);
            bool moved = shift.sqrMagnitude > 1e-6f;
            if (moved)
            {
                toll.transform.position += shift;
                EditorUtility.SetDirty(toll.transform);
            }
            if (lowered > 0 || removed > 0 || moved || opened > 0) EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            GameLog.Info(TAG, $"Gang house site: {lowered} height samples lowered under the deck and the basement's lake side, {opened} terrain cell(s) opened over the basement, {removed} barricade(s) removed, toll gate {(moved ? "moved into" : "already in")} the gateway. " +
                              (lowered > 0 ? "Capture the terrain sculpt, then run Camp Edge and Sites, rebake and save." : "Run Camp Edge and Sites, rebake and save."));
            return true;
        }

        /// <summary>
        /// The terrain cells that lie wholly inside the basement's hole rectangle (the prefab's frame, which stands
        /// turned: so cell by cell, by its four corners). Pure: <paramref name="resolution"/> cells across
        /// <paramref name="size"/> metres from <paramref name="terrainMin"/> (world x, z).
        /// </summary>
        public static List<Vector2Int> HoleCells(HouseFacts house, Vector2 terrainMin, float size, int resolution)
        {
            var cells = new List<Vector2Int>();
            if (size <= 0f || resolution <= 0) return cells;
            float cell = size / resolution;
            Quaternion toLocal = Quaternion.Inverse(house.Rotation);
            var bounds = new Bounds(house.World(new Vector3(house.Hole.x, 0f, house.Hole.y)), Vector3.zero);
            bounds.Encapsulate(house.World(new Vector3(house.Hole.z, 0f, house.Hole.y)));
            bounds.Encapsulate(house.World(new Vector3(house.Hole.x, 0f, house.Hole.w)));
            bounds.Encapsulate(house.World(new Vector3(house.Hole.z, 0f, house.Hole.w)));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.min.x - terrainMin.x) / cell)), x1 = Mathf.Min(resolution - 1, Mathf.CeilToInt((bounds.max.x - terrainMin.x) / cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((bounds.min.z - terrainMin.y) / cell)), z1 = Mathf.Min(resolution - 1, Mathf.CeilToInt((bounds.max.z - terrainMin.y) / cell));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    bool inside = true;
                    for (int corner = 0; corner < 4 && inside; corner++)
                    {
                        var world = new Vector3(terrainMin.x + (x + (corner & 1)) * cell, house.FloorWorldY, terrainMin.y + (z + (corner >> 1)) * cell);
                        Vector3 local = toLocal * (world - house.Origin);
                        inside = local.x >= house.Hole.x && local.x <= house.Hole.z && local.z >= house.Hole.y && local.z <= house.Hole.w;
                    }
                    if (inside) cells.Add(new Vector2Int(x, z));
                }
            }
            return cells;
        }

        /// <summary>Opens the basement's hole in the live terrain. Returns how many cells were still closed.</summary>
        private static int CutHole(TerrainData data, Vector3 corner, HouseFacts house)
        {
            int opened = 0;
            foreach (Vector2Int cell in HoleCells(house, new Vector2(corner.x, corner.z), data.size.x, data.holesResolution))
            {
                if (!data.GetHoles(cell.x, cell.y, 1, 1)[0, 0]) continue;
                data.SetHoles(cell.x, cell.y, new bool[1, 1]); // false: open
                opened++;
            }
            if (opened > 0) EditorUtility.SetDirty(data);
            return opened;
        }

        /// <summary>
        /// For the terrain tool: opens the basement's hole in a hole map being rebuilt (true = solid ground). False,
        /// with an error, when the parts file cannot be read: the ground is then left solid there and the run goes on.
        /// </summary>
        public static bool CutBasementHole(bool[,] holes, Vector2 terrainMin, float size)
        {
            if (holes == null) return false;
            if (!TryReadHouse(ReadParts(), out HouseFacts house, out string problem))
            {
                GameLog.Error(TAG, $"{PARTS_PATH}: {problem} (run build_gang_house.py); the gang house basement's terrain hole was not cut.");
                return false;
            }
            foreach (Vector2Int cell in HoleCells(house, terrainMin, size, holes.GetLength(0))) holes[cell.y, cell.x] = false;
            return true;
        }

        /// <summary>
        /// True when the tunnel's first point stands in the middle of the opening the warehouse's model leaves in its
        /// side room's south wall, with the warehouse where it stands in the scene.
        /// </summary>
        private static bool TunnelMeetsWarehouse(HouseFacts house, Transform warehouse, out string problem)
        {
            problem = $"{WAREHOUSE_PARTS} with 'cellar.tunnel' [min x, max x, height, outer z, inner z] (run build_warehouse.py)";
            if (!File.Exists(WAREHOUSE_PARTS)) return false;
            JArray opening;
            try
            {
                opening = JObject.Parse(File.ReadAllText(WAREHOUSE_PARTS))["cellar"]?["tunnel"] as JArray;
            }
            catch (System.Exception e) when (e is Newtonsoft.Json.JsonException || e is IOException)
            {
                return false;
            }
            if (opening == null || opening.Count != 5 || !AllNumbers(opening)) return false;
            Vector3 start = warehouse.InverseTransformPoint(house.World(house.TunnelNodes[0]));
            float middle = ((float)opening[0] + (float)opening[1]) * 0.5f, face = (float)opening[3];
            float off = Mathf.Max(Mathf.Abs(start.x - middle), Mathf.Abs(start.z - face));
            if (off <= POSE_TOLERANCE) return true;
            problem = $"the tunnel starts {off:F2} m from the opening in the warehouse cellar's side room ({WAREHOUSE} stands at {warehouse.position:F2}): rebuild both models for where the warehouse stands";
            return false;
        }

        /// <summary>The wilderness gateway's middle, from its own parts file (world x, 0, z).</summary>
        private static bool TryReadGateway(out Vector3 at, out string problem)
        {
            at = Vector3.zero;
            string path = "Assets/_Game/Art/Environment/Camp/models/wilderness gate/WildernessGate.json";
            problem = path + " (run build_wilderness_gate.py) with 'centre' [x, z]";
            if (!File.Exists(path)) return false;
            try
            {
                if (!(JObject.Parse(File.ReadAllText(path))["centre"] is JArray centre) || centre.Count != 2 || !IsNumber(centre[0]) || !IsNumber(centre[1])) return false;
                at = new Vector3((float)centre[0], 0f, (float)centre[1]);
                return true;
            }
            catch (System.Exception e) when (e is Newtonsoft.Json.JsonException || e is IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Lowers the ground inside one of the parts file's cut rectangles (the house's frame) to its height, under
        /// a bank that rises <see cref="BANK"/> over <paramref name="blend"/> metres (none when it is 0). A ceiling, not a blend: a second run
        /// finds every sample under it already. Returns the number of samples changed.
        /// </summary>
        private static int LowerGround(Terrain terrain, HouseFacts house, string anchor, float blend)
        {
            float[] cut = house.Anchor(anchor);   // min x, max x, min z, max z, to y: the prefab's frame
            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            Vector3 position = terrain.transform.position, size = data.size;
            var bounds = new Bounds(house.World(new Vector3(cut[0], 0f, cut[2])), Vector3.zero);
            foreach (float x in new[] { cut[0] - blend, cut[1] + blend })
                foreach (float z in new[] { cut[2] - blend, cut[3] + blend })
                    bounds.Encapsulate(house.World(new Vector3(x, 0f, z)));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - position.x) / size.x * (resolution - 1)), 0, resolution - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - position.x) / size.x * (resolution - 1)), 0, resolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - position.z) / size.z * (resolution - 1)), 0, resolution - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - position.z) / size.z * (resolution - 1)), 0, resolution - 1);
            float[,] heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float target = (house.FloorWorldY + cut[4] - position.y) / size.y;
            Quaternion toLocal = Quaternion.Inverse(house.Rotation);
            int changed = 0;
            for (int j = 0; j <= z1 - z0; j++)
            {
                for (int i = 0; i <= x1 - x0; i++)
                {
                    var world = new Vector3(position.x + (x0 + i) / (float)(resolution - 1) * size.x, house.FloorWorldY,
                        position.z + (z0 + j) / (float)(resolution - 1) * size.z);
                    Vector3 local = toLocal * (world - house.Origin);
                    float dx = Mathf.Max(cut[0] - local.x, 0f, local.x - cut[1]), dz = Mathf.Max(cut[2] - local.z, 0f, local.z - cut[3]);
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    float weight = blend > 0f ? Mathf.Clamp01(1f - distance / blend) : distance <= 0f ? 1f : 0f;   // no blend: inside only
                    if (weight <= 0f) continue;
                    float ceiling = target + (1f - Mathf.SmoothStep(0f, 1f, weight)) * BANK / size.y;
                    if (heights[j, i] <= ceiling + HEIGHT_STEP) continue;   // stored heights are 16-bit: within a step is level
                    heights[j, i] = ceiling;
                    changed++;
                }
            }
            if (changed == 0) return 0;
            data.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(data);
            return changed;
        }

        private static Vector3 Vector(JToken array) => new Vector3((float)array[0], (float)array[1], (float)array[2]);

        internal static JObject ReadParts()
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
