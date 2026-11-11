using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>Conservative oriented footprint checks shared by the worker layout and EditMode tests.</summary>
    public static class Area1WorkerGeometry
    {
        public static Vector2[] Corners(Vector2 at, Vector2 size, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var result = new Vector2[4];
            Vector2[] signs = { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 v = q * new Vector3(signs[i].x * size.x * 0.5f, 0, signs[i].y * size.y * 0.5f);
                result[i] = at + new Vector2(v.x, v.z);
            }
            return result;
        }

        public static bool Overlap(Vector2[] a, Vector2[] b, float gap = 0f)
        {
            foreach (Vector2[] polygon in new[] { a, b })
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 edge = polygon[(i + 1) % polygon.Length] - polygon[i];
                    Vector2 axis = new Vector2(-edge.y, edge.x).normalized;
                    Project(a, axis, out float amin, out float amax);
                    Project(b, axis, out float bmin, out float bmax);
                    if (amax + gap <= bmin || bmax + gap <= amin) return false;
                }
            return true;
        }

        public static bool Corridor(Vector2[] footprint, Vector2 a, Vector2 b, float width)
        {
            Vector2 along = b - a;
            if (along.sqrMagnitude < 0.0001f) return true;
            float yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            return Overlap(footprint, Corners((a + b) * 0.5f, new Vector2(width, along.magnitude), yaw));
        }

        private static void Project(Vector2[] p, Vector2 axis, out float min, out float max)
        {
            min = float.PositiveInfinity; max = float.NegativeInfinity;
            foreach (Vector2 v in p) { float dot = Vector2.Dot(v, axis); min = Mathf.Min(min, dot); max = Mathf.Max(max, dot); }
        }
    }
}
