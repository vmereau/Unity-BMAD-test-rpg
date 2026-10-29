using System;
using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using Game.StatusEffects;
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
        private const string CATEGORY_MEAL = "Meal";
        private const string CATEGORY_INGREDIENT = "Ingredient";
        private const string CATEGORY_CRAFTING_MATERIAL = "Crafting Material";
        private const string CATEGORY_FORGED_WEAPON = "Weapon · Forged";
        private const string CATEGORY_SKILL_TOME = "Skill Tome";
        private const string CATEGORY_RECIPE = "Recipe";
        private const string CATEGORY_USABLE = "Usable";
        private const string CATEGORY_EQUIPMENT = "Equipment";
        private const string CATEGORY_MISC = "Miscellaneous";

        private const string LABEL_DAMAGE = "Damage";
        private const string LABEL_FORGED = "Forged";
        private const string FORGED_OVER = " over ";
        private const string LABEL_RESTORES_HEALTH = "Restores Health";
        private const string OVER_TIME_PREFIX = " over ";
        private const string LABEL_TEACHES = "Teaches";
        private const string LABEL_LP_COST = "LP Cost";
        private const string LABEL_STATUS = "Status";
        private const string STATUS_ALREADY_KNOWN = "Already known";
        private const string STATUS_NOT_LEARNED = "Not learned";
        private const string LABEL_FIRST_MEAL = "First meal";
        private const string FIRST_MEAL_GAINED_SUFFIX = " (gained)";
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
            // Any item type can be a cooking ingredient (raw food is a PotionItemSO): the flag wins over the class.
            { isIngredient: true } => CATEGORY_INGREDIENT,
            { isCraftingMaterial: true } => CATEGORY_CRAFTING_MATERIAL,
            WeaponSO { IsForged: true } => CATEGORY_FORGED_WEAPON,
            WeaponSO => CATEGORY_WEAPON,
            ArmorSO armor => CATEGORY_ARMOR_PREFIX + ArmorSlotName(armor.slot),
            PotionItemSO { Kind: ConsumableKind.Meal } => CATEGORY_MEAL,
            PotionItemSO => CATEGORY_POTION,
            SkillItemSO => CATEGORY_SKILL_TOME,
            RecipeItemSO => CATEGORY_RECIPE,
            UsableItemSO => CATEGORY_USABLE,
            EquipableItemSO => CATEGORY_EQUIPMENT,
            _ => CATEGORY_MISC
        };

        /// <summary>
        /// Clears <paramref name="into"/> and fills it with the item's stat rows, in display order.
        /// <paramref name="isRecipeKnown"/> answers the recipe status row; null = no status row.
        /// <paramref name="isMealBonusClaimed"/> grays out a food's "First meal" bonus row once claimed; null = shown unclaimed.
        /// </summary>
        public static void BuildStatLines(ItemSO item, List<ItemStatLine> into, Func<RecipeFact, bool> isRecipeKnown = null,
            Func<ItemSO, bool> isMealBonusClaimed = null)
        {
            if (into == null) { GameLog.Warn(TAG, "BuildStatLines: target list is null"); return; }
            into.Clear();
            if (item == null) return;

            switch (item)
            {
                // Floats are rounded first so the shown value, the polarity and the omission rule agree.
                case WeaponSO weapon:
                    AddBonus(into, LABEL_DAMAGE, Mathf.RoundToInt(weapon.damageBonus));
                    if (weapon.IsForged)
                    {
                        // Forged bonus = damage over the standard counterpart, e.g. "+4 over Short Iron Sword".
                        int delta = Mathf.RoundToInt(weapon.damageBonus) - Mathf.RoundToInt(weapon.baseWeapon.damageBonus);
                        if (delta > 0)
                            into.Add(new ItemStatLine(LABEL_FORGED, Signed(delta) + FORGED_OVER + (string.IsNullOrEmpty(weapon.baseWeapon.itemName) ? weapon.baseWeapon.name : weapon.baseWeapon.itemName),
                                StatPolarity.Positive));
                    }
                    break;
                case PotionItemSO potion when Mathf.RoundToInt(potion.HealAmount) > 0:
                    int heal = Mathf.RoundToInt(potion.HealAmount);
                    if (potion.HealsOverTime)
                        into.Add(new ItemStatLine(LABEL_RESTORES_HEALTH,
                            Signed(heal) + OVER_TIME_PREFIX + StatusEffectMath.FormatDuration(potion.DurationMinutes), StatPolarity.Positive));
                    else
                        AddBonus(into, LABEL_RESTORES_HEALTH, heal);
                    break;
                case SkillItemSO skillItem when skillItem.Skill != null:
                    into.Add(new ItemStatLine(LABEL_TEACHES, skillItem.Skill.displayName));
                    into.Add(new ItemStatLine(LABEL_LP_COST, skillItem.Skill.lpCost.ToString()));
                    break;
                case RecipeItemSO recipeItem when recipeItem.IsConfigured:
                    into.Add(new ItemStatLine(LABEL_TEACHES, recipeItem.Recipe.output.itemName));
                    if (isRecipeKnown != null)
                        into.Add(isRecipeKnown(recipeItem.Recipe.knownFact)
                            ? new ItemStatLine(LABEL_STATUS, STATUS_ALREADY_KNOWN, StatPolarity.Negative)
                            : new ItemStatLine(LABEL_STATUS, STATUS_NOT_LEARNED, StatPolarity.Positive));
                    break;
            }

            if (item is PotionItemSO meal && meal.HasPermanentBonus)
                into.Add(FirstMealLine(meal, isMealBonusClaimed != null && isMealBonusClaimed(meal)));

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

        /// <summary>
        /// The taught skill's description followed by its effect text (one per line) for a skill tome,
        /// or null when the skill is absent or both texts are blank.
        /// </summary>
        public static string GetSkillDescription(ItemSO item)
        {
            if (item is not SkillItemSO skillItem || skillItem.Skill == null) return null;
            bool hasDesc = !string.IsNullOrWhiteSpace(skillItem.Skill.description);
            bool hasEffect = !string.IsNullOrWhiteSpace(skillItem.Skill.effectDescription);
            if (hasDesc && hasEffect) return skillItem.Skill.description.Trim() + "\n" + skillItem.Skill.effectDescription.Trim();
            if (hasDesc) return skillItem.Skill.description;
            return hasEffect ? skillItem.Skill.effectDescription.Trim() : null;
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

        private static ItemStatLine FirstMealLine(PotionItemSO meal, bool claimed)
        {
            string text = MealBonusText.Format(meal.PermanentBonuses);
            return claimed
                ? new ItemStatLine(LABEL_FIRST_MEAL, text + FIRST_MEAL_GAINED_SUFFIX, StatPolarity.Muted)
                : new ItemStatLine(LABEL_FIRST_MEAL, text, StatPolarity.Positive);
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
