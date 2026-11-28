namespace Game.Inventory
{
    /// <summary>
    /// Kind of item a dialogue choice asks the player to hand over (<c>ChoiceOption.requiredCategory</c>); the player
    /// picks which one in the Give window. Serialized as int: only append new values, never reorder.
    /// </summary>
    public enum GiveItemCategory
    {
        None = 0,
        /// <summary>Food eaten as is (<see cref="PotionItemSO.IsReadyToEat"/>): apple, bread. Not raw fish or herbs.</summary>
        ReadyFood = 1,
        /// <summary>Cooked meal (<see cref="PotionItemSO.Kind"/> = <see cref="ConsumableKind.Meal"/>).</summary>
        Meal = 2
    }

    /// <summary>Pure rules for <see cref="GiveItemCategory"/>: which items match, and the label shown on the choice row.</summary>
    public static class GiveItemRules
    {
        /// <summary>True when <paramref name="item"/> can be given for <paramref name="category"/>. Quest items never match.</summary>
        public static bool Matches(ItemSO item, GiveItemCategory category)
        {
            if (!ItemSO.CanPlayerPart(item)) return false;   // quest items and keys are never given for a category
            if (!(item is PotionItemSO food)) return false;
            switch (category)
            {
                case GiveItemCategory.ReadyFood: return food.IsReadyToEat && food.Kind != ConsumableKind.Meal;
                case GiveItemCategory.Meal: return food.Kind == ConsumableKind.Meal;
                default: return false;
            }
        }

        /// <summary>"Food" / "Meal"; empty for <see cref="GiveItemCategory.None"/>.</summary>
        public static string Label(GiveItemCategory category) => category switch
        {
            GiveItemCategory.ReadyFood => "Food",
            GiveItemCategory.Meal => "Meal",
            _ => string.Empty
        };
    }
}
