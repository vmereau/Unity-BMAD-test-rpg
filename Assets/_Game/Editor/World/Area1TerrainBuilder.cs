using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Math = Game.Editor.World.Area1TerrainMath;

namespace Game.Editor.World
{
    /// <summary>
    /// Rebuilds the StartingTown terrain from docs/World/Areas/area-001/layout.json. Same input, same terrain: the shapes
    /// outside the camp are computed from scratch, and the camp comes from a copy of the original 500 m terrain.
    /// </summary>
    public static class Area1TerrainBuilder
    {
        private const string TAG = "[Area1TerrainBuilder]";

        public const float WATER_LEVEL = -2f;

        private const string TERRAIN_ASSET = "Assets/_Game/Art/Terrain/StartingTownTerrain.asset";
        private const string SOURCE_ASSET = "Assets/_Game/Art/Terrain/StartingTownTerrain_Source.asset";
        private const string ROCK_LAYER = "Assets/Hivemind/CastleOfEternalMists/HDRP(Default)/Art/Terrain/TL_RockyCliff.terrainlayer";
        private const string TREE_FOLDER = "Assets/Hivemind/HDRP(Default)/MedievalFantasyVillage/Art/Prefabs/";
        private static readonly string[] TreePrefabs = { "SM_EuropeanBeech_L_02", "SM_EuropeanBeech_L_03", "SM_EuropeanBeech_XL_01" };
        private static readonly float[] TreeShare = { 0.3f, 0.65f, 1f };

        private const float TERRAIN_SIZE = 1000f;
        private const float TERRAIN_HEIGHT = 600f;
        private const float TERRAIN_Y = -30f;
        private const int HEIGHT_RES = 1025;
        private const int ALPHA_RES = 1024;
        private const int OLD_HEIGHT_RES = 513;
        private const int OLD_ALPHA_RES = 512;
        private const int GRID_OFFSET = 256; // 250 m at the shared 500/512 m sample spacing.
        private const float SAMPLE_STEP = TERRAIN_SIZE / (HEIGHT_RES - 1);

        private const float RIVER_BED = -6f;
        private const float LAKE_BED = -7f;
        private const float FORD_BED = -2.5f;
        private const float BANK_HEIGHT = 0.5f;
        private const float BANK_WIDTH = 14f; // centred on the layout width; keeps open banks under the 45 degree slope limit
        private const float BANK_BLEND = 37f;
        private const float DRY_MARGIN = 0.3f;
        private const float FORD_HALF_WIDTH = 4f;
        private const float FORD_FLAT = 5f;    // bed stays level this far from the river line
        private const float FORD_RAMP = 12f;   // then climbs linearly, about 30 degrees, walkable by agents
        private const float FORD_TOP = 4.5f;   // height of the camp rim at the crossing
        private const float FORD_FLANK = 12f;  // side slope from the crossing down to the river bed
        private const float FORD_RIM_BLEND = 1f; // the old ground outside the rim is low: a wider blend drags the ramp down and steepens it

        private const float ROAD_SHOULDER = 6f;
        private const float ROAD_SMOOTHING = 12f; // metres either side
        private const float PAINT_EDGE = 1f;      // fade past a painted line's edge
        private const float MAX_ROAD_GRADE = 0.25f; // about 14 degrees

        // One closed block around Ruins 2 and along the quarry's south and east edges. The outer face keeps the crest out of
        // reach: from a gentle back slope the player could walk the crest and drop into Ruins 2.
        private const float QUARRY_PAD = 25f;
        private const float RUINS2_PAD = 30f;
        private const float CLIFF_TOP = 56f;
        private const float CLIFF_CREST = 20f;   // from the floor edge to where the outer face starts
        private const float CLIFF_SLOPE = 1.6f;  // about 58 degrees, over the 45 degree slope limit
        private const float CLIFF_REACH = CLIFF_CREST + CLIFF_TOP / CLIFF_SLOPE;
        private const float CLEFT_WALL_SLOPE = 2f;

        private const float EYE_HEIGHT = 1.6f;
        private const float SIGHT_CLEARANCE = 0.5f;
        private const float SIGHT_HALF_WIDTH = 3f;
        private const float SIGHT_SHOULDER = 3f;
        private const float RUINS_SIGHT_SHARE = 0.6f; // stop before the ridge itself

        private const float TREE_CELL = 6.5f;
        private const float CLEFT_TREE_CLEARANCE = 8f;
        private const float CAVE_CLEARING = 20f;
        private const float HUNTING_CAMP_CLEARING = 22f;
        private const int MAX_TREES = 8000;
        private const int SEED = 1337;

        private const string COMPOUND_FEATURE = "compound";
        private const string FIGHT_PIT_FEATURE = "fight-pit";
        private const string LAKESIDE_FEATURE = "lakeside";
        private const string FORT_TOWER_FEATURE = "fort-tower";
        private const string GANG_HOUSE_FEATURE = "gang-house";
        private const float GANG_HOUSE_TREE_CLEARANCE = 14f; // from its centre: covers the house, deck and dock
        private const string COMPANY_ROAD = "company-road";
        private const string DELIVERY_ROAD = "delivery-road";
        private const string PATH_PREFIX = "path-";

        private enum Layer { Grass, Mud, Road, Cobble, Rock, Count }

        private readonly struct Shape
        {
            public readonly Vector2 Center, Size;
            public readonly float Blend;
            public Shape(float x, float z, float w, float d, float blend)
            {
                Center = new Vector2(x, z); Size = new Vector2(w, d); Blend = blend;
            }
        }

        private sealed class Ridge
        {
            public Vector2[] Points;
            public float StartHeight, EndHeight, Crest, Reach;
        }

        private sealed class Road
        {
            public Vector2[] Points;
            public float HalfWidth;
        }

