using UnityEngine;
using Game.Core;

namespace Game.Progression
{
    /// <summary>
    /// Tracks player XP and kill count. Call GiveExperience(amount) to award XP.
    /// GiveExperience(amount) routes per-enemy XP from PlayerRewards.
    /// Story 3.1: Initial implementation. Refactored in player-rewards-per-type-xp.
    /// </summary>
    public class XPSystem : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private GameEventSO_Int _onXPGained;

        public int CurrentXP { get; private set; }
        public int TotalKills { get; private set; }

#if false // DISABLED: debug OnGUI — to be reworked
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_onXPGained == null)
                GameLog.Warn(TAG, "OnXPGained event is not assigned — XP signals will be silent.");
        }

        public void GiveExperience(int amount)
        {
            if (amount <= 0) return;
            TotalKills++;
            CurrentXP += amount;
            GameLog.Info(TAG, $"XP gained: +{amount} (kill #{TotalKills}) — Total XP: {CurrentXP}");
            _onXPGained?.Raise(amount);
        }

#if false // DISABLED: debug OnGUI — to be reworked
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 290, 400, 26), $"XP: {CurrentXP} | Kills: {TotalKills}", _guiStyle);
        }
#endif
    }
}
