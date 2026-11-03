using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Pure helpers for <see cref="SfxPlayer"/>: variation pick, volume / pitch jitter, cooldown.
    /// Random values are passed in (0–1) so tests are deterministic.
    /// </summary>
    public static class SoundCuePicker
    {
        /// <summary>
        /// Index in [0, count) that differs from <paramref name="lastIndex"/> when count &gt; 1.
        /// Returns -1 when count &lt;= 0. <paramref name="random01"/> in [0, 1].
        /// </summary>
        public static int PickIndex(int count, int lastIndex, float random01)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            float r = Mathf.Clamp01(random01);
            bool hasLast = lastIndex >= 0 && lastIndex < count;
            int choices = hasLast ? count - 1 : count;
            int index = Mathf.Min(choices - 1, Mathf.FloorToInt(r * choices));
            if (hasLast && index >= lastIndex) index++;
            return index;
        }

        /// <summary>
        /// <paramref name="start"/> if that slot is set, else the next set slot (wrapping); -1 when all are null.
        /// Uses Unity's null check, so destroyed / missing assets count as empty.
        /// </summary>
        public static int FirstNonNull<T>(T[] items, int start) where T : Object
        {
            if (items == null || items.Length == 0) return -1;
            int n = items.Length;
            int s = ((start % n) + n) % n;
            for (int i = 0; i < n; i++)
            {
                int idx = (s + i) % n;
                if (items[idx] != null) return idx;
            }
            return -1;
        }

        /// <summary>Volume reduced by up to <paramref name="jitter"/> × volume. Result clamped to [0, 1].</summary>
        public static float JitterVolume(float volume, float jitter, float random01)
        {
            float v = Mathf.Clamp01(volume);
            return Mathf.Clamp01(v * (1f - Mathf.Clamp01(jitter) * Mathf.Clamp01(random01)));
        }

        /// <summary>Pitch between min and max (swapped if inverted).</summary>
        public static float PickPitch(float min, float max, float random01)
        {
            if (max < min) (min, max) = (max, min);
            return Mathf.Lerp(min, max, Mathf.Clamp01(random01));
        }

        /// <summary>True while <paramref name="now"/> is within <paramref name="cooldown"/> of the last play. Never-played = negative lastPlay.</summary>
        public static bool IsOnCooldown(float now, float lastPlay, float cooldown)
        {
            if (lastPlay < 0f || cooldown <= 0f) return false;
            return now - lastPlay < cooldown;
        }
    }
}
