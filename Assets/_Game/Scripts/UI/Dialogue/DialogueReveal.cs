using UnityEngine;

namespace Game.UI
{
    /// <summary>Pure pacing rules for <see cref="DialogueUI"/>: typewriter reveal and the post-show input grace.</summary>
    public static class DialogueReveal
    {
        /// <summary>
        /// Characters visible <paramref name="elapsed"/> seconds into the reveal, clamped to [0, total].
        /// A non-positive speed reveals everything at once.
        /// </summary>
        public static int VisibleCount(float elapsed, float charsPerSecond, int total)
        {
            if (total <= 0) return 0;
            if (charsPerSecond <= 0f) return total;
            return Mathf.Clamp(Mathf.FloorToInt(elapsed * charsPerSecond), 0, total);
        }

        /// <summary>
        /// True once <paramref name="sinceShown"/> seconds have passed since new content appeared, so the press that
        /// opened the dialogue (or a rushed double-click) can't skip a line or pick an option.
        /// </summary>
        public static bool IsInputAllowed(float sinceShown, float grace) => sinceShown >= grace;
    }
}
