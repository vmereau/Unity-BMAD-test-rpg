using Game.Core;
using Game.Economy;
using Game.NPC;
using Game.Player;
using Game.Progression;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Prototype trainer NPC. Look-at + E-key (via InteractionSystem) opens a stat upgrade menu (OnGUI).
    /// Direct cross-system references are an intentional prototype pragmatism (see Dev Notes).
    /// Will be superseded by Epic 5 Dialogue system.
    /// Story 3.4: Initial implementation.
    /// Story 4.7: Refactored to IInteractable — proximity+InputSystem replaced by InteractionSystem raycast.
    /// </summary>
    public class TrainerNPC : MonoBehaviour, IInteractable
    {
        private const string TAG = "[NPC]";

        [SerializeField] private TrainerSO _trainerData;

        // Prototype cross-system direct refs (inspector-assigned, see Dev Notes Story 3.4)
        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private GoldSystem _goldSystem;
        [SerializeField] private PlayerStats _playerStats;

        private bool _menuOpen;
        private int[] _purchaseCounts;

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _grayedStyle;
        private GUIStyle _promptStyle;


        public string InteractPrompt => $"Press E to train: {_trainerData?.trainerName ?? "Trainer"}";

        private void Awake()
        {
            if (_trainerData == null || _lpSystem == null || _goldSystem == null || _playerStats == null)
            {
                GameLog.Error(TAG, "TrainerNPC: required reference(s) not assigned — component disabled");
                enabled = false;
                return;
            }

            if (_trainerData.upgrades.Length == 0)
            {
                GameLog.Warn(TAG, "TrainerNPC: TrainerSO has no upgrades configured");
            }
            _purchaseCounts = new int[_trainerData.upgrades.Length];
        }

        public void Interact()
        {
            if (_trainerData == null || _lpSystem == null || _goldSystem == null || _playerStats == null) return;
            _menuOpen = !_menuOpen;
        }

        private void TryPurchaseUpgrade(int index)
        {
            if (index < 0 || index >= _trainerData.upgrades.Length) return;

            var entry = _trainerData.upgrades[index];

            if (_purchaseCounts[index] >= entry.maxLevel)
            {
                GameLog.Warn(TAG, $"{entry.label}: already at max level");
                return;
            }

            // Check both BEFORE spending either (atomicity guard)
            if (_lpSystem.CurrentLP < entry.lpCost)
            {
                GameLog.Warn(TAG, $"{entry.label}: insufficient LP ({_lpSystem.CurrentLP}/{entry.lpCost})");
                return;
            }

            if (_goldSystem.Gold < entry.goldCost)
            {
                GameLog.Warn(TAG, $"{entry.label}: insufficient gold ({_goldSystem.Gold}/{entry.goldCost})");
                return;
            }

            // Both checks passed — spend atomically
            _lpSystem.TrySpendLP(entry.lpCost);
            _goldSystem.TrySpend(entry.goldCost);
            _playerStats.UpgradeStat(entry.stat, 1);
            _purchaseCounts[index]++;
            GameLog.Info(TAG, $"Trainer purchased: {entry.label}. {entry.stat} now {_playerStats.GetStat(entry.stat)}.");
        }


        private void OnGUI()
        {
            // Initialize cached styles on first use
            if (_labelStyle == null)
            {
                _titleStyle  = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _labelStyle  = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                _grayedStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
                _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            }

            // No proximity check — InteractionSystem handles detection
            if (!_menuOpen) return;

            // Centered menu
            float menuWidth  = 400f;
            float menuHeight = 60f + _trainerData.upgrades.Length * 30f + 40f;
            float x = (Screen.width - menuWidth) / 2f;
            float y = (Screen.height - menuHeight) / 2f;

            GUI.Box(new Rect(x - 10, y - 10, menuWidth + 20, menuHeight + 20), GUIContent.none);

            GUI.Label(new Rect(x, y, menuWidth, 30),
                $"=== {_trainerData.trainerName} ===", _titleStyle);
            y += 30;

            GUI.Label(new Rect(x, y, menuWidth, 26),
                $"LP: {_lpSystem.CurrentLP}    Gold: {_goldSystem.Gold}", _labelStyle);
            y += 30;

            for (int i = 0; i < _trainerData.upgrades.Length; i++)
            {
                var entry = _trainerData.upgrades[i];
                bool canAfford = _lpSystem.CurrentLP >= entry.lpCost && _goldSystem.Gold >= entry.goldCost;
                bool atMax     = _purchaseCounts[i] >= entry.maxLevel;

                string text = $"[{i + 1}] {entry.label}  LP:{entry.lpCost}  G:{entry.goldCost}  ({_purchaseCounts[i]}/{entry.maxLevel})";
                GUIStyle style = (atMax || !canAfford) ? _grayedStyle : _labelStyle;

                GUI.Label(new Rect(x, y, menuWidth, 26), text, style);
                y += 30;
            }

            GUI.Label(new Rect(x, y, menuWidth, 26), "[E] to close", _promptStyle);

            // Number key + E key input
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.E)
                {
                    _menuOpen = false; // close via E when not looking at trainer
                }
                else
                {
                    for (int i = 0; i < _trainerData.upgrades.Length; i++)
                    {
                        if (e.keyCode == KeyCode.Alpha1 + i)
                        {
                            TryPurchaseUpgrade(i);
                            break;
                        }
                    }
                }
            }
        }
    }
}
