using Game.Stealth;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for <see cref="StealthDebugGeometry"/> — the line-segment builders shared by the
    /// EntityPerception gizmos and the runtime StealthDebugOverlay.
    /// </summary>
    public class StealthDebugGeometryTests
    {
        private const float EPS = 0.001f;

        [Test]
        public void BuildConeOutline_PointCount_IsEdgesPlusArcSegments_TimesTwo()
        {
            var buffer = new Vector3[64];
            int count = StealthDebugGeometry.BuildConeOutline(Vector3.zero, Vector3.forward, 8f, 110f, 12, buffer);
            Assert.That(count, Is.EqualTo((2 + 12) * 2));
        }

        [Test]
        public void BuildConeOutline_SideEdges_EndAtRangeAndHalfAngle()
        {
            var buffer = new Vector3[64];
            StealthDebugGeometry.BuildConeOutline(Vector3.zero, Vector3.forward, 8f, 110f, 12, buffer);
            // Segment 0: origin → left edge; segment 1: origin → right edge.
            Assert.That(buffer[0], Is.EqualTo(Vector3.zero));
            Assert.That(buffer[1].magnitude, Is.EqualTo(8f).Within(EPS));
            Assert.That(buffer[3].magnitude, Is.EqualTo(8f).Within(EPS));
            Assert.That(Vector3.Angle(Vector3.forward, buffer[1]), Is.EqualTo(55f).Within(EPS));
            Assert.That(Vector3.Angle(Vector3.forward, buffer[3]), Is.EqualTo(55f).Within(EPS));
            Assert.That(Mathf.Sign(buffer[1].x), Is.Not.EqualTo(Mathf.Sign(buffer[3].x)), "edges on opposite sides");
        }

        [Test]
        public void BuildConeOutline_ArcEndpoints_MatchSideEdges()
        {
            var buffer = new Vector3[64];
            int count = StealthDebugGeometry.BuildConeOutline(Vector3.zero, Vector3.forward, 8f, 110f, 12, buffer);
            Assert.That(Vector3.Distance(buffer[4], buffer[1]), Is.LessThan(EPS)); // arc starts at the left edge
            Assert.That(Vector3.Distance(buffer[count - 1], buffer[3]), Is.LessThan(EPS)); // and ends at the right edge
        }

        [Test]
        public void BuildConeOutline_FullCircle_HasNoEdges_AndCloses()
        {
            var buffer = new Vector3[64];
            int count = StealthDebugGeometry.BuildConeOutline(new Vector3(1, 2, 3), Vector3.forward, 5f, 360f, 16, buffer);
            Assert.That(count, Is.EqualTo(16 * 2));
            for (int i = 0; i < count; i++)
                Assert.That(Vector3.Distance(buffer[i], new Vector3(1, 2, 3)), Is.EqualTo(5f).Within(EPS));
            Assert.That(Vector3.Distance(buffer[0], buffer[count - 1]), Is.LessThan(EPS));
        }

        [Test]
        public void BuildConeOutline_FlattensOnOriginHeight()
        {
            var buffer = new Vector3[64];
            int count = StealthDebugGeometry.BuildConeOutline(new Vector3(0, 1.6f, 0), new Vector3(0, -1, 1), 8f, 110f, 8, buffer);
            for (int i = 0; i < count; i++) Assert.That(buffer[i].y, Is.EqualTo(1.6f).Within(EPS));
        }

        [Test]
        public void BuildConeOutline_BufferTooSmall_ClampsWithoutThrowing()
        {
            var buffer = new Vector3[7];
            int count = 0;
            Assert.DoesNotThrow(() =>
                count = StealthDebugGeometry.BuildConeOutline(Vector3.zero, Vector3.forward, 8f, 110f, 12, buffer));
            Assert.That(count, Is.EqualTo(6)); // whole segments only
        }

        [Test]
        public void BuildCircle_IsClosedCircleOfRadius()
        {
            var buffer = new Vector3[32];
            int count = StealthDebugGeometry.BuildCircle(Vector3.zero, 2f, 8, buffer);
            Assert.That(count, Is.EqualTo(16));
            for (int i = 0; i < count; i++) Assert.That(buffer[i].magnitude, Is.EqualTo(2f).Within(EPS));
        }
    }
}
