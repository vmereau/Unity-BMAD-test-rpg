using System;
using System.Collections.Generic;
using Game.Editor.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The wall Camp Edge draws on a palisade panel (PalisadeMesh), built here from a stand-in kit so the model file
    /// is not needed: tops never under the panel's top, every log into its own ground, the grain's scale kept on a
    /// 14 m wall, and one seed giving one mesh.
    /// </summary>
    public class PalisadeMeshTests
    {
        private const float HEAD = 1f;
        private const float TILE = 2f;
        private const float LOG_RADIUS = 0.15f;
        private const float POST_RADIUS = 0.2f;
        private const float CLEAR = POST_RADIUS * 0.8f;
        private const int SIDES = 8;

        private readonly List<Mesh> _made = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            foreach (Mesh mesh in _made) UnityEngine.Object.DestroyImmediate(mesh);
            _made.Clear();
        }

        [Test]
        public void Build_FlatGround_NoTopUnderTheTopLine()
        {
            PalisadeStretch stretch = Stretch(3f, _ => 0f, 5f);
            Mesh mesh = Build(stretch);

            AssertTopsClear(mesh, stretch);
        }

        [Test]
        public void Build_TallerNeighbours_TopLineClimbsToThem()
        {
            PalisadeStretch stretch = Stretch(3f, _ => 0f, 5f);
            stretch.TopStart = 6.5f;
            stretch.TopEnd = 8f;
            Mesh mesh = Build(stretch);

            AssertTopsClear(mesh, stretch);
        }

        [Test]
        public void Build_HandRaisedPanel_LogsReachTheTopAndTheGround()
        {
            PalisadeStretch stretch = Stretch(3.2f, _ => 1.5f, 11f); // a panel lifted well above where the tool built it
            Mesh mesh = Build(stretch);

            AssertTopsClear(mesh, stretch);
            AssertFeetBuried(mesh, stretch, 0.05f);
        }

        [Test]
        public void Build_SlopingGround_EveryLogGoesIntoItsOwnGround()
        {
            const float slope = 0.8f;
            PalisadeStretch stretch = Stretch(3.3f, along => 2f + along * slope, 9f);
            Mesh mesh = Build(stretch);

            // a log's foot is set from the ground under its axis: allow the slope across the sampling window
            AssertFeetBuried(mesh, stretch, slope * 0.2f + 0.05f);
        }

        [Test]
        public void Build_FourteenMetreWall_GrainKeepsItsScale()
        {
            PalisadeStretch stretch = Stretch(3f, along => -3f + along, 11f);
            Mesh mesh = Build(stretch);

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Assert.Greater(mesh.bounds.size.y, 13f, "the wall should be about 14 m tall");

            int checkedEdges = 0;
            int[] wood = mesh.GetTriangles(PalisadeMesh.WOOD);
            for (int i = 0; i < wood.Length; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = wood[i + e], b = wood[i + (e + 1) % 3];
                    float drop = Mathf.Abs(vertices[a].y - vertices[b].y);
                    var across = new Vector2(vertices[a].x - vertices[b].x, vertices[a].z - vertices[b].z);
                    if (drop < 2f || across.magnitude > 1e-4f) continue; // only the long straight shaft edges
                    Assert.AreEqual(1f / TILE, Mathf.Abs(uvs[a].x - uvs[b].x) / drop, 1e-3f, "U per metre down a shaft");
                    checkedEdges++;
                }
            }
            Assert.Greater(checkedEdges, 8, "expected long shaft edges on a tall wall");
        }

        [Test]
        public void Build_SameSeed_SameMesh_OtherSeed_Another()
        {
            Mesh first = Build(Stretch(3f, _ => 0f, 5f));
            Mesh again = Build(Stretch(3f, _ => 0f, 5f));
            PalisadeStretch other = Stretch(3f, _ => 0f, 5f);
            other.Seed = 99;
            Mesh different = Build(other);

            CollectionAssert.AreEqual(first.vertices, again.vertices);
            CollectionAssert.AreEqual(first.uv, again.uv);
            CollectionAssert.AreEqual(first.triangles, again.triangles);
            CollectionAssert.AreNotEqual(first.vertices, different.vertices);
        }

        [Test]
        public void Build_Posts_OnlyWhereAsked()
        {
            int lashing = StandInKit().Lashing.Triangles.Length;

            PalisadeStretch open = Stretch(3f, _ => 0f, 5f);
            open.PostAtStart = false;
            PalisadeStretch one = Stretch(3f, _ => 0f, 5f);
            PalisadeStretch both = Stretch(3f, _ => 0f, 5f);
            both.PostAtEnd = true;

            // two rails, one lashing per rail and post
            Assert.AreEqual(0, Build(open).GetTriangles(PalisadeMesh.ROPE).Length);
            Assert.AreEqual(2 * lashing, Build(one).GetTriangles(PalisadeMesh.ROPE).Length);
            Assert.AreEqual(4 * lashing, Build(both).GetTriangles(PalisadeMesh.ROPE).Length);
        }

        [Test]
        public void Build_NoDaylightBetweenLogs()
        {
            PalisadeStretch stretch = Stretch(3.4f, _ => 0f, 5f);
            Mesh mesh = Build(stretch);

            // at 2 m up, every centimetre along the wall lies behind a shaft triangle (seen square on, across the wall)
            const float height = 2f;
            Vector3[] vertices = mesh.vertices;
            int[] wood = mesh.GetTriangles(PalisadeMesh.WOOD);
            var covered = new List<(float from, float to)>();
            for (int i = 0; i < wood.Length; i += 3)
            {
                Vector3 a = vertices[wood[i]], b = vertices[wood[i + 1]], c = vertices[wood[i + 2]];
                if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > height || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < height) continue;
                float from = float.PositiveInfinity, to = float.NegativeInfinity;
                foreach ((Vector3 p, Vector3 q) in new[] { (a, b), (b, c), (c, a) })
                {
                    if ((p.y - height) * (q.y - height) > 0f || Mathf.Approximately(p.y, q.y)) continue;
                    float z = Mathf.Lerp(p.z, q.z, (height - p.y) / (q.y - p.y));
                    from = Mathf.Min(from, z);
                    to = Mathf.Max(to, z);
                }
                if (from <= to) covered.Add((from, to));
            }

            Assert.Greater(covered.Count, 0);
            for (float along = 0f; along <= stretch.Length - CLEAR; along += 0.01f)
            {
                bool behindWood = false;
                foreach ((float from, float to) in covered)
                    if (along >= from - 1e-4f && along <= to + 1e-4f) behindWood = true;
                Assert.IsTrue(behindWood, $"daylight through the wall at {along:F2} m");
            }
        }

        [Test]
        public void Build_ScaledPanelSpace_SameWallOnceBackInTheWorld()
        {
            PalisadeStretch plain = Stretch(3f, _ => 0f, 5f);
            PalisadeStretch scaled = Stretch(3f, _ => 0f, 5f);
            // a panel is a box scaled (thickness, height, length) and turned on its line
            Matrix4x4 panel = Matrix4x4.TRS(new Vector3(4f, 2f, -7f), Quaternion.Euler(0f, 35f, 0f), new Vector3(0.6f, 6.8f, 3.4f));
            scaled.ToMesh = panel.inverse;

            Mesh world = Build(plain);
            Vector3[] expected = world.vertices;
            Vector3[] expectedNormals = world.normals;
            Mesh mesh = Build(scaled);
            Vector3[] actual = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Matrix4x4 normalToWorld = panel.inverse.transpose; // what the renderer does with a mesh normal

            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Less(Vector3.Distance(expected[i], panel.MultiplyPoint3x4(actual[i])), 1e-3f, $"vertex {i}");
                Assert.AreEqual(1f, normals[i].magnitude, 1e-3f, $"normal {i}");
                Assert.Greater(Vector3.Dot(expectedNormals[i], normalToWorld.MultiplyVector(normals[i]).normalized), 0.999f, $"normal {i} turned");
            }
        }

        [Test]
        public void Build_NotchesBetweenPoints_ClearTheTopLine()
        {
            const float top = 5f;
            PalisadeStretch stretch = Stretch(3f, _ => 0f, top);
            Mesh mesh = Build(stretch);

            // the stand-in's shoulders are its only vertices near the top line: none may lie in the 0.4 m under it
            // (rails hang 0.75 m under, the heads' rings lower still)
            foreach (Vector3 v in mesh.vertices)
                Assert.IsFalse(v.y < top && v.y > top - 0.4f, $"a vertex at y {v.y:F3} under the top line: a notch below the panel's top");
        }

        [Test]
        public void Build_TallerNeighbours_HighRailStaysUnderThisStretchsTop()
        {
            const float top = 5f;
            PalisadeStretch stretch = Stretch(3f, _ => 0f, top);
            stretch.TopStart = 9f; // a neighbour raised by hand
            stretch.TopEnd = 8f;
            Mesh level = Build(Stretch(3f, _ => 0f, top));
            Mesh climbing = Build(stretch);

            // the lashings sit on the rails: same heights whether or not the top line climbs at the ends
            CollectionAssert.AreEqual(RopeHeights(level), RopeHeights(climbing));
        }

        [Test]
        public void Build_LowerNeighbour_HighRailDropsToMeetIts()
        {
            const float top = 5f, lower = 4.6f;
            PalisadeStretch stretch = Stretch(3f, _ => 0f, top);
            stretch.JointTopStart = lower; // the neighbour at the start is lower: its rail hangs lower too
            Mesh level = Build(Stretch(3f, _ => 0f, top));
            Mesh meeting = Build(stretch);

            List<float> before = RopeHeights(level), after = RopeHeights(meeting);
            float highest = float.NegativeInfinity, highestAfter = float.NegativeInfinity;
            foreach (float y in before) highest = Mathf.Max(highest, y);
            foreach (float y in after) highestAfter = Mathf.Max(highestAfter, y);
            Assert.AreEqual(highest - (top - lower), highestAfter, 2e-3f, "the high rail's lashing at the start post");
        }

        [Test]
        public void Build_EndAgainstTheWarehouse_StopsShortOfIt()
        {
            const float clear = 0.06f;
            PalisadeStretch stretch = Stretch(3f, _ => 0f, 5f);
            stretch.ClearEnd = clear;
            stretch.RunsOn = false; // no post of its own, none from a neighbour
            Mesh mesh = Build(stretch);

            float reach = float.NegativeInfinity;
            foreach (Vector3 v in mesh.vertices) reach = Mathf.Max(reach, v.z);
            Assert.LessOrEqual(reach, stretch.Length - clear + LOG_RADIUS * 0.15f, "logs or rails run into the junction log");
            Assert.Greater(reach, stretch.Length - clear - LOG_RADIUS, "the wall stops well short of the junction log");
        }

        [Test]
        public void Build_CampOnTheLeft_RailsAndRopeOnTheLeft()
        {
            PalisadeStretch right = Stretch(3f, _ => 0f, 5f);
            PalisadeStretch left = Stretch(3f, _ => 0f, 5f);
            left.CampSide = -1f;

            Assert.Greater(RopeSide(Build(right)), 0f);
            Assert.Less(RopeSide(Build(left)), 0f);
        }

        [Test]
        public void Build_BoxBottomUnderTheGround_LogsReachIt()
        {
            PalisadeStretch stretch = Stretch(3f, _ => 0f, 5f);
            stretch.Bottom = -2.5f; // the panel's box goes deeper than the bury depth
            Mesh mesh = Build(stretch);

            Assert.LessOrEqual(mesh.bounds.min.y, -2.5f + 1e-3f);
        }

        [Test]
        public void FindRing_OnePointPerPlace_InOrderRoundTheAxis()
        {
            var vertices = new List<Vector3>();
            for (int s = SIDES - 1; s >= 0; s--) vertices.Add(Round(LOG_RADIUS, -HEAD, s)); // out of order
            vertices.Add(Round(LOG_RADIUS, -HEAD, 3));                                      // a split vertex
            vertices.Add(Round(LOG_RADIUS, -0.4f, 0));                                      // not on the ring

            int[] ring = PalisadeMesh.FindRing(vertices.ToArray(), HEAD);

            Assert.AreEqual(SIDES, ring.Length);
            for (int i = 1; i < ring.Length; i++)
                Assert.Less(Mathf.Atan2(vertices[ring[i - 1]].z, vertices[ring[i - 1]].x), Mathf.Atan2(vertices[ring[i]].z, vertices[ring[i]].x));
        }

        private static void AssertTopsClear(Mesh mesh, PalisadeStretch stretch)
        {
            Vector3[] vertices = mesh.vertices;
            // between the joints: a joint without a post of its own is closed by the next stretch's post
            for (float along = CLEAR; along <= stretch.Length - CLEAR; along += 0.1f)
            {
                float highest = float.NegativeInfinity;
                foreach (Vector3 v in vertices)
                    if (Mathf.Abs(v.z - along) < LOG_RADIUS * 1.5f) highest = Mathf.Max(highest, v.y);
                Assert.GreaterOrEqual(highest, PalisadeMesh.TopAt(stretch, along), $"the wall's top at {along:F1} m is under the top line");
                Assert.GreaterOrEqual(highest, stretch.TopMid, $"the wall's top at {along:F1} m is under the panel's own top");
            }
        }

        private static void AssertFeetBuried(Mesh mesh, PalisadeStretch stretch, float tolerance)
        {
            Vector3[] vertices = mesh.vertices;
            var foot = new HashSet<int>(mesh.GetTriangles(PalisadeMesh.FOOT));
            for (float along = CLEAR; along <= stretch.Length - CLEAR; along += 0.1f)
            {
                float lowest = float.PositiveInfinity;
                foreach (int i in foot)
                    if (Mathf.Abs(vertices[i].z - along) < 0.2f) lowest = Mathf.Min(lowest, vertices[i].y);
                Assert.LessOrEqual(lowest, stretch.Ground(along) - PalisadeMesh.BURY + tolerance, $"the log at {along:F1} m does not reach into the ground");
            }
        }

        private static List<float> RopeHeights(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var heights = new List<float>();
            foreach (int i in mesh.GetTriangles(PalisadeMesh.ROPE)) heights.Add(Mathf.Round(vertices[i].y * 1000f) / 1000f);
            return heights;
        }

        /// <summary>The mean x of the rope: which face of the wall the lashings are on.</summary>
        private static float RopeSide(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] rope = mesh.GetTriangles(PalisadeMesh.ROPE);
            float sum = 0f;
            foreach (int i in rope) sum += vertices[i].x;
            return sum / rope.Length;
        }

        private Mesh Build(PalisadeStretch stretch)
        {
            Mesh mesh = PalisadeMesh.Build(StandInKit(), stretch);
            _made.Add(mesh);
            return mesh;
        }

        private static PalisadeStretch Stretch(float length, Func<float, float> ground, float top) => new PalisadeStretch
        {
            Length = length, Ground = ground, TopStart = top, TopMid = top, TopEnd = top,
            ClearStart = CLEAR, ClearEnd = CLEAR, Seed = 7,
        };

        private static PalisadeKitData StandInKit() => new PalisadeKitData
        {
            Head = HEAD, Tile = TILE, RailOffset = LOG_RADIUS + 0.05f, PostOffset = LOG_RADIUS - POST_RADIUS,
            Logs = new[] { Head(LOG_RADIUS, 0.3f), Head(LOG_RADIUS * 0.9f, 0.36f) },
            Posts = new[] { Post() },
            Rail = Prism(0.05f),
            Lashing = Prism(0.02f, POST_RADIUS + 0.05f), // like the model's: toward the rail, on the post's +x
        };

        /// <summary>A cone on a cylinder: the open ring at -HEAD, a shoulder ring, and the point at y 0.</summary>
        private static PalisadePiece Head(float radius, float point)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (float y in new[] { -HEAD, -point })
            {
                for (int s = 0; s < SIDES; s++)
                {
                    vertices.Add(Round(radius, y, s));
                    uvs.Add(new Vector2(-y / TILE, s / (float)SIDES));
                }
            }
            vertices.Add(Vector3.zero);
            uvs.Add(Vector2.zero);
            for (int s = 0; s < SIDES; s++)
            {
                int n = (s + 1) % SIDES;
                triangles.AddRange(new[] { s, SIDES + s, SIDES + n, s, SIDES + n, n, SIDES + s, 2 * SIDES, SIDES + n });
            }

            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = i == 2 * SIDES ? Vector3.up : new Vector3(vertices[i].x, 0f, vertices[i].z).normalized;
            var piece = new PalisadePiece
            {
                Vertices = vertices.ToArray(), Normals = normals, Uvs = uvs.ToArray(), Triangles = triangles.ToArray(), Radius = radius,
                Shoulder = point,
            };
            piece.Ring = PalisadeMesh.FindRing(piece.Vertices, HEAD);
            return piece;
        }

        private static PalisadePiece Post()
        {
            PalisadePiece post = Head(POST_RADIUS, 0.02f);
            post.Shoulder = 0f; // sawn flat
            return post;
        }

        /// <summary>A three-sided bar along +z from 0 to 1.</summary>
        private static PalisadePiece Prism(float radius, float offset = 0f)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            foreach (float z in new[] { 0f, 1f })
            {
                for (int s = 0; s < 3; s++)
                {
                    float a = 2f * Mathf.PI * s / 3f;
                    vertices.Add(new Vector3(offset + Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z));
                    normals.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
                    uvs.Add(new Vector2(z / TILE, s / 3f));
                }
            }
            var triangles = new List<int>();
            for (int s = 0; s < 3; s++)
            {
                int n = (s + 1) % 3;
                triangles.AddRange(new[] { s, 3 + s, 3 + n, s, 3 + n, n });
            }
            return new PalisadePiece { Vertices = vertices.ToArray(), Normals = normals.ToArray(), Uvs = uvs.ToArray(), Triangles = triangles.ToArray() };
        }

        private static Vector3 Round(float radius, float y, int side)
        {
            float a = 2f * Mathf.PI * side / SIDES;
            return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }
    }
}
