using UnityEngine;
using Game.Core;

namespace Game.Progression
{
    /// <summary>
    /// Tracks Learning Points (LP). Subscribes to OnLevelUp and awards LP each level-up.
    /// Story 3.3: Initial implementation.
    /// </summary>
    public class LearningPointSystem : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private ProgressionConfigSO _config;
        [SerializeField] private GameEventSO_Int _onLevelUp;
        [SerializeField] private GameEventSO_Int _onLPChanged;

        public int CurrentLP { get; private set; } = 0;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "ProgressionConfigSO not assigned — LearningPointSystem disabled.");
                enabled = false;
                return;
            }
            if (_onLevelUp == null)
                GameLog.Warn(TAG, "OnLevelUp event not assigned — LearningPointSystem won't respond to level-ups.");
            if (_onLPChanged == null)
                GameLog.Warn(TAG, "OnLPChanged event not assigned — LP change signals will be silent.");
        }

        private void OnEnable()
        {
            if (_onLevelUp != null)
                _onLevelUp.AddListener(HandleLevelUp);
        }

        private void OnDisable()
        {
            if (_onLevelUp == null) return; // Guard: Awake may disable before OnEnable runs
            _onLevelUp.RemoveListener(HandleLevelUp);
        }

        private void HandleLevelUp(int newLevel)
        {
            CurrentLP += _config.learningPointsPerLevel;
            GameLog.Info(TAG, $"Level {newLevel} reached — awarded {_config.learningPointsPerLevel} LP. Total: {CurrentLP}");
            _onLPChanged?.Raise(CurrentLP);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 330, 500, 26), $"LP: {CurrentLP}", _guiStyle);
        }
#endif
    }
}
