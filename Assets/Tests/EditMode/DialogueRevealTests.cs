using Game.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="DialogueReveal"/> (typewriter count and post-show input grace).</summary>
    public class DialogueRevealTests
    {
        [Test] public void VisibleCount_Start_IsZero() => Assert.That(DialogueReveal.VisibleCount(0f, 60f, 100), Is.EqualTo(0));

        [Test] public void VisibleCount_Midway_FloorsBySpeed() => Assert.That(DialogueReveal.VisibleCount(0.5f, 60f, 100), Is.EqualTo(30));

        [Test] public void VisibleCount_PastEnd_ClampedToTotal() => Assert.That(DialogueReveal.VisibleCount(10f, 60f, 100), Is.EqualTo(100));

        [Test] public void VisibleCount_NegativeElapsed_ClampedToZero() => Assert.That(DialogueReveal.VisibleCount(-1f, 60f, 100), Is.EqualTo(0));

        [Test] public void VisibleCount_ZeroSpeed_RevealsAll() => Assert.That(DialogueReveal.VisibleCount(0f, 0f, 42), Is.EqualTo(42));

        [Test] public void VisibleCount_EmptyText_IsZero() => Assert.That(DialogueReveal.VisibleCount(1f, 60f, 0), Is.EqualTo(0));

        [Test]
        public void IsInputAllowed_GraceBoundary()
        {
            Assert.That(DialogueReveal.IsInputAllowed(0.1f, 0.2f), Is.False);
            Assert.That(DialogueReveal.IsInputAllowed(0.2f, 0.2f), Is.True);
            Assert.That(DialogueReveal.IsInputAllowed(0f, 0f), Is.True);
        }
    }
}
