using UnityEngine;

namespace Game.Stealth
{
    /// <summary>
    /// Pure line-segment builders for the detection debug views. Shared by the <c>EntityPerception</c> gizmos and
    /// the runtime <c>StealthDebugOverlay</c> (GL lines). Callers own the buffers — nothing allocates per call.
    /// Every two consecutive points form one line segment. Shapes are flattened on XZ at <c>origin.y</c>.
    /// Unit-tested in <c>StealthDebugGeometryTests</c>.
    /// </summary>
    public static class StealthDebugGeometry
    {
        private const float FULL_CIRCLE = 360f;

        private static readonly Color AwareColorLow = Color.green;
        private static readonly Color AwareColorMid = Color.yellow;
        private static readonly Color AwareColorHigh = Color.red;

        /// <summary>
        /// Fills <paramref name="buffer"/> with the cone outline: two side edges plus the arc
        /// (<paramref name="arcSegments"/> segments). <paramref name="viewAngle"/> ≥ 360 → a full circle without
        /// side edges. Writes only whole segments that fit; returns the number of points written.
        /// </summary>
        public static int BuildConeOutline(Vector3 origin, Vector3 forward, float range, float viewAngle,
            int arcSegments, Vector3[] buffer)
        {
            if (buffer == null || arcSegments < 1) return 0;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.000001f) forward = Vector3.forward;
            forward.Normalize();

            bool fullCircle = viewAngle >= FULL_CIRCLE;
            float span = fullCircle ? FULL_CIRCLE : Mathf.Max(0f, viewAngle);
            float start = -span * 0.5f;
            float step = span / arcSegments;
            int count = 0;

            if (!fullCircle)
            {
                if (!TryAddSegment(buffer, ref count, origin, ArcPoint(origin, forward, range, start))) return count;
                if (!TryAddSegment(buffer, ref count, origin, ArcPoint(origin, forward, range, -start))) return count;
            }

            Vector3 previous = ArcPoint(origin, forward, range, start);
            for (int i = 1; i <= arcSegments; i++)
            {
                Vector3 next = ArcPoint(origin, forward, range, start + step * i);
                if (!TryAddSegment(buffer, ref count, previous, next)) return count;
                previous = next;
            }
            return count;
        }

        /// <summary>Fills <paramref name="buffer"/> with a closed XZ circle of <paramref name="segments"/> segments.</summary>
        public static int BuildCircle(Vector3 center, float radius, int segments, Vector3[] buffer) =>
            BuildConeOutline(center, Vector3.forward, radius, FULL_CIRCLE, segments, buffer);

        /// <summary>Green at 0 → yellow at <paramref name="suspicionThreshold"/> → red at 1.</summary>
        public static Color AwarenessColor(float awareness, float suspicionThreshold)
        {
            if (suspicionThreshold <= 0f || suspicionThreshold >= 1f)
                return Color.Lerp(AwareColorLow, AwareColorHigh, awareness);
            return awareness < suspicionThreshold
                ? Color.Lerp(AwareColorLow, AwareColorMid, awareness / suspicionThreshold)
                : Color.Lerp(AwareColorMid, AwareColorHigh, (awareness - suspicionThreshold) / (1f - suspicionThreshold));
        }

        private static Vector3 ArcPoint(Vector3 origin, Vector3 forward, float range, float angleDeg) =>
            origin + Quaternion.AngleAxis(angleDeg, Vector3.up) * forward * range;

        private static bool TryAddSegment(Vector3[] buffer, ref int count, Vector3 a, Vector3 b)
        {
            if (count + 2 > buffer.Length) return false;
            buffer[count++] = a;
            buffer[count++] = b;
            return true;
        }
    }
}
