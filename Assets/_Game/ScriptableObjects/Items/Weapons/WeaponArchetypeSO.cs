using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Shared grip / sheath / animation preset for a weapon family. Poses assume a normalized mesh
    /// (grip at origin, blade +Y, edge +Z) — see Prefabs/Items/Weapons/CLAUDE.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Weapons/Weapon Archetype", fileName = "Archetype_")]
    public class WeaponArchetypeSO : ScriptableObject
    {
        [Header("Grip")]
        [Tooltip("Pose of the Drawn child in WeaponSocket (hand) space. Assumes a normalized mesh: grip at origin, blade +Y, edge +Z.")]
        public WeaponPose drawnPose;
        [Tooltip("Pose of the Sheathed child in sheath-socket space. Assumes a normalized mesh: grip at origin, blade +Y, edge +Z.")]
        public WeaponPose sheathedPose;

        [Header("Sheath")]
        public WeaponSheathSocket sheathSocket = WeaponSheathSocket.Hip;

        [Header("Animation")]
        public AnimatorOverrideController animatorOverrideController;

        [Header("Combo")]
        [Min(1)] public int defaultComboSteps = 2;
    }
}
