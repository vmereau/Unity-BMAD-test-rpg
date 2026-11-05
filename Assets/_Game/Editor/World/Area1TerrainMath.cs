using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>Pure shaping helpers behind <see cref="Area1TerrainBuilder"/>.</summary>
    public static class Area1TerrainMath
    {
        private const float GAP_TOLERANCE = 1f;

        /// <summary>Smooth ramp: 0 at or below <paramref name="from"/>, 1 at or above <paramref name="to"/>.</summary>
        public static float Ramp(float value, float from, float to)
        {
            if (to <= from) return value >= to ? 1f : 0f;
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Distance to a polyline, plus the 0..1 position along its nearest segment's whole length.</summary>
        public static float PolylineDistance(Vector2 point, IReadOnlyList<Vector2> line, out float along)
        {
            along = 0f;
            if (line == null || line.Count == 0) return float.PositiveInfinity;
            if (line.Count == 1) return Vector2.Distance(point, line[0]);

            float total = 0f;
            for (int i = 1; i < line.Count; i++) total += Vector2.Distance(line[i - 1], line[i]);

            float best = float.PositiveInfinity;
            float walked = 0f;
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 a = line[i - 1];
                Vector2 ab = line[i] - a;
                float length = ab.magnitude;
                if (length > 0f)
                {
                    float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / (length * length));
                    float distance = Vector2.Distance(point, a + ab * t);
                    if (distance < best)
                    {
                        best = distance;
                        along = total > 0f ? (walked + t * length) / total : 0f;
                    }
                }
                walked += length;
            }
            return best;
        }

        /// <summary>Distance from a point to an axis-aligned rectangle; 0 inside it.</summary>
        public static float RectDistance(Vector2 point, Vector2 center, Vector2 size)
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - center.x) - size.x * 0.5f, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - center.y) - size.y * 0.5f, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Limits a road profile to <paramref name="maxStep"/> per sample in both directions. Anchored samples never
        /// move; free samples are pulled towards them, so a road leaves fixed ground without a step when there is room to climb.
        /// </summary>
        public static void LimitGrade(float[] heights, bool[] anchored, float maxStep)
        {
            for (int i = 1; i < heights.Length; i++)
                if (!anchored[i]) heights[i] = Mathf.Clamp(heights[i], heights[i - 1] - maxStep, heights[i - 1] + maxStep);
            for (int i = heights.Length - 2; i >= 0; i--)
                if (!anchored[i]) heights[i] = Mathf.Clamp(heights[i], heights[i + 1] - maxStep, heights[i + 1] + maxStep);
        }

        /// <summary>
        /// Height of a cliff block: climbs <paramref name="floorRise"/> from its floors, levels at <paramref name="top"/>,
        /// then falls at <paramref name="slope"/> past <paramref name="crest"/> metres from the floor edge.
        /// </summary>
        public static float CliffSurface(float floorRise, float rimDistance, float top, float crest, float slope) =>
            Mathf.Min(Mathf.Min(floorRise, top), top - slope * Mathf.Max(rimDistance - crest, 0f));

        /// <summary>Lowers <paramref name="height"/> to a flat floor within <paramref name="halfWidth"/>, with walls at <paramref name="slope"/> beyond it.</summary>
        public static float CutCleft(float height, float distance, float floor, float halfWidth, float slope) =>
            Mathf.Min(height, floor + slope * Mathf.Max(distance - halfWidth, 0f));

        /// <summary>
        /// Cuts a polyline into equal panels of at most <paramref name="panelLength"/>, leaving out the stretch within
        /// <paramref name="gapHalfWidth"/> of <paramref name="gapCentre"/> along any segment that passes through that point.
        /// </summary>
        public static List<(Vector2 a, Vector2 b)> CutPanels(IReadOnlyList<Vector2> line, float panelLength, Vector2 gapCentre, float gapHalfWidth)
        {
            var panels = new List<(Vector2 a, Vector2 b)>();
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                float length = Vector2.Distance(a, b);
                if (length <= 0f) continue;

                Vector2 direction = (b - a) / length;
                float along = Vector2.Dot(gapCentre - a, direction);
                bool onSegment = gapHalfWidth > 0f && along >= 0f && along <= length
                    && Vector2.Distance(a + direction * along, gapCentre) < GAP_TOLERANCE;

                if (!onSegment)
                {
                    AddPanels(panels, a, b, panelLength);
                    continue;
                }
                AddPanels(panels, a, a + direction * Mathf.Max(along - gapHalfWidth, 0f), panelLength);
                AddPanels(panels, a + direction * Mathf.Min(along + gapHalfWidth, length), b, panelLength);
            }
            return panels;
        }

        private static void AddPanels(List<(Vector2 a, Vector2 b)> panels, Vector2 a, Vector2 b, float panelLength)
        {
            float length = Vector2.Distance(a, b);
            if (length <= 0f) return;
            int count = Mathf.CeilToInt(length / panelLength);
            for (int n = 0; n < count; n++)
                panels.Add((Vector2.Lerp(a, b, n / (float)count), Vector2.Lerp(a, b, (n + 1) / (float)count)));
        }
    }
}
