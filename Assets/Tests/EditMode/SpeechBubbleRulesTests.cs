using Game.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for the pure speech-bubble replacement and fade rules in <see cref="SpeechBubbleRules"/>.</summary>
    public class SpeechBubbleRulesTests
    {
        private const float EPS = 0.0001f;
        private const float DURATION = 3f;
        private const float FADE_IN = 0.2f;
        private const float FADE_OUT = 0.4f;

        private static float Alpha(float elapsed, float fadeIn = FADE_IN, float fadeOut = FADE_OUT) =>
            SpeechBubbleRules.Alpha(elapsed, DURATION, fadeIn, fadeOut);

        // --- ShouldReplace ---

        [Test] public void ShouldReplace_NoActive_IsTrue() => Assert.That(SpeechBubbleRules.ShouldReplace(false, 5, 0), Is.True);
        [Test] public void ShouldReplace_LowerPriority_IsFalse() => Assert.That(SpeechBubbleRules.ShouldReplace(true, 1, 0), Is.False);
        [Test] public void ShouldReplace_EqualPriority_IsTrue() => Assert.That(SpeechBubbleRules.ShouldReplace(true, 1, 1), Is.True);
        [Test] public void ShouldReplace_HigherPriority_IsTrue() => Assert.That(SpeechBubbleRules.ShouldReplace(true, 1, 2), Is.True);

        // --- ResolveDuration ---

        [Test] public void ResolveDuration_Zero_UsesDefault() => Assert.That(SpeechBubbleRules.ResolveDuration(0f, 3f), Is.EqualTo(3f));
        [Test] public void ResolveDuration_Negative_UsesDefault() => Assert.That(SpeechBubbleRules.ResolveDuration(-1f, 3f), Is.EqualTo(3f));
        [Test] public void ResolveDuration_Positive_IsKept() => Assert.That(SpeechBubbleRules.ResolveDuration(5f, 3f), Is.EqualTo(5f));
        [Test] public void ResolveDuration_Tiny_ClampedTo0_1() => Assert.That(SpeechBubbleRules.ResolveDuration(0.01f, 3f), Is.EqualTo(0.1f).Within(EPS));
        [Test] public void ResolveDuration_ZeroDefault_ClampedTo0_1() => Assert.That(SpeechBubbleRules.ResolveDuration(0f, 0f), Is.EqualTo(0.1f).Within(EPS));

        // --- Lifetime / Alpha / IsExpired ---

        [Test] public void Lifetime_SumsPhases() => Assert.That(SpeechBubbleRules.Lifetime(DURATION, FADE_IN, FADE_OUT), Is.EqualTo(3.6f).Within(EPS));
        [Test] public void Lifetime_NegativeFades_CountAsZero() => Assert.That(SpeechBubbleRules.Lifetime(DURATION, -1f, -1f), Is.EqualTo(DURATION).Within(EPS));

        [Test] public void Alpha_AtStart_IsZero() => Assert.That(Alpha(0f), Is.EqualTo(0f).Within(EPS));
        [Test] public void Alpha_MidFadeIn_IsHalf() => Assert.That(Alpha(0.1f), Is.EqualTo(0.5f).Within(EPS));
        [Test] public void Alpha_Hold_IsOne() => Assert.That(Alpha(1.5f), Is.EqualTo(1f).Within(EPS));
        [Test] public void Alpha_MidFadeOut_IsHalf() => Assert.That(Alpha(FADE_IN + DURATION + 0.2f), Is.EqualTo(0.5f).Within(EPS));
        [Test] public void Alpha_AtEnd_IsZero() => Assert.That(Alpha(FADE_IN + DURATION + FADE_OUT), Is.EqualTo(0f).Within(EPS));
        [Test] public void Alpha_ZeroFadeIn_StartsAtOne() => Assert.That(Alpha(0f, fadeIn: 0f), Is.EqualTo(1f).Within(EPS));
        [Test] public void Alpha_ZeroFadeOut_HoldsUntilEnd() => Assert.That(Alpha(FADE_IN + DURATION - 0.01f, fadeOut: 0f), Is.EqualTo(1f).Within(EPS));
        [Test] public void Alpha_ZeroFadeOut_ZeroAtEnd() => Assert.That(Alpha(FADE_IN + DURATION, fadeOut: 0f), Is.EqualTo(0f).Within(EPS));

        // --- RestartElapsed (replacement without a visual jump) ---

        private static float Restart(float elapsed) => SpeechBubbleRules.RestartElapsed(elapsed, DURATION, FADE_IN, FADE_OUT);

        [Test] public void RestartElapsed_MidFadeIn_Unchanged() => Assert.That(Restart(0.1f), Is.EqualTo(0.1f).Within(EPS));
        [Test] public void RestartElapsed_Hold_GoesToEndOfFadeIn() => Assert.That(Restart(2f), Is.EqualTo(FADE_IN).Within(EPS));

        [Test]
        public void RestartElapsed_MidFadeOut_KeepsCurrentAlpha()
        {
            float elapsed = FADE_IN + DURATION + 0.3f; // alpha 0.25
            float before = Alpha(elapsed);
            Assert.That(Alpha(Restart(elapsed)), Is.EqualTo(before).Within(EPS));
            Assert.That(Restart(elapsed), Is.LessThan(FADE_IN)); // fades back in from there
        }

        [Test] public void IsExpired_JustBeforeLifetime_IsFalse() =>
            Assert.That(SpeechBubbleRules.IsExpired(3.59f, DURATION, FADE_IN, FADE_OUT), Is.False);

        [Test] public void IsExpired_AtLifetime_IsTrue() =>
            Assert.That(SpeechBubbleRules.IsExpired(3.6f, DURATION, FADE_IN, FADE_OUT), Is.True);
    }
}
