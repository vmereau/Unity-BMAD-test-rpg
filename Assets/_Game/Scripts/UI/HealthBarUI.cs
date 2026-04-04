using Game.Combat;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays the player's current and max health above the ActionBar.
    /// Subscribes to OnPlayerHealthChanged (float = current health) and reads
    /// max health from CombatConfigSO to avoid cross-system MonoBehaviour coupling.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private Image _fillImage;
        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private GameEventSO_Float _onPlayerHealthChanged;

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
            float max = _config.baseHealth;
            float ratio = max > 0f ? currentHealth / max : 0f;
            _fillImage.transform.localScale = new Vector3(ratio, 1f, 1f);
        }
    }
}
