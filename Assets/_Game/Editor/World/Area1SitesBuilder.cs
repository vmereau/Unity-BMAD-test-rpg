using System.Collections.Generic;
using Game.Core;
using Game.World;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Editor.World
{
    /// <summary>
    /// Rebuilds the graybox sites under Area1_Blockout from docs/World/Areas/area-001/layout.json. A run replaces only the
    /// roots it owns. Rebake the NavMesh afterwards.
    /// </summary>
    public static class Area1SitesBuilder
    {
        private const string TAG = "[Area1SitesBuilder]";

        private const string BLOCKOUT_ROOT = "Area1_Blockout";
        private const string QUARRY = "Quarry";
        private const string CLEFT_FEATURE = "quarry-cleft";
        private const string RUINS2_FEATURE = "zone-4";
        private const string BLOCKER_LAYER = "Ignore Raycast";
        private const string TIMBER = "Assets/_Game/Art/Materials/Blockout_Timber.mat";
        private const string STONE = "Assets/_Game/Art/Materials/Blockout_Stone.mat";
        private const string FACT = "Assets/_Game/Data/Facts/WorldFact_QuarryPassageOpened.asset";
        private const string FACT_EVENT = "Assets/_Game/Data/Events/OnWorldFactChanged.asset";
        private const string GLOW = "Assets/_Game/Art/Materials/Blockout_FireGlow.mat";
        private const string DAY_NIGHT = "Assets/_Game/Data/Config/DayNightConfig.asset";
        private const string ROCK_TALL = "Assets/Hivemind/HDRP/TheMessengerMountain/Art/Prefabs/SM_Rock_06.prefab";
        private const string ROCK_LOW = "Assets/Hivemind/HDRP/TheMessengerMountain/Art/Prefabs/SM_Rock_07.prefab";

        private const float BLOCKER_ALONG = 6f;  // metres into the cleft from its quarry end, where the walls have risen
        private const float BLOCKER_WIDTH = 14f; // the walls flare, so it runs well into them
        private const float BLOCKER_HEIGHT = 8f;
        private const float BLOCKER_DEPTH = 1.6f;
        private const float BLOCKER_BURY = 1f;
        private const float RING_OFFSET = 10f;   // out from the Ruins 2 pad, on the cliff's inner face
        private const float RING_BOTTOM = 20f;   // world height, below the ground along the whole loop
        private const float RING_TOP = 75f;      // world height, well over the crest and a jump
        private const float RING_THICKNESS = 1f;
        private const int PLANKS = 5;
        private const int RUBBLE = 26;
        private const int SEED = 1337;

        private const string FORT = "Fort";
        private const string DOCKS = "Docks";
        private const string RAILS = "BridgeRails";
        private const string BARRICADES = "Barricades";
        private const string HOLLOW = "Hollow";
        private const string FIRE = "HuntersFire";
        private const string DECK_PREFIX = "SM_WoodPlatformFloorKit";
        private static readonly string[] DockParts = { "DeckFloor_", "Boat", "MooringPost", "MooringRope" }; // all the tool owns in Docks
        private const string HAND_BRIDGE = "CampBridge/floor";
        private const string BARRICADE_PREFIX = "SM_Barricade_Var";
        private static readonly string[] FortWallIds = { "fort-wall-north", "fort-wall-west", "fort-wall-east" };
        private const string TOWER_FEATURE = "fort-tower";
        private const string HOLLOW_FEATURE = "hollow";
        private const string FIRE_FEATURE = "hunter-fire";
        private const string RUINS = "Ruins";
        private const string QUARRY_DRESSING = "QuarryDressing";
        private const string RUINS2_DRESSING = "Ruins2Dressing";
        private const string WOOD_CAMP = "WoodCamp";
        private const string HUNTING_CAMP = "HuntingCamp";
        private const string CAVE = "Cave";
        private const string ARCH_FEATURE = "ruins-arch";
        private const string QUARRY_FEATURE = "quarry";
        private const string WOOD_FEATURE = "wood-camp";
        private const string HUNTING_FEATURE = "hunting-camp";
        private const string CAVE_FEATURE = "cave";
        private const string INNER_GATE = "InnerGate";
        private const string WAREHOUSE = "Warehouse";
        private const string DOCK_GATE = "DockGate";
        private const string WORK_GANG = "WorkGangTents";
        private const string LATRINE = "Latrine";
        private const string TIMBER_YARD = "TimberYard";
        private const string WORKSHOP_YARD = "WorkshopYard";
        private const string WALKWAYS = "Walkways";
        private const string INNER_GATE_FEATURE = "inner-gate";
        private const string WAREHOUSE_FEATURE = "warehouse";
        private const string DOCK_GATE_FEATURE = "dock-gate";
        private const string WORK_GANG_FEATURE = "work-gang-tents";
        private const string LATRINE_FEATURE = "latrine";
        private const string TIMBER_YARD_FEATURE = "timber-yard";
        private const string WORKSHOP_YARD_FEATURE = "workshop-yard";
        private const string COMPOUND_GATE = "CompoundGate";
        private const string STAGING_YARD = "StagingYard";
        private const string COMPOUND_GATE_FEATURE = "compound-gate";
        private const string STAGING_YARD_FEATURE = "staging-yard";
        private const float DOCK_GATE_WIDTH = 3f;
        private static readonly string[] WalkwayIds = { "walkway-canteen" };
        private const string TENT = "Assets/_Game/Prefabs/World/Tent_Graybox.prefab";
        private const float WAREHOUSE_MIN_WIDTH = 8f;  // doors, landing and hoist are laid out from the middle
        private const float WAREHOUSE_MIN_DEPTH = 4f;

        private const float WALL_TOP = 14f;      // world height: 4 m above the outcrop
        private const float WALL_THICKNESS = 1.2f;
        private const float WALL_PANEL = 3f;
        private const float WALL_BURY = 1.5f;
        private const float HOARDING_HEIGHT = 1.2f;
        private const float HOARDING_THICKNESS = 0.25f;
        private const float TOWER_WIDTH = 4f;    // clears SM_smallStoneE_a1
        private const float TOWER_TOP = 25f;     // world height: 15 m above the outcrop
        private const float MERLON_HEIGHT = 0.9f;
        private const int MERLONS_PER_SIDE = 3;

        private const float DECK_REACH = 5.5f;    // either way along each deck piece
        private const float DECK_Y = 0.76f;       // puts the floor at the height of the visible planks
        private const float DECK_THICKNESS = 0.1f;
        private const float DECK_WIDTH = 2.6f;    // walkable floor: the deck's own colliders sit 0.45 m under the planks and miss some
        private const float WATER_LEVEL = -2f;
        private const float BOAT_OFFSET = 4f;     // out from the head of the T
        private const float MOORING_TOP = 1.4f;   // the post runs on down into the water

        private const float RAIL_INSET = 0.15f;   // in from the deck edge
        private const float RAIL_END_INSET = 3f;  // the deck's ends sit on the banks
        private const float RAIL_HEIGHT = 1f;
        private const float RAIL_POST_SPACING = 3f;
        private const float RAIL_POST_WIDTH = 0.14f;
        private const float RAIL_BAR = 0.07f;

        private const float BARRICADE_MARGIN = 0.35f; // agent radius and a little: Not Walkable areas aren't eroded
        private const float BARRICADE_VOLUME_HEIGHT = 6f;
        private const int NOT_WALKABLE_AREA = 1;

        private const float FOLD_WEST = 10f;      // from the hollow's centre: wide enough for sight lines from the tower
        private const float FOLD_EAST = 8f;
        private const float FOLD_GAP = 0.1f;      // south of the floor, which puts it just north of the Mafia tents
        private const float FOLD_SPACING = 2.4f;
        private const float FOLD_SINK = 0.5f;

        private const float FLAME_HEIGHT = 1.9f;  // the camp rim clears the tents' sight line by 0.4 m, so it sits high
        private const float FLAME_WIDTH = 1.8f;
        private const float FLAME_TALL = 3f;
        private const float GLOW_SIZE = 6f;
        private const float FIRE_LIGHT_INTENSITY = 30f;
        private const float FIRE_LIGHT_RANGE = 18f;
        private static readonly Color FireColor = new Color(1f, 0.55f, 0.2f);
        private static readonly Color GlowColor = new Color(3f, 1.3f, 0.35f, 1f);

        [MenuItem("Tools/World/Build Area 1 Sites")]
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
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE);
            WorldFact fact = AssetDatabase.LoadAssetAtPath<WorldFact>(FACT);
            GameEventSO_Fact factEvent = AssetDatabase.LoadAssetAtPath<GameEventSO_Fact>(FACT_EVENT);
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(GLOW);
            DayNightConfigSO dayNight = AssetDatabase.LoadAssetAtPath<DayNightConfigSO>(DAY_NIGHT);
            GameObject rockTall = AssetDatabase.LoadAssetAtPath<GameObject>(ROCK_TALL);
            GameObject rockLow = AssetDatabase.LoadAssetAtPath<GameObject>(ROCK_LOW);
            GameObject tent = AssetDatabase.LoadAssetAtPath<GameObject>(TENT);
            if (terrain == null || terrain.terrainData == null || root == null || timber == null || stone == null || fact == null || factEvent == null
                || glow == null || dayNight == null || rockTall == null || rockLow == null || tent == null)
            {
                GameLog.Error(TAG, $"Need an active terrain, {BLOCKOUT_ROOT}, the blockout materials, {FACT}, {FACT_EVENT}, {DAY_NIGHT}, the rock prefabs and {TENT}; nothing changed.");
                return;
            }

            if (!Area1BlockoutBuilder.TryReadLayout(out Dictionary<string, JObject> features, out string problem))
            {
                GameLog.Error(TAG, $"{problem}; nothing changed.");
                return;
            }

            if (!features.TryGetValue(CLEFT_FEATURE, out JObject cleft) || !Area1BlockoutBuilder.HasPoints(cleft))
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{CLEFT_FEATURE}'; nothing changed.");
                return;
            }

            if (!features.TryGetValue(RUINS2_FEATURE, out JObject ruins2) || !Area1BlockoutBuilder.HasPosition(ruins2)
                || !Area1BlockoutBuilder.IsNumber(ruins2["w"]) || !Area1BlockoutBuilder.IsNumber(ruins2["d"]))
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{RUINS2_FEATURE}'; nothing changed.");
                return;
            }

            foreach (string id in FortWallIds)
            {
                if (features.TryGetValue(id, out JObject wall) && Area1BlockoutBuilder.HasPoints(wall)) continue;
                GameLog.Error(TAG, $"Layout has no usable feature '{id}'; nothing changed.");
                return;
            }

            foreach (string id in new[] { TOWER_FEATURE, HOLLOW_FEATURE, FIRE_FEATURE, ARCH_FEATURE, QUARRY_FEATURE, WOOD_FEATURE, HUNTING_FEATURE, CAVE_FEATURE,
                                                 INNER_GATE_FEATURE, WAREHOUSE_FEATURE, DOCK_GATE_FEATURE, WORK_GANG_FEATURE, LATRINE_FEATURE, TIMBER_YARD_FEATURE, WORKSHOP_YARD_FEATURE,
                                                 COMPOUND_GATE_FEATURE, STAGING_YARD_FEATURE })
            {
                if (features.TryGetValue(id, out JObject point) && Area1BlockoutBuilder.HasPosition(point)) continue;
                GameLog.Error(TAG, $"Layout has no usable feature '{id}'; nothing changed.");
                return;
            }

            foreach (string id in WalkwayIds)
            {
                if (features.TryGetValue(id, out JObject walkway) && Area1BlockoutBuilder.HasPoints(walkway) && Area1BlockoutBuilder.IsNumber(walkway["width"]) && (float)walkway["width"] > 0f) continue;
                GameLog.Error(TAG, $"Layout has no usable feature '{id}'; nothing changed.");
                return;
            }

            JObject compoundGate = features[COMPOUND_GATE_FEATURE];
            if (!Area1BlockoutBuilder.IsNumber(compoundGate["width"]) || (float)compoundGate["width"] <= 0f)
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{COMPOUND_GATE_FEATURE}'; nothing changed.");
                return;
            }

            JObject warehouseZone = features[WAREHOUSE_FEATURE];
            if (!Area1BlockoutBuilder.IsNumber(warehouseZone["w"]) || !Area1BlockoutBuilder.IsNumber(warehouseZone["d"])
                || (float)warehouseZone["w"] < WAREHOUSE_MIN_WIDTH || (float)warehouseZone["d"] < WAREHOUSE_MIN_DEPTH)
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{WAREHOUSE_FEATURE}'; nothing changed.");
                return;
            }

            JObject hollowZone = features[HOLLOW_FEATURE];
            if (!Area1BlockoutBuilder.IsNumber(hollowZone["d"]))
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{HOLLOW_FEATURE}'; nothing changed.");
                return;
            }

            Transform docks = root.transform.Find(DOCKS);
            List<Transform> deck = FindDeck(docks);
            Transform bridge = root.transform.Find(HAND_BRIDGE);
            bool deckFound = deck.Count == 2 && TryBounds(deck[0], out _) && TryBounds(deck[1], out _);
            Bounds bridgeDeck = default;
            bool bridgeFound = bridge != null && TryBounds(bridge, out bridgeDeck) && bridgeDeck.size.z > RAIL_END_INSET * 2f;
            bool barricadesFound = TryFindBarricades(root, out List<BoxCollider> barricades);
            if (!deckFound || !bridgeFound || !barricadesFound)
            {
                GameLog.Error(TAG, $"Need {DECK_PREFIX}* twice under {DOCKS}, both rendered, {HAND_BRIDGE} and {BARRICADE_PREFIX}* barricades, each with a box collider; nothing changed.");
                return;
            }

            Transform quarry = NewRoot(root.transform, QUARRY);
            Vector2[] line = Area1BlockoutBuilder.Points(cleft);
            BuildCleftBlocker(line, terrain, timber, stone, fact, factEvent, quarry);
            BuildRuinsRing(line, ruins2, quarry);

            BuildFort(features, terrain, stone, timber, NewRoot(root.transform, FORT));
            ClearDockParts(docks);
            BuildDocks(deck, timber, docks);
            BuildBridgeRails(bridgeDeck, timber, NewRoot(root.transform, RAILS));
            BuildBarricadeVolumes(barricades, NewRoot(root.transform, BARRICADES));
            BuildHollow(Area1BlockoutBuilder.Position(hollowZone), (float)hollowZone["d"], terrain, rockTall, rockLow, NewRoot(root.transform, HOLLOW));
            BuildFire(Area1BlockoutBuilder.Position(features[FIRE_FEATURE]), terrain, timber, stone, glow, dayNight, NewRoot(root.transform, FIRE));

            Area1RemoteSites.BuildRuins(Area1BlockoutBuilder.Position(features[ARCH_FEATURE]), terrain, stone, NewRoot(root.transform, RUINS));
            Area1RemoteSites.BuildQuarry(Area1BlockoutBuilder.Position(features[QUARRY_FEATURE]), terrain, timber, stone, NewRoot(root.transform, QUARRY_DRESSING));
            Area1RemoteSites.BuildRuins2(Area1BlockoutBuilder.Position(ruins2), terrain, stone, NewRoot(root.transform, RUINS2_DRESSING));
            Area1RemoteSites.BuildWoodCamp(Area1BlockoutBuilder.Position(features[WOOD_FEATURE]), terrain, timber, NewRoot(root.transform, WOOD_CAMP));
            Area1RemoteSites.BuildHuntingCamp(Area1BlockoutBuilder.Position(features[HUNTING_FEATURE]), terrain, timber, stone, NewRoot(root.transform, HUNTING_CAMP));
            Area1RemoteSites.BuildCave(Area1BlockoutBuilder.Position(features[CAVE_FEATURE]), terrain, stone, rockTall, rockLow, NewRoot(root.transform, CAVE));

            Area1CampInterior.BuildInnerGate(Area1BlockoutBuilder.Position(features[INNER_GATE_FEATURE]), terrain, timber, stone, NewRoot(root.transform, INNER_GATE));
            Area1CampInterior.BuildWarehouse(Area1BlockoutBuilder.Position(warehouseZone), (float)warehouseZone["w"], (float)warehouseZone["d"], terrain, timber, NewRoot(root.transform, WAREHOUSE));
            Area1CampInterior.BuildGate(Area1BlockoutBuilder.Position(features[DOCK_GATE_FEATURE]), DOCK_GATE_WIDTH, terrain, timber, NewRoot(root.transform, DOCK_GATE));
            Area1CampInterior.BuildGate(Area1BlockoutBuilder.Position(compoundGate), (float)compoundGate["width"], terrain, timber, NewRoot(root.transform, COMPOUND_GATE));
            Area1CampInterior.BuildStagingYard(Area1BlockoutBuilder.Position(features[STAGING_YARD_FEATURE]), terrain, timber, stone, NewRoot(root.transform, STAGING_YARD));
            Area1CampInterior.BuildWorkGangTents(Area1BlockoutBuilder.Position(features[WORK_GANG_FEATURE]), tent, terrain, stone, NewRoot(root.transform, WORK_GANG));
            Area1CampInterior.BuildLatrine(Area1BlockoutBuilder.Position(features[LATRINE_FEATURE]), terrain, timber, stone, NewRoot(root.transform, LATRINE));
            Area1CampInterior.BuildTimberYard(Area1BlockoutBuilder.Position(features[TIMBER_YARD_FEATURE]), terrain, timber, NewRoot(root.transform, TIMBER_YARD));
            Area1CampInterior.BuildWorkshopYard(Area1BlockoutBuilder.Position(features[WORKSHOP_YARD_FEATURE]), terrain, timber, stone, NewRoot(root.transform, WORKSHOP_YARD));
            Transform walkways = NewRoot(root.transform, WALKWAYS);
            foreach (string id in WalkwayIds)
                Area1CampInterior.BuildWalkway(id, Area1BlockoutBuilder.Points(features[id]), (float)features[id]["width"], terrain, timber, walkways);

            EditorSceneManager.MarkSceneDirty(root.scene);
            GameLog.Info(TAG, "Done. Rebake the NavMesh.");
        }

        private static Transform NewRoot(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        /// <summary>
        /// Baked as if it weren't there, so the cleft stays in the NavMesh, and carved at runtime. Removing it needs no rebake.
        /// </summary>
        private static void BuildCleftBlocker(Vector2[] line, Terrain terrain, Material timber, Material stone, WorldFact fact, GameEventSO_Fact factEvent, Transform parent)
        {
            Vector2 direction = (line[1] - line[0]).normalized;
            Vector2 at = BlockerPoint(line);
            float ground = Area1BlockoutBuilder.Ground(terrain, at);

            var blocker = new GameObject("CleftBlocker").transform;
            blocker.SetParent(parent, false);
            blocker.SetPositionAndRotation(new Vector3(at.x, ground, at.y), Area1BlockoutBuilder.Yaw(direction));
            blocker.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var seal = new GameObject("Seal").transform;
            seal.SetParent(blocker, false);

            var wall = seal.gameObject.AddComponent<BoxCollider>();
            wall.center = new Vector3(0f, BLOCKER_HEIGHT * 0.5f - BLOCKER_BURY, 0f);
            wall.size = new Vector3(BLOCKER_WIDTH, BLOCKER_HEIGHT, BLOCKER_DEPTH);

            var obstacle = seal.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = wall.center;
            obstacle.size = wall.size;
            obstacle.carving = true;
            obstacle.enabled = false; // a bake cuts an enabled carving obstacle into the NavMesh; FactRemovedObject enables it at runtime

            // Closes the air over the seal, so nobody crosses the cliff above the cleft and drops in behind it.
            var cap = new GameObject("Cap").transform;
            cap.SetParent(seal, false);
            cap.position = new Vector3(at.x, (ground + RING_TOP) * 0.5f, at.y);
            cap.gameObject.layer = LayerMask.NameToLayer(BLOCKER_LAYER);
            cap.gameObject.AddComponent<BoxCollider>().size = new Vector3(BLOCKER_WIDTH, RING_TOP - ground, BLOCKER_DEPTH);

            AddVisuals(seal, timber, stone);

            var remover = blocker.gameObject.AddComponent<FactRemovedObject>();
            var so = new SerializedObject(remover);
            so.FindProperty("_fact").objectReferenceValue = fact;
            so.FindProperty("_target").objectReferenceValue = seal.gameObject;
            so.FindProperty("_onFactChanged").objectReferenceValue = factEvent;
            so.FindProperty("_runtimeCarve").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The player can jump up any steep face, so the cliffs alone don't seal Ruins 2. This invisible loop runs from one end
        /// of the blocker around the pad to the other, so with the seal in place there is no way in, and without it only the cleft.
        /// </summary>
        private static void BuildRuinsRing(Vector2[] line, JObject ruins2, Transform parent)
        {
            Vector2 at = BlockerPoint(line);
            Vector2 direction = (line[1] - line[0]).normalized;
            Vector2 across = new Vector2(-direction.y, direction.x) * (BLOCKER_WIDTH * 0.5f); // towards the quarry's east side
            Vector2 start = at + across, end = at - across;

            Vector2 center = Area1BlockoutBuilder.Position(ruins2);
            float east = center.x + (float)ruins2["w"] * 0.5f + RING_OFFSET;
            float west = center.x - (float)ruins2["w"] * 0.5f - RING_OFFSET;
            float south = center.y - (float)ruins2["d"] * 0.5f - RING_OFFSET;
            Vector2[] loop =
            {
                start, new Vector2(east, start.y), new Vector2(east, south), new Vector2(west, south), new Vector2(west, end.y), end,
            };

            var ring = new GameObject("RuinsRing").transform;
            ring.SetParent(parent, false);
            for (int i = 1; i < loop.Length; i++)
            {
                Vector2 middle = (loop[i - 1] + loop[i]) * 0.5f;
                Vector2 along = loop[i] - loop[i - 1];
                var wall = new GameObject($"Wall_{i - 1}").transform;
                wall.SetParent(ring, false);
                wall.SetPositionAndRotation(new Vector3(middle.x, (RING_BOTTOM + RING_TOP) * 0.5f, middle.y), Area1BlockoutBuilder.Yaw(along));
                wall.gameObject.layer = LayerMask.NameToLayer(BLOCKER_LAYER);
                wall.gameObject.isStatic = true;
                wall.gameObject.AddComponent<BoxCollider>().size =
                    new Vector3(RING_THICKNESS, RING_TOP - RING_BOTTOM, along.magnitude + RING_THICKNESS);
            }
        }

        /// <summary>
        /// Old stone with new timber on top. Each line ends inside something solid: the tower, the House or a ruin piece, so
        /// the ruin's pillars stay the only way in. The lines keep to the rim, so no panel reaches down into the chamber.
        /// </summary>
        private static void BuildFort(Dictionary<string, JObject> features, Terrain terrain, Material stone, Material timber, Transform fort)
        {
            foreach (string id in FortWallIds)
            {
                var group = new GameObject(id).transform;
                group.SetParent(fort, false);
                Vector2[] points = Area1BlockoutBuilder.Points(features[id]);

                int index = 0;
                foreach ((Vector2 a, Vector2 b) in Area1TerrainMath.CutPanels(points, WALL_PANEL, Vector2.zero, 0f))
                {
                    Area1BlockoutBuilder.GroundUnder(terrain, a, b, out float low, out _, out _);
                    Vector2 middle = (a + b) * 0.5f;
                    Vector2 along = b - a;
                    float bottom = low - WALL_BURY;
                    Transform panel = Area1BlockoutBuilder.Box($"Wall_{index:00}", group, stone,
                        new Vector3(middle.x, (WALL_TOP + bottom) * 0.5f, middle.y),
                        new Vector3(WALL_THICKNESS, WALL_TOP - bottom, along.magnitude + WALL_THICKNESS));
                    panel.rotation = Area1BlockoutBuilder.Yaw(along);

                    if (index % 3 != 2) // gaps, so the timber reads as repairs
                    {
                        Transform hoarding = Area1BlockoutBuilder.Box($"Hoarding_{index:00}", group, timber,
                            new Vector3(middle.x, WALL_TOP + HOARDING_HEIGHT * 0.5f, middle.y),
                            new Vector3(HOARDING_THICKNESS, HOARDING_HEIGHT, along.magnitude));
                        hoarding.rotation = panel.rotation;
                    }
                    index++;
                }

                AddShed(points, stone, timber, group);
            }

            BuildTower(Area1BlockoutBuilder.Position(features[TOWER_FEATURE]), terrain, stone, fort);
        }

        private static void AddShed(Vector2[] line, Material stone, Material timber, Transform parent)
        {
            int segment = line.Length / 2;
            Vector2 a = line[segment - 1], b = line[segment];
            Vector2 at = (a + b) * 0.5f;
            Quaternion yaw = Area1BlockoutBuilder.Yaw(b - a);

            Transform shed = Area1BlockoutBuilder.Box("Shed", parent, timber, new Vector3(at.x, WALL_TOP + 1.3f, at.y), new Vector3(3f, 2.6f, 3.6f));
            shed.rotation = yaw;
            Transform roof = Area1BlockoutBuilder.Box("ShedRoof", parent, stone, new Vector3(at.x, WALL_TOP + 2.8f, at.y), new Vector3(3.6f, 0.4f, 4.2f));
            roof.rotation = yaw;
        }

        private static void BuildTower(Vector2 at, Terrain terrain, Material stone, Transform parent)
        {
            float half = TOWER_WIDTH * 0.5f;
            float low = float.PositiveInfinity;
            foreach (Vector2 corner in new[] { new Vector2(-half, -half), new Vector2(half, -half), new Vector2(-half, half), new Vector2(half, half) })
                low = Mathf.Min(low, Area1BlockoutBuilder.Ground(terrain, at + corner));
            float bottom = low - WALL_BURY;

            var tower = new GameObject("Tower").transform;
            tower.SetParent(parent, false);
            Area1BlockoutBuilder.Box("Keep", tower, stone, new Vector3(at.x, (TOWER_TOP + bottom) * 0.5f, at.y),
                new Vector3(TOWER_WIDTH, TOWER_TOP - bottom, TOWER_WIDTH));

            float merlon = TOWER_WIDTH / (MERLONS_PER_SIDE * 2f - 1f);
            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i < MERLONS_PER_SIDE; i++)
                {
                    float offset = -half + merlon * 0.5f + i * merlon * 2f;
                    Vector2 local = side switch
                    {
                        0 => new Vector2(offset, half - merlon * 0.5f),
                        1 => new Vector2(offset, -half + merlon * 0.5f),
                        2 => new Vector2(half - merlon * 0.5f, offset),
                        _ => new Vector2(-half + merlon * 0.5f, offset),
                    };
                    Area1BlockoutBuilder.Box($"Merlon_{side}_{i}", tower, stone,
                        new Vector3(at.x + local.x, TOWER_TOP + MERLON_HEIGHT * 0.5f, at.y + local.y),
                        new Vector3(merlon, MERLON_HEIGHT, merlon));
                }
            }
        }

        private static List<Transform> FindDeck(Transform docks)
        {
            var deck = new List<Transform>();
            if (docks == null) return deck;
            foreach (Transform child in docks)
                if (child.name.StartsWith(DECK_PREFIX)) deck.Add(child);
            return deck;
        }

        /// <summary>The deck lives in this root too, so only the tool's own parts go, by name.</summary>
        private static void ClearDockParts(Transform docks)
        {
            for (int i = docks.childCount - 1; i >= 0; i--)
            {
                Transform child = docks.GetChild(i);
                foreach (string part in DockParts)
                {
                    if (!child.name.StartsWith(part)) continue;
                    Object.DestroyImmediate(child.gameObject);
                    break;
                }
            }
        }

        /// <summary>
        /// The deck is hand-built and stays. Its colliders sit under the planks and leave holes, and its gaps split a bake, so
        /// an invisible floor gives the player and agents the surface they see. ShadowsOnly draws only into the shadow map,
        /// and a render-mesh bake still collects it; a disabled renderer would not be collected.
        /// </summary>
        private static void BuildDocks(List<Transform> deck, Material timber, Transform docks)
        {
            Transform head = null;
            int index = 0;
            foreach (Transform piece in deck)
            {
                Vector2 center = DeckCenter(piece);
                Transform floor = Area1BlockoutBuilder.Box($"DeckFloor_{index++}", docks, timber, new Vector3(center.x, DECK_Y, center.y),
                    new Vector3(DECK_WIDTH, DECK_THICKNESS, DECK_REACH * 2f));
                floor.rotation = Area1BlockoutBuilder.Yaw(YawDirection(piece));
                var surface = floor.GetComponent<MeshRenderer>();
                surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                surface.receiveShadows = false;

                if (head == null || center.y > DeckCenter(head).y) head = piece; // the head of the T is the piece furthest out
            }

            Vector2 headCenter = DeckCenter(head);
            Vector2 headAlong = YawDirection(head);
            Vector2 river = new Vector2(-headAlong.y, headAlong.x);
            if (river.y < 0f) river = -river; // the river lies north of the deck

            BuildBoat(headCenter + river * BOAT_OFFSET, headAlong, timber, docks);

            Vector2 mooring = headCenter + river * 2f;
            Area1BlockoutBuilder.Box("MooringPost", docks, timber, new Vector3(mooring.x, (MOORING_TOP + WATER_LEVEL - 1f) * 0.5f, mooring.y),
                new Vector3(0.25f, MOORING_TOP - WATER_LEVEL + 1f, 0.25f));
            Vector3 from = new Vector3(mooring.x, 0.9f, mooring.y);
            Vector2 bow = headCenter + river * (BOAT_OFFSET - 0.6f);
            Vector3 to = new Vector3(bow.x, WATER_LEVEL + 0.6f, bow.y);
            Transform rope = Area1BlockoutBuilder.Box("MooringRope", docks, timber, (from + to) * 0.5f, new Vector3(0.05f, 0.05f, Vector3.Distance(from, to)));
            rope.rotation = Quaternion.LookRotation(to - from);
            Object.DestroyImmediate(rope.GetComponent<Collider>());
        }

        private static Vector2 DeckCenter(Transform piece)
        {
            TryBounds(piece, out Bounds bounds);
            return new Vector2(bounds.center.x, bounds.center.z);
        }

        private static bool TryBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
            {
                if (any) bounds.Encapsulate(part.bounds);
                else bounds = part.bounds;
                any = true;
            }
            return any;
        }

        private static Vector2 YawDirection(Transform piece)
        {
            float yaw = piece.eulerAngles.y * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        }

        private static void BuildBoat(Vector2 at, Vector2 along, Material timber, Transform parent)
        {
            var boat = new GameObject("Boat").transform;
            boat.SetParent(parent, false);
            boat.SetPositionAndRotation(new Vector3(at.x, WATER_LEVEL, at.y), Area1BlockoutBuilder.Yaw(along));
            boat.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            (string name, Vector3 local, Vector3 size)[] parts =
            {
                ("Hull", new Vector3(0f, 0.1f, 0f), new Vector3(1.4f, 0.15f, 5f)),
                ("Side_L", new Vector3(-0.7f, 0.35f, 0f), new Vector3(0.1f, 0.5f, 5f)),
                ("Side_R", new Vector3(0.7f, 0.35f, 0f), new Vector3(0.1f, 0.5f, 5f)),
                ("Bow", new Vector3(0f, 0.35f, 2.5f), new Vector3(1.4f, 0.5f, 0.1f)),
                ("Stern", new Vector3(0f, 0.35f, -2.5f), new Vector3(1.4f, 0.5f, 0.1f)),
                ("Thwart", new Vector3(0f, 0.45f, 0.4f), new Vector3(1.3f, 0.08f, 0.3f)),
            };
            foreach ((string name, Vector3 local, Vector3 size) in parts)
            {
                Transform part = Area1BlockoutBuilder.Box(name, boat, timber, Vector3.zero, size);
                part.localPosition = local;
                part.localRotation = Quaternion.identity;
            }
        }

        private static void BuildBridgeRails(Bounds deck, Material timber, Transform rails)
        {
            float fromZ = deck.min.z + RAIL_END_INSET;
            float toZ = deck.max.z - RAIL_END_INSET;
            int posts = Mathf.CeilToInt((toZ - fromZ) / RAIL_POST_SPACING);
            foreach (float x in new[] { deck.min.x + RAIL_INSET, deck.max.x - RAIL_INSET })
            {
                string side = x < deck.center.x ? "West" : "East";
                for (int i = 0; i <= posts; i++)
                {
                    float z = Mathf.Lerp(fromZ, toZ, i / (float)posts);
                    Area1BlockoutBuilder.Box($"Post_{side}_{i:00}", rails, timber,
                        new Vector3(x, deck.max.y + RAIL_HEIGHT * 0.5f, z), new Vector3(RAIL_POST_WIDTH, RAIL_HEIGHT, RAIL_POST_WIDTH));
                }

                Area1BlockoutBuilder.Box($"Rail_{side}", rails, timber,
                    new Vector3(x, deck.max.y + RAIL_HEIGHT, (fromZ + toZ) * 0.5f), new Vector3(RAIL_BAR, RAIL_BAR, toZ - fromZ));
            }
        }

        private static bool TryFindBarricades(GameObject root, out List<BoxCollider> found)
        {
            found = new List<BoxCollider>();
            foreach (GameObject top in root.scene.GetRootGameObjects())
            {
                if (!top.name.StartsWith(BARRICADE_PREFIX)) continue;
                BoxCollider box = top.GetComponentInChildren<BoxCollider>();
                if (box == null) return false;
                found.Add(box);
            }
            return found.Count > 0;
        }

        /// <summary>
        /// The barricades' sparse meshes leave NavMesh inside their box colliders, which runs paths into them. A Not Walkable
        /// volume over each fixes that without moving them.
        /// </summary>
        private static void BuildBarricadeVolumes(List<BoxCollider> barricades, Transform parent)
        {
            for (int i = 0; i < barricades.Count; i++)
            {
                BoxCollider box = barricades[i];
                Vector3 scale = box.transform.lossyScale;
                Vector3 size = new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y), Mathf.Abs(box.size.z * scale.z));

                var volume = new GameObject($"NotWalkable_{i}").transform;
                volume.SetParent(parent, false);
                volume.SetPositionAndRotation(box.transform.TransformPoint(box.center), Quaternion.Euler(0f, box.transform.eulerAngles.y, 0f));

                var modifier = volume.gameObject.AddComponent<NavMeshModifierVolume>();
                modifier.center = Vector3.zero;
                modifier.size = new Vector3(size.x + BARRICADE_MARGIN * 2f, BARRICADE_VOLUME_HEIGHT, size.z + BARRICADE_MARGIN * 2f);
                modifier.area = NOT_WALKABLE_AREA;
            }
        }

        /// <summary>
        /// A rock fold along the north edge of the Mafia tents hides the hollow from the fort; the crater rim is its back wall.
        /// Tall rocks set the height and low ones close the gaps at their feet.
        /// </summary>
        private static void BuildHollow(Vector2 center, float depth, Terrain terrain, GameObject rockTall, GameObject rockLow, Transform hollow)
        {
            float foldZ = center.y - depth * 0.5f - FOLD_GAP;
            var random = new System.Random(SEED);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);

            int index = 0;
            for (float x = center.x - FOLD_WEST; x <= center.x + FOLD_EAST; x += FOLD_SPACING)
            {
                float z = foldZ + Range(-0.3f, 0.3f);
                float ground = Area1BlockoutBuilder.Ground(terrain, new Vector2(x, z));
                PlaceRock(rockTall, $"Fold_{index:00}", hollow, new Vector3(x, ground - FOLD_SINK, z), Range(-20f, 20f) + (index % 2) * 180f,
                    new Vector3(1.2f, Range(0.8f, 0.92f), 0.75f));
                var foot = new Vector2(x + 1.2f, z + 0.9f);
                PlaceRock(rockLow, $"Foot_{index:00}", hollow, new Vector3(foot.x, Area1BlockoutBuilder.Ground(terrain, foot) + 0.2f, foot.y), Range(0f, 360f), Vector3.one * 0.8f);
                index++;
            }
        }

        internal static void PlaceRock(GameObject prefab, string name, Transform parent, Vector3 position, float yaw, Vector3 scale)
        {
            var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            rock.name = name;
            rock.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            rock.transform.localScale = scale;
        }

        /// <summary>
        /// The light only reaches the ground around the fire, so at 85 m the flame and glow are what reads. Both are unlit
        /// quads, and <see cref="ClockNightLight"/> turns everything off by day.
        /// </summary>
        private static void BuildFire(Vector2 at, Terrain terrain, Material timber, Material stone, Material glow, DayNightConfigSO dayNight, Transform fire)
        {
            float ground = Area1BlockoutBuilder.Ground(terrain, at);
            fire.position = new Vector3(at.x, ground, at.y);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Transform ring = Area1BlockoutBuilder.Box($"Stone_{i}", fire, stone, Vector3.zero, new Vector3(0.35f, 0.25f, 0.35f));
                ring.localPosition = new Vector3(Mathf.Cos(angle) * 0.8f, 0.12f, Mathf.Sin(angle) * 0.8f);
            }

            for (int i = 0; i < 3; i++)
            {
                Transform log = Area1BlockoutBuilder.Box($"Log_{i}", fire, timber, Vector3.zero, new Vector3(0.14f, 0.14f, 1.3f));
                log.localPosition = new Vector3(0f, 0.3f, 0f);
                log.localRotation = Quaternion.Euler(35f, i * 120f, 0f);
                Object.DestroyImmediate(log.GetComponent<Collider>());
            }

            // Lean-to behind the fire, on the side away from the river.
            (string name, Vector3 local, Vector3 size, Vector3 euler)[] shelter =
            {
                ("PostA", new Vector3(-1.6f, 0.9f, 3f), new Vector3(0.15f, 1.8f, 0.15f), Vector3.zero),
                ("PostB", new Vector3(1.6f, 0.9f, 3f), new Vector3(0.15f, 1.8f, 0.15f), Vector3.zero),
                ("Roof", new Vector3(0f, 1.2f, 3.8f), new Vector3(3.6f, 0.1f, 2.2f), new Vector3(25f, 0f, 0f)),
                ("Bedroll", new Vector3(0f, 0.1f, 3.7f), new Vector3(0.8f, 0.2f, 1.9f), Vector3.zero),
            };
            foreach ((string name, Vector3 local, Vector3 size, Vector3 euler) in shelter)
            {
                Transform part = Area1BlockoutBuilder.Box(name, fire, timber, Vector3.zero, size);
                part.localPosition = local;
                part.localRotation = Quaternion.Euler(euler);
            }

            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(fire, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = FireColor;
            light.range = FIRE_LIGHT_RANGE;
            light.intensity = FIRE_LIGHT_INTENSITY;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var billboard = new GameObject("Glow").transform;
            billboard.SetParent(fire, false);
            billboard.localPosition = new Vector3(0f, FLAME_HEIGHT, 0f);
            Renderer flameQuad = GlowQuad("Flame", billboard, glow, new Vector3(FLAME_WIDTH, FLAME_TALL, 1f));
            Renderer haloQuad = GlowQuad("Halo", billboard, glow, new Vector3(GLOW_SIZE, GLOW_SIZE, 1f));

            var nightLight = fire.gameObject.AddComponent<ClockNightLight>();
            var so = new SerializedObject(nightLight);
            so.FindProperty("_config").objectReferenceValue = dayNight;
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_billboard").objectReferenceValue = billboard;
            so.FindProperty("_glowColor").colorValue = GlowColor;
            SerializedProperty renderers = so.FindProperty("_glowRenderers");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = flameQuad;
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = haloQuad;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Renderer GlowQuad(string name, Transform parent, Material glow, Vector3 size)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localScale = size;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false; // lit by ClockNightLight; also keeps it out of render-mesh bakes
            return renderer;
        }

        private static Vector2 BlockerPoint(Vector2[] line) => line[0] + (line[1] - line[0]).normalized * BLOCKER_ALONG;

        private static void AddVisuals(Transform seal, Material timber, Material stone)
        {
            var random = new System.Random(SEED);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);

            for (int i = 0; i < PLANKS; i++)
            {
                Transform plank = Visual($"Plank_{i}", seal, timber, new Vector3(-2f + i, 1.8f, -0.2f), new Vector3(0.45f, 4.6f, 0.12f));
                plank.localRotation = Quaternion.Euler(Range(6f, 16f), 0f, Range(-14f, 14f));
            }

            for (int i = 0; i < RUBBLE; i++)
            {
                float size = Range(0.8f, 1.9f);
                Transform rock = Visual($"Rubble_{i}", seal, stone,
                    new Vector3(Range(-3.6f, 3.6f), Range(-0.3f, 1.3f), Range(-1.8f, -0.4f)),
                    new Vector3(size, size * Range(0.6f, 1f), size * Range(0.7f, 1.1f)));
                rock.localRotation = Quaternion.Euler(Range(0f, 40f), Range(0f, 360f), Range(0f, 40f));
            }
        }

        private static Transform Visual(string name, Transform parent, Material material, Vector3 localPosition, Vector3 size)
        {
            Transform box = Area1BlockoutBuilder.Box(name, parent, material, Vector3.zero, size);
            box.localPosition = localPosition;
            box.gameObject.isStatic = false; // the seal goes away at runtime
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
    }
}
