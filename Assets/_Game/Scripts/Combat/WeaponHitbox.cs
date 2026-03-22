using Game.AI;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Placed on the weapon mesh child GO inside a weapon visual prefab.
    /// Manages the trigger collider(s) that represent the weapon's hit zone.
    /// Colliders start disabled (dormant) and are enabled only during an active attack frame.
    /// Story 7.9: Initial implementation.
    /// </summary>
    public class WeaponHitbox : MonoBehaviour
    {
        public event System.Action<EnemyHealth> OnEnemyHit;

        private Collider[] _colliders;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            Disable(); // Always dormant until attack frame
        }

        /// <summary>Activates all hitbox colliders — call at the start of an attack window.</summary>
        public void Enable()
        {
            foreach (var c in _colliders) c.enabled = true;
        }

        /// <summary>Deactivates all hitbox colliders — call at the end of an attack window.</summary>
        public void Disable()
        {
            foreach (var c in _colliders) c.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            var health = other.GetComponentInParent<EnemyHealth>();
            if (health != null && !health.IsDead)
                OnEnemyHit?.Invoke(health);
        }
    }
}
