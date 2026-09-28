using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Math for the normalized weapon frame: grip point at the origin, blade/head along +Y,
    /// leading cutting edge along +Z. The weapon's <c>Mesh</c> child transform converts
    /// art-pack mesh space into this frame.
    /// </summary>
    public static class WeaponGripMath
    {
        public static Vector3 ToVector(SignedAxis a) => a switch
        {
            SignedAxis.PosX => Vector3.right,
            SignedAxis.NegX => Vector3.left,
            SignedAxis.PosY => Vector3.up,
            SignedAxis.NegY => Vector3.down,
            SignedAxis.PosZ => Vector3.forward,
            SignedAxis.NegZ => Vector3.back,
            _ => Vector3.zero,
        };

        /// <summary>False when blade and edge lie on the same axis (e.g. PosY / NegY) — no valid frame. Relies on SignedAxis pairing (value / 2 = axis).</summary>
        public static bool AreValid(SignedAxis blade, SignedAxis edge) => (int)blade / 2 != (int)edge / 2;

        /// <summary>Rotation mapping the mesh blade axis to +Y and the mesh edge axis to +Z.</summary>
        public static Quaternion NormalizationRotation(SignedAxis blade, SignedAxis edge)
            => Quaternion.Inverse(Quaternion.LookRotation(ToVector(edge), ToVector(blade)));

        /// <summary>Local transform for the <c>Mesh</c> child so the grip lands at the parent's origin in the normalized frame.</summary>
        public static void GetMeshChildLocal(Vector3 gripMeshLocal, SignedAxis blade, SignedAxis edge,
            out Vector3 localPosition, out Quaternion localRotation)
        {
            localRotation = NormalizationRotation(blade, edge);
            localPosition = -(localRotation * gripMeshLocal);
        }

        /// <summary>
        /// New parent pose that keeps the mesh's socket-space placement identical when a normalizing
        /// <c>Mesh</c> child (meshChildPos / meshChildRot) is inserted under an existing posed parent.
        /// </summary>
        public static void RebaseParentPose(Vector3 oldPos, Quaternion oldRot, Vector3 meshChildPos, Quaternion meshChildRot,
            out Vector3 newPos, out Quaternion newRot)
        {
            newRot = oldRot * Quaternion.Inverse(meshChildRot);
            newPos = oldPos - newRot * meshChildPos;
        }
    }
}
