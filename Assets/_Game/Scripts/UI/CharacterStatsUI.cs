using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Progression;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class CharacterStatsUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[UI]";

        // TMP Labels — wire in Inspector
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _xpLabel;
        [SerializeField] private TMP_Text _lpLabel;
        [SerializeField] private TMP_Text _healthLabel;
        [SerializeField] private TMP_Text _staminaLabel;
        [SerializeField] private TMP_Text _strengthLabel;
        [SerializeField] private TMP_Text _dexterityLabel;
        [SerializeField] private TMP_Text _enduranceLabel;
        [SerializeField] private TMP_Text _intelligenceLabel;
        [SerializeField] private TMP_Text _defenseLabel;

        // Config SO
        [SerializeField] private ProgressionConfigSO _progressionConfig;

        // Cross-system MonoBehaviour refs (prototype: no event channel exposes all needed values)
        [SerializeField] private LevelSystem _levelSystem;
        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private XPSystem _xpSystem;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private StaminaSystem _staminaSystem;
        [SerializeField] private PlayerStats _playerStats;

        // Event SOs
        [SerializeField] private GameEventSO_Int _onLevelUp;
        [SerializeField] private GameEventSO_Int _onLPChanged;
        [SerializeField] private GameEventSO_Float _onPlayerHealthChanged;
        [SerializeField] private GameEventSO_Float _onPlayerStaminaChanged; // ratio — used as trigger only
        [SerializeField] private GameEventSO_Void _onStatsChanged;
        [SerializeField] private GameEventSO_Int _onXPGained;

        private void Awake()
        {
            bool missingRequired = _levelLabel == null || _xpLabel == null || _lpLabel == null ||
                                   _healthLabel == null || _staminaLabel == null ||
                                   _strengthLabel == null || _dexterityLabel == null ||
                                   _enduranceLabel == null || _intelligenceLabel == null ||
                                   _defenseLabel == null || _progressionConfig == null ||
                                   _levelSystem == null || _lpSystem == null || _xpSystem == null ||
                                   _playerHealth == null || _staminaSystem == null || _playerStats == null;

            if (missingRequired)
            {
                GameLog.Error(TAG, "CharacterStatsUI: missing required reference — disabled");
                enabled = false;
                return;
            }

            if (_onLevelUp == null)            GameLog.Warn(TAG, "CharacterStatsUI: _onLevelUp not assigned — level updates won't be live");
            if (_onLPChanged == null)           GameLog.Warn(TAG, "CharacterStatsUI: _onLPChanged not assigned — LP updates won't be live");
            if (_onPlayerHealthChanged == null) GameLog.Warn(TAG, "CharacterStatsUI: _onPlayerHealthChanged not assigned — HP updates won't be live");
            if (_onPlayerStaminaChanged == null) GameLog.Warn(TAG, "CharacterStatsUI: _onPlayerStaminaChanged not assigned — stamina updates won't be live");
            if (_onStatsChanged == null)        GameLog.Warn(TAG, "CharacterStatsUI: _onStatsChanged not assigned — stat updates won't be live");
            if (_onXPGained == null)            GameLog.Warn(TAG, "CharacterStatsUI: _onXPGained not assigned — XP updates won't be live");
        }

        private void OnEnable()
        {
            _onLevelUp?.AddListener(HandleLevelUp);
            _onLPChanged?.AddListener(HandleLPChanged);
            _onPlayerHealthChanged?.AddListener(HandleHealthChanged);
            _onPlayerStaminaChanged?.AddListener(HandleStaminaChanged);
            _onStatsChanged?.AddListener(HandleStatsChanged);
            _onXPGained?.AddListener(HandleXPGained);
            Refresh();
        }

        private void OnDisable()
        {
            _onLevelUp?.RemoveListener(HandleLevelUp);
            _onLPChanged?.RemoveListener(HandleLPChanged);
            _onPlayerHealthChanged?.RemoveListener(HandleHealthChanged);
            _onPlayerStaminaChanged?.RemoveListener(HandleStaminaChanged);
            _onStatsChanged?.RemoveListener(HandleStatsChanged);
            _onXPGained?.RemoveListener(HandleXPGained);
        }

        // IScreenPanel
        public void OnScreenOpen()  => GameLog.Info(TAG, "Character Stats opened");
        public void OnScreenClose() => GameLog.Info(TAG, "Character Stats closed");

        // Event handlers
        private void HandleLevelUp(int _)        { RefreshLevel(); RefreshXP(); }
        private void HandleLPChanged(int _)       => RefreshLP();
        private void HandleHealthChanged(float _) => RefreshHealth();
        private void HandleStaminaChanged(float _) => RefreshStamina();
        private void HandleStatsChanged(bool _)   => RefreshStats();
        private void HandleXPGained(int _)        => RefreshXP();

        private void Refresh()
        {
            RefreshLevel();
            RefreshXP();
            RefreshLP();
            RefreshHealth();
            RefreshStamina();
            RefreshStats();
        }

        private void RefreshLevel()
        {
            _levelLabel.SetText($"Level: {_levelSystem.CurrentLevel}");
        }

        private void RefreshXP()
        {
            int lvl = _levelSystem.CurrentLevel;
            int max = _levelSystem.MaxLevel;
            string threshold = lvl >= max ? "MAX" : _progressionConfig.xpPerLevel[lvl - 1].ToString();
            _xpLabel.SetText($"XP: {_xpSystem.CurrentXP} / {threshold}");
        }

        private void RefreshLP()
        {
            _lpLabel.SetText($"Learning Points: {_lpSystem.CurrentLP}");
        }

        private void RefreshHealth()
        {
            _healthLabel.SetText($"HP: {_playerHealth.CurrentHealth:F0} / {_playerHealth.MaxHealth:F0}");
        }

        private void RefreshStamina()
        {
            _staminaLabel.SetText($"STA: {_staminaSystem.CurrentStamina:F0} / {_staminaSystem.MaxStamina:F0}");
        }

        private void RefreshStats()
        {
            _strengthLabel.SetText($"STR: {_playerStats.Strength}");
            _dexterityLabel.SetText($"DEX: {_playerStats.Dexterity}");
            _enduranceLabel.SetText($"END: {_playerStats.Endurance}");
            _intelligenceLabel.SetText($"INT: {_playerStats.Intelligence}");
            _defenseLabel.SetText($"DEF: {_playerStats.Defense}");
        }
    }
}