        // Camp footprint, in world metres. Inside these the original ground is kept.
        private static readonly Vector2 CampCenter = new Vector2(0f, 3f);
        private const float CAMP_RADIUS = 46f;
        private const float CAMP_HARD_RADIUS = 44f;
        private const float CAMP_BLEND = 14f;
        private static readonly Shape[] KeptRects =
        {
            new Shape(12f, -40f, 44f, 28f, 14f),   // outcrop and Company house
            new Shape(-19f, -49f, 38f, 10f, 10f),  // ground of the removed CivilizationGate; the palisade stands on it
            new Shape(29f, 50f, 30f, 8f, 8f),      // ground of the removed WildernessPath fences; the north palisade stands on it
        };

        private static readonly Ridge[] Ridges =
        {
            new Ridge { Points = Line(70, 150, 149, 166, 240, 190, 345, 230), StartHeight = 20f, EndHeight = 50f, Crest = 10f, Reach = 85f }, // ruins ridge
            new Ridge { Points = Line(92, -62, 226, -118, 345, -170), StartHeight = 14f, EndHeight = 45f, Crest = 12f, Reach = 90f },          // quarry high ground
            new Ridge { Points = Line(-120, -70, -200, -110, -345, -140), StartHeight = 10f, EndHeight = 34f, Crest = 12f, Reach = 70f },      // south-west
            new Ridge { Points = Line(-140, 190, -240, 215, -345, 230), StartHeight = 10f, EndHeight = 36f, Crest = 12f, Reach = 70f },        // north-west
        };

        // Sight lines the forest must leave open: tents and docks to the hunter's fire, tents to the ruins.
        private static readonly Vector2 TentsEye = new Vector2(15f, 28f);

        /// <summary>Colour only: these never reach <see cref="CarveRoads"/>, so ground heights stay as they are.</summary>
        private sealed class PaintedLine
        {
            public Layer Layer;
            public Vector2[] Points;
            public float HalfWidth;
        }

        private sealed class Layout
        {
            public Dictionary<string, JObject> Features;
            public float WallInner, GapMin, GapMax;
            public Vector2[] River;
            public float RiverHalf;
            public Vector2 LakeCenter, LakeRadius;
            public (string id, float height)[] Pads;
            public Road[] Roads;
            public PaintedLine[] PaintedLines; // in paint order: later lines lie on top
            public Vector2 CompoundCenter, CompoundSize;
            public Math.FightPit Pit;
            public Vector2 LakesideCenter, LakesideSize;
            public Vector2 GangHouse;
            public Vector2[][] Corridors;
            public Vector2 Fire;
            public Vector2 Ford;
            public Vector2 QuarryCenter, QuarrySize, Ruins2Center, Ruins2Size;
            public Vector2[] QuarryCliff;
            public Vector2[] Cleft;
            public float CleftHalf;
            public (Vector2 center, float radius)[] Clearings;
        }

        [MenuItem("Tools/World/Build Area 1 Terrain")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                GameLog.Error(TAG, "Leave play mode first; nothing changed.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                GameLog.Error(TAG, "No active terrain.");
                return;
            }

            TerrainData data = terrain.terrainData;
            if (AssetDatabase.GetAssetPath(data) != TERRAIN_ASSET)
            {
                GameLog.Error(TAG, $"The active terrain is not {TERRAIN_ASSET}; nothing changed.");
                return;
            }

            bool firstRun = data.heightmapResolution == OLD_HEIGHT_RES && Mathf.Approximately(data.size.x, 500f)
                && terrain.transform.position == new Vector3(-250f, 0f, -250f);
            bool rerun = data.heightmapResolution == HEIGHT_RES && Mathf.Approximately(data.size.x, TERRAIN_SIZE);
            if (!firstRun && !rerun)
            {
                GameLog.Error(TAG, $"Unexpected terrain {data.size} at {data.heightmapResolution}; nothing changed.");
                return;
            }

            if (!TryLoadLayout(out Layout layout, out string problem))
            {
                GameLog.Error(TAG, $"{problem}; nothing changed.");
                return;
            }

            GameObject[] prefabs = TreePrefabs.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(TREE_FOLDER + n + ".prefab")).ToArray();
            TerrainLayer rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ROCK_LAYER);
            if (prefabs.Any(p => p == null) || rock == null)
            {
                GameLog.Error(TAG, "Tree prefab or rock layer missing; nothing changed.");
                return;
            }

