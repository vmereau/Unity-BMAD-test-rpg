using UnityEngine;

namespace Game.Stealth
{
    /// <summary>
    /// Pure vision-cone / awareness math used by <c>EntityPerception</c>. No Unity object access, no allocations,
    /// unit-tested in <c>StealthDetectionTests</c>. Line of sight is not part of this class (it needs physics):
    /// callers pass <c>visibility = 0</c> when the ray is blocked.
    /// </summary>
    public static class StealthDetection
    {
        private const float ZERO_VECTOR_SQR = 0.000001f;

        public static float EffectiveSightRange(float sightRange, bool sneaking, float sneakMultiplier) =>
            sneaking ? sightRange * sneakMultiplier : sightRange;

        public static float EffectiveProximityRadius(float radius, bool sneaking, float sneakMultiplier) =>
            sneaking ? radius * sneakMultiplier : radius;

        /// <summary>Angle in degrees (0..180) between <paramref name="forward"/> and <paramref name="toTarget"/> on the XZ plane.</summary>
        public static float AngleToTarget(Vector3 forward, Vector3 toTarget)
        {
            forward.y = 0f;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < ZERO_VECTOR_SQR || forward.sqrMagnitude < ZERO_VECTOR_SQR) return 0f;
            return Vector3.Angle(forward, toTarget);
        }

        /// <summary>
        /// Visibility 0..1. Inside the proximity radius → 1 at any angle. Outside the sight range or the cone → 0.
        /// Otherwise falls off linearly with distance (to <paramref name="edgeDistanceFactor"/> at the far edge)
        /// and with angle (to <paramref name="peripheralAngleFactor"/> at the cone's side edge).
        /// </summary>
        public static float ComputeVisibility(float distance, float angleDeg, float sightRange, float viewAngle,
            float proximityRadius, float edgeDistanceFactor, float peripheralAngleFactor)
        {
            if (proximityRadius > 0f && distance <= proximityRadius) return 1f; // radius 0 = no proximity sense
            float halfAngle = viewAngle * 0.5f;
            if (distance > sightRange || sightRange <= 0f || angleDeg > halfAngle) return 0f;

            float distanceFactor = Mathf.Lerp(1f, edgeDistanceFactor, distance / sightRange);
            float angleFactor = halfAngle > 0f ? Mathf.Lerp(1f, peripheralAngleFactor, angleDeg / halfAngle) : 1f;
            return distanceFactor * angleFactor;
        }

        /// <summary>
        /// Visibility for a non-hostile witness: <see cref="ComputeVisibility"/> while the target sneaks, 0 otherwise
        /// (witnesses only care about a sneaking player).
        /// </summary>
        public static float ComputeWitnessVisibility(bool targetSneaking, float distance, float angleDeg, float witnessRange,
            float viewAngle, float proximityRadius, float edgeDistanceFactor, float peripheralAngleFactor) =>
            targetSneaking
                ? ComputeVisibility(distance, angleDeg, witnessRange, viewAngle, proximityRadius, edgeDistanceFactor,
                    peripheralAngleFactor)
                : 0f;

        /// <summary>Awareness gained per second at the given visibility. 0 when <paramref name="fillTime"/> ≤ 0.</summary>
        public static float FillPerSecond(float visibility, float fillTime, bool sneaking, float sneakFillMultiplier)
        {
            if (fillTime <= 0f) return 0f;
            return visibility / fillTime * (sneaking ? sneakFillMultiplier : 1f);
        }

        /// <summary>Fills while visible with a positive rate, drains otherwise; clamped to [0, 1].</summary>
        public static float StepAwareness(float current, float fillPerSecond, float drainPerSecond, bool visible, float dt)
        {
            float next = visible && fillPerSecond > 0f
                ? current + fillPerSecond * dt
                : current - drainPerSecond * dt;
            return Mathf.Clamp01(next);
        }
    }
}
