using Game.Editor.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class Area1TerrainSculptTests
    {
        private const int SIZE = 8;
        private const float TOLERANCE = 0.006f; // offsets are whole centimetres

        [Test]
        public void Capture_ThenApply_RestoresTheEdits()
        {
            float[,] generated = Grid(5f);
            float[,] live = Grid(5f);
            live[2, 3] = 5f - 24.6f;
            live[4, 6] = 5f + 9.13f;

            Area1TerrainSculpt sculpt = Area1TerrainSculpt.Capture(generated, live);
            sculpt.Apply(generated);

            Assert.AreEqual(-19.6f, generated[2, 3], TOLERANCE);
            Assert.AreEqual(14.13f, generated[4, 6], TOLERANCE);
            Assert.AreEqual(5f, generated[0, 0], TOLERANCE);
        }

        [Test]
        public void Capture_CropsToTheChangedSamples()
        {
            float[,] live = Grid(5f);
            live[2, 3] = 7f;
            live[4, 6] = 3f;

            Area1TerrainSculpt sculpt = Area1TerrainSculpt.Capture(Grid(5f), live);

            Assert.AreEqual(3, sculpt.X);
            Assert.AreEqual(2, sculpt.Z);
            Assert.AreEqual(4, sculpt.Width);
            Assert.AreEqual(3, sculpt.Depth);
        }

        [Test]
        public void Capture_IgnoresHeightmapRounding()
        {
            float[,] live = Grid(5f);
            live[1, 1] = 5.01f;

            Area1TerrainSculpt sculpt = Area1TerrainSculpt.Capture(Grid(5f), live);

            Assert.AreEqual(0, sculpt.Width);
            Assert.AreEqual(0, sculpt.Depth);
        }

        [Test]
        public void Apply_ThenCapture_OfAnEmptySculpt_ChangesNothing()
        {
            float[,] heights = Grid(5f);

            new Area1TerrainSculpt().Apply(heights);

            Assert.AreEqual(5f, heights[3, 3]);
        }

        private static float[,] Grid(float height)
        {
            var grid = new float[SIZE, SIZE];
            for (int z = 0; z < SIZE; z++)
                for (int x = 0; x < SIZE; x++)
                    grid[z, x] = height;
            return grid;
        }
    }
}
