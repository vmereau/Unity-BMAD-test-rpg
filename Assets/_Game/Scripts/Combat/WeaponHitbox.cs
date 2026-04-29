using System.Collections.Generic;
using Game.AI;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Placed on the weapon mesh child GO inside a weapon visual prefab.
    /// Manages the trigger collider(s) that represent the weapon's hit zone.
    /// Colliders start disabled (dormant) and are enabled only during an active attack frame.
    /// Only reacts to colliders on the CharacterHitbox layer; deduplicates to one hit per entity per swing.
    /// Story 7.9: Initial implementation.
    /// </summary>
    public class WeaponHitbox : MonoBehaviour
    {
        public event System.Action<EntityHealth> OnEnemyHit;

        private Collider[] _colliders;
        private int _characterHitboxLayer;
        private readonly HashSet<EntityHealth> _hitThisSwing = new();

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            _characterHitboxLayer = LayerMask.NameToLayer("CharacterHitbox");
            Disable(); // Always dormant until attack frame
        }

        /// <summary>Activates all hitbox colliders — call at the start of an attack window.</summary>
        public void Enable()
        {
            _hitThisSwing.Clear();
            foreach (var c in _colliders) c.enabled = true;
        }

        /// <summary>Deactivates all hitbox colliders — call at the end of an attack window.</summary>
        public void Disable()
        {
            _hitThisSwing.Clear();
            foreach (var c in _colliders) c.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != _characterHitboxLayer) return;

            var health = other.GetComponentInParent<EntityHealth>();
            if (health == null || health.IsDead) return;
            if (!_hitThisSwing.Add(health)) return; // already hit this entity this swing
            OnEnemyHit?.Invoke(health);
        }
    }
}
