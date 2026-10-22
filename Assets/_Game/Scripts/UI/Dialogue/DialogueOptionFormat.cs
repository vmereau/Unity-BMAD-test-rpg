using System.Collections.Generic;
using System.Text;
using Game.Dialogue;

namespace Game.UI
{
    /// <summary>What an option leads to, shown as a coloured prefix tag.</summary>
    public enum DialogueOptionTag { None, Trade, Teach }

    /// <summary>
    /// Pure formatting rules for <see cref="DialogueUI"/> option rows: Trade / Teach classification, the rich-text
    /// label (dim number key, tag, dimmed seen text), teach cost labels with unpayable parts in red, and the
    /// unseen-first topic order. EditMode-tested.
    /// </summary>
    public static class DialogueOptionFormat
    {
        /// <summary>Nodes walked along <c>nextNode</c> before giving up — also stops authored loops.</summary>
        public const int MAX_CLASSIFY_STEPS = 32;

        // Art/ART_DIRECTION.md palette: dim parchment (number key, seen text), lightened brass (Trade),
        // mossy green (Teach), lightened dried blood (unpayable cost — the raw #7A2A22 is unreadable on the band).
        private const string KEY_HEX = "B8A988";
        private const string SEEN_HEX = "B8A98899";
        private const string TRADE_HEX = "B89A5E";
        private const string TEACH_HEX = "8A9466";
        private const string SHORTFALL_HEX = "A8473B";

        private static readonly StringBuilder _sb = new();

        /// <summary>
        /// Follows <paramref name="node"/> and its <c>nextNode</c> chain: Trade at a shop node, Teach at a teach node,
        /// None at a choice node (the player decides there), the end of the chain or after
        /// <see cref="MAX_CLASSIFY_STEPS"/> nodes.
        /// </summary>
        public static DialogueOptionTag Classify(DialogueNode node)
        {
            for (int i = 0; i < MAX_CLASSIFY_STEPS && node != null; i++)
            {
                switch (node)
                {
                    case ShopDialogueNode: return DialogueOptionTag.Trade;
                    case TeachChoiceDialogueNode: return DialogueOptionTag.Teach;
                    case ChoiceDialogueNode: return DialogueOptionTag.None;
                }
                node = node.nextNode;
            }
            return DialogueOptionTag.None;
        }

        /// <summary>
        /// Row label: "<c>1.</c> [Trade] text". <paramref name="keyLabel"/> null / empty = no number key (past slot 10).
        /// <paramref name="seen"/> draws the text in faded dim parchment. Authored text is wrapped in &lt;noparse&gt;;
        /// <paramref name="richSuffix"/> (e.g. a <see cref="CostLabel"/>) is appended as-is.
        /// </summary>
        public static string Label(string keyLabel, string text, DialogueOptionTag tag, bool seen, string richSuffix = null)
        {
            _sb.Clear();
            if (!string.IsNullOrEmpty(keyLabel))
                _sb.Append("<color=#").Append(KEY_HEX).Append('>').Append(keyLabel).Append(".</color> ");
            if (tag != DialogueOptionTag.None)
                _sb.Append("<color=#").Append(tag == DialogueOptionTag.Trade ? TRADE_HEX : TEACH_HEX).Append(">[")
                   .Append(tag).Append("]</color> ");
            if (seen) _sb.Append("<color=#").Append(SEEN_HEX).Append('>');
            _sb.Append("<noparse>").Append(text ?? string.Empty).Append("</noparse>");
            if (seen) _sb.Append("</color>");
            if (!string.IsNullOrEmpty(richSuffix)) _sb.Append(richSuffix);
            return _sb.ToString();
        }

        /// <summary>
        /// " (10 LP, 50g, Strength: 30)" — each part the player can't pay (<paramref name="shortfall"/>) in red.
        /// Empty when the choice is free and has no stat requirement.
        /// </summary>
        public static string CostLabel(TeachChoiceOption choice, TeachCostShortfall shortfall)
        {
            if (choice == null) return string.Empty;
            int lpCost = choice.skill != null ? choice.skill.lpCost : choice.statPoints;
            int gold = choice.goldCost;
            bool hasStatReqs = choice.skill != null && choice.skill.statsRequirements.Count > 0;
            if (lpCost <= 0 && gold <= 0 && !hasStatReqs) return string.Empty;

            _sb.Clear();
            _sb.Append(" (");
            bool first = true;
            if (lpCost > 0)
                AppendPart(ref first, $"{lpCost} LP", (shortfall & TeachCostShortfall.LearningPoints) != 0);
            if (gold > 0)
                AppendPart(ref first, $"{gold}g", (shortfall & TeachCostShortfall.Gold) != 0);
            if (hasStatReqs)
            {
                bool statsShort = (shortfall & TeachCostShortfall.Stats) != 0;
                foreach (var req in choice.skill.statsRequirements)
                    AppendPart(ref first, $"{req.statType}: {req.value}", statsShort);
            }
            _sb.Append(')');
            return _sb.ToString();
        }

        /// <summary>Stable partition: unseen items first, then seen ones, each group in its original order.</summary>
        public static List<T> OrderUnseenFirst<T>(IReadOnlyList<T> items, System.Func<T, bool> isSeen)
        {
            var result = new List<T>(items?.Count ?? 0);
            if (items == null) return result;
            foreach (var item in items)
                if (!isSeen(item)) result.Add(item);
            foreach (var item in items)
                if (isSeen(item)) result.Add(item);
            return result;
        }

        private static void AppendPart(ref bool first, string part, bool unpayable)
        {
            if (!first) _sb.Append(", ");
            first = false;
            if (unpayable) _sb.Append("<color=#").Append(SHORTFALL_HEX).Append('>').Append(part).Append("</color>");
            else _sb.Append(part);
        }
    }
}
