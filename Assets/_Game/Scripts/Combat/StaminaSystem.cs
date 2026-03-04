using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Manages the player's stamina pool. All combat actions call Consume() before executing.
    /// Stamina regenerates automatically after a configurable delay since the last consumption.
    /// Attach to the Player prefab root alongside PlayerController.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;

        private float _currentStamina;
        private float _regenCooldown;

        /// <summary>Current stamina value in range [0, MaxStamina].</summary>
        public float CurrentStamina => _currentStamina;

        /// <summary>Maximum stamina pool from config.</summary>
        public float MaxStamina => _config != null ? _config.baseStaminaPool : 0f;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — StaminaSystem disabled");
                enabled = false;
                return;
            }
            _currentStamina = _config.baseStaminaPool;
            GameLog.Info(TAG, $"StaminaSystem initialized. Pool: {_config.baseStaminaPool}");
        }

        private void Update()
        {
            if (_config == null) return;

            if (_regenCooldown > 0f)
            {
                _regenCooldown -= Time.deltaTime;
                return;
            }

            if (_currentStamina < _config.baseStaminaPool)
            {
                _currentStamina = Mathf.Min(
                    _currentStamina + _config.staminaRegenRate * Time.deltaTime,
                    _config.baseStaminaPool);
            }
        }

        /// <summary>
        /// Attempts to consume <paramref name="amount"/> stamina.
        /// Returns true if stamina was consumed; false if insufficient (stamina unchanged).
        /// Resets the regen cooldown on successful consumption.
        /// </summary>
        public bool Consume(float amount)
        {
            if (amount <= 0f) return true;

            if (_currentStamina < amount)
            {
                GameLog.Warn(TAG, $"Insufficient stamina: needed {amount}, had {_currentStamina:F1}");
                return false;
            }

            _currentStamina -= amount;
            _currentStamina = Mathf.Max(_currentStamina, 0f);  // safety clamp
            _regenCooldown = _config.staminaRegenDelay;
            GameLog.Info(TAG, $"Stamina consumed: -{amount}. Remaining: {_currentStamina:F1}");
            return true;
        }

        /// <summary>Returns true if the player has at least <paramref name="amount"/> stamina.</summary>
        public bool HasEnough(float amount) => _currentStamina >= amount;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null) return;
            GUI.Label(new Rect(10, 50, 300, 20),
                $"Stamina: {_currentStamina:F0} / {_config.baseStaminaPool:F0}");
        }
#endif
    }
}
