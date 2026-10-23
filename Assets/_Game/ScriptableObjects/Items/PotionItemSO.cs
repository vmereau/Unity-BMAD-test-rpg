using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// A consumable potion / food that restores player health when used from the inventory.
    /// A meal may also list permanent stat bonuses, granted only the first time it is eaten
    /// (claimed through <see cref="WorldStateManager.ClaimMealBonus"/>, so the claim is saved).
    /// Story 4.10: First concrete UsableItemSO subclass that affects player stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Potion Item", fileName = "Item_")]
    public class PotionItemSO : UsableItemSO
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private float _healAmount = 30f;

        [Header("First meal bonus")]
        [Tooltip("Permanent stat bonuses granted the first time this item is eaten.")]
        [SerializeField] private List<MealBonus> _permanentBonuses = new List<MealBonus>();
        [Tooltip("Raised with the bonus text (e.g. \"+5 Max HP\") when the first-meal bonus is granted (toast).")]
        [SerializeField] private GameEventSO_String _onMealBonusGained;

        public float HealAmount => _healAmount;

        public IReadOnlyList<MealBonus> PermanentBonuses => _permanentBonuses;

        /// <summary>True when at least one bonus entry has a positive amount.</summary>
        public bool HasPermanentBonus
        {
            get
            {
                if (_permanentBonuses == null) return false;
                foreach (var bonus in _permanentBonuses)
                    if (bonus.amount > 0) return true;
                return false;
            }
        }

        public override bool OnUse(GameObject user)
        {
            var health = user.GetComponent<PlayerHealth>();
            if (health == null)
            {
                GameLog.Warn(TAG, $"PotionItemSO.OnUse: no PlayerHealth on {user.name}");
                return false;
            }

            if (health.IsDead)
                return false;

            health.Heal(_healAmount);
            TryGrantPermanentBonus(user, health);
            return true;
        }

        private void TryGrantPermanentBonus(GameObject user, PlayerHealth health)
        {
            if (!HasPermanentBonus) return;
            var world = WorldStateManager.Instance;
            if (world == null)
            {
                GameLog.Warn(TAG, $"PotionItemSO.OnUse: no WorldStateManager (Core not loaded?) — {name} first-meal bonus skipped");
                return;
            }
            // No itemId → the claim can't be stored, so the bonus would be granted on every meal.
            if (string.IsNullOrEmpty(itemId))
            {
                GameLog.Warn(TAG, $"PotionItemSO.OnUse: {name} has no itemId — first-meal bonus skipped");
                return;
            }
            if (world.IsMealBonusClaimed(this)) return;

            // Check every target before applying anything: a claim without the bonus would lose it for good.
            if (health.MaxHealth <= 0f)
            {
                GameLog.Warn(TAG, $"PotionItemSO.OnUse: PlayerHealth on {user.name} has no config — {name} first-meal bonus not granted");
                return;
            }
            var stats = user.GetComponent<PlayerStats>();
            if (stats == null && NeedsPlayerStats())
            {
                GameLog.Warn(TAG, $"PotionItemSO.OnUse: no PlayerStats on {user.name} — {name} first-meal bonus not granted");
                return;
            }

            foreach (var bonus in _permanentBonuses)
            {
                if (bonus.amount <= 0) continue;
                if (bonus.stat == MealBonusStat.MaxHealth)
                    health.AddMaxHealthBonus(bonus.amount);
                else if (TryGetStatType(bonus.stat, out var statType))
                    stats.UpgradeStat(statType, bonus.amount);
                else
                    GameLog.Error(TAG, $"PotionItemSO.OnUse: {bonus.stat} has no PlayerStats mapping — bonus skipped");
            }

            world.ClaimMealBonus(this);
            string text = MealBonusText.Format(_permanentBonuses);
            _onMealBonusGained?.Raise(text);
            GameLog.Info(TAG, $"First meal of {name}: permanent {text}");
        }

        private bool NeedsPlayerStats()
        {
            foreach (var bonus in _permanentBonuses)
                if (bonus.amount > 0 && bonus.stat != MealBonusStat.MaxHealth) return true;
            return false;
        }

        // Every non-MaxHealth MealBonusStat must be listed — a new one needs its PlayerStats mapping here.
        private static bool TryGetStatType(MealBonusStat stat, out StatType statType)
        {
            switch (stat)
            {
                case MealBonusStat.Strength: statType = StatType.Strength; return true;
                case MealBonusStat.Dexterity: statType = StatType.Dexterity; return true;
                case MealBonusStat.Endurance: statType = StatType.Endurance; return true;
                case MealBonusStat.Intelligence: statType = StatType.Intelligence; return true;
                default: statType = default; return false;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_permanentBonuses == null) return;
            foreach (var bonus in _permanentBonuses)
                if (bonus.amount <= 0)
                    GameLog.Warn(TAG, $"{name}: first-meal bonus {bonus.stat} has amount {bonus.amount} — entry is ignored");
            if (HasPermanentBonus && _onMealBonusGained == null)
                GameLog.Warn(TAG, $"{name}: first-meal bonus has no _onMealBonusGained event — no toast will show");
        }
#endif
    }
}
