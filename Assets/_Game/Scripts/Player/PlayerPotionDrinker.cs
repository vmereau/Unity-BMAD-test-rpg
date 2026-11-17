using Game.Combat;
using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// The player's potion drink (spec-potion-drink-animation). <see cref="PotionItemSO.OnUse"/> hands a potion here: the
    /// drink starts (the caller consumes the item), <c>IsDrinking</c> plays the UpperBody clip, and the heal lands at
    /// <see cref="CombatConfigSO.drinkHealTime"/>. Damage taken, a stance reaction, a knockout, death, swimming or a load
    /// interrupts it — before the sip the potion is lost. Attach to the Player root.
    /// </summary>
    public class PlayerPotionDrinker : MonoBehaviour, IPotionDrinker
    {
        private const string TAG = "[Player]";

        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private PlayerStateManager _stateManager;
        [Tooltip("Damage taken mid-drink interrupts it (Player root PlayerHealth).")]
        [SerializeField] private PlayerHealth _playerHealth;

        private DrinkTimeline _timeline;
        private PotionItemSO _potion;
        private uint _damageAtStart;

        public bool IsDrinking => _timeline.IsActive;

        private void Awake()
        {
            if (_stateManager == null) _stateManager = GetComponent<PlayerStateManager>();
            if (_playerHealth == null) _playerHealth = GetComponent<PlayerHealth>();
            if (_config == null) GameLog.Error(TAG, $"PlayerPotionDrinker on {gameObject.name}: no CombatConfigSO — potions heal instantly");
        }

        private void OnDisable() => Interrupt("disabled");

        public bool TryBeginDrink(PotionItemSO potion)
        {
            if (potion == null) return false;
            // Misconfigured: fall back to today's instant heal rather than eating the potion for nothing.
            if (_config == null || _stateManager == null || _playerHealth == null) return potion.ApplyHeal(gameObject);
            if (_playerHealth.IsDead || !_stateManager.CanDrink()) return false;

            _potion = potion;
            _damageAtStart = _playerHealth.DamageRevision;
            _timeline.Begin(_config.drinkHealTime, _config.drinkDuration);
            _stateManager.SetDrinking(true);
            // The potion is already gone and the heal pending: a save now would keep neither.
            SaveSystem.BlockSaving(this);
            GameLog.Info(TAG, $"Drinking {potion.itemName}");
            return true;
        }

        private void Update()
        {
            if (!_timeline.IsActive) return;

            if (TryGetInterruptReason(out string reason))
            {
                Interrupt(reason);
                return;
            }

            DrinkTick tick = _timeline.Tick(Time.deltaTime);
            if (tick == DrinkTick.Sip)
            {
                if (_potion != null) _potion.ApplyHeal(gameObject);
            }
            else if (tick == DrinkTick.Done)
            {
                Finish();
            }
        }

        private bool TryGetInterruptReason(out string reason)
        {
            reason = null;
            if (_playerHealth.IsDead) reason = "dead";
            else if (_playerHealth.DamageRevision != _damageAtStart) reason = "hit";
            else if (_stateManager.IsIncapacitated) reason = "staggered / down";
            else if (_stateManager.IsSwimming) reason = "swimming";
            else if (_stateManager.IsAirborne) reason = "airborne"; // fall / jump play full-body: the drink would be hidden
            else if (_stateManager.IsInDialogue) reason = "dialogue";
            else if (_stateManager.IsExecuting || _stateManager.IsScriptedHold) reason = "scripted";
            else if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) reason = "loading";
            return reason != null;
        }

        private void Interrupt(string reason)
        {
            bool lost = _timeline.Interrupt();
            if (_potion == null) return; // nothing running
            GameLog.Info(TAG, lost ? $"Drink interrupted ({reason}) — {_potion.itemName} lost" : $"Drink cut short ({reason})");
            Clear();
        }

        private void Finish() => Clear();

        private void Clear()
        {
            _potion = null;
            SaveSystem.UnblockSaving(this);
            if (_stateManager != null && _stateManager.IsDrinking) _stateManager.SetDrinking(false);
        }
    }
}
