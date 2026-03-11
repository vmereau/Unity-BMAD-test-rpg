using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Manages enemy health. Handles damage and death.
    /// On death: stops NavMeshAgent, calls PersistentID.RegisterDeath(), deactivates GameObject.
    /// Attach to the Enemy prefab root alongside EnemyBrain.
    /// Story 2.9: Initial implementation.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private AIConfigSO _config;
        [SerializeField] private PersistentID _persistentID;

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, $"AIConfigSO not assigned on {gameObject.name} — EnemyHealth disabled");
                enabled = false;
                return;
            }

            if (_persistentID == null)
                GameLog.Warn(TAG, $"{gameObject.name}: PersistentID not assigned — kill will not be registered");

            CurrentHealth = _config.baseHealth;
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
            GameLog.Info(TAG, $"{gameObject.name} took {amount} damage — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");

            if (CurrentHealth <= 0f)
                Die();
        }

        private void Die()
        {
            IsDead = true;
            GameLog.Info(TAG, $"{gameObject.name} died — registering kill");

            // Stop NavMeshAgent so it doesn't thrash during deactivation
            if (TryGetComponent<NavMeshAgent>(out var agent))
                agent.isStopped = true;

            _persistentID?.RegisterDeath();

            gameObject.SetActive(false);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _guiStyle;

        private void OnGUI()
        {
            if (_config == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 270, 400, 26),
                $"EnemyHP: {CurrentHealth:F0}/{_config.baseHealth:F0} | Dead:{IsDead}",
                _guiStyle);
        }
#endif
    }
}
