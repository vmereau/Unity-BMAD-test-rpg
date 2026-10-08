using Game.Core;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// A reusable set of short one-liners ("barks") an NPC can say through a speech bubble (sneak warnings, later
    /// pickpocket / lockpick caught, ambient greetings). Holds no runtime state: the caller owns the last picked
    /// index so the same line is never said twice in a row.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Bark Set", fileName = "Barks_")]
    public class BarkSetSO : ScriptableObject
    {
        private const string TAG = "[Dialogue]";

        [SerializeField, TextArea] private string[] _lines;

        public int Count => _lines != null ? _lines.Length : 0;

        /// <summary>Random line that differs from <paramref name="lastIndex"/> (updated); null when empty.</summary>
        public string GetRandomLine(ref int lastIndex)
        {
            int index = PickIndex(Count, lastIndex, Random.value);
            if (index < 0) return null;
            lastIndex = index;
            return _lines[index];
        }

        /// <summary>
        /// Index in <c>[0, count)</c> from <paramref name="random01"/> (0..1 inclusive), never equal to a valid
        /// <paramref name="lastIndex"/> when count ≥ 2. −1 when count ≤ 0.
        /// </summary>
        public static int PickIndex(int count, int lastIndex, float random01)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            random01 = Mathf.Clamp01(random01);
            if (lastIndex < 0 || lastIndex >= count)
                return Mathf.Min((int)(random01 * count), count - 1);
            int i = Mathf.Min((int)(random01 * (count - 1)), count - 2);
            return i >= lastIndex ? i + 1 : i;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Count == 0)
            {
                GameLog.Warn(TAG, $"'{name}': bark set has no lines");
                return;
            }
            for (int i = 0; i < _lines.Length; i++)
                if (string.IsNullOrWhiteSpace(_lines[i]))
                    GameLog.Warn(TAG, $"'{name}': line {i} is empty");
        }
#endif
    }
}
