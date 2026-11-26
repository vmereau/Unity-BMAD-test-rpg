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
    /// The Area 1 canteen: the cookhouse prefab built round the Blender model
    /// (<c>Art/Environment/Camp/models/cookhouse</c>) and the command that places it in StartingTown with its
    /// dressing, the store's restricted area and the gameplay objects that live there. The model script writes a
    /// parts file (colliders, walk surfaces, lights, table poses, anchors) that both commands read, so no size is
    /// written twice. Local frame: x east, y up, z north, origin on the floor's top at the building's centre.
    /// </summary>
    internal static class CookhouseBuilder
    {
        private const string TAG = "[CookhouseBuilder]";
        private const string PREFAB_PATH = "Assets/_Game/Prefabs/World/Cookhouse/Cookhouse.prefab";
        private const string MODEL_FOLDER = "Assets/_Game/Art/Environment/Camp/models/cookhouse/";
        private const string MODEL_PATH = MODEL_FOLDER + "Cookhouse.fbx";
        private const string PARTS_PATH = MODEL_FOLDER + "Cookhouse.json";
        private const string REMAP_SOURCE = "Assets/_Game/Art/Environment/Camp/models/warehouse/Warehouse.fbx";
        private const string LANTERN_PATH = "Assets/Hivemind/HDRP(Default)/Medieval_Docks/Art/Prefabs/SM_Lantern_Hanging.prefab";
        private const string DAY_NIGHT = "Assets/_Game/Data/Config/DayNightConfig.asset";
        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";
        private const string COOK = "Assets/_Game/Data/NPCs/Cook/NPC_Cook.asset";
        private const string KITCHEN_HAND = "Assets/_Game/Data/NPCs/KitchenHand/NPC_KitchenHand.asset";
        private const string OWNER_TYPE = "NPCEntity";
        private const string MATERIAL_PREFIX = "M_Camp_";
        private const string MODEL = "Model";
        private const string TABLE_SET = "TableSet";
        private const string TABLES = "Tables";
        private const string COLLIDERS = "Colliders";
        private static readonly string[] NotWalkableParts = { "Roof", "Shelters", "Furniture", "Hearth", "Yard", "Doors", "Rubble" };
        private static readonly string[] PartLists = { "objects", "walk", "colliders", "lanterns", "lights", "tables" };
        private const int NOT_WALKABLE = 1;

        // Scene objects of the canteen (StartingTown).
        private const string CANTEEN = "/Area1_Blockout/TentCamp/Canteen";
        private const string INSTANCE = "Cookhouse";
        private const string DRESSING = "Dressing";
        private static readonly string[] Graybox = { "worker-canteen", "worker-dining" };
        private const string OLD_DRESSING = "/Camp/Canteen";
        private const string STATION = "/CookingStation_Fireplace";
        private const string BED = CANTEEN + "/KitchenHandBed";
        private const string CART = "/Decor/SM_cartB_a1";
        private const string COOK_NPC = "/Cook";
        private const string KITCHEN_HAND_NPC = "/KitchenHand";
        private const string COOK_NIGHT = "/NpcRoutineSpots/Spot_Cook_Night";
        private const string AREAS = "/RestrictedAreas";
        // Every anchor the place command reads, with the number of values it needs: checked before anything changes.
        private static readonly (string name, int count)[] Anchors =
        {
            ("station", 3), ("bed", 3), ("behindCounter", 3), ("frontOfHatch", 3), ("byTheHearth", 3), ("alley", 4),
            ("storeBox", 6), ("storeFloor", 4), ("storeShelves", 7), ("hooks", 3), ("workTable", 3), ("kitchenShelf", 5),
            ("counterTop", 3), ("hearthPot", 3), ("porch", 4), ("storeDoor", 3), ("fishRack", 4), ("choppingBlock", 3),
        };
        private const string STORE_AREA = "RA_CookhouseStore";
        private const string TERRAIN = "/Terrain_StartingArea";

        // The ground the buildings stand on, levelled to GROUND_Y (world rects: min x, max x, min z, max z).
        private const float GROUND_Y = 0f;
        private const float BLEND = 1.2f;
        private const float BANK = 0.6f;
        private const float HEIGHT_STEP = 1f / 32766f;         // how high the ground may stand at the blend's outer edge
        private static readonly Vector4[] Level =
        {
            new Vector4(-21f, -12f, 12.4f, 25.2f),   // cookhouse, porch, back alley
            new Vector4(-12.5f, -5f, 9.9f, 15.2f),   // mess shelter
        };

        // Night lights, tuned against the warehouse's door lanterns.
        private static readonly Color LanternColor = new Color(1f, 0.7f, 0.42f);
        private static readonly Color LanternGlow = new Color(2.4f, 1.2f, 0.45f, 1f);
        private static readonly Color FireColor = new Color(1f, 0.6f, 0.32f);
        private const float LANTERN_INTENSITY = 5f, LANTERN_RANGE = 7f;
        private const float HEARTH_INTENSITY = 6f, HEARTH_RANGE = 7f;

        // ---------------------------------------------------------------------------------- prefab

        [MenuItem("Tools/World/Build Cookhouse Prefab")]
        private static void BuildMenu() => Build();

        /// <summary>
        /// Rebuilds the prefab from the model and its parts file. A missing model, parts list or camp material stops
        /// before saving, so the prefab (and the scene instance that follows it) is left as it was.
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
            var absent = new List<string>();
            if (model == null) absent.Add(MODEL_PATH + " (run build_cookhouse.py)");
            if (parts == null) absent.Add(PARTS_PATH + " (run build_cookhouse.py)");
            if (lantern == null) absent.Add(LANTERN_PATH);
            if (dayNight == null) absent.Add(DAY_NIGHT);
            if (glow == null) absent.Add(GLOW);
            if (absent.Count > 0)
            {
                GameLog.Error(TAG, $"Missing: {string.Join(", ", absent)}; {PREFAB_PATH} left as it was.");
                return null;
            }
            foreach (string list in PartLists)
            {
                if (parts[list] is JArray) continue;
                GameLog.Error(TAG, $"{PARTS_PATH} has no list '{list}' (rerun build_cookhouse.py); {PREFAB_PATH} left as it was.");
                return null;
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(PREFAB_PATH));
            try
            {
                if (!BuildModel(root.transform, model, parts)) return null;
                BuildBoxes(root.transform, parts);
                foreach (JToken point in parts["lanterns"]) BuildLantern(root.transform, (JObject)point, lantern, dayNight, glow);
                foreach (JToken point in parts["lights"]) BuildFireLight(root.transform, (JObject)point, dayNight);

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

        /// <summary>
        /// The model's objects under <c>Model</c>; the table set is taken out and copied to each table pose.
        /// Roofs, furniture, hearth, doors, fence, rubble and tables are Not Walkable, so no NavMesh island forms on
        /// them (the bake collects render meshes). Frame, walls and iron are left alone: nothing of theirs is a
        /// surface with headroom, and a modifier on the frame would cut the floor at every post.
        /// </summary>
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
            Transform set = instance.transform.Find(TABLE_SET);
            if (set == null)
            {
                GameLog.Error(TAG, $"'{MODEL_PATH}' has no object '{TABLE_SET}'; {PREFAB_PATH} left as it was.");
                complete = false;
            }
            foreach (string name in NotWalkableParts)
            {
                if (instance.transform.Find(name) != null) continue;
                GameLog.Error(TAG, $"'{MODEL_PATH}' has no object '{name}'; {PREFAB_PATH} left as it was.");
                complete = false;
            }
            if (!complete) return false;
            foreach (string name in NotWalkableParts) NotWalkable(instance.transform.Find(name).gameObject);

            var tables = new GameObject(TABLES).transform;
            tables.SetParent(root, false);
            int index = 0;
            foreach (JToken token in parts["tables"])
            {
                Transform table = Object.Instantiate(set.gameObject, tables).transform;
                table.name = $"Table_{index++}";
                table.localPosition = Vector(token["position"]);
                table.localRotation = Quaternion.Euler(0f, (float)token["yaw"], 0f);
                NotWalkable(table.gameObject);
                AddBox(table, new Vector3(0f, 0.38f, 0f), new Vector3(2.2f, 0.76f, 0.76f));
                AddBox(table, new Vector3(0f, 0.22f, 0.68f), new Vector3(2.16f, 0.44f, 0.26f));
                AddBox(table, new Vector3(0f, 0.22f, -0.68f), new Vector3(2.16f, 0.44f, 0.26f));
            }
            Object.DestroyImmediate(set.gameObject);
            return true;
        }

        private static void NotWalkable(GameObject target)
        {
            var modifier = target.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NOT_WALKABLE;
        }

        private static void AddBox(Transform holder, Vector3 centre, Vector3 size)
        {
            var box = holder.gameObject.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        /// <summary>Walk surfaces (scaled boxes directly under the root) and solid colliders by group, from the parts file.</summary>
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

            // Out from the wall, so the boards behind the lantern are lit rather than cut by the light's own shell.
            Light light = NightLight(lantern.transform, Vector(point["out"]) * 0.25f + new Vector3(0f, -0.05f, 0f),
                LanternColor, LANTERN_INTENSITY, LANTERN_RANGE, LightShadows.None);
            Wire(lantern.AddComponent<ClockNightLight>(), dayNight, light, quad.transform, glow);
        }

        /// <summary>The hearth's fire: a clock-driven light with shadows, so it shows through the hatch and no further.</summary>
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

        // ----------------------------------------------------------------------------------- scene

        [MenuItem("Tools/World/Place Area 1 Canteen")]
        private static void PlaceMenu() => Place();

        /// <summary>
        /// Levels the ground, removes the graybox canteen and the old kit tables, places the prefab and its dressing,
        /// moves the cooking station, Tob's bed, the cart, both cooks and the Cook's night spot, and builds the
        /// store's restricted area. Rerunning gives the same scene. Nothing changes when something is missing.
        /// Rebake the NavMesh afterwards.
        /// </summary>
        internal static bool Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                GameLog.Error(TAG, "Leave Play mode and close Prefab Mode first; nothing changed.");
                return false;
            }
            JObject parts = ReadParts();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            GameObject canteen = GameObject.Find(CANTEEN);
            GameObject areas = GameObject.Find(AREAS);
            GameObject terrainObject = GameObject.Find(TERRAIN);
            Terrain terrain = terrainObject == null ? null : terrainObject.GetComponent<Terrain>();
            var owners = new[] { AssetDatabase.LoadAssetAtPath<ScriptableObject>(COOK), AssetDatabase.LoadAssetAtPath<ScriptableObject>(KITCHEN_HAND) };
            var missing = new List<string>();
            if (prefab == null) missing.Add(PREFAB_PATH + " (run Tools/World/Build Cookhouse Prefab)");
            if (parts == null) missing.Add(PARTS_PATH);
            else MissingParts(parts, missing);
            if (canteen == null) missing.Add(CANTEEN);
            if (areas == null) missing.Add(AREAS);
            if (terrain == null) missing.Add(TERRAIN);
            // Loaded untyped (the builder needs no NPC code), so the type is checked by name: any other asset would be
            // stored as an empty owner and the area would belong to nobody.
            if (owners[0] == null || owners[0].GetType().Name != OWNER_TYPE) missing.Add(COOK + $" (an {OWNER_TYPE})");
            if (owners[1] == null || owners[1].GetType().Name != OWNER_TYPE) missing.Add(KITCHEN_HAND + $" (an {OWNER_TYPE})");
            foreach (string path in new[] { STATION, BED, COOK_NPC, KITCHEN_HAND_NPC, COOK_NIGHT })
                if (GameObject.Find(path) == null) missing.Add(path);
            if (missing.Count > 0)
            {
                GameLog.Error(TAG, $"Open StartingTown first. Missing: {string.Join(", ", missing)}; nothing changed.");
                return false;
            }

            LevelGround(terrain);
            foreach (string name in Graybox)
            {
                Transform old = canteen.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            GameObject oldDressing = GameObject.Find(OLD_DRESSING);
            if (oldDressing != null)
                for (int i = oldDressing.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(oldDressing.transform.GetChild(i).gameObject);

            var centre = (JArray)parts["centre"];
            float floor = GROUND_Y + (float)parts["floorAboveGround"];
            var origin = new Vector3((float)centre[0], floor, (float)centre[1]);
            Transform house = Replace(canteen.transform, INSTANCE, prefab);
            house.SetPositionAndRotation(origin, Quaternion.identity);   // anchors are read in world axes
            var anchors = (JObject)parts["anchors"];

            Transform dressing = Replace(canteen.transform, DRESSING, null);
            dressing.SetPositionAndRotation(origin, Quaternion.identity);
            WorkerHutKitBuilder.ResetKit();
            Dress(dressing, anchors, terrain, origin);

            float[] station = Floats(anchors["station"]);
            Move(STATION, origin + new Vector3(station[0], station[1], station[2]), 0f);
            float[] bed = Floats(anchors["bed"]);
            Move(BED, origin + new Vector3(bed[0], bed[1], bed[2]), 0f);
            float[] counter = Floats(anchors["behindCounter"]);
            Move(KITCHEN_HAND_NPC, origin + new Vector3(counter[0], counter[1], counter[2]), 90f);
            float[] front = Floats(anchors["frontOfHatch"]);
            Move(COOK_NPC, origin + new Vector3(front[0], front[1], front[2]), 135f);
            float[] hearth = Floats(anchors["byTheHearth"]);
            Move(COOK_NIGHT, origin + new Vector3(hearth[0], hearth[1], hearth[2]), 270f);
            float[] alley = Floats(anchors["alley"]);
            if (GameObject.Find(CART) == null) GameLog.Warn(TAG, $"No active {CART}: the cart was not moved into the alley.");
            else
                Move(CART, origin + new Vector3((alley[0] + alley[1]) * 0.5f - 0.35f, -(float)parts["floorAboveGround"], -1.7f), 90f);

            BuildArea(areas.transform, Floats(anchors["storeBox"]), origin, owners);
            EditorSceneManager.MarkSceneDirty(canteen.scene);
            if (WorkerHutKitBuilder.MissingCount > 0) GameLog.Warn(TAG, $"{WorkerHutKitBuilder.MissingCount} kit piece(s) missing from the dressing.");
            GameLog.Info(TAG, $"Canteen placed at {origin}. Capture the terrain sculpt, rebake the NavMesh, then save the scene.");
            return true;
        }

        /// <summary>What the place command needs from the parts file and does not find there (rerun build_cookhouse.py).</summary>
        private static void MissingParts(JObject parts, List<string> missing)
        {
            if (!(parts["centre"] is JArray centre) || centre.Count != 2) missing.Add($"'centre' in {PARTS_PATH}");
            JToken floor = parts["floorAboveGround"];
            if (floor == null || (floor.Type != JTokenType.Float && floor.Type != JTokenType.Integer)) missing.Add($"'floorAboveGround' in {PARTS_PATH}");
            var anchors = parts["anchors"] as JObject;
            if (anchors == null)
            {
                missing.Add($"'anchors' in {PARTS_PATH}");
                return;
            }
            foreach ((string name, int count) in Anchors)
                if (!(anchors[name] is JArray values) || values.Count < count) missing.Add($"anchor '{name}' ({count} values) in {PARTS_PATH}");
        }

        /// <summary>
        /// Cuts the plot down to <see cref="GROUND_Y"/> (never fills), under a bank that rises <see cref="BANK"/> over <see cref="BLEND"/> metres. A sculpt
        /// pass on the live terrain: Tools/World/Capture Area 1 Terrain Sculpt keeps it.
        /// </summary>
        private static void LevelGround(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            Vector3 position = terrain.transform.position, size = data.size;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector4 rect in Level)
            {
                minX = Mathf.Min(minX, rect.x - BLEND); maxX = Mathf.Max(maxX, rect.y + BLEND);
                minZ = Mathf.Min(minZ, rect.z - BLEND); maxZ = Mathf.Max(maxZ, rect.w + BLEND);
            }
            int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - position.x) / size.x * (resolution - 1)), 0, resolution - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - position.x) / size.x * (resolution - 1)), 0, resolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - position.z) / size.z * (resolution - 1)), 0, resolution - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - position.z) / size.z * (resolution - 1)), 0, resolution - 1);
            float[,] heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float target = (GROUND_Y - position.y) / size.y;
            int changed = 0;
            for (int j = 0; j <= z1 - z0; j++)
            {
                for (int i = 0; i <= x1 - x0; i++)
                {
                    float x = position.x + (x0 + i) / (float)(resolution - 1) * size.x;
                    float z = position.z + (z0 + j) / (float)(resolution - 1) * size.z;
                    float weight = 0f;
                    foreach (Vector4 rect in Level)
                    {
                        float dx = Mathf.Max(rect.x - x, 0f, x - rect.y), dz = Mathf.Max(rect.z - z, 0f, z - rect.w);
                        weight = Mathf.Max(weight, Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz) / BLEND));
                    }
                    if (weight <= 0f) continue;
                    // A ceiling, not a blend toward the target: a second run finds every sample under it already.
                    float ceiling = target + (1f - Mathf.SmoothStep(0f, 1f, weight)) * BANK / size.y;
                    if (heights[j, i] <= ceiling + HEIGHT_STEP) continue;   // stored heights are 16-bit: within a step is level
                    heights[j, i] = ceiling;
                    changed++;
                }
            }
            if (changed == 0) return;
            data.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(data);
            GameLog.Info(TAG, $"Levelled the canteen's ground ({changed} height samples lowered).");
        }

        /// <summary>A fresh child: the prefab's instance, or an empty group when there is no prefab.</summary>
        private static Transform Replace(Transform parent, string name, GameObject prefab)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject created = prefab == null ? new GameObject(name) : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            created.name = name;
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static void Move(string path, Vector3 position, float yaw)
        {
            Transform target = GameObject.Find(path).transform;
            target.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            EditorUtility.SetDirty(target);
        }

        /// <summary>The store room as a restricted area, owned by both cooks at all hours.</summary>
        private static void BuildArea(Transform areas, float[] box, Vector3 origin, ScriptableObject[] owners)
        {
            Transform old = areas.Find(STORE_AREA);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var area = new GameObject(STORE_AREA) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            area.transform.SetParent(areas, false);
            area.transform.position = origin + new Vector3(box[0], box[1], box[2]);
            var collider = area.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(box[3], box[4], box[5]);
            var so = new SerializedObject(area.AddComponent<RestrictedArea>());
            SerializedProperty list = so.FindProperty("_ownerEntities");
            list.arraySize = owners.Length;
            for (int i = 0; i < owners.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = owners[i];
            so.FindProperty("_useHours").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // -------------------------------------------------------------------------------- dressing

        /// <summary>Kit props, in the prefab's frame: the store's goods, the kitchen's tools, the alley and the wood corner.</summary>
        private static void Dress(Transform root, JObject anchors, Terrain terrain, Vector3 origin)
        {
            float ground = GROUND_Y - origin.y;
            float[] storeFloor = Floats(anchors["storeFloor"]);     // min x, max x, min z, max z
            float[] shelves = Floats(anchors["storeShelves"]);      // min x, max x, z, then the levels' tops
            float[] hooks = Floats(anchors["hooks"]);
            float[] table = Floats(anchors["workTable"]);
            float[] kitchenShelf = Floats(anchors["kitchenShelf"]); // x, low y, z, high y, length
            float[] counter = Floats(anchors["counterTop"]);
            float[] pot = Floats(anchors["hearthPot"]);
            float[] porch = Floats(anchors["porch"]);               // min x, max x, min z, max z
            float[] alley = Floats(anchors["alley"]);               // min x, max x, min z, max z
            float[] door = Floats(anchors["storeDoor"]);            // x, z from, z to
            float[] rack = Floats(anchors["fishRack"]);             // min x, max x, y, z

            Transform store = Group(root, "Store");
            for (int i = 0; i < 3; i++)
                Prop(store, "SM_barrelClosedA_a1", new Vector3(storeFloor[0] + 0.5f + i * 0.85f, 0f, storeFloor[2] + 0.48f), i * 70f);
            Prop(store, "SM_burlapSackA_a1", new Vector3(-1.1f, 0f, shelves[2] - 0.75f), 15f, 0.8f);
            Prop(store, "SM_burlapSackA_a1", new Vector3(-0.25f, 0f, shelves[2] - 0.8f), 100f, 0.8f);
            Prop(store, "SM_Sack_01", new Vector3(-0.7f, 0.38f, shelves[2] - 0.78f), 40f);
            Prop(store, "SM_Crate_B_01", new Vector3(0.9f, 0f, shelves[2] - 0.75f), 8f);
            string[][] onShelves =
            {
                new[] { "SM_Sack_02", "SM_Sack_02", "SM_Sack_01", "SM_Sack_02", "SM_MetalKitPot_02a" },
                new[] { "SM_jugA_a1", "SM_FoodKitBread_01a", "SM_FoodKitBread_01a", "SM_jugD_a1", "SM_FoodKitBread_01a", "SM_Sack_01" },
                new[] { "SM_jugC_a1", "SM_jugB_a1", "SM_Bowl_01", "SM_Bowl_01", "SM_jugE_a1", "SM_FoodKitBread_01a", "SM_jugA_a1" },
                new[] { "SM_jugC_a1", "SM_jugD_a1", "SM_jugA_a1", "SM_Sack_02", "SM_jugE_a1" },
            };
            for (int level = 0; level < onShelves.Length; level++)
            {
                string[] row = onShelves[level];
                for (int i = 0; i < row.Length; i++)
                {
                    float x = Mathf.Lerp(shelves[0] + 0.3f, shelves[1] - 0.3f, row.Length == 1 ? 0.5f : i / (float)(row.Length - 1));
                    Prop(store, row[i], new Vector3(x, shelves[3 + level], shelves[2] + ((i + level) % 2 == 0 ? 0.03f : -0.04f)), i * 47f + level * 20f);
                }
            }
            for (int i = 0; i + 2 < hooks.Length; i += 3)
            {
                // The kit's joint lies flat along x: stood on end and scaled down, it hangs from the hook.
                Transform joint = Prop(store, "SM_Meat_01b", new Vector3(hooks[i], hooks[i + 1] - 0.36f, hooks[i + 2]), 0f, 0.55f);
                joint.localRotation = Quaternion.Euler(0f, 80f + i * 7f, 90f);
            }

            Transform kitchen = Group(root, "Kitchen");
            Prop(kitchen, "SM_FoodKitBread_01a", new Vector3(table[0] - 0.4f, table[1], table[2] + 0.1f), 25f);
            Prop(kitchen, "SM_Bowl_01", new Vector3(table[0] + 0.1f, table[1], table[2] - 0.12f), 0f);
            Prop(kitchen, "SM_MetalKitPot_02a", new Vector3(table[0] + 0.55f, table[1], table[2] + 0.05f), 60f);
            string[] low = { "SM_Bowl_01", "SM_Bowl_01", "SM_jugB_a1", "SM_MetalKitPot_02b" }, high = { "SM_jugA_a1", "SM_jugC_a1", "SM_jugE_a1" };
            for (int i = 0; i < low.Length; i++)
                Prop(kitchen, low[i], new Vector3(kitchenShelf[0], kitchenShelf[1], kitchenShelf[2] + Mathf.Lerp(-0.6f, 0.6f, i / (float)(low.Length - 1))), i * 33f);
            for (int i = 0; i < high.Length; i++)
                Prop(kitchen, high[i], new Vector3(kitchenShelf[0], kitchenShelf[3], kitchenShelf[2] + Mathf.Lerp(-0.55f, 0.55f, i / (float)(high.Length - 1))), i * 51f);
            Prop(kitchen, "SM_MetalKitPot_02a", new Vector3(pot[0], pot[1], pot[2]), 0f, 1.2f);
            Prop(kitchen, "SM_barrelOpenedA_a1", new Vector3(storeFloor[0] + 0.45f, 0f, -0.5f), 20f);
            Prop(kitchen, "SM_bucketA_a1", new Vector3(0.45f, 0f, 0.2f), 0f, 0.55f);
            Prop(kitchen, "SM_Bowl_01", new Vector3(counter[0] - 0.05f, counter[1], counter[2] - 0.6f), 0f);
            Prop(kitchen, "SM_Bowl_01", new Vector3(counter[0] + 0.1f, counter[1], counter[2] - 0.25f), 0f);
            Prop(kitchen, "SM_MetalKitPot_02b", new Vector3(counter[0] - 0.1f, counter[1], counter[2] + 0.55f), 0f);

            Transform yard = Group(root, "Yard");
            Prop(yard, "SM_firewoodA_a1", new Vector3(porch[0] + 0.55f, ground, porch[2] + 1.0f), 0f);
            Prop(yard, "SM_Stool_Short", new Vector3(porch[0] + 1.3f, ground, porch[2] + 2.1f), 30f);
            for (int i = 0; i < 2; i++)
                Prop(yard, "SM_barrelOpenedA_a1", new Vector3(porch[0] - 0.55f, ground, porch[2] + 0.6f + i * 0.9f), i * 40f);   // water butts
            Prop(yard, "SM_bucketB_a1", new Vector3(porch[0] - 1.6f, ground, porch[2] + 1.4f), 0f, 0.85f);                       // wash tub
            float wallX = door[0] - 0.5f;
            Prop(yard, "SM_barrelClosedA_a1", new Vector3(wallX, ground, door[1] - 0.9f), 10f);
            Prop(yard, "SM_barrelClosedA_a1", new Vector3(wallX, ground, door[1] - 1.75f), 80f);
            Prop(yard, "SM_Crate_B_01", new Vector3(wallX + 0.1f, ground, door[2] + 0.75f), 5f);
            Prop(yard, "SM_Crate_B_01", new Vector3(wallX + 0.05f, ground + 0.6f, door[2] + 0.78f), 22f, 0.8f);
            Prop(yard, "SM_burlapSackA_a1", new Vector3(wallX + 0.05f, ground, door[1] - 2.7f), 95f, 0.8f);
            for (int i = 0; i < 5; i++)
                Prop(yard, "SM_Fish_01a", new Vector3(Mathf.Lerp(rack[0], rack[1], i / 4f), rack[2] - 0.42f, rack[3]), 0f).localRotation = Quaternion.Euler(0f, 0f, 90f);

            // The wood corner against the palisade stands on the unlevelled strip: each piece takes the terrain's height.
            Transform wood = Group(root, "WoodCorner");
            for (int i = 0; i < 3; i++)
                OnTerrain(Prop(wood, "SM_firewoodA_a1", new Vector3(porch[0] + 0.5f + i * 1.0f, 0f, porch[2] - 2.25f), 90f), terrain);
            float[] block = Floats(anchors["choppingBlock"]);   // the block itself is in the model
            Prop(wood, "SM_firewoodB_a3", new Vector3(block[0] - 0.45f, ground, block[2] + 0.3f), 40f);
            Prop(wood, "SM_firewoodB_a5", new Vector3(block[0] + 0.4f, ground, block[2] + 0.35f), 110f);

            Transform mess = Group(root, "Mess");
            Transform tables = root.parent.Find(INSTANCE + "/" + TABLES);
            if (tables == null)
            {
                GameLog.Warn(TAG, $"No {INSTANCE}/{TABLES}: the mess tables get no bowls.");
                return;
            }
            for (int i = 0; i < tables.childCount; i++)
            {
                // In the table's own frame, so a turned table keeps its bowls: one toward an end, a second on every third.
                Transform messTable = tables.GetChild(i);
                float side = i % 2 == 0 ? -1f : 1f;
                Prop(mess, "SM_Bowl_01", root.InverseTransformPoint(messTable.TransformPoint(new Vector3(side * 0.45f, 0.76f, 0.1f))), i * 60f);
                if (i % 3 == 0) Prop(mess, "SM_Bowl_01", root.InverseTransformPoint(messTable.TransformPoint(new Vector3(-side * 0.5f, 0.76f, -0.12f))), 0f);
            }
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A kit prop without colliders, left out of the NavMesh bake.</summary>
        private static Transform Prop(Transform parent, string kit, Vector3 localPosition, float yaw, float scale = 1f)
        {
            Transform prop = WorkerHutKitBuilder.Piece(kit, parent, localPosition);
            prop.localRotation = Quaternion.Euler(0f, yaw, 0f);
            prop.localScale = Vector3.one * scale;
            prop.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return prop;
        }

        private static void OnTerrain(Transform prop, Terrain terrain)
        {
            Vector3 world = prop.position;
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            prop.position = world;
        }

        // ------------------------------------------------------------------------------ parts file

        private static float[] Floats(JToken array)
        {
            var list = (JArray)array;
            var values = new float[list.Count];
            for (int i = 0; i < values.Length; i++) values[i] = (float)list[i];
            return values;
        }

        private static Vector3 Vector(JToken array) => new Vector3((float)array[0], (float)array[1], (float)array[2]);

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
