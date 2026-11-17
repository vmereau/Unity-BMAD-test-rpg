using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// An NPC's potion drink (spec-potion-drink-animation). <see cref="EntityCombatTactics"/> starts it on the
    /// <see cref="CombatAction.Drink"/> decision: the first drinkable <see cref="PotionItemSO"/> leaves the
    /// <see cref="InventorySystem"/>, the UpperBody drink clip plays (<c>IsDrinking</c>) and the heal lands at
    /// <see cref="CombatConfigSO.drinkHealTime"/>. Damage taken, <see cref="EntityBrain"/> ending tactics (stagger, knockout,
    /// death, disengage) or a disable interrupts it — before the sip the potion is lost. Drinks per fight and the cooldown
    /// come from the <see cref="CombatStyleSO"/>; <see cref="ResetFight"/> on leaving combat.
    /// </summary>
    public class EntityPotionDrinker : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private PersistentID _persistentID;
        [Tooltip("Plays the drink (IsDrinking). Auto-resolved on the same GO if null.")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        private EntityHealth _health;
        private InventorySystem _inventory;
        private EntityMeleeAttacker _attacker;
        private EntityStance _stance;
        private NpcArmament _armament;
        private DrinkTimeline _timeline;
        private PotionItemSO _potion;
        private float _lastHealth;
        private float _nextDrinkTime;
        private bool _warnedNoConfig;

        public bool IsDrinking => _timeline.IsActive;
        public int DrinksUsed { get; private set; }
        public float CooldownLeft => Mathf.Max(0f, _nextDrinkTime - Time.time);

        /// <summary>Set up to drink at all (inventory + CombatConfigSO). False → tactics never plan a drink.</summary>
        public bool CanDrinkAtAll => _inventory != null && _health != null && _health.CombatConfig != null;

        /// <summary>Carries a potion it can drink (meals and raw food are never drunk in a fight).</summary>
        public bool HasPotion => FindPotion() != null;

        private CombatStyleSO Style => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.CombatStyle : null;

        private void Awake()
        {
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
            if (_animationDriver == null) _animationDriver = GetComponent<AIAnimationDriver>();
            _health = GetComponent<EntityHealth>();
            _inventory = GetComponent<InventorySystem>();
            _attacker = GetComponent<EntityMeleeAttacker>();
            _stance = GetComponent<EntityStance>();
            _armament = GetComponent<NpcArmament>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _lastHealth = _health.CurrentHealth;
                _health.HealthChanged += HandleHealthChanged;
            }
            DrinksUsed = 0;
            _nextDrinkTime = 0f;
        }

        private void OnDisable()
        {
            if (_health != null) _health.HealthChanged -= HandleHealthChanged;
            Interrupt("disabled");
        }

        /// <summary>Starts drinking the first potion it carries. False: no potion, no config, already drinking, down.</summary>
        public bool TryBegin()
        {
            if (IsDrinking || _health == null || _inventory == null || _health.IsDead || _health.IsUnconscious) return false;
            // Never over a swing / its recovery, a draw / sheathe or a stance reaction: the clip would be hidden or cut.
            if ((_attacker != null && _attacker.IsInAttackState) || (_armament != null && _armament.IsSwitchingWeapon)
                || (_stance != null && _stance.IsIncapacitated)) return false;
            CombatConfigSO config = _health.CombatConfig;
            if (config == null)
            {
                if (!_warnedNoConfig) GameLog.Warn(TAG, $"{gameObject.name}: no CombatConfigSO on EntityHealth — never drinks");
                _warnedNoConfig = true;
                return false;
            }

            PotionItemSO potion = FindPotion();
            if (potion == null || !_inventory.TryConsume(potion)) return false;

            _potion = potion;
            _lastHealth = _health.CurrentHealth;
            _timeline.Begin(config.drinkHealTime, config.drinkDuration);
            DrinksUsed++;
            if (_animationDriver != null) _animationDriver.SetDrinking(true);
            GameLog.Info(TAG, $"{gameObject.name} drinks {potion.itemName} (HP {_health.CurrentHealth:F0}/{_health.MaxHealth:F0})");
            return true;
        }

        /// <summary>Stops a running drink (before the sip the potion is lost). Idempotent.</summary>
        public void Interrupt(string reason)
        {
            bool lost = _timeline.Interrupt();
            if (_potion == null) return;
            GameLog.Info(TAG, lost
                ? $"{gameObject.name} drink interrupted ({reason}) — {_potion.itemName} lost"
                : $"{gameObject.name} drink cut short ({reason})");
            EndDrink();
        }

        /// <summary>New fight: the drinks-per-fight count and the cooldown start over.</summary>
        public void ResetFight()
        {
            DrinksUsed = 0;
            _nextDrinkTime = 0f;
        }

        private void Update()
        {
            if (!_timeline.IsActive) return;
            // Re-sent every frame: an NpcArmament controller swap resets every animator parameter.
            if (_animationDriver != null) _animationDriver.SetDrinking(true);

            DrinkTick tick = _timeline.Tick(Time.deltaTime);
            if (tick == DrinkTick.Sip)
            {
                if (_potion != null && _potion.ApplyHeal(_health)) _lastHealth = _health.CurrentHealth;
            }
            else if (tick == DrinkTick.Done)
            {
                EndDrink();
            }
        }

        // A hit that lands (health went down) interrupts the drink — a blocked hit can't happen (no guard while drinking).
        private void HandleHealthChanged(float current, float max)
        {
            bool decreased = current < _lastHealth;
            _lastHealth = current;
            if (decreased && IsDrinking) Interrupt("hit");
        }

        private void EndDrink()
        {
            _potion = null;
            CombatStyleSO style = Style;
            _nextDrinkTime = Time.time + (style != null ? style.drinkCooldown : 0f);
            if (_animationDriver != null) _animationDriver.SetDrinking(false);
        }

        private PotionItemSO FindPotion()
        {
            if (_inventory == null) return null;
            var items = _inventory.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Item is PotionItemSO potion && potion.IsDrunk && potion.HealAmount > 0f) return potion;
            return null;
        }
    }
}
