namespace Game.UI
{
    /// <summary>Which price the item detail panel shows, depending on the screen hosting it.</summary>
    public enum ItemPriceContext
    {
        /// <summary>Shows <c>sellValue</c> as "Value" (inventory, equipment, containers).</summary>
        Value,
        /// <summary>Shows <c>buyValue</c> as "Price" (trade, NPC side).</summary>
        Buy,
        /// <summary>Shows <c>sellValue</c> as "Sells for" (trade, player side).</summary>
        Sell
    }
}
