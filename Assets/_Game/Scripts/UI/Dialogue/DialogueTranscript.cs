using System.Collections.Generic;
using System.Text;

namespace Game.UI
{
    /// <summary>
    /// Short rolling history of the current dialogue topic ("You: …" / "Guard: …"), rendered as TMP rich text
    /// above the NPC line. Oldest lines are dropped past <see cref="Capacity"/> and drawn more transparent; long
    /// lines are cropped to <see cref="MaxLineChars"/> so the history can't push the current line out of the band.
    /// Speaker and text are wrapped in &lt;noparse&gt; so authored text can't break the fade markup.
    /// Plain class: owned and cleared by <see cref="DialogueUI"/>.
    /// </summary>
    public sealed class DialogueTranscript
    {
        public const string PLAYER_SPEAKER = "You";

        // Art/ART_DIRECTION.md palette: torch (NPC name), dim parchment (player name), parchment (text).
        private const string NPC_NAME_HEX = "E0A35A";
        private const string PLAYER_NAME_HEX = "B8A988";
        private const string TEXT_HEX = "D8CCB0";
        private const int NEWEST_ALPHA = 0xCC;
        private const int OLDEST_ALPHA = 0x66;

        private readonly struct Line
        {
            public readonly string Speaker;
            public readonly string Text;
            public readonly bool IsPlayer;

            public Line(string speaker, string text, bool isPlayer)
            {
                Speaker = speaker;
                Text = text;
                IsPlayer = isPlayer;
            }
        }

        private readonly List<Line> _lines = new();
        private readonly StringBuilder _sb = new();

        public int Capacity { get; }
        /// <summary>Longest text kept per line (longer is cropped with an ellipsis). 0 = no limit.</summary>
        public int MaxLineChars { get; }
        public int Count => _lines.Count;

        public DialogueTranscript(int capacity, int maxLineChars = 0)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            MaxLineChars = maxLineChars < 0 ? 0 : maxLineChars;
        }

        /// <summary>
        /// Appends a line; empty text and an exact repeat of the newest line (a re-shown prompt) are ignored.
        /// Drops the oldest line past capacity.
        /// </summary>
        public void Add(string speaker, string text, bool isPlayer)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            speaker ??= string.Empty;
            text = Crop(text.Trim(), MaxLineChars);
            if (_lines.Count > 0)
            {
                Line last = _lines[_lines.Count - 1];
                if (last.IsPlayer == isPlayer && last.Speaker == speaker && last.Text == text) return;
            }
            _lines.Add(new Line(speaker, text, isPlayer));
            while (_lines.Count > Capacity)
                _lines.RemoveAt(0);
        }

        public void Clear() => _lines.Clear();

        /// <summary>Lines oldest first, one per row, speaker-coloured; empty string when there is no line.</summary>
        public string BuildRichText()
        {
            _sb.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                Line line = _lines[i];
                string alpha = LineAlpha(_lines.Count - 1 - i, Capacity).ToString("X2");
                if (i > 0) _sb.Append('\n');
                _sb.Append("<color=#").Append(line.IsPlayer ? PLAYER_NAME_HEX : NPC_NAME_HEX).Append(alpha).Append('>')
                   .Append("<noparse>").Append(line.Speaker).Append("</noparse>:</color> ")
                   .Append("<color=#").Append(TEXT_HEX).Append(alpha).Append('>')
                   .Append("<noparse>").Append(line.Text).Append("</noparse></color>");
            }
            return _sb.ToString();
        }

        /// <summary>Crops <paramref name="text"/> to <paramref name="maxChars"/> (ellipsis included). 0 = unchanged.</summary>
        public static string Crop(string text, int maxChars)
        {
            if (maxChars <= 0 || text.Length <= maxChars) return text;
            return maxChars == 1 ? "…" : text.Substring(0, maxChars - 1).TrimEnd() + "…";
        }

        /// <summary>
        /// Alpha (0–255) of a line <paramref name="ageFromNewest"/> rows above the newest one: newest is
        /// <see cref="NEWEST_ALPHA"/>, falling linearly to <see cref="OLDEST_ALPHA"/> at the capacity's last row.
        /// </summary>
        public static int LineAlpha(int ageFromNewest, int capacity)
        {
            if (ageFromNewest <= 0 || capacity <= 1) return NEWEST_ALPHA;
            if (ageFromNewest >= capacity - 1) return OLDEST_ALPHA;
            return NEWEST_ALPHA - ageFromNewest * (NEWEST_ALPHA - OLDEST_ALPHA) / (capacity - 1);
        }
    }
}
