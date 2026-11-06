using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Grades the cart ramp to the fort along the path-fort-ramp line of layout.json. The hill is a hand sculpt, so this edits the
    /// live terrain like a sculpt pass: run Tools/World/Capture Area 1 Terrain Sculpt afterwards, or the terrain tool refuses to
    /// run. Running it again changes nothing, but it only reshapes the ground around the current line: move the line and the old
    /// cut stays in the sculpt until it's smoothed by hand.
    /// </summary>
    public static class Area1FortRamp
    {
        private const string TAG = "[Area1FortRamp]";
        private const string RAMP_FEATURE = "path-fort-ramp";
        private const string TERRAIN_NAME = "Terrain_StartingArea";

        private const float EDGE_MARGIN = 0.5f;  // heightmap samples are about 1 m apart, so the flat runs past the road's edge
        private const float MAX_ANGLE = 17.6f;   // the warehouse cart ramps
        private const float REACH = 9f;          // past the road's edge, where the bank clears everything on the hill
        private const float GRADE_RUN = 2f;      // metres of road the steepest grade is measured over
        private const float GRADE_STEP = 0.5f;

        [MenuItem("Tools/World/Grade Area 1 Fort Ramp")]
        public static void Grade()
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

            if (!features.TryGetValue(RAMP_FEATURE, out JObject ramp) || !Area1BlockoutBuilder.HasPoints(ramp) || !Area1BlockoutBuilder.IsNumber(ramp["width"]) || (float)ramp["width"] <= 0f
                || !Area1BlockoutBuilder.IsNumber(ramp["foot"]) || !Area1BlockoutBuilder.IsNumber(ramp["head"]))
            {
                GameLog.Error(TAG, $"Layout has no usable feature '{RAMP_FEATURE}'; nothing changed.");
                return;
            }

            Vector2[] line = Area1BlockoutBuilder.Points(ramp);
            float halfWidth = (float)ramp["width"] * 0.5f + EDGE_MARGIN;
            float foot = (float)ramp["foot"], head = (float)ramp["head"]; // from the layout: read from the ground, each pass would shift them

            float length = 0f;
            for (int i = 1; i < line.Length; i++) length += Vector2.Distance(line[i - 1], line[i]);
            float mean = Mathf.Atan2(Mathf.Abs(head - foot), length) * Mathf.Rad2Deg;
            if (mean > MAX_ANGLE)
            {
                GameLog.Error(TAG, $"The ramp would average {mean:F1} degrees, over {MAX_ANGLE}; nothing changed.");
                return;
            }

            TerrainData data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Grade Area 1 Fort Ramp");
            Apply(terrain, line, halfWidth, foot, head);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);

            float steepest = Steepest(terrain, line);
            GameLog.Info(TAG, $"Graded {length:F1} m from y {foot:F2} to y {head:F2}: {mean:F1} degrees on average, {steepest:F1} at the steepest. Capture the sculpt next.");
            if (steepest > MAX_ANGLE) GameLog.Warn(TAG, $"The steepest {GRADE_RUN} m is over {MAX_ANGLE} degrees: round the line's bends or lengthen it.");
        }

        private static float Steepest(Terrain terrain, Vector2[] line)
        {
            var points = new List<Vector2>();
            for (int i = 1; i < line.Length; i++)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(line[i - 1], line[i]) / GRADE_STEP);
                for (int step = 0; step < steps; step++) points.Add(Vector2.Lerp(line[i - 1], line[i], step / (float)steps));
            }
            points.Add(line[line.Length - 1]);

            int span = Mathf.RoundToInt(GRADE_RUN / GRADE_STEP);
            float steepest = 0f;
            for (int i = span; i < points.Count; i++)
            {
                float rise = Mathf.Abs(Area1BlockoutBuilder.Ground(terrain, points[i]) - Area1BlockoutBuilder.Ground(terrain, points[i - span]));
                steepest = Mathf.Max(steepest, Mathf.Atan2(rise, Vector2.Distance(points[i], points[i - span])) * Mathf.Rad2Deg);
            }
            return steepest;
        }

        private static void Apply(Terrain terrain, Vector2[] line, float halfWidth, float foot, float head)
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
                    float target = Area1TerrainMath.GradeAlong(point, line, foot, head, out float distance);
                    float ground = heights[z, x] * data.size.y + origin.y;
                    heights[z, x] = (Area1TerrainMath.GradedHeight(ground, distance, target, halfWidth) - origin.y) / data.size.y;
                }
            }
            data.SetHeights(fromX, fromZ, heights);
        }
    }
}
