using Game.Inventory;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Computed fact: true once the first-meal bonus of <see cref="Meal"/> was claimed
    /// (<see cref="WorldStateManager.IsMealBonusClaimed"/>), i.e. the player ate it for the first time.
    /// Only meaningful for a food with a permanent bonus: without one nothing is ever claimed, so it stays false
    /// (warned here and by Quest Explorer V19). NOT stored in <c>_worldFacts</c>; <c>ClaimMealBonus</c> raises
    /// <c>OnFactChanged</c> with <see cref="ToString"/> so quests and memories update. Prefix intentionally not set.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Meal Eaten Fact", fileName = "MealEatenFact_")]
    public class MealEatenFact : Fact
    {
        [Tooltip("Food whose first eat (first-meal bonus claimed) makes this fact true. Needs a first-meal bonus.")]
        [SerializeField] private PotionItemSO _meal;

        public PotionItemSO Meal => _meal;

        /// <summary>Runtime/test initialiser.</summary>
        public MealEatenFact Init(PotionItemSO meal)
        {
            _meal = meal;
            return this;
        }

        /// <summary>Change-event key for a meal (debug only, not a stored key).</summary>
        public static string KeyFor(string itemId) => $"{WorldFactPrefix.Meal}.Eaten.{itemId}";

        /// <summary>Why this fact can never become true, or null when it is fine (shared with Quest Explorer V19).</summary>
        public static string Problem(PotionItemSO meal)
        {
            if (meal == null) return "has no meal — always false";
            if (string.IsNullOrEmpty(meal.itemId)) return $"meal '{meal.name}' has no itemId — always false";
            if (!meal.HasPermanentBonus) return $"meal '{meal.name}' has no first-meal bonus — never claimed, always false";
            return null;
        }

        /// <summary>For debugging / change events only — not a stored key.</summary>
        public override string ToString() => KeyFor(_meal != null ? _meal.itemId : "null");

#if UNITY_EDITOR
        private void OnValidate()
        {
            string problem = Problem(_meal);
            if (problem != null) GameLog.Warn("[MealEatenFact]", $"{name} {problem}");
        }
#endif
    }
}
