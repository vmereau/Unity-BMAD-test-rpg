using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure bow-accuracy math. Aim progress runs 0 (full spread) → 1 (perfect shot); the spread is an angular cone
    /// (degrees, half-angle) around the crosshair ray.
    /// </summary>
    public static class BowSpreadMath
    {
        /// <summary>
        /// Advances the aim progress: shrinks over <paramref name="shrinkDuration"/> while still, widens back at
        /// <paramref name="widenMultiplier"/> × that rate while moving. Clamped to [0, 1].
        /// </summary>
        public static float StepProgress(float progress, float dt, float shrinkDuration, bool moving, float widenMultiplier)
        {
            if (dt <= 0f) return Mathf.Clamp01(progress);
            if (shrinkDuration <= 0f) return moving ? (widenMultiplier > 0f ? 0f : Mathf.Clamp01(progress)) : 1f;
            float rate = dt / shrinkDuration;
            float next = moving ? progress - rate * Mathf.Max(0f, widenMultiplier) : progress + rate;
            return Mathf.Clamp01(next);
        }

        /// <summary>Cone half-angle in degrees: <paramref name="maxAngle"/> at progress 0, 0 at progress 1.</summary>
        public static float SpreadAngle(float progress, float maxAngle) =>
            Mathf.Max(0f, maxAngle) * (1f - Mathf.Clamp01(progress));

        /// <summary>
        /// Direction inside the cone of half-angle <paramref name="angleDeg"/> around <paramref name="direction"/>,
        /// uniform over the cone's cross-section disk. <paramref name="u"/>, <paramref name="v"/> ∈ [0, 1]:
        /// u = radius (u = 0 → the centre ray), v = angle around the axis.
        /// </summary>
        public static Vector3 SampleCone(Vector3 direction, float angleDeg, float u, float v)
        {
            if (direction.sqrMagnitude < 1e-8f) return direction;
            Vector3 dir = direction.normalized;
            if (angleDeg <= 0f) return dir;

            float radius = angleDeg * Mathf.Sqrt(Mathf.Clamp01(u));
            float theta = 2f * Mathf.PI * Mathf.Clamp01(v);

            // Any basis perpendicular to dir; pick the reference axis least aligned with it.
            Vector3 reference = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 right = Vector3.Cross(reference, dir).normalized;
            Vector3 up = Vector3.Cross(dir, right);
            Vector3 axis = right * Mathf.Cos(theta) + up * Mathf.Sin(theta);
            return Quaternion.AngleAxis(radius, axis) * dir;
        }

        /// <summary>
        /// <paramref name="direction"/> rotated toward <paramref name="axis"/> so it is at most
        /// <paramref name="angleDeg"/> away from it (unchanged when already inside). Keeps its length.
        /// </summary>
        public static Vector3 ClampToCone(Vector3 axis, Vector3 direction, float angleDeg)
        {
            if (axis.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f) return direction;
            float limit = Mathf.Max(0f, angleDeg);
            if (Vector3.Angle(axis, direction) <= limit) return direction;
            float magnitude = direction.magnitude;
            return Vector3.RotateTowards(axis.normalized, direction / magnitude, limit * Mathf.Deg2Rad, 0f) * magnitude;
        }

        /// <summary>
        /// Screen-space radius (pixels) of a cone of half-angle <paramref name="angleDeg"/> seen through a camera with
        /// vertical field of view <paramref name="verticalFovDeg"/>.
        /// </summary>
        public static float ScreenRadius(float angleDeg, float verticalFovDeg, float screenHeight)
        {
            if (angleDeg <= 0f || verticalFovDeg <= 0f || screenHeight <= 0f) return 0f;
            float halfFovTan = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            if (halfFovTan <= 0f) return 0f;
            return Mathf.Tan(Mathf.Min(angleDeg, 89f) * Mathf.Deg2Rad) / halfFovTan * screenHeight * 0.5f;
        }
    }
}
