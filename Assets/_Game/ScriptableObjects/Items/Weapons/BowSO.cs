using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Ranged weapon: an attack consumes one <see cref="ammoItem"/> and launches <see cref="projectilePrefab"/>
    /// towards the crosshair. No melee hitbox — <c>PlayerCombat</c> bypasses the combo while a bow is equipped.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Weapons/Bow", fileName = "Weapon_Bow_")]
    public class BowSO : WeaponSO
    {
        [Header("Ranged")]
        [Tooltip("Item consumed from the inventory per shot (e.g. Item_Arrow).")]
        public ItemSO ammoItem;
        [Tooltip("Prefab with an ArrowProjectile component, spawned per shot.")]
        public GameObject projectilePrefab;
        [Min(1f)] public float projectileSpeed = 40f;
        [Tooltip("Seconds between two shots.")]
        [Min(0f)] public float fireCooldown = 0.8f;
        [Tooltip("Seconds before a projectile that hit nothing is destroyed.")]
        [Min(0.1f)] public float projectileLifetime = 5f;
        [Tooltip("Aim point distance when the crosshair ray hits nothing.")]
        [Min(1f)] public float maxAimDistance = 100f;

        /// <summary>True when ammo and projectile are both assigned.</summary>
        public bool IsConfigured => ammoItem != null && projectilePrefab != null;
    }
}
