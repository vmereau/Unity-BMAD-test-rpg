using UnityEngine;
using Game.Core;

namespace Game.Progression
{
    /// <summary>
    /// Tracks player level. Subscribes to OnXPGained event and fires OnLevelUp when XP crosses thresholds.
    /// Story 3.2: Initial implementation.
    /// </summary>
    public class LevelSystem : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private ProgressionConfigSO _config;
        [SerializeField] private XPSystem _xpSystem;
        [SerializeField] private GameEventSO_Int _onXPGained;
        [SerializeField] private GameEventSO_Int _onLevelUp;

        public int CurrentLevel { get; private set; } = 1;
        public int MaxLevel => _config != null ? _config.xpPerLevel.Length + 1 : 1;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
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
