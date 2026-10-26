using Game.Combat;
using Game.Core;
using Game.Player;
using Game.StatusEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays the player's current and max health above the ActionBar.
    /// Subscribes to OnPlayerHealthChanged (float = current health). Max health comes from PlayerHealth
    /// (base + permanent first-meal bonuses) when wired, else from CombatConfigSO's base health.
    /// An optional darker <c>_pendingFillImage</c> behind the fill previews the health still to come from heal effects
    /// (<see cref="StatusEffectHost.PendingHeal"/>), repainted on health and status-effect changes.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private Image _fillImage;
        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private GameEventSO_Float _onPlayerHealthChanged;
        // Cross-system read (like CharacterStatsUI): the health event carries only the current value.
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("Incoming heal preview")]
        [SerializeField] private Image _pendingFillImage;
        // Cross-system read, same as _playerHealth: the pending amount isn't carried by any event.
        [SerializeField] private StatusEffectHost _statusEffects;
        [SerializeField] private GameEventSO_Void _onStatusEffectsChanged;

        private float _currentHealth = -1f;

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
            if (_pendingFillImage == null || _statusEffects == null)
                GameLog.Warn(TAG, "HealthBarUI: _pendingFillImage / _statusEffects not assigned — no incoming-heal preview");
            if (_pendingFillImage != null) _pendingFillImage.fillAmount = 0f;
        }

        private void OnEnable()
        {
            _onPlayerHealthChanged?.AddListener(HandleHealthChanged);
            _onStatusEffectsChanged?.AddListener(HandleStatusEffectsChanged);
        }

        private void OnDisable()
        {
            _onPlayerHealthChanged?.RemoveListener(HandleHealthChanged);
            _onStatusEffectsChanged?.RemoveListener(HandleStatusEffectsChanged);
        }

        private void HandleHealthChanged(float currentHealth)
        {
            _currentHealth = currentHealth;
            Repaint();
        }

        private void HandleStatusEffectsChanged(bool _)
        {
            // Before the first health event the current value is unknown — the first one repaints both bars.
            if (_currentHealth >= 0f) Repaint();
        }

        private void Repaint()
        {
            if (_fillImage == null || _config == null) return;
            float max = _playerHealth != null ? _playerHealth.MaxHealth : _config.baseHealth;
            _fillImage.fillAmount = max > 0f ? Mathf.Clamp01(_currentHealth / max) : 0f;
            if (_pendingFillImage == null) return;
            float pending = _statusEffects != null ? _statusEffects.PendingHeal : 0f;
            _pendingFillImage.fillAmount = max > 0f ? Mathf.Clamp01((_currentHealth + pending) / max) : 0f;
        }
    }
}
