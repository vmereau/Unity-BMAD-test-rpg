using Game.Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class GameSessionTests
    {
        [SetUp]
        public void SetUp() => GameSession.Reset();

        [TearDown]
        public void TearDown() => GameSession.Reset();

        [Test]
        public void NoRequest_NothingPending()
        {
            Assert.IsFalse(GameSession.HasPendingLoad);
            Assert.IsFalse(GameSession.TryTakePendingLoad(out var slot));
            Assert.IsNull(slot);
        }

        [Test]
        public void Request_ThenTake_ReturnsSlotOnce()
        {
            GameSession.RequestLoad("slot_3");
            Assert.IsTrue(GameSession.HasPendingLoad);

            Assert.IsTrue(GameSession.TryTakePendingLoad(out var slot));
            Assert.AreEqual("slot_3", slot);
            Assert.IsFalse(GameSession.HasPendingLoad);
            Assert.IsFalse(GameSession.TryTakePendingLoad(out _));
        }

        [Test]
        public void RequestNullOrEmpty_ClearsPending()
        {
            GameSession.RequestLoad("auto");
            GameSession.RequestLoad(null);
            Assert.IsFalse(GameSession.HasPendingLoad);

            GameSession.RequestLoad("auto");
            GameSession.RequestLoad(string.Empty);
            Assert.IsFalse(GameSession.HasPendingLoad);
        }

        [Test]
        public void Reset_ClearsPending()
        {
            GameSession.RequestLoad("quick");
            GameSession.Reset();
            Assert.IsFalse(GameSession.HasPendingLoad);
        }
    }
}
