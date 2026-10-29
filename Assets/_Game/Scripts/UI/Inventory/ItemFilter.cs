using Game.Inventory;

namespace Game.UI
{
    /// <summary>Inventory filter tabs, in display order. <see cref="All"/> is never an item's own category.</summary>
    public enum ItemFilter
    {
        All,
        Weapons,
        Armor,
        Potions,
        Meals,
        Ingredients,
        CraftingMaterials,
        BooksAndRecipes,
        Misc
    }

    /// <summary>
    /// Pure filter logic for the inventory tabs. Same precedence as <see cref="ItemDetailFormatter.GetCategory"/>
    /// (the ingredient / crafting flags win over the class), so a tab and the detail category always agree.
    /// </summary>
    public static class ItemFilterRules
    {
        public const int COUNT = (int)ItemFilter.Misc + 1;

        public static ItemFilter Classify(ItemSO item) => item switch
        {
            null => ItemFilter.Misc,
            { isIngredient: true } => ItemFilter.Ingredients,
            { isCraftingMaterial: true } => ItemFilter.CraftingMaterials,
            WeaponSO => ItemFilter.Weapons,
            ArmorSO => ItemFilter.Armor,
            PotionItemSO { Kind: ConsumableKind.Meal } => ItemFilter.Meals,
            PotionItemSO => ItemFilter.Potions,
            SkillItemSO => ItemFilter.BooksAndRecipes,
            RecipeItemSO => ItemFilter.BooksAndRecipes,
            _ => ItemFilter.Misc
        };

        public static bool Matches(ItemSO item, ItemFilter filter) => filter == ItemFilter.All || Classify(item) == filter;

        public static string Label(ItemFilter filter) => filter switch
        {
            ItemFilter.All => "All",
            ItemFilter.Weapons => "Weapons",
            ItemFilter.Armor => "Armor",
            ItemFilter.Potions => "Potions",
            ItemFilter.Meals => "Meals",
            ItemFilter.Ingredients => "Ingredients",
            ItemFilter.CraftingMaterials => "Crafting Materials",
            ItemFilter.BooksAndRecipes => "Books & Recipes",
            _ => "Misc"
        };

        /// <summary>"Weapons (3)" — tab tooltip text.</summary>
        public static string TooltipText(ItemFilter filter, int count) => $"{Label(filter)} ({count})";
    }
}
