using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Manages player health. Handles damage and death.
    /// On death: raises OnPlayerDied event, deactivates the player GameObject.
    /// Respawn/checkpoint logic deferred to Epic 8.
    /// Attach to the Player prefab root.
    /// Story 2.9: Initial implementation.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private GameEventSO_Void _onPlayerDied;

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — PlayerHealth disabled");
                enabled = false;
                return;
            }

            if (_onPlayerDied == null)
                GameLog.Warn(TAG, "OnPlayerDied event not assigned — death will not be broadcast");

            CurrentHealth = _config.baseHealth;
        }

        /// <summary>
        /// Applies damage to the player. Triggers death when health reaches zero.
        /// Calls are ignored if the player is already dead.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth -= amount;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"Player took {amount} damage — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");

            if (CurrentHealth <= 0f)
                Die();
        }

        private void Die()
        {
            IsDead = true;
            GameLog.Info(TAG, "Player has died");

            // Raise event so Save/UI systems can react (Epic 8 subscribers)
            _onPlayerDied?.Raise(true);

            // Simple prototype death: deactivate player (respawn in Epic 8)
            gameObject.SetActive(false);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _guiStyle;

        private void OnGUI()
        {
            if (_config == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 250, 400, 26),
                $"PlayerHP: {CurrentHealth:F0}/{_config.baseHealth:F0} | Dead:{IsDead}",
                _guiStyle);
        }
#endif
    }
}
