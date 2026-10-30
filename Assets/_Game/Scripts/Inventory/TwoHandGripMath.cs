using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Pose of a two-handed weapon held by both hands (normalized weapon frame: grip at origin, blade +Y, edge +Z).
    /// Shared by <see cref="TwoHandGripConstraint"/> and the Weapon Pose Tuner preview.
    /// </summary>
    public static class TwoHandGripMath
    {
        /// <summary>Hands closer than this give no usable blade direction.</summary>
        public const float MIN_HAND_DISTANCE = 0.05f;
        /// <summary>Full grip weight from here … (measured 2H clips: 0.19–0.35 m).</summary>
        public const float FULL_GRIP_MIN_DISTANCE = 0.10f;
        /// <summary>… up to here; beyond it the left hand is letting go (sheathe, knockdown, death).</summary>
        public const float FULL_GRIP_MAX_DISTANCE = 0.40f;
        /// <summary>Hands this far apart: the grip weight reaches 0 (rigid pose).</summary>
        public const float RELEASE_HAND_DISTANCE = 0.55f;
        /// <summary>Edge reference within this angle of the blade (sin) gives no usable roll.</summary>
        public const float MIN_EDGE_SIN = 0.05f;
        /// <summary>Full grip weight once the edge reference is at least this far from the blade (sin).</summary>
        public const float FULL_EDGE_SIN = 0.25f;
        private const float MIN_VECTOR_SQR = 1e-8f;

        /// <summary>
        /// How much the two-hand solve should win over the rigid pose: 1 in a normal grip, easing to 0 as the hands get
        /// too close, let go of the handle, or the edge reference nears the blade — so the hand-off is never a pop.
        /// </summary>
        public static float GripWeight(float handDistance, float edgeSin)
        {
            float tooClose = Mathf.InverseLerp(MIN_HAND_DISTANCE, FULL_GRIP_MIN_DISTANCE, handDistance);
            float released = 1f - Mathf.InverseLerp(FULL_GRIP_MAX_DISTANCE, RELEASE_HAND_DISTANCE, handDistance);
            float edge = Mathf.InverseLerp(MIN_EDGE_SIN, FULL_EDGE_SIN, edgeSin);
            return Mathf.Clamp01(tooClose * released * edge);
        }

        /// <summary>
        /// Blade = right − left (the right hand is nearer the guard, so the blade extends past it, away from the left
        /// hand). Edge = <paramref name="edgeReference"/> made perpendicular to the blade, then rolled
        /// <paramref name="rollDeg"/> about it. Position = right grip moved <paramref name="slide"/> metres along the blade.
        /// <paramref name="weight"/> = <see cref="GripWeight"/>. Returns false (weight 0, position default, rotation
        /// identity) when the hands are too close or the edge reference is parallel to the blade.
        /// </summary>
        public static bool TrySolve(Vector3 rightGrip, Vector3 leftGrip, Vector3 edgeReference, float rollDeg, float slide,
            out Vector3 position, out Quaternion rotation, out float weight)
        {
            position = default;
            rotation = Quaternion.identity;
            weight = 0f;

            Vector3 line = rightGrip - leftGrip;
            float distance = line.magnitude;
            if (distance < MIN_HAND_DISTANCE) return false;
            Vector3 blade = line / distance;

            float refLength = edgeReference.magnitude;
            if (refLength * refLength < MIN_VECTOR_SQR) return false;
            Vector3 edge = Vector3.ProjectOnPlane(edgeReference, blade);
            float edgeSin = edge.magnitude / refLength;
            if (edgeSin < MIN_EDGE_SIN) return false;
            edge = Quaternion.AngleAxis(rollDeg, blade) * (edge / (edgeSin * refLength));

            rotation = Quaternion.LookRotation(edge, blade);
            position = rightGrip + blade * slide;
            weight = GripWeight(distance, edgeSin);
            return weight > 0f;
        }
    }
}
