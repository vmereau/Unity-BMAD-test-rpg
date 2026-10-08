using Game.AI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for <see cref="TheftPursuitRegistry"/> (first catcher claims a theft incident).
    /// The registry is static and never cleared here — every test uses its own large, distinct ids.
    /// </summary>
    public class TheftPursuitRegistryTests
    {
        [Test]
        public void TryClaim_FirstTrue_SecondFalse()
        {
            const int id = 900_001;
            Assert.IsTrue(TheftPursuitRegistry.TryClaim(id));
            Assert.IsFalse(TheftPursuitRegistry.TryClaim(id));
        }

        [Test]
        public void IsClaimed_BeforeAndAfterClaim()
        {
            const int id = 900_002;
            Assert.IsFalse(TheftPursuitRegistry.IsClaimed(id));
            TheftPursuitRegistry.TryClaim(id);
            Assert.IsTrue(TheftPursuitRegistry.IsClaimed(id));
        }

        [Test]
        public void DistinctIds_Independent()
        {
            const int a = 900_003;
            const int b = 900_004;
            Assert.IsTrue(TheftPursuitRegistry.TryClaim(a));
            Assert.IsFalse(TheftPursuitRegistry.IsClaimed(b));
            Assert.IsTrue(TheftPursuitRegistry.TryClaim(b));
        }
    }
}
