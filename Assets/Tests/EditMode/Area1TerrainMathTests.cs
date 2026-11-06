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

        [TestCase(30f, 0f, 30f)]   // on the floor edge
        [TestCase(40f, 0f, 40f)]   // on the rise
        [TestCase(70f, 5f, 52f)]   // levels at the top
        [TestCase(70f, 18f, 52f)]  // end of the crest
        [TestCase(70f, 23f, 44f)]  // outer fall
        public void CliffSurface_RisesLevelsAndFalls(float floorRise, float rim, float expected)
        {
            Assert.AreEqual(expected, Area1TerrainMath.CliffSurface(floorRise, rim, 52f, 18f, 1.6f), 1e-4f);
        }

        private static readonly Area1TerrainMath.FightPit Pit =
            new Area1TerrainMath.FightPit(Vector2.zero, 5.5f, Vector2.left, Vector2.up);

        [Test]
        public void FightPit_Floor_IsLoweredEvenly()
        {
            Assert.AreEqual(-1.4f, Pit.Offset(Vector2.zero), 1e-5f);
            Assert.AreEqual(-1.4f, Pit.Offset(new Vector2(5.4f, 0f)), 1e-5f);
        }

        [Test]
        public void FightPit_Crest_IsHighestTowardsTheHighSide()
        {
            float high = Pit.Offset(new Vector2(-Area1TerrainMath.FightPit.CREST_RADIUS, 0f));
            float low = Pit.Offset(new Vector2(Area1TerrainMath.FightPit.CREST_RADIUS, 0f));

            Assert.AreEqual(2.7f, high, 1e-5f);
            Assert.AreEqual(1.3f, low, 1e-5f);
        }

        [Test]
        public void FightPit_Gap_OpensAtGroundLevel()
        {
            Assert.AreEqual(0f, Pit.Offset(new Vector2(0f, Area1TerrainMath.FightPit.CREST_RADIUS)), 1e-5f);
            Assert.AreEqual(0f, Pit.Offset(new Vector2(1f, 11f)), 1e-5f);
        }

        [Test]
        public void FightPit_Gap_DoesNotOpenTheFarSide()
        {
            Assert.AreEqual(1.3f, Pit.Offset(new Vector2(0f, -Area1TerrainMath.FightPit.CREST_RADIUS)), 1e-5f);
        }

        [Test]
        public void FightPit_PastTheFoot_LeavesTheGround()
        {
            Assert.AreEqual(0f, Pit.Offset(new Vector2(Area1TerrainMath.FightPit.FOOT_RADIUS, 0f)));
            Assert.AreEqual(0f, Pit.Offset(new Vector2(0f, -30f)));
        }

        [TestCase(1f, 27f)]    // on the floor
        [TestCase(2f, 27f)]    // edge of the floor
        [TestCase(5f, 33f)]    // on the wall
        [TestCase(20f, 40f)]   // past the wall the ground stays
        public void CutCleft_FloorThenWalls(float distance, float expected)
        {
            Assert.AreEqual(expected, Area1TerrainMath.CutCleft(40f, distance, 27f, 2f, 2f), 1e-4f);
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

        [TestCase(0f, 1f)]    // on the line
        [TestCase(2.5f, 1f)]  // at the edge
        [TestCase(3f, 0.5f)]  // half way through the fade
        [TestCase(3.5f, 0f)]  // past it
        [TestCase(40f, 0f)]
        public void LineBlend_IsFullAcrossTheWidthThenFades(float offset, float expected)
        {
            var line = new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f) };
            Assert.AreEqual(expected, Area1TerrainMath.LineBlend(new Vector2(0f, offset), line, 2.5f, 1f), 1e-4f);
        }

        [Test]
        public void BlendLayer_KeepsTheTotal_AndTheOthersInProportion()
        {
            var weights = new[] { 0.6f, 0.3f, 0.1f, 0f };
            Area1TerrainMath.BlendLayer(weights, 3, 0.5f);
            Assert.AreEqual(0.5f, weights[3], 1e-5f);
            Assert.AreEqual(0.3f, weights[0], 1e-5f);
            Assert.AreEqual(0.15f, weights[1], 1e-5f);
            Assert.AreEqual(1f, weights[0] + weights[1] + weights[2] + weights[3], 1e-5f);
        }

        [Test]
        public void BlendLayer_FullShare_LeavesOnlyThatLayer()
        {
            var weights = new[] { 0.5f, 0.5f, 0f };
            Area1TerrainMath.BlendLayer(weights, 2, 1f);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 1f }, weights);
        }

        [Test]
        public void BlendLayer_NeverThinsTheLayer()
        {
            var weights = new[] { 0.1f, 0.9f };
            Area1TerrainMath.BlendLayer(weights, 1, 0.3f);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.9f }, weights);
        }

        [Test]
        public void BlendLayer_NoShare_ChangesNothing()
        {
            var weights = new[] { 0.5f, 0.5f };
            Area1TerrainMath.BlendLayer(weights, 1, 0f);
            CollectionAssert.AreEqual(new[] { 0.5f, 0.5f }, weights);
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

        [TestCase(15f, 52f, true)]
        [TestCase(21f, 56f, true)]
        [TestCase(21.1f, 52f, false)]
        [TestCase(15f, 47.9f, false)]
        public void InsideRect_IncludesEdges_AndNothingBeyond(float x, float z, bool expected)
        {
            Assert.AreEqual(expected, Area1TerrainMath.InsideRect(new Vector2(x, z), new Vector2(15f, 52f), new Vector2(12f, 8f)));
        }

        [Test]
        public void ClipOutsideRect_WhollyInside_IsDropped()
        {
            Vector2 a = new Vector2(12f, 52f), b = new Vector2(15f, 52f);

            Assert.IsFalse(Area1TerrainMath.ClipOutsideRect(ref a, ref b, new Vector2(15f, 52f), new Vector2(12f, 8f)));
        }

        [Test]
        public void ClipOutsideRect_Straddling_StopsAtTheEdge()
        {
            Vector2 a = new Vector2(7f, 52f), b = new Vector2(10f, 52f);

            Assert.IsTrue(Area1TerrainMath.ClipOutsideRect(ref a, ref b, new Vector2(15f, 52f), new Vector2(12f, 8f)));

            Assert.AreEqual(new Vector2(7f, 52f), a);
            Assert.AreEqual(9f, b.x, 0.001f);
        }

        [Test]
        public void ClipOutsideRect_Outside_IsUnchanged()
        {
            Vector2 a = new Vector2(30f, 52f), b = new Vector2(33f, 52f);

            Assert.IsTrue(Area1TerrainMath.ClipOutsideRect(ref a, ref b, new Vector2(15f, 52f), new Vector2(12f, 8f)));

            Assert.AreEqual(new Vector2(33f, 52f), b);
        }

        [Test]
        public void CutPanels_SplitsEachSegmentEvenly_AndKeepsCorners()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(9f, 0f), new Vector2(9f, 4f) };

            var panels = Area1TerrainMath.CutPanels(line, 3f, Vector2.zero, 0f);

            Assert.AreEqual(5, panels.Count);
            Assert.AreEqual(new Vector2(3f, 0f), panels[0].b);
            Assert.AreEqual(new Vector2(9f, 0f), panels[2].b);
            Assert.AreEqual(new Vector2(9f, 0f), panels[3].a);
            Assert.AreEqual(new Vector2(9f, 4f), panels[4].b);
        }

        [Test]
        public void CutPanels_SkipsTheGap_OnlyOnTheSegmentThroughIt()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(20f, 0f), new Vector2(20f, 10f) };

            var panels = Area1TerrainMath.CutPanels(line, 4f, new Vector2(10f, 0f), 4f);

            float covered = 0f;
            foreach (var panel in panels)
            {
                covered += Vector2.Distance(panel.a, panel.b);
                Assert.IsFalse(panel.a.y == 0f && panel.a.x >= 6f && panel.a.x < 14f, "Panel starts inside the gap");
            }
            Assert.AreEqual(22f, covered, 1e-3f);
        }

        [Test]
        public void CutPanels_GapOffTheLine_ChangesNothing()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(12f, 0f) };

            Assert.AreEqual(4, Area1TerrainMath.CutPanels(line, 3f, new Vector2(6f, 20f), 4f).Count);
        }
    }
}
