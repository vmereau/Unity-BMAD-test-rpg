using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Inventory
{
    /// <summary>Stat a meal's one-time permanent bonus raises.</summary>
    public enum MealBonusStat { MaxHealth, Strength, Dexterity, Endurance, Intelligence }

    /// <summary>One permanent stat bonus granted the first time a meal is eaten (<see cref="PotionItemSO"/>).</summary>
    [Serializable]
    public struct MealBonus
    {
        public MealBonusStat stat;
        public int amount;
    }

    /// <summary>Pure display text for meal bonuses — shared by the item detail row and the toast.</summary>
    public static class MealBonusText
    {
        /// <summary>"+5 Max HP, +1 Strength"; entries with a non-positive amount are skipped. Empty when nothing is left.</summary>
        public static string Format(IReadOnlyList<MealBonus> bonuses)
        {
            if (bonuses == null) return "";
            var sb = new StringBuilder();
            foreach (var bonus in bonuses)
            {
                if (bonus.amount <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append('+').Append(bonus.amount).Append(' ').Append(StatName(bonus.stat));
            }
            return sb.ToString();
        }

        public static string StatName(MealBonusStat stat) => stat switch
        {
            MealBonusStat.MaxHealth => "Max HP",
            _ => stat.ToString()
        };
    }
}
