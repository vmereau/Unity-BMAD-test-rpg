using Game.Core;
using Game.Player;
using Game.Progression;
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

        // TODO(Epic4-tech-debt): Cross-system direct refs (Game.Combat → Game.Player/Progression).
        // Prototype exception per Story 3.6 dev notes. Replace with a StatBonusProvider event channel.
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private ProgressionConfigSO _progressionConfig;

        private float _currentStamina;
        private float _regenCooldown;

        /// <summary>Current stamina value in range [0, MaxStamina].</summary>
        public float CurrentStamina => _currentStamina;

        /// <summary>Maximum stamina pool — base from config plus Endurance bonus (Story 3.6).</summary>
        public float MaxStamina
        {
            get
            {
                // Clamp bonus to 0 — negative Endurance delta (future debuff/penalty system) must not reduce MaxStamina below base.
                float bonus = (_playerStats != null && _progressionConfig != null)
                    ? Mathf.Max(0f, (_playerStats.Endurance - _progressionConfig.baseEndurance) * _progressionConfig.staminaPerEndurance)
                    : 0f;
                return _config.baseStaminaPool + bonus;
            }
        }

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — StaminaSystem disabled");
                enabled = false;
                return;
            }
            if (_playerStats == null)
                GameLog.Warn(TAG, "PlayerStats not assigned — Endurance stamina bonus inactive");
            if (_progressionConfig == null)
                GameLog.Warn(TAG, "ProgressionConfigSO not assigned — Endurance stamina bonus inactive");

            _currentStamina = MaxStamina;  // Use dynamic MaxStamina so save/load with upgraded stats initializes correctly.
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

            float max = MaxStamina;
            if (_currentStamina < max)
            {
                _currentStamina = Mathf.Min(
                    _currentStamina + _config.staminaRegenRate * Time.deltaTime,
                    max);
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
        private GUIStyle _guiStyle;

        private void OnGUI()
        {
            if (_config == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 50, 300, 20),
                $"Stamina: {_currentStamina:F0} / {MaxStamina:F0}", _guiStyle);
        }
#endif
    }
}
