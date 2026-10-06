using UnityEngine;
using Game.Core;

namespace Game.Progression
{
    /// <summary>
    /// Tracks player level. Subscribes to OnXPGained event and fires OnLevelUp when XP crosses thresholds.
    /// Raises OnPlayerXPProgressChanged (normalized 0–1 progress within the current level) at startup
    /// and after every XP gain, for the HUD experience bar.
    /// Story 3.2: Initial implementation.
    /// </summary>
    public class LevelSystem : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private ProgressionConfigSO _config;
        [SerializeField] private XPSystem _xpSystem;
        [SerializeField] private GameEventSO_Int _onXPGained;
        [SerializeField] private GameEventSO_Int _onLevelUp;
        [SerializeField] private GameEventSO_Float _onPlayerXPProgressChanged;

        public int CurrentLevel { get; private set; } = 1;
        public int MaxLevel => _config != null ? _config.xpPerLevel.Length + 1 : 1;

#if false // DISABLED: debug OnGUI — to be reworked
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "ProgressionConfigSO not assigned — LevelSystem disabled.");
                enabled = false;
                return;
            }
            if (_xpSystem == null)
            {
                GameLog.Error(TAG, "XPSystem reference not assigned — LevelSystem disabled.");
                enabled = false;
                return;
            }
            if (_onXPGained == null)
                GameLog.Warn(TAG, "OnXPGained event not assigned — LevelSystem won't respond to XP gains.");
            if (_onLevelUp == null)
                GameLog.Warn(TAG, "OnLevelUp event not assigned — level-up signals will be silent (Story 3.3 LP won't trigger).");
            if (_onPlayerXPProgressChanged == null)
                GameLog.Warn(TAG, "OnPlayerXPProgressChanged event not assigned — XP bar will not update.");
        }

        private void Start()
        {
            RaiseProgress();
        }

        private void OnEnable()
        {
            if (_onXPGained != null)
                _onXPGained.AddListener(HandleXPGained);
        }

        private void OnDisable()
        {
            if (_onXPGained == null) return; // Guard: Awake may disable before OnEnable runs
            _onXPGained.RemoveListener(HandleXPGained);
        }

        private void HandleXPGained(int _) // xpGained unused — CheckLevelUp reads XPSystem.CurrentXP directly
        {
            CheckLevelUp();
            RaiseProgress(); // After CheckLevelUp so a level-up never reports progress > 1
        }

        private void RaiseProgress() =>
            _onPlayerXPProgressChanged?.Raise(CalculateLevelProgress(_xpSystem.CurrentXP, CurrentLevel, _config.xpPerLevel));

        /// <summary>
        /// Normalized 0–1 progress through the current level. Thresholds are cumulative XP.
        /// Returns 1 at max level or when thresholds are missing/degenerate.
        /// </summary>
        public static float CalculateLevelProgress(int currentXP, int currentLevel, int[] xpPerLevel)
        {
            if (xpPerLevel == null || xpPerLevel.Length == 0) return 1f;
            int level = Mathf.Max(1, currentLevel);
            if (level > xpPerLevel.Length) return 1f;           // max level → full
            int prev = level == 1 ? 0 : xpPerLevel[level - 2];
            int next = xpPerLevel[level - 1];
            int span = next - prev;
            if (span <= 0) return 1f;                            // config error → full
            return Mathf.Clamp01((currentXP - prev) / (float)span);
        }

        private void CheckLevelUp()
        {
            while (CurrentLevel < MaxLevel)
            {
                int thresholdIndex = CurrentLevel - 1; // Level 1 → xpPerLevel[0]=100
                if (_xpSystem.CurrentXP >= _config.xpPerLevel[thresholdIndex])
                {
                    CurrentLevel++;
                    GameLog.Info(TAG, $"Level up! Now Level {CurrentLevel}");
                    _onLevelUp?.Raise(CurrentLevel);
                }
                else
                {
                    break;
                }
            }
        }

#if false // DISABLED: debug OnGUI — to be reworked
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };

            string levelText = CurrentLevel < MaxLevel
                ? $"Level: {CurrentLevel} / {MaxLevel} | Next LvUp: {_config.xpPerLevel[CurrentLevel - 1]} XP"
                : $"Level: {CurrentLevel} / {MaxLevel} | MAX";

            GUI.Label(new Rect(10, 310, 500, 26), levelText, _guiStyle);
        }
#endif
    }
}
