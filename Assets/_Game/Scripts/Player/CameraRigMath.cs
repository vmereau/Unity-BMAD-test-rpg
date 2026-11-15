using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Pure rules for the third-person camera rig driven by <see cref="CameraController"/>
    /// (CinemachineThirdPersonFollow). Pitch sign: negative = looking up, positive = looking down.
    /// </summary>
    public static class CameraRigMath
    {
        /// <summary>
        /// Arm distance multiplier: 1 at level or looking down, easing linearly to <paramref name="minScale"/>
        /// at full look-up (<paramref name="pitchMin"/>). Looking down needs no change: the orbit raises the camera.
        /// </summary>
        public static float LookUpDistanceScale(float pitch, float pitchMin, float minScale)
        {
            if (pitch >= 0f || pitchMin >= 0f) return 1f;
            return Mathf.Lerp(1f, minScale, Mathf.InverseLerp(0f, pitchMin, pitch));
        }

        /// <summary>Shortest arm the rig may get from the aim blend (a positive aim z would flip the camera).</summary>
        public const float MinArmDistance = 0.5f;

        /// <summary>
        /// Look-up shortening applied to the rig: full at no aim, none at full aim
        /// (the aim arm is already short — compounding would put the camera inside the head).
        /// </summary>
        public static float RigDistanceScale(float pitch, float pitchMin, float minScale, float aimBlend)
            => Mathf.Lerp(LookUpDistanceScale(pitch, pitchMin, minScale), 1f, Mathf.Clamp01(aimBlend));

        /// <summary>Lowest (most upward) pitch allowed: limited to <paramref name="swimPitchMin"/> while swimming.</summary>
        public static float EffectivePitchMin(float pitchMin, bool swimming, float swimPitchMin)
            => swimming ? Mathf.Max(pitchMin, swimPitchMin) : pitchMin;

        /// <summary>
        /// One frame of the eased pitch limit: the floor moves towards <paramref name="targetFloor"/> by at most
        /// <paramref name="maxStep"/> degrees, and the pitch is pushed up to stay at or above it (eased, never snapped).
        /// </summary>
        public static void StepPitchFloor(ref float floor, ref float pitch, float targetFloor, float maxStep)
        {
            floor = Mathf.MoveTowards(floor, targetFloor, maxStep);
            if (pitch < floor) pitch = floor;
        }

        /// <summary>
        /// Blends the resting rig towards the bow aim offset. <paramref name="aimOffset"/> uses the
        /// follow-offset convention: x, y = shoulder offset, -z = arm distance.
        /// </summary>
        public static void BlendRig(Vector3 baseShoulder, float baseDistance, Vector3 aimOffset, float blend,
            out Vector3 shoulder, out float distance)
        {
            blend = Mathf.Clamp01(blend);
            shoulder = new Vector3(
                Mathf.Lerp(baseShoulder.x, aimOffset.x, blend),
                Mathf.Lerp(baseShoulder.y, aimOffset.y, blend),
                baseShoulder.z);
            distance = Mathf.Max(MinArmDistance, Mathf.Lerp(baseDistance, -aimOffset.z, blend));
        }
    }
}
