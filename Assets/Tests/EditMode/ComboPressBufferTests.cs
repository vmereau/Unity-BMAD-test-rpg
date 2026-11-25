using Game.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class ComboPressBufferTests
    {
        [Test]
        public void Consume_WithoutPress_ReturnsFalse()
        {
            var buffer = new ComboPressBuffer();
            Assert.IsFalse(buffer.Consume(1f, 0.4f));
        }

        [Test]
        public void Consume_RecentPress_ReturnsTrueOnce()
        {
            var buffer = new ComboPressBuffer();
            buffer.Store(1.0f);
            Assert.IsTrue(buffer.Consume(1.3f, 0.4f));
            Assert.IsFalse(buffer.HasPress);
            Assert.IsFalse(buffer.Consume(1.31f, 0.4f));
        }

        [Test]
        public void Consume_PressOlderThanMaxAge_ReturnsFalseAndClears()
        {
            var buffer = new ComboPressBuffer();
            buffer.Store(1.0f);
            Assert.IsFalse(buffer.Consume(1.5f, 0.4f));
            Assert.IsFalse(buffer.HasPress);
        }

        [Test]
        public void Store_LaterPressReplacesEarlierOne()
        {
            var buffer = new ComboPressBuffer();
            buffer.Store(0.2f);
            buffer.Store(0.9f);
            Assert.IsTrue(buffer.Consume(1.2f, 0.4f));
        }

        [Test]
        public void Clear_ForgetsThePress()
        {
            var buffer = new ComboPressBuffer();
            buffer.Store(1.0f);
            buffer.Clear();
            Assert.IsFalse(buffer.Consume(1.1f, 0.4f));
        }

        [Test]
        public void Consume_ZeroMaxAge_OnlySameFramePress()
        {
            var buffer = new ComboPressBuffer();
            buffer.Store(1.0f);
            Assert.IsTrue(buffer.Consume(1.0f, 0f));
        }
    }
}
