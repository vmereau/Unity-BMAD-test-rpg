namespace Game.World
{
    /// <summary>Where the player holds a dialogue choice's requiredItem.</summary>
    public enum ChoiceItemSource { None, Bag, Equipped }

    /// <summary>
    /// Pure rules for a ChoiceOption.requiredItem hand-over: the bag wins over a worn copy, so a worn item is only
    /// unequipped when no unit is loose in the inventory.
    /// </summary>
    public static class ChoiceItemRules
    {
        public static ChoiceItemSource Locate(int bagCount, bool equipped) =>
            bagCount > 0 ? ChoiceItemSource.Bag
            : equipped ? ChoiceItemSource.Equipped
            : ChoiceItemSource.None;
    }
}
