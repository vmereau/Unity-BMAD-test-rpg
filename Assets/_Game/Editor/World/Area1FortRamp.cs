using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Grades the cart ramp to the fort along the path-fort-ramp line of layout.json, and the goat track (path-goat-track) into
    /// Ruins 2. The hill is a hand sculpt, so this edits the
    /// live terrain like a sculpt pass: run Tools/World/Capture Area 1 Terrain Sculpt afterwards, or the terrain tool refuses to
    /// run. Running it again changes nothing, but it only reshapes the ground around the current line: move the line and the old
    /// cut stays in the sculpt until it's smoothed by hand.
    /// </summary>
    public static class Area1FortRamp
    {
        private const string TAG = "[Area1FortRamp]";
        private const string RAMP_FEATURE = "path-fort-ramp";
        internal const string GOAT_TRACK_FEATURE = "path-goat-track";
        private const string MUD_LAYER = "Mud";
        internal const string TERRAIN_NAME = "Terrain_StartingArea";

        private const float EDGE_MARGIN = 0.5f;  // heightmap samples are about 1 m apart, so the flat runs past the road's edge
        private const float MAX_ANGLE = 17.6f;   // the warehouse cart ramps
        private const float REACH = 9f;          // past the road's edge, where the bank clears everything on the hill
        private const float GRADE_RUN = 2f;      // metres of road the steepest grade is measured over
        private const float GRADE_STEP = 0.5f;
        private const float GOAT_MAX_ANGLE = 35f;     // steepest segment of the goat track: steep but walkable
        private const float GOAT_MAX_STEEPEST = 40f;  // under the player's 45 degree slope limit, with room for the bank
        private const float TREE_CLEARANCE = 1.5f;    // past the track's edge
        private const float PAINT_EDGE = 1f;          // as the terrain tool's painted lines

        [MenuItem("Tools/World/Grade Area 1 Fort Ramp")]
        public static void Grade() => GradeLine(RAMP_FEATURE, MAX_ANGLE, MAX_ANGLE, profile: false, clearTrack: false, "Grade Area 1 Fort Ramp");

        /// <summary>
        /// The quest-002 goat track: from the forest edge south-east of the quarry, over the Ruins 2 rim and down onto the pad. Its
        /// layout feature lists one height per point, because it rises to the rim and falls again. Also paints it in Mud and clears
        /// trees from it, which the terrain tool repeats on a rerun.
        /// </summary>
        [MenuItem("Tools/World/Grade Area 1 Goat Track")]
        public static void GradeGoatTrack() => GradeLine(GOAT_TRACK_FEATURE, GOAT_MAX_ANGLE, GOAT_MAX_STEEPEST, profile: true, clearTrack: true, "Grade Area 1 Goat Track");

        private static void GradeLine(string featureId, float maxAngle, float maxSteepest, bool profile, bool clearTrack, string undoName)
        {
            if (EditorApplication.isPlaying)
            {
                GameLog.Error(TAG, "Leave play mode first; nothing changed.");
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.name != TERRAIN_NAME)
            {
                GameLog.Error(TAG, $"Need {TERRAIN_NAME} as the active terrain; nothing changed.");
                return;
            }

            if (!Area1BlockoutBuilder.TryReadLayout(out Dictionary<string, JObject> features, out string problem))
            {
                GameLog.Error(TAG, $"{problem}; nothing changed.");
                return;
            }

            if (!features.TryGetValue(featureId, out JObject ramp) || !Area1BlockoutBuilder.HasPoints(ramp) || !Area1BlockoutBuilder.IsNumber(ramp["width"]) || (float)ramp["width"] <= 0f)
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{featureId}'; nothing changed.");
                return;
            }

            Vector2[] line = Area1BlockoutBuilder.Points(ramp);
            float[] heights; // from the layout: read from the ground, each pass would shift them
            if (profile)
            {
                heights = ramp["heights"] is JArray list && list.Count == line.Length && list.All(Area1BlockoutBuilder.IsNumber)
                    ? list.Select(h => (float)h).ToArray() : null;
            }
            else
            {
                heights = Area1BlockoutBuilder.IsNumber(ramp["foot"]) && Area1BlockoutBuilder.IsNumber(ramp["head"])
                    ? new[] { (float)ramp["foot"], (float)ramp["head"] } : null;
            }
            if (heights == null)
            {
                GameLog.Error(TAG, profile ? $"'{featureId}' needs one number in 'heights' per point; nothing changed."
                    : $"'{featureId}' needs 'foot' and 'head'; nothing changed.");
                return;
            }
            float halfWidth = (float)ramp["width"] * 0.5f + EDGE_MARGIN;
            HashSet<int> drops = Drops(ramp);

            float length = 0f, mean = 0f;
            for (int i = 1; i < line.Length; i++)
            {
                float run = Vector2.Distance(line[i - 1], line[i]);
                length += run;
                if (profile && !drops.Contains(i)) mean = Mathf.Max(mean, Mathf.Atan2(Mathf.Abs(heights[i] - heights[i - 1]), run) * Mathf.Rad2Deg);
            }
            if (!profile) mean = Mathf.Atan2(Mathf.Abs(heights[1] - heights[0]), length) * Mathf.Rad2Deg;
            if (mean > maxAngle)
            {
                GameLog.Error(TAG, $"'{featureId}' would {(profile ? "climb" : "average")} {mean:F1} degrees{(profile ? " on its steepest segment" : "")}, over {maxAngle}; nothing changed.");
                return;
            }

            TerrainData data = terrain.terrainData;
            var undone = new List<Object> { data }; // splat weights live in separate alphamap textures
            if (clearTrack) undone.AddRange(data.alphamapTextures);
            Undo.RegisterCompleteObjectUndo(undone.ToArray(), undoName);
            Apply(terrain, line, halfWidth, profile ? heights : null, heights[0], heights[heights.Length - 1]);
            foreach (int drop in drops)
                if (drop > 0 && drop < line.Length) CutDropPit(terrain, line[drop - 1], line[drop], heights[drop]);
            if (clearTrack)
            {
                int removed = ClearTrees(terrain, line, halfWidth + TREE_CLEARANCE);
                bool painted = PaintMud(terrain, line, halfWidth - EDGE_MARGIN);
                GameLog.Info(TAG, $"'{featureId}': {removed} trees removed, {(painted ? "painted in Mud" : "no Mud layer, not painted")}.");
            }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);

            float steepest = Steepest(terrain, line, drops);
            GameLog.Info(TAG, $"Graded '{featureId}', {length:F1} m from y {heights[0]:F2} to y {heights[heights.Length - 1]:F2}: {mean:F1} degrees {(profile ? "on the steepest segment" : "on average")}, {steepest:F1} at the steepest{(drops.Count > 0 ? $" (drop segments {string.Join(", ", drops)} not counted)" : "")}. Capture the sculpt next.");
            if (steepest > maxSteepest) GameLog.Warn(TAG, $"The steepest {GRADE_RUN} m is over {maxSteepest} degrees: round the line's bends or lengthen it.");
        }

        private const float DROP_PIT_START = 1.5f;      // metres past the drop's top point: the ledge itself stays
        private const float DROP_PIT_LENGTH = 7f;
        private const float DROP_PIT_HALF_WIDTH = 6f;   // wider than the ring gap: the drop blockers stand on its edges

        /// <summary>
        /// Cuts the pit behind a drop down to its foot height: a clean landing floor in front of the ledge. The terrain face alone
        /// is still jump-climbable (a CharacterController counts terrain contacts as ground at any steepness): only vertical box
        /// faces would make the drop one-way, and StartingTown has none there by design (the pit can be climbed back out of).
        /// </summary>
        private static void CutDropPit(Terrain terrain, Vector2 top, Vector2 foot, float floor)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector2 cell = new Vector2(data.size.x, data.size.z) / (data.heightmapResolution - 1);
            float reach = DROP_PIT_START + DROP_PIT_LENGTH + DROP_PIT_HALF_WIDTH;
            int last = data.heightmapResolution - 1;
            int fromX = Mathf.Clamp(Mathf.FloorToInt((top.x - reach - origin.x) / cell.x), 0, last), toX = Mathf.Clamp(Mathf.CeilToInt((top.x + reach - origin.x) / cell.x), 0, last);
            int fromZ = Mathf.Clamp(Mathf.FloorToInt((top.y - reach - origin.z) / cell.y), 0, last), toZ = Mathf.Clamp(Mathf.CeilToInt((top.y + reach - origin.z) / cell.y), 0, last);
            float[,] heights = data.GetHeights(fromX, fromZ, toX - fromX + 1, toZ - fromZ + 1);
            for (int z = 0; z <= toZ - fromZ; z++)
            {
                for (int x = 0; x <= toX - fromX; x++)
                {
                    var point = new Vector2(origin.x + (fromX + x) * cell.x, origin.z + (fromZ + z) * cell.y);
                    float ground = heights[z, x] * data.size.y + origin.y;
                    float cut = Area1TerrainMath.DropPitHeight(point, top, foot, DROP_PIT_START, DROP_PIT_LENGTH, DROP_PIT_HALF_WIDTH, floor, ground);
                    heights[z, x] = (cut - origin.y) / data.size.y;
                }
            }
            data.SetHeights(fromX, fromZ, heights);
        }

        /// <summary>Removes tree instances within <paramref name="reach"/> of the line. Returns how many went.</summary>
        private static int ClearTrees(Terrain terrain, Vector2[] line, float reach)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            TreeInstance[] trees = data.treeInstances;
            var kept = new List<TreeInstance>(trees.Length);
            foreach (TreeInstance tree in trees)
            {
                var point = new Vector2(origin.x + tree.position.x * data.size.x, origin.z + tree.position.z * data.size.z);
                if (Area1TerrainMath.PolylineDistance(point, line, out _) >= reach) kept.Add(tree);
            }
            if (kept.Count != trees.Length) data.SetTreeInstances(kept.ToArray(), true);
            return trees.Length - kept.Count;
        }

        /// <summary>Paints the line in the Mud layer, as the terrain tool does for every path-* line.</summary>
        private static bool PaintMud(Terrain terrain, Vector2[] line, float halfWidth)
        {
            TerrainData data = terrain.terrainData;
            int mud = System.Array.FindIndex(data.terrainLayers, l => l != null && l.name.Contains(MUD_LAYER));
            if (mud < 0) return false;

            Vector3 origin = terrain.transform.position;
            Vector2 cell = new Vector2(data.size.x, data.size.z) / data.alphamapResolution;
            Vector2 min = line[0], max = line[0];
            foreach (Vector2 point in line)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            float reach = halfWidth + PAINT_EDGE;
            int last = data.alphamapResolution - 1;
            int fromX = Mathf.Clamp(Mathf.FloorToInt((min.x - reach - origin.x) / cell.x), 0, last), toX = Mathf.Clamp(Mathf.CeilToInt((max.x + reach - origin.x) / cell.x), 0, last);
            int fromZ = Mathf.Clamp(Mathf.FloorToInt((min.y - reach - origin.z) / cell.y), 0, last), toZ = Mathf.Clamp(Mathf.CeilToInt((max.y + reach - origin.z) / cell.y), 0, last);

            float[,,] alpha = data.GetAlphamaps(fromX, fromZ, toX - fromX + 1, toZ - fromZ + 1);
            int layers = alpha.GetLength(2);
            var weights = new float[layers];
            for (int z = 0; z <= toZ - fromZ; z++)
            {
                for (int x = 0; x <= toX - fromX; x++)
                {
                    var point = new Vector2(origin.x + (fromX + x + 0.5f) * cell.x, origin.z + (fromZ + z + 0.5f) * cell.y);
                    float share = Area1TerrainMath.LineBlend(point, line, halfWidth, PAINT_EDGE);
                    if (share <= 0f) continue;
                    for (int l = 0; l < layers; l++) weights[l] = alpha[z, x, l];
                    Area1TerrainMath.BlendLayer(weights, mud, share);
                    for (int l = 0; l < layers; l++) alpha[z, x, l] = weights[l];
                }
            }
            data.SetAlphamaps(fromX, fromZ, alpha);
            return true;
        }

        /// <summary>
        /// The feature's "drops": indices of points that end a deliberate drop (a cliff the player falls down and can't climb back up),
        /// so the segment from the point before is left out of the grade checks.
        /// </summary>
        internal static HashSet<int> Drops(JObject feature) =>
            feature["drops"] is JArray list
                ? new HashSet<int>(list.Where(Area1BlockoutBuilder.IsNumber).Select(d => (int)d))
                : new HashSet<int>();

        private static float Steepest(Terrain terrain, Vector2[] line, HashSet<int> drops)
        {
            var points = new List<Vector2>();
            var segments = new List<int>(); // the segment (index of its end point) each sample lies on
            for (int i = 1; i < line.Length; i++)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(line[i - 1], line[i]) / GRADE_STEP);
                for (int step = 0; step < steps; step++)
                {
                    points.Add(Vector2.Lerp(line[i - 1], line[i], step / (float)steps));
                    segments.Add(i);
                }
            }
            points.Add(line[line.Length - 1]);
            segments.Add(line.Length - 1);

            int span = Mathf.RoundToInt(GRADE_RUN / GRADE_STEP);
            float steepest = 0f;
            for (int i = span; i < points.Count; i++)
            {
                bool acrossDrop = false; // a run with a sample on a drop segment measures the cliff, not the track
                for (int s = segments[i - span]; s <= segments[i] && !acrossDrop; s++) acrossDrop = drops.Contains(s);
                if (acrossDrop) continue;
                float rise = Mathf.Abs(Area1BlockoutBuilder.Ground(terrain, points[i]) - Area1BlockoutBuilder.Ground(terrain, points[i - span]));
                steepest = Mathf.Max(steepest, Mathf.Atan2(rise, Vector2.Distance(points[i], points[i - span])) * Mathf.Rad2Deg);
            }
            return steepest;
        }

        private static void Apply(Terrain terrain, Vector2[] line, float halfWidth, float[] profile, float foot, float head)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector2 cell = new Vector2(data.size.x, data.size.z) / (data.heightmapResolution - 1);

            Vector2 min = line[0], max = line[0];
            foreach (Vector2 point in line)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            float reach = halfWidth + REACH;
            int last = data.heightmapResolution - 1;
            int fromX = Mathf.Clamp(Mathf.FloorToInt((min.x - reach - origin.x) / cell.x), 0, last), toX = Mathf.Clamp(Mathf.CeilToInt((max.x + reach - origin.x) / cell.x), 0, last);
            int fromZ = Mathf.Clamp(Mathf.FloorToInt((min.y - reach - origin.z) / cell.y), 0, last), toZ = Mathf.Clamp(Mathf.CeilToInt((max.y + reach - origin.z) / cell.y), 0, last);

            float[,] heights = data.GetHeights(fromX, fromZ, toX - fromX + 1, toZ - fromZ + 1);
            for (int z = 0; z <= toZ - fromZ; z++)
            {
                for (int x = 0; x <= toX - fromX; x++)
                {
                    var point = new Vector2(origin.x + (fromX + x) * cell.x, origin.z + (fromZ + z) * cell.y);
                    float distance;
                    float target = profile != null
                        ? Area1TerrainMath.GradeAlongProfile(point, line, profile, out distance)
                        : Area1TerrainMath.GradeAlong(point, line, foot, head, out distance);
                    float ground = heights[z, x] * data.size.y + origin.y;
                    heights[z, x] = (Area1TerrainMath.GradedHeight(ground, distance, target, halfWidth) - origin.y) / data.size.y;
                }
            }
            data.SetHeights(fromX, fromZ, heights);
        }
    }
}
