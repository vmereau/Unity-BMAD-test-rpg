using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Player
{
    public enum StatType { Strength, Dexterity, Endurance, Mana }

    /// <summary>
    /// Holds the player's base stats. Stats are initialized from ProgressionConfigSO
    /// and can be permanently raised by TrainerNPC (Story 3.4).
    /// Story 3.4: Initial implementation.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private ProgressionConfigSO _config;
        [SerializeField] private GameEventSO_Void _onStatsChanged;

        public int Strength { get; private set; }
        public int Dexterity { get; private set; }
        public int Endurance { get; private set; }
        public int Mana { get; private set; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "ProgressionConfigSO not assigned — PlayerStats disabled.");
                enabled = false;
                return;
            }

            Strength = _config.baseStrength;
            Dexterity = _config.baseDexterity;
            Endurance = _config.baseEndurance;
            Mana = _config.baseMana;
        }

        /// <summary>
        /// Increments a stat by the given number of points and raises the stats-changed event.
        /// Called by TrainerNPC after a successful purchase (Story 3.4).
        /// </summary>
        public void UpgradeStat(StatType stat, int points)
        {
            if (points <= 0)
            {
                GameLog.Warn(TAG, $"UpgradeStat called with non-positive points ({points}) — ignored.");
                return;
            }

            switch (stat)
            {
                case StatType.Strength:  Strength  += points; break;
                case StatType.Dexterity: Dexterity += points; break;
                case StatType.Endurance: Endurance += points; break;
                case StatType.Mana:      Mana      += points; break;
            }
            GameLog.Info(TAG, $"Stat upgraded: {stat} +{points}. STR:{Strength} DEX:{Dexterity} END:{Endurance} MNA:{Mana}");
            _onStatsChanged?.Raise(true);
        }

        /// <summary>
        /// Returns the current value of a stat. Used by TrainerNPC for purchase logging.
        /// </summary>
        public int GetStat(StatType stat)
        {
            return stat switch
            {
                StatType.Strength  => Strength,
                StatType.Dexterity => Dexterity,
                StatType.Endurance => Endurance,
                StatType.Mana      => Mana,
                _                  => 0
            };
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 350, 500, 26),
                $"STR:{Strength} DEX:{Dexterity} END:{Endurance} MNA:{Mana}",
                _guiStyle);
        }
#endif
    }
}