            bool needsSource = AssetDatabase.LoadAssetAtPath<TerrainData>(SOURCE_ASSET) == null;
            if (firstRun && needsSource && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(data), SOURCE_ASSET))
            {
                GameLog.Error(TAG, $"Could not write {SOURCE_ASSET}; nothing changed.");
                return;
            }

            TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>(SOURCE_ASSET);
            if (source == null || source.heightmapResolution != OLD_HEIGHT_RES || source.terrainLayers.Length < (int)Layer.Rock)
            {
                GameLog.Error(TAG, $"{SOURCE_ASSET} missing or wrong size; nothing changed.");
                return;
            }

            Area1TerrainSculpt sculpt = null;
            if (rerun && !Area1TerrainSculpt.TryLoad(out sculpt))
            {
                GameLog.Error(TAG, $"{Area1TerrainSculpt.FILE} is missing or broken, so a rebuild would lose the hand edits; nothing changed.");
                return;
            }

            if (rerun && HeightHash(data.GetHeights(0, 0, HEIGHT_RES, HEIGHT_RES)) != sculpt.BuiltHash)
            {
                GameLog.Error(TAG, "The terrain has hand edits that aren't captured. Run Tools/World/Capture Area 1 Terrain Sculpt first; nothing changed.");
                return;
            }

            float[,] heights = SculptHeights(layout, source, out float[,] roadDistance, out float[,] protect);
            sculpt?.Apply(heights);
            float[,,] alpha = Paint(layout, source, heights, roadDistance, protect);
            TreeInstance[] trees = ScatterTrees(layout, heights, roadDistance);

            data.heightmapResolution = HEIGHT_RES;
            data.alphamapResolution = ALPHA_RES;
            data.size = new Vector3(TERRAIN_SIZE, TERRAIN_HEIGHT, TERRAIN_SIZE);

            var normalized = new float[HEIGHT_RES, HEIGHT_RES];
            for (int z = 0; z < HEIGHT_RES; z++)
                for (int x = 0; x < HEIGHT_RES; x++)
                    normalized[z, x] = (heights[z, x] - TERRAIN_Y) / TERRAIN_HEIGHT;
            data.SetHeights(0, 0, normalized);
            data.SetHoles(0, 0, CopyHoles(source, data.holesResolution));

            TerrainLayer[] layers = source.terrainLayers;
            data.terrainLayers = new[] { layers[0], layers[1], layers[2], layers[3], rock };
            data.SetAlphamaps(0, 0, alpha);

            data.treePrototypes = prefabs.Select(p => new TreePrototype { prefab = p }).ToArray();
            data.SetTreeInstances(trees, true);

            terrain.transform.position = new Vector3(-TERRAIN_SIZE * 0.5f, TERRAIN_Y, -TERRAIN_SIZE * 0.5f);
            terrain.treeDistance = 800f;
            terrain.Flush();

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            AssetDatabase.SaveAssets();

            sculpt ??= new Area1TerrainSculpt();
            sculpt.BuiltHash = HeightHash(data.GetHeights(0, 0, HEIGHT_RES, HEIGHT_RES));
            sculpt.Save();

            GameLog.Info(TAG, $"Done. {trees.Length} trees, height hash {HeightHash(normalized)}.");
        }

        /// <summary>Stores the live terrain's hand edits next to layout.json, so later builds keep them.</summary>
        [MenuItem("Tools/World/Capture Area 1 Terrain Sculpt")]
        public static void CaptureSculpt()
        {
            Terrain terrain = Terrain.activeTerrain;
            TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>(SOURCE_ASSET);
            if (EditorApplication.isPlaying || terrain == null || source == null || terrain.terrainData.heightmapResolution != HEIGHT_RES)
            {
                GameLog.Error(TAG, "Need edit mode, the built valley terrain and its source asset; nothing changed.");
                return;
            }

            if (!TryLoadLayout(out Layout layout, out string problem))
            {
                GameLog.Error(TAG, $"{problem}; nothing changed.");
                return;
            }

            float[,] normalized = terrain.terrainData.GetHeights(0, 0, HEIGHT_RES, HEIGHT_RES);
            var live = new float[HEIGHT_RES, HEIGHT_RES];
            for (int z = 0; z < HEIGHT_RES; z++)
                for (int x = 0; x < HEIGHT_RES; x++)
                    live[z, x] = normalized[z, x] * TERRAIN_HEIGHT + TERRAIN_Y;

            Area1TerrainSculpt sculpt = Area1TerrainSculpt.Capture(SculptHeights(layout, source, out _, out _), live);
            sculpt.BuiltHash = HeightHash(normalized);
            sculpt.Save();
            GameLog.Info(TAG, $"Captured {sculpt.Width} x {sculpt.Depth} samples to {Area1TerrainSculpt.FILE}.");
        }

        /// <summary>The source terrain's holes at their old world positions; everywhere else stays solid.</summary>
        private static bool[,] CopyHoles(TerrainData source, int resolution)
        {
            var holes = new bool[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    holes[z, x] = true;

            int size = source.holesResolution;
            bool[,] old = source.GetHoles(0, 0, size, size);
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    holes[z + GRID_OFFSET, x + GRID_OFFSET] = old[z, x];
            return holes;
        }

        /// <summary>FNV-1a over quantised heights, for comparing two runs.</summary>
        public static uint HeightHash(float[,] normalized)
        {
            uint hash = 2166136261;
            foreach (float value in normalized)
            {
                hash ^= (uint)Mathf.RoundToInt(value * 65535f);
                hash *= 16777619;
            }
            return hash;
        }

        private static float[,] SculptHeights(Layout layout, TerrainData source, out float[,] roadDistance, out float[,] protect)
        {
            float[,] old = source.GetHeights(0, 0, OLD_HEIGHT_RES, OLD_HEIGHT_RES);
            float oldScale = source.size.y;

            var heights = new float[HEIGHT_RES, HEIGHT_RES];
            protect = new float[HEIGHT_RES, HEIGHT_RES];

            for (int z = 0; z < HEIGHT_RES; z++)
            {
                for (int x = 0; x < HEIGHT_RES; x++)
                {
                    Vector2 p = World(x, z);
                    float h = Water(layout, p, Land(layout, p));

                    float keep = Keep(layout, p);
                    protect[z, x] = keep;
                    if (keep > 0f)
                    {
                        int ox = Mathf.Clamp(x - GRID_OFFSET, 0, OLD_HEIGHT_RES - 1);
                        int oz = Mathf.Clamp(z - GRID_OFFSET, 0, OLD_HEIGHT_RES - 1);
                        h = Mathf.Lerp(h, old[oz, ox] * oldScale, keep);
                    }
                    heights[z, x] = h;
                }
            }

            roadDistance = CarveRoads(layout, heights);
            OpenSightLines(layout, heights);
            AddFightPit(layout, heights);
            return heights;
        }

        /// <summary>Last, as an offset: <see cref="Area1TerrainSculpt"/> stores hand edits over this result, so a capture keeps no pit in them.</summary>
        private static void AddFightPit(Layout layout, float[,] heights)
        {
            Vector2 reach = Vector2.one * Math.FightPit.FOOT_RADIUS;
            Vector2Int from = Grid(layout.Pit.Center - reach), to = Grid(layout.Pit.Center + reach);
            for (int z = from.y; z <= to.y; z++)
                for (int x = from.x; x <= to.x; x++)
                    heights[z, x] += layout.Pit.Offset(World(x, z));
        }

        /// <summary>Hills, ridges, pads and mountains, before water.</summary>
        private static float Land(Layout layout, Vector2 p)
        {
            float h = 2.5f + Fbm(p, 180f, 4, 0f) * 9f;

            float ridge = 0f;
            foreach (Ridge r in Ridges)
            {
                float d = Math.PolylineDistance(p, r.Points, out float along);
                if (d >= r.Reach) continue;
                float crest = Mathf.Lerp(r.StartHeight, r.EndHeight, along);
                ridge = Mathf.Max(ridge, crest * (1f - Math.Ramp(d, r.Crest, r.Reach)));
            }
            h += ridge * (0.92f + Fbm(p, 60f, 2, 40f) * 0.16f);

            foreach ((string id, float height) in layout.Pads)
            {
                JObject f = layout.Features[id];
                float d = Math.RectDistance(p, new Vector2((float)f["x"], (float)f["z"]), new Vector2((float)f["w"], (float)f["d"]));
                h = Mathf.Lerp(height, h, Math.Ramp(d, 0f, 25f));
            }

            return Cliffs(layout, p, h) + Mountain(layout, p);
        }

        /// <summary>The quarry's cliffs, the ring around Ruins 2, and the cleft between them.</summary>
        private static float Cliffs(Layout layout, Vector2 p, float land)
        {
            float toRuins = Math.RectDistance(p, layout.Ruins2Center, layout.Ruins2Size);
            float toCliff = Math.PolylineDistance(p, layout.QuarryCliff, out _);
            float rim = Mathf.Min(toRuins, toCliff);
            if (rim >= CLIFF_REACH) return land;

            float toQuarry = Math.RectDistance(p, layout.QuarryCenter, layout.QuarrySize);
            float rise = Mathf.Min(RUINS2_PAD + toRuins * CLIFF_SLOPE, QUARRY_PAD + toQuarry * CLIFF_SLOPE);
            float h = Mathf.Max(land, Math.CliffSurface(rise, rim, CLIFF_TOP, CLIFF_CREST, CLIFF_SLOPE));

            // Measured between the pad edges, not along the line: the line runs into both pads, and neither may be lowered.
            float across = toQuarry + toRuins > 0f ? toQuarry / (toQuarry + toRuins) : 0f;
            float floor = Mathf.Lerp(QUARRY_PAD, RUINS2_PAD, across);
            return Math.CutCleft(h, Math.PolylineDistance(p, layout.Cleft, out _), floor, layout.CleftHalf, CLEFT_WALL_SLOPE);
        }

        private static float Mountain(Layout layout, Vector2 p)
        {
            float span = TERRAIN_SIZE * 0.5f - layout.WallInner; // depth of the ring
            float ax = Mathf.Abs(p.x), az = Mathf.Abs(p.y);
            float round = Mathf.Pow(Mathf.Pow(ax, 6f) + Mathf.Pow(az, 6f), 1f / 6f);
            float warp = (Mathf.PerlinNoise(p.x / 140f + 7f, p.y / 140f + 3f) - 0.5f) * 44f;
            float wall = Mathf.Clamp01((round + warp - layout.WallInner) / (span * 0.5f));

            float gapDistance = p.y < layout.GapMin ? layout.GapMin - p.y : p.y > layout.GapMax ? p.y - layout.GapMax : 0f;
            float gap = Mathf.Clamp01(gapDistance / 45f);
            wall = Mathf.Lerp(wall, Mathf.Min(wall, gap), 1f - Math.Ramp(p.x, -layout.WallInner - 10f, -layout.WallInner + 20f));
            if (wall <= 0f) return 0f;

            float foot = 14f * Math.Ramp(wall, 0f, 0.12f);
            float cliff = (60f + 40f * Mathf.PerlinNoise(p.x / 90f, p.y / 90f)) * Math.Ramp(wall, 0.12f, 0.34f);
            float upper = (70f + 50f * Fbm(p, 110f, 4, 90f)) * Math.Ramp(wall, 0.34f, 1f);
            return foot + cliff + upper;
        }

        /// <summary>River, lake, ford and the dry-ground rule.</summary>
        private static float Water(Layout layout, Vector2 p, float land)
        {
            float riverDistance = Math.PolylineDistance(p, layout.River, out _);
            float river = land;
            float reach = layout.RiverHalf + BANK_WIDTH * 0.5f;
            if (riverDistance < reach + BANK_BLEND)
            {
                float t = Math.Ramp(riverDistance, layout.RiverHalf - BANK_WIDTH * 0.5f, reach);
                float bed = Mathf.Lerp(RIVER_BED, BANK_HEIGHT, t);
                float ford = Mathf.Lerp(FORD_BED, FORD_TOP, Mathf.Clamp01((riverDistance - FORD_FLAT) / FORD_RAMP));
                float fordShare = 1f - Math.Ramp(Mathf.Abs(p.x - layout.Ford.x), FORD_HALF_WIDTH, FORD_HALF_WIDTH + FORD_FLANK);
                bed = Mathf.Lerp(bed, ford, fordShare);
                river = Mathf.Lerp(bed, land, Math.Ramp(riverDistance, reach, reach + BANK_BLEND));
            }

            float e = Ellipse(layout, p);
            float lake = land;
            if (e < 1.7f)
            {
                float bed = Mathf.Lerp(LAKE_BED, BANK_HEIGHT, Math.Ramp(e, 0.75f, 1.1f));
                lake = Mathf.Lerp(bed, land, Math.Ramp(e, 1.1f, 1.7f));
            }

            float h = Mathf.Min(river, lake);
            bool open = riverDistance >= reach && e >= 1.1f;
            return open ? Mathf.Max(h, WATER_LEVEL + DRY_MARGIN) : h;
        }

        private static float Ellipse(Layout layout, Vector2 p)
        {
            float dx = (p.x - layout.LakeCenter.x) / layout.LakeRadius.x;
            float dz = (p.y - layout.LakeCenter.y) / layout.LakeRadius.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>1 where the original ground stays, blending to 0 around the footprint.</summary>
        private static float Keep(Layout layout, Vector2 p)
        {
            float keep = 1f - Math.Ramp(Vector2.Distance(p, CampCenter), CAMP_RADIUS, CAMP_RADIUS + CAMP_BLEND);
            bool ramp = OnFordRamp(layout, p);
            float toFlank = Math.Ramp(Mathf.Abs(p.x - layout.Ford.x), FORD_HALF_WIDTH, FORD_HALF_WIDTH + FORD_FLANK);
            foreach (Shape s in KeptRects)
            {
                // Tight only on the crossing: across the whole flank it would leave ledges either side.
                float blend = ramp ? Mathf.Lerp(FORD_RIM_BLEND, s.Blend, toFlank) : s.Blend;
                keep = Mathf.Max(keep, 1f - Math.Ramp(Math.RectDistance(p, s.Center, s.Size), 0f, blend));
            }
            return keep;
        }

        /// <summary>
        /// True on the ford's camp-side ramp, which keeps its own grade: neither road grading nor a wide camp blend may steepen it.
        /// The north side has no fixed ground to meet, so the road grades into it as usual.
        /// </summary>
        private static bool OnFordRamp(Layout layout, Vector2 p) =>
            p.y < layout.Ford.y
            && Mathf.Abs(p.x - layout.Ford.x) < FORD_HALF_WIDTH + FORD_FLANK
            && Math.PolylineDistance(p, layout.River, out _) < FORD_FLAT + FORD_RAMP + FORD_RIM_BLEND;

        /// <summary>True on ground roads must not touch, because camp objects stand on it.</summary>
        private static bool IsFixedGround(Vector2 p)
        {
            if (Vector2.Distance(p, CampCenter) <= CAMP_HARD_RADIUS) return true;
            foreach (Shape s in KeptRects)
                if (Math.RectDistance(p, s.Center, s.Size) <= 0f) return true;
            return false;
        }

        /// <summary>Grades every road into the ground and returns each sample's distance past the road's edge.</summary>
        private static float[,] CarveRoads(Layout layout, float[,] heights)
        {
            var edge = new float[HEIGHT_RES, HEIGHT_RES];
            for (int z = 0; z < HEIGHT_RES; z++)
                for (int x = 0; x < HEIGHT_RES; x++)
                    edge[z, x] = float.PositiveInfinity;

            foreach (Road road in layout.Roads)
            {
                List<Vector2> line = Resample(road.Points, 1f);
                var profile = new float[line.Count];
                var anchored = new bool[line.Count];
                for (int i = 0; i < line.Count; i++)
                {
                    profile[i] = SampleHeight(heights, line[i]);
                    anchored[i] = IsFixedGround(line[i]);
                }

                float[] smooth = BoxBlur(profile, Mathf.RoundToInt(ROAD_SMOOTHING));
                for (int i = 0; i < line.Count; i++)
                    if (!anchored[i]) profile[i] = smooth[i];
                Math.LimitGrade(profile, anchored, MAX_ROAD_GRADE);

                var best = new Dictionary<int, (float distance, float height)>();
                float reach = road.HalfWidth + ROAD_SHOULDER;
                int brush = Mathf.CeilToInt(reach / SAMPLE_STEP);
                for (int i = 0; i < line.Count; i++)
                {
                    Vector2Int c = Grid(line[i]);
                    for (int z = c.y - brush; z <= c.y + brush; z++)
                    {
                        for (int x = c.x - brush; x <= c.x + brush; x++)
                        {
                            if (x < 0 || z < 0 || x >= HEIGHT_RES || z >= HEIGHT_RES) continue;
                            float d = Vector2.Distance(World(x, z), line[i]);
                            if (d > reach) continue;
                            int key = z * HEIGHT_RES + x;
                            if (!best.TryGetValue(key, out var hit) || d < hit.distance) best[key] = (d, profile[i]);
                        }
                    }
                }

                foreach (KeyValuePair<int, (float distance, float height)> hit in best)
                {
                    int z = hit.Key / HEIGHT_RES, x = hit.Key % HEIGHT_RES;
                    Vector2 p = World(x, z);
                    edge[z, x] = Mathf.Min(edge[z, x], hit.Value.distance - road.HalfWidth);
                    if (IsFixedGround(p) || InWater(layout, p) || OnFordRamp(layout, p)) continue;
                    float share = 1f - Math.Ramp(hit.Value.distance, road.HalfWidth + 0.5f, reach);
                    float graded = Mathf.Lerp(heights[z, x], hit.Value.height, share);
                    heights[z, x] = Mathf.Max(graded, WATER_LEVEL + DRY_MARGIN);
                }
            }
            return edge;
        }

        /// <summary>Lowers rims that block a sight line, leaving the camp's fixed ground alone.</summary>
        private static void OpenSightLines(Layout layout, float[,] heights)
        {
            float reach = SIGHT_HALF_WIDTH + SIGHT_SHOULDER;
            int brush = Mathf.CeilToInt(reach / SAMPLE_STEP);
            float floor = WATER_LEVEL + DRY_MARGIN;
            foreach (Vector2[] corridor in layout.Corridors)
            {
                var uncut = (float[,])heights.Clone();
                bool fire = corridor[1] == layout.Fire;
                float fromY = Mathf.Max(SampleHeight(heights, corridor[0]), floor) + EYE_HEIGHT;
                float toY = SampleHeight(heights, corridor[1]) + (fire ? EYE_HEIGHT : 6f);
                float share = fire ? 1f : RUINS_SIGHT_SHARE;

                foreach (Vector2 point in Resample(new[] { corridor[0], Vector2.Lerp(corridor[0], corridor[1], share) }, 1f))
                {
                    float along = Vector2.Distance(corridor[0], point) / Vector2.Distance(corridor[0], corridor[1]);
                    float limit = Mathf.Max(Mathf.Lerp(fromY, toY, along) - SIGHT_CLEARANCE, floor);
                    Vector2Int g = Grid(point);
                    for (int z = g.y - brush; z <= g.y + brush; z++)
                    {
                        for (int x = g.x - brush; x <= g.x + brush; x++)
                        {
                            if (x < 0 || z < 0 || x >= HEIGHT_RES || z >= HEIGHT_RES || uncut[z, x] <= limit) continue;
                            Vector2 p = World(x, z);
                            if (IsFixedGround(p) || InWater(layout, p)) continue;
                            float strength = 1f - Math.Ramp(Vector2.Distance(p, point), SIGHT_HALF_WIDTH, reach);
                            heights[z, x] = Mathf.Min(heights[z, x], Mathf.Lerp(uncut[z, x], limit, strength));
                        }
                    }
                }
            }
        }

        private static bool InWater(Layout layout, Vector2 p) =>
            Math.PolylineDistance(p, layout.River, out _) < layout.RiverHalf + 2f || Ellipse(layout, p) < 1.05f;

        private static float[,,] Paint(Layout layout, TerrainData source, float[,] heights, float[,] roadDistance, float[,] protect)
        {
            int layers = (int)Layer.Count;
            float[,,] oldAlpha = source.GetAlphamaps(0, 0, OLD_ALPHA_RES, OLD_ALPHA_RES);
            var alpha = new float[ALPHA_RES, ALPHA_RES, layers];

            for (int z = 0; z < ALPHA_RES; z++)
            {
                for (int x = 0; x < ALPHA_RES; x++)
                {
                    Vector2 p = World(x + 0.5f, z + 0.5f);
                    float slope = SlopeDegrees(heights, x, z);
                    float height = heights[z, x];

                    float rock = Mathf.Max(Math.Ramp(slope, 34f, 44f), Math.Ramp(height, 50f, 85f));
                    float river = Math.PolylineDistance(p, layout.River, out _);
                    float mud = 1f - Math.Ramp(Mathf.Min(river - layout.RiverHalf, (Ellipse(layout, p) - 1f) * layout.LakeRadius.x), 6f, 13f);
                    mud = Mathf.Max(mud, Math.Ramp(Fbm(p, 45f, 3, 20f), 0.62f, 0.72f) * 0.6f);
                    mud *= 1f - rock;

                    var w = new float[layers];
                    w[(int)Layer.Rock] = rock;
                    w[(int)Layer.Mud] = mud;
                    w[(int)Layer.Grass] = Mathf.Max(0f, 1f - rock - mud);

                    float keep = protect[z, x];
                    int ox = x - GRID_OFFSET, oz = z - GRID_OFFSET;
                    if (keep > 0f && ox >= 0 && oz >= 0 && ox < OLD_ALPHA_RES && oz < OLD_ALPHA_RES)
                        for (int l = 0; l < (int)Layer.Rock; l++)
                            w[l] = Mathf.Lerp(w[l], oldAlpha[oz, ox, l], keep);
                    w[(int)Layer.Rock] *= 1f - keep;

                    PaintLines(layout, w, p, true); // mud goes under every road

                    float road = 1f - Math.Ramp(roadDistance[z, x], 0.5f, 2f);
                    if (road > 0f && !InWater(layout, p)) Math.BlendLayer(w, (int)Layer.Road, road);

                    float pitFloor = layout.Pit.FloorRadius;
                    Math.BlendLayer(w, (int)Layer.Mud, 1f - Math.Ramp(Vector2.Distance(p, layout.Pit.Center), pitFloor - 0.5f, pitFloor + 1f));

                    PaintLines(layout, w, p, false);

                    float total = w.Sum();
                    for (int l = 0; l < layers; l++) alpha[z, x, l] = w[l] / total;
                }
            }
            return alpha;
        }

        private static void PaintLines(Layout layout, float[] weights, Vector2 p, bool mud)
        {
            foreach (PaintedLine line in layout.PaintedLines)
            {
                if ((line.Layer == Layer.Mud) != mud) continue;
                float share = Math.LineBlend(p, line.Points, line.HalfWidth, PAINT_EDGE);
                if (share > 0f && !InWater(layout, p)) Math.BlendLayer(weights, (int)line.Layer, share);
            }
        }

        private static TreeInstance[] ScatterTrees(Layout layout, float[,] heights, float[,] roadDistance)
        {
            var random = new System.Random(SEED);
            var trees = new List<TreeInstance>();
            float half = TERRAIN_SIZE * 0.5f;

            for (float cz = -half; cz < half; cz += TREE_CELL)
            {
                for (float cx = -half; cx < half; cx += TREE_CELL)
                {
                    // Draw every value first, so one rejected cell never shifts the next cell's numbers.
                    var p = new Vector2(cx + (float)random.NextDouble() * TREE_CELL, cz + (float)random.NextDouble() * TREE_CELL);
                    float roll = (float)random.NextDouble();
                    float kind = (float)random.NextDouble();
                    float size = 0.95f + (float)random.NextDouble() * 0.4f;
                    float width = (float)random.NextDouble() * 0.2f - 0.1f;
                    float spin = (float)random.NextDouble() * Mathf.PI * 2f;

                    if (!TreeAllowed(layout, heights, roadDistance, p)) continue;
                    float chance = Fbm(p, 140f, 3, 60f) > 0.46f ? 0.92f : 0.14f;
                    if (roll > chance) continue;

                    trees.Add(new TreeInstance
                    {
                        position = new Vector3((p.x + half) / TERRAIN_SIZE, 0f, (p.y + half) / TERRAIN_SIZE),
                        prototypeIndex = Array.FindIndex(TreeShare, share => kind <= share),
                        heightScale = size,
                        widthScale = size + width,
                        rotation = spin,
                        color = Color.white,
                        lightmapColor = Color.white,
                    });
                }
            }

            if (trees.Count <= MAX_TREES) return trees.ToArray();

            // Thin evenly rather than cutting off the north of the map.
            float stride = trees.Count / (float)MAX_TREES;
            var kept = new TreeInstance[MAX_TREES];
            for (int i = 0; i < MAX_TREES; i++) kept[i] = trees[Mathf.FloorToInt(i * stride)];
            return kept;
        }

        private static bool TreeAllowed(Layout layout, float[,] heights, float[,] roadDistance, Vector2 p)
        {
            if (Mathf.Abs(p.x) > 498f || Mathf.Abs(p.y) > 498f) return false;
            if (Math.InsideRect(p, layout.CompoundCenter, layout.CompoundSize)) return false;
            if (Math.RectDistance(p, layout.LakesideCenter, layout.LakesideSize) < 4f) return false;
            if (Vector2.Distance(p, layout.Pit.Center) < Math.FightPit.FOOT_RADIUS + 4f) return false;
            if (Vector2.Distance(p, layout.GangHouse) < GANG_HOUSE_TREE_CLEARANCE) return false;
            if (Vector2.Distance(p, CampCenter) < 62f) return false;
            foreach (Shape s in KeptRects)
                if (Math.RectDistance(p, s.Center, s.Size) < 6f) return false;

            Vector2Int g = Grid(p);
            float height = heights[g.y, g.x];
            if (height > 70f || height < 0.2f) return false;
            if (roadDistance[g.y, g.x] < 4f) return false;
            if (SlopeDegrees(heights, g.x, g.y) > 32f) return false;

            if (Math.PolylineDistance(p, layout.River, out _) < layout.RiverHalf + BANK_WIDTH * 0.5f + 4f) return false;
            if (Ellipse(layout, p) < 1.2f) return false;

            foreach ((string id, _) in layout.Pads)
            {
                JObject f = layout.Features[id];
                if (Math.RectDistance(p, new Vector2((float)f["x"], (float)f["z"]), new Vector2((float)f["w"], (float)f["d"])) < 4f)
                    return false;
            }

            JObject docks = layout.Features["docks"];
            if (Math.RectDistance(p, Position(docks), new Vector2((float)docks["w"], (float)docks["d"])) < 4f) return false;

            foreach (Vector2[] corridor in layout.Corridors)
                if (Math.PolylineDistance(p, corridor, out _) < 8f) return false;

            if (Math.PolylineDistance(p, layout.Cleft, out _) < CLEFT_TREE_CLEARANCE) return false;

            foreach ((Vector2 center, float radius) in layout.Clearings)
                if (Vector2.Distance(p, center) < radius) return false;

            return Vector2.Distance(p, layout.Fire) >= 7f;
        }

        private static bool TryLoadLayout(out Layout layout, out string problem)
        {
            layout = null;
            problem = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs/World/Areas/area-001/layout.json"));
            if (!File.Exists(path))
            {
                problem = $"Layout not found at {path}";
                return false;
            }

            Dictionary<string, JObject> features = JObject.Parse(File.ReadAllText(path))["features"]
                .Cast<JObject>().ToDictionary(f => (string)f["id"]);

            string[] required =
            {
                "mountains", "river", "lake", "ford", "hunter-fire", "ruins", "quarry", "zone-4", "wood-camp", "docks",
                "arrival-road", "quarry-road", "wood-road", "wilderness-trail", "quarry-cleft", "cave", "hunting-camp",
            };
            string missing = required.FirstOrDefault(id => !features.ContainsKey(id));
            if (missing != null)
            {
                problem = $"Layout has no feature '{missing}'";
                return false;
            }

            if (!(features["quarry-cleft"]["points"] is JArray cleftPoints) || cleftPoints.Count < 2 || features["quarry-cleft"]["width"] == null)
            {
                problem = "Layout has no usable feature 'quarry-cleft'";
                return false;
            }

            if (!features.TryGetValue(COMPOUND_FEATURE, out JObject compound) || !Area1BlockoutBuilder.HasPosition(compound)
                || !Area1BlockoutBuilder.IsNumber(compound["w"]) || !Area1BlockoutBuilder.IsNumber(compound["d"])
                || (float)compound["w"] <= 0f || (float)compound["d"] <= 0f)
            {
                problem = $"Layout has no usable feature '{COMPOUND_FEATURE}'";
                return false;
            }

            foreach (string id in new[] { FIGHT_PIT_FEATURE, LAKESIDE_FEATURE })
            {
                if (features.TryGetValue(id, out JObject zone) && Area1BlockoutBuilder.HasPosition(zone)
                    && Area1BlockoutBuilder.IsNumber(zone["w"]) && Area1BlockoutBuilder.IsNumber(zone["d"]) && (float)zone["w"] > 0f && (float)zone["d"] > 0f) continue;
                problem = $"Layout has no usable feature '{id}'";
                return false;
            }

            if (!Area1SitesBuilder.IsUsableFightPit(features[FIGHT_PIT_FEATURE]))
            {
                problem = $"Layout has no usable feature '{FIGHT_PIT_FEATURE}'";
                return false;
            }

            foreach (string id in new[] { FORT_TOWER_FEATURE, GANG_HOUSE_FEATURE })
            {
                if (features.TryGetValue(id, out JObject point) && Area1BlockoutBuilder.HasPosition(point)) continue;
                problem = $"Layout has no usable feature '{id}'";
                return false;
            }

            if (!TryReadPaintedLines(features, out PaintedLine[] paintedLines, out problem)) return false;

            Vector2[] wall = Points(features["mountains"]);
            float wallCentre = wall.Max(p => Mathf.Abs(p.x));
            JObject lake = features["lake"];
            Vector2 fire = Position(features["hunter-fire"]);
            JObject quarry = features["quarry"];
            JObject ruins2 = features["zone-4"];
            Vector2 quarryCenter = Position(quarry);
            Vector2 quarrySize = new Vector2((float)quarry["w"], (float)quarry["d"]);
            Vector2 corner = quarryCenter + new Vector2(quarrySize.x, -quarrySize.y) * 0.5f;
            JObject lakeside = features[LAKESIDE_FEATURE];

            layout = new Layout
            {
                Features = features,
                WallInner = wallCentre - (float)features["mountains"]["width"] * 0.5f,
                GapMin = Mathf.Min(wall[0].y, wall[wall.Length - 1].y),
                GapMax = Mathf.Max(wall[0].y, wall[wall.Length - 1].y),
                River = Points(features["river"]),
                RiverHalf = (float)features["river"]["width"] * 0.5f,
                LakeCenter = Position(lake),
                LakeRadius = new Vector2((float)lake["w"], (float)lake["d"]) * 0.5f,
                Pads = new[] { ("ruins", 35f), ("quarry", QUARRY_PAD), ("zone-4", RUINS2_PAD), ("wood-camp", 6f) },
                Roads = new[] { "arrival-road", "quarry-road", "wood-road", "wilderness-trail" }.Select(id => new Road
                {
                    Points = Points(features[id]),
                    HalfWidth = (float)features[id]["width"] * 0.5f,
                }).ToArray(),
                PaintedLines = paintedLines,
                CompoundCenter = Position(compound),
                CompoundSize = new Vector2((float)compound["w"], (float)compound["d"]),
                Pit = Area1SitesBuilder.ReadFightPit(features[FIGHT_PIT_FEATURE], features[FORT_TOWER_FEATURE]),
                GangHouse = Position(features[GANG_HOUSE_FEATURE]),
                LakesideCenter = Position(lakeside),
                LakesideSize = new Vector2((float)lakeside["w"], (float)lakeside["d"]),
                Corridors = new[]
                {
                    new[] { TentsEye, fire },
                    new[] { Position(features["docks"]), fire },
                    new[] { TentsEye, Position(features["ruins"]) },
                },
                Fire = fire,
                Ford = Position(features["ford"]),
                QuarryCenter = quarryCenter,
                QuarrySize = quarrySize,
                Ruins2Center = Position(ruins2),
                Ruins2Size = new Vector2((float)ruins2["w"], (float)ruins2["d"]),
                QuarryCliff = new[]
                {
                    // Short of the pad's far corners: the crest rounds past each end, and would otherwise wrap around them.
                    new Vector2(corner.x - quarrySize.x + CLIFF_CREST, corner.y), corner, new Vector2(corner.x, corner.y + quarrySize.y - CLIFF_CREST),
                },
                Cleft = Points(features["quarry-cleft"]),
                CleftHalf = (float)features["quarry-cleft"]["width"] * 0.5f,
                Clearings = new[]
                {
                    (Position(features["cave"]), CAVE_CLEARING),
                    (Position(features["hunting-camp"]), HUNTING_CAMP_CLEARING),
                },
            };
            return true;
        }

        /// <summary>In paint order within each pass; the Company Road goes last, so it stays clean over everything.</summary>
        private static bool TryReadPaintedLines(Dictionary<string, JObject> features, out PaintedLine[] lines, out string problem)
        {
            lines = null;
            problem = null;
            var wanted = features.Keys.Where(id => id.StartsWith(PATH_PREFIX, StringComparison.Ordinal)).Select(id => (id, layer: Layer.Mud)).ToList();
            wanted.Add((DELIVERY_ROAD, Layer.Road));
            wanted.Add((COMPANY_ROAD, Layer.Cobble));

            var read = new List<PaintedLine>();
            foreach ((string id, Layer layer) in wanted)
            {
                if (!features.TryGetValue(id, out JObject line) || !Area1BlockoutBuilder.HasPoints(line)
                    || !Area1BlockoutBuilder.IsNumber(line["width"]) || (float)line["width"] <= 0f)
                {
                    problem = $"Layout has no usable feature '{id}'";
                    return false;
                }
                read.Add(new PaintedLine { Layer = layer, Points = Points(line), HalfWidth = (float)line["width"] * 0.5f });
            }

            lines = read.ToArray();
            return true;
        }

        private static Vector2[] Line(params float[] xz)
        {
            var points = new Vector2[xz.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
            return points;
        }

        private static Vector2[] Points(JObject feature) =>
            feature["points"].Select(p => new Vector2((float)p[0], (float)p[1])).ToArray();

        private static Vector2 Position(JObject feature) => new Vector2((float)feature["x"], (float)feature["z"]);

        private static Vector2 World(float x, float z) =>
            new Vector2(x * SAMPLE_STEP - TERRAIN_SIZE * 0.5f, z * SAMPLE_STEP - TERRAIN_SIZE * 0.5f);

        private static Vector2Int Grid(Vector2 p) => new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt((p.x + TERRAIN_SIZE * 0.5f) / SAMPLE_STEP), 0, HEIGHT_RES - 1),
            Mathf.Clamp(Mathf.RoundToInt((p.y + TERRAIN_SIZE * 0.5f) / SAMPLE_STEP), 0, HEIGHT_RES - 1));

        private static float SampleHeight(float[,] heights, Vector2 p)
        {
            Vector2Int g = Grid(p);
            return heights[g.y, g.x];
        }

        private static float SlopeDegrees(float[,] heights, int x, int z)
        {
            int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, HEIGHT_RES - 1);
            int z0 = Mathf.Max(z - 1, 0), z1 = Mathf.Min(z + 1, HEIGHT_RES - 1);
            float dx = (heights[z, x1] - heights[z, x0]) / ((x1 - x0) * SAMPLE_STEP);
            float dz = (heights[z1, x] - heights[z0, x]) / ((z1 - z0) * SAMPLE_STEP);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        private static float Fbm(Vector2 p, float scale, int octaves, float offset)
        {
            float sum = 0f, amplitude = 1f, total = 0f, frequency = 1f / scale;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise((p.x + 1000f) * frequency + offset, (p.y + 1000f) * frequency + offset) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return sum / total;
        }

        private static List<Vector2> Resample(Vector2[] points, float step)
        {
            var result = new List<Vector2> { points[0] };
            for (int i = 1; i < points.Length; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / step));
                for (int n = 1; n <= count; n++) result.Add(Vector2.Lerp(points[i - 1], points[i], n / (float)count));
            }
            return result;
        }

        private static float[] BoxBlur(float[] values, int radius)
        {
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                int from = Mathf.Max(i - radius, 0), to = Mathf.Min(i + radius, values.Length - 1);
                float sum = 0f;
                for (int n = from; n <= to; n++) sum += values[n];
                result[i] = sum / (to - from + 1);
            }
            return result;
        }
    }
}
