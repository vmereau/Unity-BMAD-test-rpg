namespace Game.UI
{
    /// <summary>How a stat value is tinted in the item detail panel.</summary>
    public enum StatPolarity { Neutral, Positive, Negative }

    /// <summary>One label/value row shown in the item detail panel's stats block.</summary>
    public readonly struct ItemStatLine
    {
        public readonly string Label;
        /// <summary>Empty for label-only tag rows (e.g. "Consumable").</summary>
        public readonly string Value;
        public readonly StatPolarity Polarity;

        public ItemStatLine(string label, string value, StatPolarity polarity = StatPolarity.Neutral)
        {
            Label = label;
            Value = value;
            Polarity = polarity;
        }
    }
}
