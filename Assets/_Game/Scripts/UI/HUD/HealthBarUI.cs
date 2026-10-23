using Game.Combat;
using Game.Core;
using Game.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays the player's current and max health above the ActionBar.
    /// Subscribes to OnPlayerHealthChanged (float = current health). Max health comes from PlayerHealth
    /// (base + permanent first-meal bonuses) when wired, else from CombatConfigSO's base health.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private Image _fillImage;
        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private GameEventSO_Float _onPlayerHealthChanged;
        // Cross-system read (like CharacterStatsUI): the health event carries only the current value.
        [SerializeField] private PlayerHealth _playerHealth;

        private void Awake()
        {
            if (_fillImage == null)
            {
                GameLog.Error(TAG, "HealthBarUI: _fillImage not assigned");
                enabled = false;
                return;
            }
            if (_config == null)
            {
                GameLog.Error(TAG, "HealthBarUI: _config not assigned");
                enabled = false;
                return;
            }
            if (_onPlayerHealthChanged == null)
                GameLog.Warn(TAG, "HealthBarUI: _onPlayerHealthChanged not assigned — bar will not update");
            if (_playerHealth == null)
                GameLog.Warn(TAG, "HealthBarUI: _playerHealth not assigned — bar ignores max-health bonuses");
        }

        private void OnEnable()
        {
            _onPlayerHealthChanged?.AddListener(HandleHealthChanged);
        }

        private void OnDisable()
        {
            _onPlayerHealthChanged?.RemoveListener(HandleHealthChanged);
        }

        private void HandleHealthChanged(float currentHealth)
        {
            GameLog.Info(TAG, $"HandleHealthChanged: {currentHealth}");
            float max = _playerHealth != null ? _playerHealth.MaxHealth : _config.baseHealth;
            float ratio = max > 0f ? Mathf.Clamp01(currentHealth / max) : 0f;
            _fillImage.fillAmount = ratio;
        }
    }
}
