using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for the pure legality rule <see cref="Ownership.IsIllegalInteraction"/>.</summary>
    public class OwnershipTests
    {
        [Test]
        public void NoOwner_Legal()
        {
            Assert.IsFalse(Ownership.IsIllegalInteraction(false, false, false, false));
        }

        [Test]
        public void OwnerAlive_Illegal()
        {
            Assert.IsTrue(Ownership.IsIllegalInteraction(true, false, false, false));
        }

        [Test]
        public void OwnerKilled_Legal()
        {
            Assert.IsFalse(Ownership.IsIllegalInteraction(true, true, false, false));
        }

        [Test]
        public void OwnedDoorOpen_Legal()
        {
            // Closing an owned door is free.
            Assert.IsFalse(Ownership.IsIllegalInteraction(true, false, true, true));
        }

        [Test]
        public void OwnedDoorClosed_Illegal()
        {
            Assert.IsTrue(Ownership.IsIllegalInteraction(true, false, true, false));
        }

        [Test]
        public void NonDoorOpenFlag_Ignored()
        {
            Assert.IsTrue(Ownership.IsIllegalInteraction(true, false, false, true));
        }
    }
}
