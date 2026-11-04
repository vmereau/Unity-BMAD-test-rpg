using Game.Editor.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class Area1TerrainMathTests
    {
        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(5f, 0.5f)]
        [TestCase(10f, 1f)]
        [TestCase(20f, 1f)]
        public void Ramp_ClampsAndEases(float value, float expected)
        {
            Assert.AreEqual(expected, Area1TerrainMath.Ramp(value, 0f, 10f), 1e-5f);
        }

        [Test]
        public void Ramp_ZeroWidth_IsAStep()
        {
            Assert.AreEqual(0f, Area1TerrainMath.Ramp(1f, 2f, 2f));
            Assert.AreEqual(1f, Area1TerrainMath.Ramp(2f, 2f, 2f));
        }

        [Test]
        public void PolylineDistance_ReportsNearestSegmentAndPosition()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f) };

            float distance = Area1TerrainMath.PolylineDistance(new Vector2(13f, 5f), line, out float along);

            Assert.AreEqual(3f, distance, 1e-5f);
            Assert.AreEqual(0.75f, along, 1e-5f);
        }

        [Test]
        public void PolylineDistance_PastTheEnd_MeasuresToTheEndPoint()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };

            float distance = Area1TerrainMath.PolylineDistance(new Vector2(13f, 4f), line, out float along);

            Assert.AreEqual(5f, distance, 1e-5f);
            Assert.AreEqual(1f, along, 1e-5f);
        }

        [Test]
        public void PolylineDistance_RepeatedPoint_IsSkipped()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 0f) };

            Assert.AreEqual(2f, Area1TerrainMath.PolylineDistance(new Vector2(2f, 2f), line, out _), 1e-5f);
        }

        [Test]
        public void PolylineDistance_Empty_IsInfinite()
        {
            Assert.IsTrue(float.IsPositiveInfinity(
                Area1TerrainMath.PolylineDistance(Vector2.zero, new Vector2[0], out _)));
        }

        [Test]
        public void RectDistance_IsZeroInsideAndEuclideanAtCorners()
        {
            Vector2 size = new Vector2(4f, 2f);

            Assert.AreEqual(0f, Area1TerrainMath.RectDistance(new Vector2(1f, 0.5f), Vector2.zero, size));
            Assert.AreEqual(5f, Area1TerrainMath.RectDistance(new Vector2(5f, 5f), Vector2.zero, size), 1e-5f);
        }

        [Test]
        public void LimitGrade_KeepsAnchorsAndRampsTowardsThem()
        {
            float[] heights = { 0f, 10f, 10f, 10f, 0f };
            bool[] anchored = { true, false, false, false, true };

            Area1TerrainMath.LimitGrade(heights, anchored, 1f);

            CollectionAssert.AreEqual(new[] { 0f, 1f, 2f, 1f, 0f }, heights);
        }

        [Test]
        public void LimitGrade_FlatProfile_IsUnchanged()
        {
            float[] heights = { 3f, 3f, 3f };

            Area1TerrainMath.LimitGrade(heights, new bool[3], 0.1f);

            CollectionAssert.AreEqual(new[] { 3f, 3f, 3f }, heights);
        }
    }
}
