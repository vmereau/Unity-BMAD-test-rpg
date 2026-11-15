namespace Game.UI
{
    /// <summary>
    /// Pure batch-size and label rules for the Shift bulk transfer on the container and trade screens
    /// (take / put / buy / sell). Covered by <c>BulkTransferRulesTests</c>.
    /// </summary>
    public static class BulkTransferRules
    {
        /// <summary>Most units one Shift action moves.</summary>
        public const int BulkSize = 10;

        /// <summary>
        /// Units one action moves. Without <paramref name="bulk"/>: 1. With it: min(BulkSize, slot units, units the
        /// budget affords), never below 1 while the slot exists (an unaffordable item still shows ×1 — the button is
        /// disabled by the caller). <paramref name="unitPrice"/> ≤ 0 means no price cap (take / put / free items).
        /// Returns 0 only for an empty slot.
        /// </summary>
        public static int Quantity(bool bulk, int slotCount, int unitPrice, int budget)
        {
            if (slotCount <= 0) return 0;
            if (!bulk) return 1;
            int qty = slotCount < BulkSize ? slotCount : BulkSize;
            if (unitPrice > 0)
            {
                int affordable = budget > 0 ? budget / unitPrice : 0;
                if (affordable < qty) qty = affordable;
            }
            return qty < 1 ? 1 : qty;
        }

        /// <summary>"Buy" at ×1, "Buy ×7" above.</summary>
        public static string ActionLabel(string verb, int quantity) =>
            quantity > 1 ? $"{verb} ×{quantity}" : verb;
    }
}
