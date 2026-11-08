using System.Collections.Generic;
using System.Linq;
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
        private const string OLD_HOLLOW = "Hollow";  // the rock fold the pit replaced; a run deletes it
        private const string FIRE = "HuntersFire";
        private const string DECK_PREFIX = "SM_WoodPlatformFloorKit";
        private static readonly HashSet<string> BuiltRoots = new HashSet<string>(); // filled by NewRoot, so it can't drift from what a run rebuilds
        private static readonly string[] DockParts = { "DeckFloor_", "Boat", "MooringPost", "MooringRope" }; // all the tool owns in Docks
        private const string HAND_BRIDGE = "CampBridge/floor";
        private const string BARRICADE_PREFIX = "SM_Barricade_Var";
        private static readonly string[] FortWallIds = { "fort-wall-north", "fort-wall-west", "fort-wall-east" };
        private const string TOWER_FEATURE = "fort-tower";
        private const string RAMP_GATE = "RampGate";
        private const string RAMP_GATE_FEATURE = "ramp-gate";
        private const string CART_GATE_FEATURE = "fort-cart-gate";
        private static readonly string[] FortPathIds = { "path-fort-road", "path-fort-ramp" };
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
        private const string GARRISON = "Garrison";
        private const string WORKSHOP_YARD = "WorkshopYard";
        private const string WALKWAYS = "Walkways";
        private const string INNER_GATE_FEATURE = "inner-gate";
        private const string WAREHOUSE_FEATURE = "warehouse";
        private const string DOCK_GATE_FEATURE = "dock-gate";
        private const string WORK_GANG_FEATURE = "work-gang-tents";
        private const string LATRINE_FEATURE = "latrine";
        private const string TIMBER_YARD_FEATURE = "timber-yard";
        private const string STONE_LODGE_FEATURE = "stone-lodge";
        private const string MASONS_ROW_FEATURE = "masons-row";
        private const string TALLY_POST_FEATURE = "tally-post";
        private const string FORT_HOIST_FEATURE = "fort-hoist";
        private const string OX_PEN_FEATURE = "ox-pen";
        private const string RUBBLE_TIP_FEATURE = "rubble-tip";
        private const string SLEDGE_PATH_FEATURE = "path-sledge";
        private const string TIMBERWORKS_PATH_FEATURE = "path-timberworks";
        private const string FORGE_FEATURE = "forge";
        private const string COMPANY_STORE_FEATURE = "company-store";
        private const string SEASONING_SHED_FEATURE = "seasoning-shed";
        private const string CARPENTERS_SHOP_FEATURE = "carpenters-shop";
        private const string CHARCOAL_CLAMP_FEATURE = "charcoal-clamp";
        private const int FORT_HOIST_POINTS = 4; // two cranes, each with its rim frame
        private const string COMPOUND_GATE = "CompoundGate";
        private const string STAGING_YARD = "StagingYard";
        private const string GUARD_POST = "GuardPost";
        private const string GANG_HOUSE = "GangHouse";
        private const string GANG_DOCK = "GangDock";
        private const string COMPOUND_GATE_FEATURE = "compound-gate";
        private const string STAGING_YARD_FEATURE = "staging-yard";
        private const string GUARD_POST_FEATURE = "guard-post";
        private const string GANG_HOUSE_FEATURE = "gang-house";
        private const string GANG_DOCK_FEATURE = "gang-dock";
        private const float DOCK_GATE_WIDTH = 3f;
        private const string GARRISON_BARRACKS_FEATURE = "garrison-barracks";
        private const string STAFF_HOUSE_FEATURE = "staff-house";
        private const string SHORT_MEASURE_FEATURE = "short-measure";
        private const string GRAIN_STORE_FEATURE = "grain-store";
        private const string DRILL_YARD_FEATURE = "drill-yard";
        private const string WORKERS_GRAVES_FEATURE = "workers-graves";
        private const string GARRISON_WELL_FEATURE = "garrison-well";
        private const string GARRISON_BELL_FEATURE = "garrison-bell";

        // Smallest footprint each builder's fixed door, rooms and props fit in.
        private static readonly (string Id, float MinWidth, float MinDepth)[] GarrisonPlots =
        {
            (GARRISON_BARRACKS_FEATURE, 14f, 5f), (STAFF_HOUSE_FEATURE, 12f, 5f), (SHORT_MEASURE_FEATURE, 11f, 5f),
            (GRAIN_STORE_FEATURE, 4f, 3f), (DRILL_YARD_FEATURE, 12f, 6f), (WORKERS_GRAVES_FEATURE, 2f, 5f),
        };

        private static readonly string[] GarrisonPoints = { GARRISON_WELL_FEATURE, GARRISON_BELL_FEATURE, "reserve-kennels", "reserve-barber" };
        private static readonly string[] GarrisonPaths = { "path-garrison", "path-drill-yard" };

        private const string FARM = "Farm";
        private const string CROP = "Assets/_Game/Art/Materials/Blockout_Crop.mat";
        private const string FARM_WELL_FEATURE = "farm-well";
        private const string FARM_STEP_FEATURE = "farm-step"; // top point on the cart ramp, then the foot
        private const string FIELD_PREFIX = "field-";
        // Smallest footprint each builder's fixed door, rooms and props fit in. The mill's width is its diameter.
        private static readonly (string Id, float MinWidth, float MinDepth)[] FarmPlots =
        {
            ("farm-manager-house", 8f, 5f), ("tithe-barn", 10f, 6f), ("threshing-floor", 6f, 6f), ("ox-mill", 9f, 9f),
            ("ox-byre", 10f, 5f), ("ox-paddock", 12f, 6f),
        };

        private static readonly string[] FarmPoints = { "hay-yard", "dung-heap", FARM_WELL_FEATURE, "farm-tally" };
        internal static readonly string[] FarmFields = { "field-wheat", "field-barley", "field-oats", "field-hay", "field-garden" };
        private static readonly string[] FarmPaths = { "path-farm", "path-field-lane" };
        private const float FIELD_MIN_SIZE = 6f; // the fence gap and a few rows
        private const float FARM_STEP_MIN_LENGTH = 1f;

        private static readonly string[] WalkwayIds = { "walkway-canteen", "walkway-guard-post" };
        private const string TENT = "Assets/_Game/Prefabs/World/Tent_Graybox.prefab";
        private const float WAREHOUSE_MIN_WIDTH = 8f;  // doors, landing and hoist are laid out from the middle
        private const float WAREHOUSE_MIN_DEPTH = 4f;
        // The room and yard props are fitted to 8 x 5 m, so a size outside these stops the run.
        private const float GUARD_POST_MIN_WIDTH = 7.8f;
        private const float GUARD_POST_MAX_WIDTH = 8.5f;
        private const float GUARD_POST_MIN_DEPTH = 4.5f;
        private const float GUARD_POST_MAX_DEPTH = 6f;
        // The room is fitted to 9 x 6 m, so a size outside these stops the run.
        private const float GANG_HOUSE_MIN_WIDTH = 8.5f;
        private const float GANG_HOUSE_MAX_WIDTH = 10f;
        private const float GANG_HOUSE_MIN_DEPTH = 5.5f;
        private const float GANG_HOUSE_MAX_DEPTH = 7f;
        private const float GANG_DOCK_MIN_LENGTH = 2f;

        private const float WALL_TOP = 14f;      // world height: 4 m above the outcrop
        private const float WALL_THICKNESS = 1.2f;
        private const float WALL_PANEL = 3f;
        private const float WALL_BURY = 1.5f;
        private const float CART_PIER_WIDTH = 2f;
        private const float CART_PIER_RISE = 1f;  // over the wall top
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

            if (!Run(GameObject.Find(BLOCKOUT_ROOT), true, out string problem)) GameLog.Error(TAG, problem);
        }

        /// <summary>Builds into <paramref name="root"/>, which <see cref="Area1HandEditsWindow"/> points at a temporary clone with <paramref name="applyEdits"/> off.</summary>
        internal static bool Run(GameObject root, bool applyEdits, out string problem)
        {
            problem = null;
            Terrain terrain = Terrain.activeTerrain;
            Material timber = AssetDatabase.LoadAssetAtPath<Material>(TIMBER);
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE);
            Material crop = AssetDatabase.LoadAssetAtPath<Material>(CROP);
            WorldFact fact = AssetDatabase.LoadAssetAtPath<WorldFact>(FACT);
            GameEventSO_Fact factEvent = AssetDatabase.LoadAssetAtPath<GameEventSO_Fact>(FACT_EVENT);
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(GLOW);
            DayNightConfigSO dayNight = AssetDatabase.LoadAssetAtPath<DayNightConfigSO>(DAY_NIGHT);
            GameObject rockTall = AssetDatabase.LoadAssetAtPath<GameObject>(ROCK_TALL);
            GameObject rockLow = AssetDatabase.LoadAssetAtPath<GameObject>(ROCK_LOW);
            GameObject tent = AssetDatabase.LoadAssetAtPath<GameObject>(TENT);
            if (terrain == null || terrain.terrainData == null || root == null || timber == null || stone == null || crop == null || fact == null || factEvent == null
                || glow == null || dayNight == null || rockTall == null || rockLow == null || tent == null)
            {
                problem = $"Need an active terrain, {BLOCKOUT_ROOT}, the blockout materials, {FACT}, {FACT_EVENT}, {DAY_NIGHT}, the rock prefabs and {TENT}; nothing changed.";
                return false;
            }

            if (!Area1BlockoutBuilder.TryReadLayout(out Dictionary<string, JObject> features, out problem))
            {
                problem = $"{problem}; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(CLEFT_FEATURE, out JObject cleft) || !Area1BlockoutBuilder.HasPoints(cleft))
            {
                problem = $"Layout has no usable feature '{CLEFT_FEATURE}'; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(RUINS2_FEATURE, out JObject ruins2) || !Area1BlockoutBuilder.HasPosition(ruins2)
                || !Area1BlockoutBuilder.IsNumber(ruins2["w"]) || !Area1BlockoutBuilder.IsNumber(ruins2["d"]))
            {
                problem = $"Layout has no usable feature '{RUINS2_FEATURE}'; nothing changed.";
                return false;
            }

            foreach (string id in FortWallIds)
            {
                if (features.TryGetValue(id, out JObject wall) && Area1BlockoutBuilder.HasPoints(wall)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach (string id in new[] { TOWER_FEATURE, FIRE_FEATURE, ARCH_FEATURE, QUARRY_FEATURE, WOOD_FEATURE, HUNTING_FEATURE, CAVE_FEATURE,
                                                 INNER_GATE_FEATURE, WAREHOUSE_FEATURE, DOCK_GATE_FEATURE, WORK_GANG_FEATURE, LATRINE_FEATURE, TIMBER_YARD_FEATURE, FORGE_FEATURE, COMPANY_STORE_FEATURE, SEASONING_SHED_FEATURE, CARPENTERS_SHOP_FEATURE, CHARCOAL_CLAMP_FEATURE, STONE_LODGE_FEATURE, MASONS_ROW_FEATURE, TALLY_POST_FEATURE, OX_PEN_FEATURE, RUBBLE_TIP_FEATURE,
                                                 COMPOUND_GATE_FEATURE, STAGING_YARD_FEATURE, GUARD_POST_FEATURE })
            {
                if (features.TryGetValue(id, out JObject point) && Area1BlockoutBuilder.HasPosition(point)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach (string id in WalkwayIds)
            {
                if (features.TryGetValue(id, out JObject walkway) && Area1BlockoutBuilder.HasPoints(walkway) && Area1BlockoutBuilder.IsNumber(walkway["width"]) && (float)walkway["width"] > 0f) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(FORT_HOIST_FEATURE, out JObject fortHoist) || !Area1BlockoutBuilder.HasPoints(fortHoist) || ((JArray)fortHoist["points"]).Count != FORT_HOIST_POINTS)
            {
                problem = $"Layout has no usable feature '{FORT_HOIST_FEATURE}'; nothing changed.";
                return false;
            }

            if (!Area1BlockoutBuilder.IsNumber(features[OX_PEN_FEATURE]["yaw"]))
            {
                problem = $"Layout has no usable feature '{OX_PEN_FEATURE}'; nothing changed.";
                return false;
            }

            foreach (string id in new[] { RAMP_GATE_FEATURE, CART_GATE_FEATURE })
            {
                if (features.TryGetValue(id, out JObject gate) && Area1BlockoutBuilder.HasPosition(gate)
                    && Area1BlockoutBuilder.IsNumber(gate["width"]) && (float)gate["width"] > 0f && Area1BlockoutBuilder.IsNumber(gate["yaw"])) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach (string id in FortPathIds)
            {
                if (features.TryGetValue(id, out JObject path) && Area1BlockoutBuilder.HasPoints(path)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach ((string id, float minWidth, float minDepth) in GarrisonPlots)
            {
                if (features.TryGetValue(id, out JObject plot) && Area1BlockoutBuilder.HasPosition(plot)
                    && Area1BlockoutBuilder.IsNumber(plot["w"]) && (float)plot["w"] >= minWidth && Area1BlockoutBuilder.IsNumber(plot["d"]) && (float)plot["d"] >= minDepth && Area1BlockoutBuilder.IsNumber(plot["yaw"])) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach (string id in GarrisonPoints)
            {
                if (features.TryGetValue(id, out JObject point) && Area1BlockoutBuilder.HasPosition(point)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            // Only the terrain tool reads these, and it skips a missing path without a word.
            foreach (string id in GarrisonPaths)
            {
                if (features.TryGetValue(id, out JObject path) && Area1BlockoutBuilder.HasPoints(path)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach ((string id, float minWidth, float minDepth) in FarmPlots)
            {
                if (features.TryGetValue(id, out JObject plot) && Area1BlockoutBuilder.HasPosition(plot)
                    && Area1BlockoutBuilder.IsNumber(plot["w"]) && (float)plot["w"] >= minWidth && Area1BlockoutBuilder.IsNumber(plot["d"]) && (float)plot["d"] >= minDepth && Area1BlockoutBuilder.IsNumber(plot["yaw"])) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach (string id in FarmPoints)
            {
                if (features.TryGetValue(id, out JObject point) && Area1BlockoutBuilder.HasPosition(point)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            foreach (string id in FarmFields)
            {
                if (features.TryGetValue(id, out JObject field) && IsUsableField(field, out _)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(FARM_STEP_FEATURE, out JObject farmStep) || !Area1BlockoutBuilder.HasPoints(farmStep) || ((JArray)farmStep["points"]).Count != 2
                || !Area1BlockoutBuilder.IsNumber(farmStep["width"]) || (float)farmStep["width"] <= 0f
                || Vector2.Distance(Area1BlockoutBuilder.Points(farmStep)[0], Area1BlockoutBuilder.Points(farmStep)[1]) < FARM_STEP_MIN_LENGTH)
            {
                problem = $"Layout has no usable feature '{FARM_STEP_FEATURE}'; nothing changed.";
                return false;
            }

            // Only the terrain tool reads these, and it skips a missing path without a word.
            foreach (string id in FarmPaths)
            {
                if (features.TryGetValue(id, out JObject path) && Area1BlockoutBuilder.HasPoints(path)) continue;
                problem = $"Layout has no usable feature '{id}'; nothing changed.";
                return false;
            }

            // Only the terrain tool reads it, and it skips a missing path without a word.
            if (!features.TryGetValue(TIMBERWORKS_PATH_FEATURE, out JObject timberworksPath) || !Area1BlockoutBuilder.HasPoints(timberworksPath))
            {
                problem = $"Layout has no usable feature '{TIMBERWORKS_PATH_FEATURE}'; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(SLEDGE_PATH_FEATURE, out JObject sledgePath) || !Area1BlockoutBuilder.HasPoints(sledgePath))
            {
                problem = $"Layout has no usable feature '{SLEDGE_PATH_FEATURE}'; nothing changed.";
                return false;
            }

            JObject compoundGate = features[COMPOUND_GATE_FEATURE];
            if (!Area1BlockoutBuilder.IsNumber(compoundGate["width"]) || (float)compoundGate["width"] <= 0f)
            {
                problem = $"Layout has no usable feature '{COMPOUND_GATE_FEATURE}'; nothing changed.";
                return false;
            }

            JObject warehouseZone = features[WAREHOUSE_FEATURE];
            if (!Area1BlockoutBuilder.IsNumber(warehouseZone["w"]) || !Area1BlockoutBuilder.IsNumber(warehouseZone["d"])
                || (float)warehouseZone["w"] < WAREHOUSE_MIN_WIDTH || (float)warehouseZone["d"] < WAREHOUSE_MIN_DEPTH)
            {
                problem = $"Layout has no usable feature '{WAREHOUSE_FEATURE}'; nothing changed.";
                return false;
            }

            JObject guardPost = features[GUARD_POST_FEATURE];
            if (!Area1BlockoutBuilder.IsNumber(guardPost["w"]) || !Area1BlockoutBuilder.IsNumber(guardPost["d"]) || !Area1BlockoutBuilder.IsNumber(guardPost["yaw"])
                || (float)guardPost["w"] < GUARD_POST_MIN_WIDTH || (float)guardPost["w"] > GUARD_POST_MAX_WIDTH
                || (float)guardPost["d"] < GUARD_POST_MIN_DEPTH || (float)guardPost["d"] > GUARD_POST_MAX_DEPTH)
            {
                problem = $"Layout has no usable feature '{GUARD_POST_FEATURE}'; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(GANG_HOUSE_FEATURE, out JObject gangHouse) || !Area1BlockoutBuilder.HasPosition(gangHouse)
                || !Area1BlockoutBuilder.IsNumber(gangHouse["w"]) || !Area1BlockoutBuilder.IsNumber(gangHouse["d"]) || !Area1BlockoutBuilder.IsNumber(gangHouse["yaw"])
                || (float)gangHouse["w"] < GANG_HOUSE_MIN_WIDTH || (float)gangHouse["w"] > GANG_HOUSE_MAX_WIDTH
                || (float)gangHouse["d"] < GANG_HOUSE_MIN_DEPTH || (float)gangHouse["d"] > GANG_HOUSE_MAX_DEPTH)
            {
                problem = $"Layout has no usable feature '{GANG_HOUSE_FEATURE}'; nothing changed.";
                return false;
            }

            if (!features.TryGetValue(GANG_DOCK_FEATURE, out JObject gangDock) || !IsUsableGangDock(gangDock))
            {
                problem = $"Layout has no usable feature '{GANG_DOCK_FEATURE}'; nothing changed.";
                return false;
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
                problem = $"Need {DECK_PREFIX}* twice under {DOCKS}, both rendered, {HAND_BRIDGE} and {BARRICADE_PREFIX}* barricades, each with a box collider; nothing changed.";
                return false;
            }

            if (!Area1HandEdits.TryLoad(out Area1HandEdits edits, out problem)) return false;

            BuiltRoots.Clear();
            Transform quarry = NewRoot(root.transform, QUARRY);
            Vector2[] line = Area1BlockoutBuilder.Points(cleft);
            BuildCleftBlocker(line, terrain, timber, stone, fact, factEvent, quarry);
            BuildRuinsRing(line, ruins2, quarry);

            BuildFort(features, terrain, stone, timber, NewRoot(root.transform, FORT));
            JObject rampGate = features[RAMP_GATE_FEATURE];
            Area1CampInterior.BuildGate(Area1BlockoutBuilder.Position(rampGate), (float)rampGate["width"], terrain, timber, NewRoot(root.transform, RAMP_GATE), (float)rampGate["yaw"], guardPost: false); // rocks on one side, the road on the other
            ClearDockParts(docks);
            BuildDocks(deck, timber, docks);
            BuildBridgeRails(bridgeDeck, timber, NewRoot(root.transform, RAILS));
            BuildBarricadeVolumes(barricades, NewRoot(root.transform, BARRICADES));
            Transform oldHollow = root.transform.Find(OLD_HOLLOW);
            if (oldHollow != null) Object.DestroyImmediate(oldHollow.gameObject);
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
            Area1CampInterior.BuildGuardPost(Area1BlockoutBuilder.Position(guardPost), (float)guardPost["w"], (float)guardPost["d"], (float)guardPost["yaw"], terrain, timber, stone, NewRoot(root.transform, GUARD_POST));
            Area1CampInterior.BuildWorkGangTents(Area1BlockoutBuilder.Position(features[WORK_GANG_FEATURE]), tent, terrain, stone, NewRoot(root.transform, WORK_GANG));
            Area1CampInterior.BuildLatrine(Area1BlockoutBuilder.Position(features[LATRINE_FEATURE]), terrain, timber, stone, NewRoot(root.transform, LATRINE));
            Transform timberworks = NewRoot(root.transform, TIMBER_YARD);
            Area1Timberworks.BuildReceivingYard(Area1BlockoutBuilder.Position(features[TIMBER_YARD_FEATURE]), terrain, timber, timberworks);
            Area1Timberworks.BuildForge(Area1BlockoutBuilder.Position(features[FORGE_FEATURE]), terrain, timber, stone, timberworks);
            Area1Timberworks.BuildSeasoningShed(Area1BlockoutBuilder.Position(features[SEASONING_SHED_FEATURE]), terrain, timber, timberworks);
            Area1Timberworks.BuildCarpentersShop(Area1BlockoutBuilder.Position(features[CARPENTERS_SHOP_FEATURE]), terrain, timber, timberworks);
            Area1Timberworks.BuildCharcoalClamp(Area1BlockoutBuilder.Position(features[CHARCOAL_CLAMP_FEATURE]), terrain, timber, stone, timberworks);
            Area1Timberworks.BuildCompanyStore(Area1BlockoutBuilder.Position(features[COMPANY_STORE_FEATURE]), terrain, timber, stone, timberworks);
            Transform garrison = NewRoot(root.transform, GARRISON);
            Area1Garrison.BuildBarracks(Plot(features[GARRISON_BARRACKS_FEATURE]), terrain, timber, stone, garrison);
            Area1Garrison.BuildStaffHouse(Plot(features[STAFF_HOUSE_FEATURE]), terrain, timber, stone, garrison);
            Area1Garrison.BuildShortMeasure(Plot(features[SHORT_MEASURE_FEATURE]), terrain, timber, stone, garrison);
            Area1Garrison.BuildGrainStore(Plot(features[GRAIN_STORE_FEATURE]), terrain, timber, stone, garrison);
            Area1Garrison.BuildDrillYard(Plot(features[DRILL_YARD_FEATURE]), Area1BlockoutBuilder.Position(features[GARRISON_WELL_FEATURE]), Area1BlockoutBuilder.Position(features[GARRISON_BELL_FEATURE]), terrain, timber, stone, garrison);
            Area1Garrison.BuildGraves(Plot(features[WORKERS_GRAVES_FEATURE]), terrain, timber, stone, garrison);
            Transform farm = NewRoot(root.transform, FARM);
            Vector2[] stepLine = Area1BlockoutBuilder.Points(farmStep);
            Area1Farm.BuildStep(stepLine[0], stepLine[1], (float)farmStep["width"], terrain, timber, farm);
            Area1Farm.BuildManagerHouse(Plot(features["farm-manager-house"]), terrain, timber, stone, farm);
            Area1Farm.BuildTitheBarn(Plot(features["tithe-barn"]), terrain, timber, stone, farm);
            Area1Farm.BuildThreshingFloor(Plot(features["threshing-floor"]), terrain, timber, stone, crop, farm);
            Area1Farm.BuildOxMill(Plot(features["ox-mill"]), terrain, timber, stone, farm);
            Area1Farm.BuildByre(Plot(features["ox-byre"]), terrain, timber, stone, farm);
            Area1Farm.BuildPaddock(Plot(features["ox-paddock"]), terrain, timber, farm);
            Area1Farm.BuildHayYard(Area1BlockoutBuilder.Position(features["hay-yard"]), terrain, timber, crop, farm);
            Area1Farm.BuildDungHeap(Area1BlockoutBuilder.Position(features["dung-heap"]), terrain, timber, farm);
            Vector2 farmWell = Area1BlockoutBuilder.Position(features[FARM_WELL_FEATURE]);
            Area1Farm.BuildWell(farmWell, terrain, timber, stone, farm);
            Area1Farm.BuildTally(Area1BlockoutBuilder.Position(features["farm-tally"]), terrain, timber, farm);
            foreach (string id in FarmFields)
            {
                JObject field = features[id];
                IsUsableField(field, out Area1Farm.Crop kind);
                Area1Farm.BuildField(FieldName(id), Area1BlockoutBuilder.Position(field), new Vector2((float)field["w"], (float)field["d"]), kind, Gate(field), farmWell, terrain, timber, crop, farm);
            }
            Transform stoneworks = NewRoot(root.transform, WORKSHOP_YARD);
            Area1Stoneworks.BuildLodge(Area1BlockoutBuilder.Position(features[STONE_LODGE_FEATURE]), terrain, timber, stone, stoneworks);
            Area1Stoneworks.BuildMasonsRow(Area1BlockoutBuilder.Position(features[MASONS_ROW_FEATURE]), terrain, timber, stone, stoneworks);
            Area1Stoneworks.BuildTallyPost(Area1BlockoutBuilder.Position(features[TALLY_POST_FEATURE]), terrain, timber, stone, stoneworks);
            Area1Stoneworks.BuildHoists(Area1BlockoutBuilder.Points(fortHoist), terrain, timber, stone, stoneworks);
            Area1Stoneworks.BuildOxPen(Area1BlockoutBuilder.Position(features[OX_PEN_FEATURE]), (float)features[OX_PEN_FEATURE]["yaw"], terrain, timber, stone, stoneworks);
            Area1Stoneworks.BuildRubble(Area1BlockoutBuilder.Position(features[RUBBLE_TIP_FEATURE]), terrain, stone, stoneworks);
            Transform house = NewRoot(root.transform, GANG_HOUSE);
            Area1CampInterior.BuildGangHouse(Area1BlockoutBuilder.Position(gangHouse), (float)gangHouse["w"], (float)gangHouse["d"], (float)gangHouse["yaw"], terrain, timber, stone, house);
            Area1CampInterior.BuildGangDock(Area1BlockoutBuilder.Points(gangDock), (float)gangDock["width"], terrain, timber, house, NewRoot(root.transform, GANG_DOCK));
            Transform walkways = NewRoot(root.transform, WALKWAYS);
            foreach (string id in WalkwayIds)
                Area1CampInterior.BuildWalkway(id, Area1BlockoutBuilder.Points(features[id]), (float)features[id]["width"], terrain, timber, walkways);

            if (!applyEdits) return true;

            edits.Apply(root.transform, Owns);
            EditorSceneManager.MarkSceneDirty(root.scene);
            GameLog.Info(TAG, "Done. Rebake the NavMesh.");
            return true;
        }

        private static Transform NewRoot(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            BuiltRoots.Add(name);
            return root;
        }

        /// <summary>What the last run owns under <paramref name="root"/>: its roots, and only its own parts of the hand-built Docks.</summary>
        internal static IEnumerable<Transform> OwnedTops(Transform root)
        {
            foreach (string name in BuiltRoots)
            {
                Transform top = root.Find(name);
                if (top != null) yield return top;
            }

            Transform docks = root.Find(DOCKS);
            if (docks == null) yield break;
            foreach (Transform child in docks)
                if (IsDockPart(child)) yield return child;
        }

        /// <summary>By the last run's roots, so an entry for a root that left the tool can't touch what is now hand-owned.</summary>
        private static bool Owns(string path)
        {
            string[] segments = path.Split('/');
            return BuiltRoots.Contains(segments[0]) || (segments[0] == DOCKS && segments.Length > 1 && IsDockPart(segments[1]));
        }

        private static Area1Garrison.Plot Plot(JObject feature) =>
            new Area1Garrison.Plot(Area1BlockoutBuilder.Position(feature), (float)feature["w"], (float)feature["d"], (float)feature["yaw"]);

        /// <summary>
        /// A sized footprint with a crop named in letters (a number or a comma list would parse as another crop) and, if set, a
        /// `gate` of n, s, e or w. The terrain tool reads the same <see cref="FarmFields"/>, so both stop on the same bad entry.
        /// </summary>
        internal static bool IsUsableField(JObject field, out Area1Farm.Crop crop)
        {
            crop = default;
            string name = field["crop"]?.Type == JTokenType.String ? (string)field["crop"] : null;
            JToken gate = field["gate"];
            return Area1BlockoutBuilder.HasPosition(field)
                && Area1BlockoutBuilder.IsNumber(field["w"]) && (float)field["w"] >= FIELD_MIN_SIZE
                && Area1BlockoutBuilder.IsNumber(field["d"]) && (float)field["d"] >= FIELD_MIN_SIZE
                && !string.IsNullOrEmpty(name) && name.All(char.IsLetter) && System.Enum.TryParse(name, true, out crop)
                && (gate == null || (gate.Type == JTokenType.String && GateSides.ContainsKey((string)gate)));
        }

        private static readonly Dictionary<string, Vector2> GateSides = new Dictionary<string, Vector2>
        {
            { "n", Vector2.up }, { "s", Vector2.down }, { "e", Vector2.right }, { "w", Vector2.left },
        };

        private static Vector2? Gate(JObject field) => field["gate"] == null ? (Vector2?)null : GateSides[(string)field["gate"]];

        /// <summary>`field-wheat` becomes `Field_Wheat`, culture-independent.</summary>
        private static string FieldName(string id)
        {
            string crop = id.Substring(FIELD_PREFIX.Length);
            return "Field_" + char.ToUpperInvariant(crop[0]) + crop.Substring(1);
        }

        private static bool IsDockPart(Transform child) => IsDockPart(child.name);

        private static bool IsDockPart(string name) => DockParts.Any(part => name.StartsWith(part));

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
        /// Old stone with new timber on top. Each line ends inside something solid: the tower, a ruin piece, a cart gate pier or
        /// the hill palisade, so the ruin's pillars and the cart gate stay the only ways in. The lines keep to the rim, so no panel
        /// reaches down into the chamber.
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
            BuildCartGate(features[CART_GATE_FEATURE], terrain, stone, timber, fort);
        }

        /// <summary>The timber gate of the ramp, between two stone piers that end the west wall and the hill palisade.</summary>
        private static void BuildCartGate(JObject feature, Terrain terrain, Material stone, Material timber, Transform fort)
        {
            var gate = new GameObject("CartGate").transform;
            gate.SetParent(fort, false);

            Vector2 at = Area1BlockoutBuilder.Position(feature);
            float width = (float)feature["width"], yaw = (float)feature["yaw"];
            Area1CampInterior.BuildGate(at, width, terrain, timber, gate, yaw);

            foreach ((string name, float side) in new[] { ("Pier_South", -1f), ("Pier_North", 1f) })
            {
                Vector2 pier = Area1CampInterior.GateOffset(at, new Vector2(0f, side * (width * 0.5f + CART_PIER_WIDTH * 0.5f)), yaw);
                float height = WALL_TOP + CART_PIER_RISE - Area1BlockoutBuilder.Ground(terrain, pier);
                Area1RemoteSites.Column(name, gate, stone, terrain, pier, new Vector2(CART_PIER_WIDTH, CART_PIER_WIDTH), height, yaw);
            }
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
                if (IsDockPart(child)) Object.DestroyImmediate(child.gameObject);
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

        internal static void BuildBoat(Vector2 at, Vector2 along, Material timber, Transform parent)
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

        /// <summary>The bank's radii are fixed, so a floor outside these limits breaks its shape.</summary>
        internal static bool IsUsableFightPit(JObject zone)
        {
            if (!Area1BlockoutBuilder.IsNumber(zone["w"])) return false;
            float radius = (float)zone["w"] * 0.5f;
            return radius >= Area1TerrainMath.FightPit.MIN_FLOOR_RADIUS && radius <= Area1TerrainMath.FightPit.MAX_FLOOR_RADIUS;
        }

        /// <summary>A straight run: the builder has no bends.</summary>
        private static bool IsUsableGangDock(JObject dock)
        {
            if (!Area1BlockoutBuilder.HasPoints(dock) || !Area1BlockoutBuilder.IsNumber(dock["width"]) || (float)dock["width"] <= 0f) return false;
            Vector2[] line = Area1BlockoutBuilder.Points(dock);
            return line.Length == 2 && Vector2.Distance(line[0], line[1]) >= GANG_DOCK_MIN_LENGTH;
        }

        /// <summary>The bank is highest towards the fort, and the gap faces north, away from it.</summary>
        internal static Area1TerrainMath.FightPit ReadFightPit(JObject zone, JObject tower)
        {
            Vector2 center = Area1BlockoutBuilder.Position(zone);
            return new Area1TerrainMath.FightPit(center, (float)zone["w"] * 0.5f, Area1BlockoutBuilder.Position(tower) - center, Vector2.up);
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
