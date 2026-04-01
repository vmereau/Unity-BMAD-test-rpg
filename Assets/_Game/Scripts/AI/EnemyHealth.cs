using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Manages enemy health. Handles damage and death.
    /// On death: stops NavMeshAgent, calls PersistentID.RegisterDeath(), triggers death animation.
    /// Body remains in scene permanently after ragdoll activates (no SetActive(false)).
    /// Attach to the Enemy prefab root alongside EnemyBrain.
    /// Story 2.9: Initial implementation.
    /// Enemy Creature System: Migrated to EnemyTypeSO; death triggers EnemyAnimator instead of SetActive(false).
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private EnemyTypeSO _type;
        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private EnemyAnimator _enemyAnimator;

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        private void Awake()
        {
            if (_type == null)
            {
                GameLog.Error(TAG, $"EnemyTypeSO not assigned on {gameObject.name} — EnemyHealth disabled");
                enabled = false;
                return;
            }

            if (_persistentID == null)
                GameLog.Warn(TAG, $"{gameObject.name}: PersistentID not assigned — kill will not be registered");

            CurrentHealth = _type.BaseHealth;
        }

        /// <summary>
        /// Called by Unity when the GameObject is re-enabled (e.g. by EnemyRespawner).
        /// Resets health and dead flag so the enemy is fully combat-ready again.
        /// </summary>
        private void OnEnable()
        {
            if (_type == null) return;
            CurrentHealth = _type.BaseHealth;
            IsDead = false;
        }

        /// <summary>
        /// Applies damage to this enemy. Triggers death when health reaches zero.
        /// Calls are ignored if the enemy is already dead.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth -= amount;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"{gameObject.name} took {amount} damage — HP: {CurrentHealth:F0}/{_type.BaseHealth:F0}");

            if (CurrentHealth <= 0f) { Die(); return; }  // death path — no GetHit
            _enemyAnimator?.TriggerGetHit();              // hit reaction only if still alive
        }

        private void Die()
        {
            IsDead = true;
            GameLog.Info(TAG, $"{gameObject.name} died — registering kill");

            // Stop NavMeshAgent so it doesn't thrash during death animation
            if (TryGetComponent<NavMeshAgent>(out var agent))
                agent.isStopped = true;

            _persistentID?.RegisterDeath();

            _enemyAnimator?.TriggerDeath();
            // Body remains active in scene — ragdoll activated by SMB_DeathState.OnStateExit via EnemyAnimator.EnableRagdoll()
            // If EnemyAnimator is absent (e.g. Enemy_Grunt), this is a no-op and body stays active indefinitely.
        }

#if false // DISABLED: debug OnGUI — to be reworked
        private GUIStyle _guiStyle;

        private void OnGUI()
        {
            if (_type == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 270, 400, 26),
                $"EnemyHP: {CurrentHealth:F0}/{_type.BaseHealth:F0} | Dead:{IsDead}",
                _guiStyle);
        }
#endif
    }
}
