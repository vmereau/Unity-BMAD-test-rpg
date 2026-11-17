namespace Game.Inventory
{
    /// <summary>
    /// Something that drinks a potion with the drink clip instead of healing at once (spec-potion-drink-animation).
    /// Implemented by the player's <c>PlayerPotionDrinker</c>; <see cref="PotionItemSO.OnUse"/> hands drunk potions to it.
    /// </summary>
    public interface IPotionDrinker
    {
        /// <summary>
        /// Starts drinking <paramref name="potion"/>. True = started: the caller consumes the item now (an interrupted drink
        /// loses it). False = refused (busy, already drinking, dead) — the item is kept.
        /// </summary>
        bool TryBeginDrink(PotionItemSO potion);
    }
}
