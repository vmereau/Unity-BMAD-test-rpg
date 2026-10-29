using NUnit.Framework;
using Game.UI;

namespace Tests.EditMode
{
    /// <summary>Edit-mode tests for HudHiderSet (world screens hiding the HUD).</summary>
    public class HudHiderSetTests
    {
        [Test]
        public void Empty_NotHidden()
        {
            Assert.IsFalse(new HudHiderSet().IsHidden);
        }

        [Test]
        public void OneHider_HidesThenShows()
        {
            var set = new HudHiderSet();
            var trade = new object();
            Assert.IsTrue(set.Add(trade));
            Assert.IsTrue(set.IsHidden);
            Assert.IsTrue(set.Remove(trade));
            Assert.IsFalse(set.IsHidden);
        }

        [Test]
        public void TwoHiders_StayHiddenUntilBothRemoved()
        {
            var set = new HudHiderSet();
            var trade = new object();
            var container = new object();
            set.Add(trade);
            Assert.IsFalse(set.Add(container));
            Assert.IsFalse(set.Remove(trade));
            Assert.IsTrue(set.IsHidden);
            Assert.IsTrue(set.Remove(container));
            Assert.IsFalse(set.IsHidden);
        }

        [Test]
        public void SameHiderTwice_Idempotent()
        {
            var set = new HudHiderSet();
            var trade = new object();
            set.Add(trade);
            Assert.IsFalse(set.Add(trade));
            Assert.IsTrue(set.Remove(trade));
            Assert.IsFalse(set.IsHidden);
            Assert.IsFalse(set.Remove(trade));
        }

        [Test]
        public void UnknownOrNullHider_NoOp()
        {
            var set = new HudHiderSet();
            Assert.IsFalse(set.Remove(new object()));
            Assert.IsFalse(set.Add(null));
            Assert.IsFalse(set.Remove(null));
            Assert.IsFalse(set.IsHidden);
        }
    }
}
