using UnityEngine;

namespace Game.Inventory
{
    /// <summary>Local position + rotation of a weapon visual child (Drawn / Sheathed) relative to its socket.</summary>
    [System.Serializable]
    public struct WeaponPose
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        public void ApplyTo(Transform t)
        {
            t.localPosition = localPosition;
            t.localRotation = Quaternion.Euler(localEulerAngles);
        }

        public static WeaponPose From(Transform t) => new()
        {
            localPosition = t.localPosition,
            localEulerAngles = t.localEulerAngles,
        };
    }
}
