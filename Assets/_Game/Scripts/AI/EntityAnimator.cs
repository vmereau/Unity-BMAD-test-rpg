using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Generic animator bridge for any world entity (enemies, NPCs, friendlies).
    /// Lives on the entity ROOT GameObject. The Animator lives on a child GO (e.g. CreatureVisual) —
    /// assign via Inspector; do NOT use [RequireComponent(typeof(Animator))].
    ///
    /// AnimatorOverride: wire the per-creature AnimatorOverrideController directly (e.g. from monsterTypeSO.AnimatorOverride).
    /// ComponentsToDisableOnDeath: list any MonoBehaviours (EnemyBrain, EnemyHealth, NPCScheduler…)
    ///   that should be stopped when EnableRagdoll() fires. Configured per-prefab in Inspector.
    ///
    /// EnableRagdoll() is called by SMB_DeathState.OnStateExit when the Death animation completes.
    /// </summary>
    public class EntityAnimator : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private Animator _animator;
        [SerializeField] private MonoBehaviour[] _componentsToDisableOnDeath;

        private Rigidbody[] _ragdollBodies;
        private bool _ragdollActive;

        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DeathHash  = Animator.StringToHash("Death");

        private void Awake()
        {
            if (_animator == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No Animator assigned — EntityAnimator is a no-op");
                return;
            }

            if (_persistentID.Entity.AnimatorOverride != null)
                _animator.runtimeAnimatorController = _persistentID.Entity.AnimatorOverride;

            _ragdollBodies = _animator.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in _ragdollBodies)
                rb.isKinematic = true;
        }

        public void SetMoveSpeed(float speed)
        {
            _animator?.SetFloat(SpeedHash, speed);
        }

        public void TriggerAttack()
        {
            _animator?.SetTrigger(AttackHash);
        }

        public void TriggerGetHit()
        {
            _animator?.SetTrigger(GetHitHash);
        }

        public void TriggerDeath()
        {
            if (_animator == null) return;
            _animator.SetTrigger(DeathHash);
        }

        /// <summary>
        /// Called by SMB_DeathState.OnStateExit when the Death animation finishes.
        /// Disables the Animator (to freeze pose), makes all ragdoll Rigidbodies non-kinematic,
        /// then disables every MonoBehaviour listed in _componentsToDisableOnDeath.
        /// If no ragdoll bodies are found, components are still disabled and the body stays in scene.
        /// </summary>
        public void EnableRagdoll()
        {
            if (_ragdollActive) return;

            if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            {
                GameLog.Info(TAG, $"{gameObject.name} has no ragdoll bodies — disabling components");
                DisableDeathComponents();
                return;
            }

            _animator.enabled = false;
            foreach (var rb in _ragdollBodies)
                rb.isKinematic = false;

            _ragdollActive = true;

            DisableDeathComponents();

            GameLog.Info(TAG, $"{gameObject.name} ragdoll activated");
        }

        private void DisableDeathComponents()
        {
            if (_componentsToDisableOnDeath == null) return;
            foreach (var component in _componentsToDisableOnDeath)
            {
                if (component != null)
                    component.enabled = false;
            }
        }
    }
}
