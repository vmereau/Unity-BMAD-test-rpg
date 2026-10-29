using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Ranged weapon: an attack consumes one <see cref="ammoItem"/> and launches <see cref="projectilePrefab"/>
    /// towards a random point of the aim spread cone, then lets it drop (straight first, gravity eased in). No melee hitbox — <c>PlayerCombat</c> bypasses the combo while a bow is equipped.
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

        [Header("Arrow Drop")]
        [Tooltip("Share of Physics.gravity applied to the arrow once fully ramped in.")]
        [Min(0f)] public float gravityScale = 0.5f;
        [Tooltip("Seconds the arrow flies dead straight before gravity starts.")]
        [Min(0f)] public float gravityDelay = 0.25f;
        [Tooltip("Seconds over which gravity ramps from 0 to gravityScale after the delay.")]
        [Min(0f)] public float gravityRampTime = 0.5f;

        /// <summary>True when ammo and projectile are both assigned.</summary>
        public bool IsConfigured => ammoItem != null && projectilePrefab != null;
    }
}
