using UnityEngine;
using Game.Core;

namespace Game.Progression
{
    /// <summary>
    /// Tracks player XP and kill count. Subscribes to OnEntityKilled event and awards flat XP per kill.
    /// Story 3.1: Initial implementation.
    /// </summary>
    public class XPSystem : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private ProgressionConfigSO _config;
        [SerializeField] private GameEventSO_String _onEntityKilled;
        [SerializeField] private GameEventSO_Int _onXPGained;

        public int CurrentXP { get; private set; }
        public int TotalKills { get; private set; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "ProgressionConfigSO is not assigned — XPSystem disabled.");
                enabled = false;
                return;
            }

            if (_onEntityKilled == null)
                GameLog.Warn(TAG, "OnEntityKilled event is not assigned — XPSystem will not receive kill events.");

            if (_onXPGained == null)
                GameLog.Warn(TAG, "OnXPGained event is not assigned — XP signals will be silent (Story 3.2 LevelSystem won't receive them).");
        }

        private void OnEnable()
        {
            if (_onEntityKilled != null)
                _onEntityKilled.AddListener(HandleEntityKilled);
        }

        private void OnDisable()
        {
            if (_onEntityKilled == null) return; // Guard: Awake may disable before OnEnable runs
            _onEntityKilled.RemoveListener(HandleEntityKilled);
        }

        private void HandleEntityKilled(string guid)
        {
            // guid is the PersistentID GUID — ignored for flat-XP award in Story 3.1
            TotalKills++;
            int xpGained = _config.xpPerKill;
            CurrentXP += xpGained;
            GameLog.Info(TAG, $"XP gained: +{xpGained} (kill #{TotalKills}) — Total XP: {CurrentXP}");
            _onXPGained?.Raise(xpGained);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 290, 400, 26), $"XP: {CurrentXP} | Kills: {TotalKills}", _guiStyle);
        }
#endif
    }
}
