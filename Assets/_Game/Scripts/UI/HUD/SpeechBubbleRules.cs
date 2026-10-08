using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Pure speech-bubble rules used by <see cref="SpeechBubbleUI"/>: replacement priority and the
    /// fade-in / hold / fade-out timeline. Unit-tested in <c>SpeechBubbleRulesTests</c>.
    /// </summary>
    public static class SpeechBubbleRules
    {
        private const float MIN_DURATION = 0.1f;

        /// <summary>A new request replaces the speaker's visible bubble when its priority is equal or higher.</summary>
        public static bool ShouldReplace(bool hasActive, int activePriority, int newPriority) =>
            !hasActive || newPriority >= activePriority;

        /// <summary>Requested duration, or <paramref name="defaultDuration"/> when ≤ 0; never below 0.1 s.</summary>
        public static float ResolveDuration(float requested, float defaultDuration) =>
            Mathf.Max(MIN_DURATION, requested > 0f ? requested : defaultDuration);

        /// <summary>Total time on screen: fade-in + hold + fade-out (negative fades count as 0).</summary>
        public static float Lifetime(float duration, float fadeIn, float fadeOut) =>
            Mathf.Max(0f, fadeIn) + duration + Mathf.Max(0f, fadeOut);

        /// <summary>Bubble alpha 0..1 at <paramref name="elapsed"/> seconds since it appeared.</summary>
        public static float Alpha(float elapsed, float duration, float fadeIn, float fadeOut)
        {
            fadeIn = Mathf.Max(0f, fadeIn);
            fadeOut = Mathf.Max(0f, fadeOut);
            if (elapsed >= Lifetime(duration, fadeIn, fadeOut)) return 0f;
            if (elapsed < 0f) return 0f;
            if (elapsed < fadeIn) return Mathf.Clamp01(elapsed / fadeIn);
            float holdEnd = fadeIn + duration;
            if (elapsed < holdEnd || fadeOut <= 0f) return 1f;
            return Mathf.Clamp01(1f - (elapsed - holdEnd) / fadeOut);
        }

        /// <summary>
        /// Elapsed time to restart a replaced bubble at without a visual jump: hold / fade-in keep (at most) full
        /// fade-in; a fading-out bubble fades back in from its current alpha.
        /// </summary>
        public static float RestartElapsed(float elapsed, float duration, float fadeIn, float fadeOut)
        {
            fadeIn = Mathf.Max(0f, fadeIn);
            if (elapsed <= fadeIn + duration) return Mathf.Min(elapsed, fadeIn);
            return Alpha(elapsed, duration, fadeIn, fadeOut) * fadeIn;
        }

        public static bool IsExpired(float elapsed, float duration, float fadeIn, float fadeOut) =>
            elapsed >= Lifetime(duration, fadeIn, fadeOut);
    }
}
