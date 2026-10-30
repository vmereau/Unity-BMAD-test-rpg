using System.Collections.Generic;

namespace Game.Combat
{
    /// <summary>
    /// Drops the second copy of a clip event in one frame. The FullBody Animator layer is synced to the Attack layer and
    /// plays the same clip, so while it has weight every clip event can fire twice in one frame (once per layer).
    /// A copy = same event, same <c>key</c> (player: clip + event time; NPC: hitbox id), same frame — so different
    /// events in one frame (<c>HitboxDisable</c> + <c>ComboWindowOpen</c> at the same time) and the same event from two
    /// different clips (outgoing / incoming state of a crossfade) are all forwarded.
    /// </summary>
    public sealed class ClipEventDeduper
    {
        public enum ClipEvent { ComboWindowOpen, ComboWindowClose, HitboxEnable, HitboxDisable, WeaponSwap, Count }

        private readonly int[] _frame = new int[(int)ClipEvent.Count];
        private readonly List<int>[] _keys = new List<int>[(int)ClipEvent.Count];

        public ClipEventDeduper()
        {
            for (int i = 0; i < _frame.Length; i++)
            {
                _frame[i] = int.MinValue;
                _keys[i] = new List<int>(2);
            }
        }

        /// <summary>True the first time <paramref name="clipEvent"/> with <paramref name="key"/> arrives in <paramref name="frame"/>.</summary>
        public bool ShouldForward(ClipEvent clipEvent, int key, int frame)
        {
            int index = (int)clipEvent;
            List<int> keys = _keys[index];
            if (_frame[index] != frame)
            {
                _frame[index] = frame;
                keys.Clear();
            }
            else if (keys.Contains(key))
            {
                return false;
            }
            keys.Add(key);
            return true;
        }

        /// <summary>Key of a player clip event: the clip it came from and its time (the synced copy has both equal).</summary>
        public static int ClipKey(int clipId, float eventTime) =>
            unchecked(clipId * 397 ^ eventTime.GetHashCode());
    }
}
