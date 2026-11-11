using Game.Editor.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class Area1WorkerSettlementTests
    {
        [Test]
        public void RotatedFootprints_DetectIntersectionAndSeparation()
        {
            var a = Area1WorkerGeometry.Corners(Vector2.zero, new Vector2(2, 6), 45);
            Assert.IsTrue(Area1WorkerGeometry.Overlap(a, Area1WorkerGeometry.Corners(new Vector2(1, 1), new Vector2(2, 2), 0)));
            Assert.IsFalse(Area1WorkerGeometry.Overlap(a, Area1WorkerGeometry.Corners(new Vector2(10, 0), new Vector2(2, 2), 0)));
        }

        [Test]
        public void Corridor_DetectsCrossingAndClearFootprints()
        {
            var footprint = Area1WorkerGeometry.Corners(Vector2.zero, new Vector2(2, 2), 30);
            Assert.IsTrue(Area1WorkerGeometry.Corridor(footprint, new Vector2(-5, 0), new Vector2(5, 0), 2));
            Assert.IsFalse(Area1WorkerGeometry.Corridor(footprint, new Vector2(-5, 5), new Vector2(5, 5), 2));
        }
    }
}
