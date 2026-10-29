using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure math for BowAimCorrection: yaw / pitch delta between two directions and the per-bone share of it.
    /// Covered by Tests/EditMode/AimCorrectionMathTests.
    /// </summary>
    public static class AimCorrectionMath
    {
        // Compared against normalized vectors: below ≈ 0.06° from vertical, yaw is undefined.
        private const float MIN_FLAT_SQR = 1e-6f;

        /// <summary>
        /// Degrees to turn <paramref name="current"/> onto <paramref name="desired"/>: x = yaw about world up
        /// (Unity sign — positive turns clockwise seen from above), y = pitch (positive = up). Yaw is 0 when either
        /// direction is (near) vertical or zero.
        /// </summary>
        public static Vector2 YawPitchDelta(Vector3 current, Vector3 desired)
        {
            Vector3 currentFlat = Flat(current.normalized);
            Vector3 desiredFlat = Flat(desired.normalized);
            float yaw = currentFlat.sqrMagnitude < MIN_FLAT_SQR || desiredFlat.sqrMagnitude < MIN_FLAT_SQR
                ? 0f
                : Vector3.SignedAngle(currentFlat, desiredFlat, Vector3.up);
            return new Vector2(yaw, Elevation(desired) - Elevation(current));
        }

        /// <summary>Elevation of a direction above the horizontal plane, in degrees (−90..90). Zero vector → 0.</summary>
        public static float Elevation(Vector3 direction)
        {
            if (direction.sqrMagnitude < MIN_FLAT_SQR) return 0f;
            return Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Share of the remaining (yaw, pitch) delta for the next bone: remaining spread over the bones left, each
        /// axis clamped to ±<paramref name="maxPerBone"/>. Called parent first and re-measured between bones, so three
        /// bones get ≈ ⅓ each and the last one absorbs any drift.
        /// </summary>
        public static Vector2 BoneStep(Vector2 remaining, int bonesLeft, float maxPerBone)
        {
            if (bonesLeft <= 0) return Vector2.zero;
            float max = Mathf.Max(0f, maxPerBone);
            Vector2 share = remaining / bonesLeft;
            return new Vector2(Mathf.Clamp(share.x, -max, max), Mathf.Clamp(share.y, -max, max));
        }

        /// <summary>
        /// Blend factor (0..1) for a yaw delta: 1 up to <paramref name="fullUpTo"/>, 0 from <paramref name="zeroFrom"/>.
        /// Fades the correction out when the target is (nearly) behind the arrow line — the ±180° yaw sign is unstable
        /// there (e.g. starting the aim while running towards the camera, before the body has turned).
        /// </summary>
        public static float YawReachFactor(float yaw, float fullUpTo, float zeroFrom)
            => zeroFrom <= fullUpTo ? (Mathf.Abs(yaw) <= fullUpTo ? 1f : 0f)
                                    : Mathf.InverseLerp(zeroFrom, fullUpTo, Mathf.Abs(yaw));

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
