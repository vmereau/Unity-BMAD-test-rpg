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
    /// Story 7.3: Added Defense reduction via PlayerStats.Defense.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        [SerializeField] private GameEventSO_Float _onPlayerHealthChanged;

        // Same-system reference — PlayerHealth and PlayerStats both in Scripts/Player/. No architecture violation.
        [SerializeField] private PlayerStats _playerStats;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _config != null ? _config.baseHealth : 0f;
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

            if (_playerStats == null)
                GameLog.Warn(TAG, "PlayerStats not assigned — damage will not be reduced by Defense");

            CurrentHealth = _config.baseHealth;
        }

        private void Start()
        {
            // Raise after all OnEnable calls have run so subscribers (e.g. HealthBarUI) are already listening.
            _onPlayerHealthChanged?.Raise(CurrentHealth);
        }

        /// <summary>
        /// Applies damage to the player, reduced by Defense. Minimum 1 damage always gets through.
        /// Triggers death when health reaches zero. Calls are ignored if the player is already dead.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            float defense = _playerStats != null ? _playerStats.Defense : 0f;
            float effective = Mathf.Max(1f, amount - defense);
            CurrentHealth -= effective;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"Player took {effective:F0} damage (raw {amount:F0} - def {defense:F0}) — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");
            _onPlayerHealthChanged?.Raise(CurrentHealth);
            if (CurrentHealth <= 0f) Die();
        }

        /// <summary>
        /// Restores health by the given amount, capped at baseHealth. Ignored if dead.
        /// Story 4.10: Added to support PotionItemSO.OnUse.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(CurrentHealth + amount, _config.baseHealth);
            _onPlayerHealthChanged?.Raise(CurrentHealth);
            GameLog.Info(TAG, $"Player healed {amount} HP — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");
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

#if false // DISABLED: debug OnGUI — to be reworked
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
