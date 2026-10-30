using Game.Combat;
using NUnit.Framework;
using ClipEvent = Game.Combat.ClipEventDeduper.ClipEvent;

namespace Tests.EditMode
{
    public class ClipEventDeduperTests
    {
        private const int CLIP_A = 7;
        private const int CLIP_B = 8;

        [Test]
        public void FirstEventOfAFrame_IsForwarded()
            => Assert.That(new ClipEventDeduper().ShouldForward(ClipEvent.HitboxEnable, CLIP_A, 10), Is.True);

        [Test]
        public void SameEventAndKeyTwiceInAFrame_SecondIsDropped()
        {
            var deduper = new ClipEventDeduper();
            deduper.ShouldForward(ClipEvent.HitboxEnable, CLIP_A, 10);
            Assert.That(deduper.ShouldForward(ClipEvent.HitboxEnable, CLIP_A, 10), Is.False);
        }

        [Test]
        public void SameEventNextFrame_IsForwarded()
        {
            var deduper = new ClipEventDeduper();
            deduper.ShouldForward(ClipEvent.HitboxEnable, CLIP_A, 10);
            Assert.That(deduper.ShouldForward(ClipEvent.HitboxEnable, CLIP_A, 11), Is.True);
        }

        [Test]
        public void SameEventFromAnotherClipInAFrame_IsForwarded()
        {
            // Crossfade: the outgoing and incoming attack states fire the same event in one frame.
            var deduper = new ClipEventDeduper();
            deduper.ShouldForward(ClipEvent.HitboxDisable, CLIP_A, 10);
            Assert.That(deduper.ShouldForward(ClipEvent.HitboxDisable, CLIP_B, 10), Is.True);
            Assert.That(deduper.ShouldForward(ClipEvent.HitboxDisable, CLIP_B, 10), Is.False);
        }

        [Test]
        public void InterleavedEventsFromTwoLayers_EachForwardedOnce()
        {
            // HitboxDisable + ComboWindowOpen at the same clip time, fired by the Attack layer then the FullBody layer.
            var deduper = new ClipEventDeduper();
            Assert.That(deduper.ShouldForward(ClipEvent.HitboxDisable, CLIP_A, 5), Is.True);
            Assert.That(deduper.ShouldForward(ClipEvent.ComboWindowOpen, CLIP_A, 5), Is.True);
            Assert.That(deduper.ShouldForward(ClipEvent.HitboxDisable, CLIP_A, 5), Is.False);
            Assert.That(deduper.ShouldForward(ClipEvent.ComboWindowOpen, CLIP_A, 5), Is.False);
        }

        [Test]
        public void FrameZero_IsForwarded()
            => Assert.That(new ClipEventDeduper().ShouldForward(ClipEvent.WeaponSwap, CLIP_A, 0), Is.True);

        [Test]
        public void ClipKey_DiffersByClipAndTime()
        {
            Assert.That(ClipEventDeduper.ClipKey(1, 0.5f), Is.EqualTo(ClipEventDeduper.ClipKey(1, 0.5f)));
            Assert.That(ClipEventDeduper.ClipKey(1, 0.5f), Is.Not.EqualTo(ClipEventDeduper.ClipKey(2, 0.5f)));
            Assert.That(ClipEventDeduper.ClipKey(1, 0.5f), Is.Not.EqualTo(ClipEventDeduper.ClipKey(1, 0.6f)));
        }
    }
}
