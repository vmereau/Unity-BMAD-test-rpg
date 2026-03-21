using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Player
{
    public enum StatType { Strength, Dexterity, Endurance, Mana, Defense }

    /// <summary>
    /// Holds the player's base stats. Stats are initialized from ProgressionConfigSO
    /// and can be permanently raised by TrainerNPC (Story 3.4).
    /// Story 3.4: Initial implementation.
    /// Story 7.3: Split base/equipment storage; added Defense property; ApplyEquipmentBonuses().
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private ProgressionConfigSO _config;
        [SerializeField] private GameEventSO_Void _onStatsChanged;

        // Base values — initialized from config, permanently incremented by UpgradeStat()
        private int _baseStrength, _baseDexterity, _baseEndurance, _baseMana;

        // Equipment bonuses — replaced wholesale by ApplyEquipmentBonuses()
        private int _equipStrBonus, _equipDexBonus, _equipEndBonus, _equipMnaBonus, _equipDefBonus;

        // Effective values — computed properties; all callers receive base + equipment total automatically
        public int Strength  => _baseStrength  + _equipStrBonus;
        public int Dexterity => _baseDexterity + _equipDexBonus;
        public int Endurance => _baseEndurance + _equipEndBonus;
        public int Mana      => _baseMana      + _equipMnaBonus;

        // New — no base defense; purely equipment-derived
        public int Defense => _equipDefBonus;

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

            _baseStrength  = _config.baseStrength;
            _baseDexterity = _config.baseDexterity;
            _baseEndurance = _config.baseEndurance;
            _baseMana      = _config.baseMana;
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
                case StatType.Strength:  _baseStrength  += points; break;
                case StatType.Dexterity: _baseDexterity += points; break;
                case StatType.Endurance: _baseEndurance += points; break;
                case StatType.Mana:      _baseMana      += points; break;
                default:
                    GameLog.Warn(TAG, $"UpgradeStat: {stat} has no base field — upgrade ignored");
                    return;
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
                StatType.Defense   => Defense,
                _                  => 0
            };
        }

        /// <summary>
        /// Replaces all equipment stat bonuses with the new totals.
        /// Called by EquipmentSystem whenever the equipped loadout changes.
        /// Raises _onStatsChanged so UI and systems refresh immediately.
        /// </summary>
        public void ApplyEquipmentBonuses(int str, int dex, int end, int mna, int def)
        {
            _equipStrBonus = str;
            _equipDexBonus = dex;
            _equipEndBonus = end;
            _equipMnaBonus = mna;
            _equipDefBonus = def;
            GameLog.Info(TAG, $"Equipment bonuses applied — STR+{str} DEX+{dex} END+{end} MNA+{mna} DEF+{def}");
            _onStatsChanged?.Raise(true);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 350, 500, 26),
                $"STR:{Strength} DEX:{Dexterity} END:{Endurance} MNA:{Mana} DEF:{Defense}",
                _guiStyle);
        }
#endif
    }
}
