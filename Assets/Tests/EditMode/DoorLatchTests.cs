using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DoorLatchTests
    {
        [Test]
        public void IsOnLatchSide_OnlyInFrontOfTheLatch()
        {
            Assert.IsTrue(DoorLatch.IsOnLatchSide(Vector3.zero, Vector3.forward, new Vector3(0.5f, 0f, 1f)));
            Assert.IsFalse(DoorLatch.IsOnLatchSide(Vector3.zero, Vector3.forward, new Vector3(0.5f, 0f, -1f)));
        }
    }
}
