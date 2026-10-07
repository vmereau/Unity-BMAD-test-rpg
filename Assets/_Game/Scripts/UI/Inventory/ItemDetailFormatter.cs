using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Pure display logic for <see cref="ItemDetailPanelUI"/>: category line, stat rows and price.
    /// No Unity lifecycle, so it is covered by EditMode tests.
    /// </summary>
    public static class ItemDetailFormatter
    {
        private const string TAG = "[ItemDetailFormatter]";

        private const string CATEGORY_WEAPON = "Weapon";
        private const string CATEGORY_ARMOR_PREFIX = "Armor · ";
        private const string CATEGORY_POTION = "Potion";
        private const string CATEGORY_SKILL_TOME = "Skill Tome";
        private const string CATEGORY_USABLE = "Usable";
        private const string CATEGORY_EQUIPMENT = "Equipment";
        private const string CATEGORY_MISC = "Miscellaneous";

        private const string LABEL_DAMAGE = "Damage";
        private const string LABEL_RESTORES_HEALTH = "Restores Health";
        private const string LABEL_TEACHES = "Teaches";
        private const string LABEL_LP_COST = "LP Cost";
        private const string LABEL_STRENGTH = "Strength";
        private const string LABEL_DEXTERITY = "Dexterity";
        private const string LABEL_ENDURANCE = "Endurance";
        private const string LABEL_INTELLIGENCE = "Intelligence";
        private const string LABEL_DEFENSE = "Defense";
        private const string LABEL_CONSUMABLE = "Consumable";
        private const string LABEL_REUSABLE = "Reusable";
        private const string LABEL_STACK = "Stack";
        private const string STACK_PREFIX = "up to ";

        private const string PRICE_LABEL_VALUE = "Value";
        private const string PRICE_LABEL_BUY = "Price";
        private const string PRICE_LABEL_SELL = "Sells for";

        public static string GetCategory(ItemSO item) => item switch
        {
            null => "",
            WeaponSO => CATEGORY_WEAPON,
            ArmorSO armor => CATEGORY_ARMOR_PREFIX + ArmorSlotName(armor.slot),
            PotionItemSO => CATEGORY_POTION,
            SkillItemSO => CATEGORY_SKILL_TOME,
            UsableItemSO => CATEGORY_USABLE,
            EquipableItemSO => CATEGORY_EQUIPMENT,
            _ => CATEGORY_MISC
        };

        /// <summary>Clears <paramref name="into"/> and fills it with the item's stat rows, in display order.</summary>
        public static void BuildStatLines(ItemSO item, List<ItemStatLine> into)
        {
            if (into == null) { GameLog.Warn(TAG, "BuildStatLines: target list is null"); return; }
            into.Clear();
            if (item == null) return;

            switch (item)
            {
                // Floats are rounded first so the shown value, the polarity and the omission rule agree.
                case WeaponSO weapon:
                    AddBonus(into, LABEL_DAMAGE, Mathf.RoundToInt(weapon.damageBonus));
                    break;
                case PotionItemSO potion when Mathf.RoundToInt(potion.HealAmount) > 0:
                    AddBonus(into, LABEL_RESTORES_HEALTH, Mathf.RoundToInt(potion.HealAmount));
                    break;
                case SkillItemSO skillItem when skillItem.Skill != null:
                    into.Add(new ItemStatLine(LABEL_TEACHES, skillItem.Skill.displayName));
                    into.Add(new ItemStatLine(LABEL_LP_COST, skillItem.Skill.lpCost.ToString()));
                    break;
            }

            if (item is EquipableItemSO equipable)
            {
                AddBonus(into, LABEL_STRENGTH, equipable.strengthBonus);
                AddBonus(into, LABEL_DEXTERITY, equipable.dexterityBonus);
                AddBonus(into, LABEL_ENDURANCE, equipable.enduranceBonus);
                AddBonus(into, LABEL_INTELLIGENCE, equipable.intelligenceBonus);
                AddBonus(into, LABEL_DEFENSE, equipable.defenseBonus);
            }

            if (item is UsableItemSO usable)
                into.Add(new ItemStatLine(usable.consumable ? LABEL_CONSUMABLE : LABEL_REUSABLE, ""));

            if (item.IsStackable)
                into.Add(new ItemStatLine(LABEL_STACK, STACK_PREFIX + item.maxStacks));
        }

        /// <summary>The taught skill's description for a skill tome, or null when absent or blank.</summary>
        public static string GetSkillDescription(ItemSO item)
        {
            if (item is not SkillItemSO skillItem || skillItem.Skill == null) return null;
            string desc = skillItem.Skill.description;
            return string.IsNullOrWhiteSpace(desc) ? null : desc;
        }

        public static string GetPriceLabel(ItemPriceContext context) => context switch
        {
            ItemPriceContext.Buy => PRICE_LABEL_BUY,
            ItemPriceContext.Sell => PRICE_LABEL_SELL,
            _ => PRICE_LABEL_VALUE
        };

        public static int GetPrice(ItemSO item, ItemPriceContext context)
        {
            if (item == null) return 0;
            return context == ItemPriceContext.Buy ? item.buyValue : item.sellValue;
        }

        private static void AddBonus(List<ItemStatLine> into, string label, int value)
        {
            if (value == 0) return;
            into.Add(new ItemStatLine(label, Signed(value), Polarity(value)));
        }

        private static string ArmorSlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Helmet => "Helmet",
            EquipmentSlot.Armor => "Body Armor",
            EquipmentSlot.Ring1 => "Ring",
            EquipmentSlot.Ring2 => "Ring",
            EquipmentSlot.Necklace => "Necklace",
            _ => slot.ToString()
        };

        // Negative numbers already carry their "-" from ToString.
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private static StatPolarity Polarity(int value) =>
            value > 0 ? StatPolarity.Positive : value < 0 ? StatPolarity.Negative : StatPolarity.Neutral;
    }
}
