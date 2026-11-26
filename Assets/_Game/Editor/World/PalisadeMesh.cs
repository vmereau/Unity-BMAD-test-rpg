using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>One kit piece as plain arrays, so the assembly can be tested without the model.</summary>
    public sealed class PalisadePiece
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] Uvs;
        public int[] Triangles;
        public float Radius;

        /// <summary>How far under its top a log head's lowest shoulder lies: where its point starts. 0 for a flat-topped post.</summary>
        public float Shoulder;

        /// <summary>The open ring at the bottom of a log or post head, in order round the axis. Empty for other pieces.</summary>
        public int[] Ring = Array.Empty<int>();
    }

    /// <summary>The palisade kit (Art/Environment/Camp/models/palisade): sizes come from the model script's sizes file.</summary>
    public sealed class PalisadeKitData
    {
        public float Head;          // log and post heads end in their open ring this far under their top
        public float Tile;          // metres per tile along a log
        public float RailOffset;    // across the wall, from the logs' line toward the camp side
        public float PostOffset;
        public PalisadePiece[] Logs;
        public PalisadePiece[] Posts;
        public PalisadePiece Rail;
        public PalisadePiece Lashing;
    }

    /// <summary>
    /// One stretch of wall, in wall space: the origin under its start, z along it, x to its right, y the world height.
    /// </summary>
    public sealed class PalisadeStretch
    {
        public float Length;
        /// <summary>
        /// World height of the ground a log must go under, at a distance along the stretch: on a bank, the lowest
        /// ground across the wall's thickness, or the log's outer face hangs in the air.
        /// </summary>
        public Func<float, float> Ground;
        /// <summary>World height of the ground on the camp side, which the low rail follows. Null: <see cref="Ground"/>.</summary>
        public Func<float, float> RailGround;
        /// <summary>No log ends above this world height, however the ground lies (the panel box's own bottom).</summary>
        public float Bottom = float.PositiveInfinity;
        /// <summary>The line no log top may come under, at the start, the middle and the end.</summary>
        public float TopStart, TopMid, TopEnd;
        /// <summary>
        /// The lower of the two tops that meet at each end: the high rail hangs under these, so it meets the neighbour's
        /// rail at the joint and never rises above this stretch's own shoulders. NaN: <see cref="TopMid"/>.
        /// </summary>
        public float JointTopStart = float.NaN, JointTopEnd = float.NaN;
        /// <summary>+1 when the camp is on the stretch's right (+x), -1 on its left. Rails and lashings go there.</summary>
        public float CampSide = 1f;
        public bool PostAtStart = true;
        public bool PostAtEnd;
        /// <summary>How far the ordinary logs keep from each end (a post's half width, or the gap to a neighbour).</summary>
        public float ClearStart, ClearEnd;
        /// <summary>
        /// Another stretch runs on from the end and stands its post there, so the rails reach the end. False with no
        /// post of its own: the wall stops <see cref="ClearEnd"/> short (against the warehouse's junction log).
        /// </summary>
        public bool RunsOn = true;
        public int Seed;
        /// <summary>Wall space to the mesh's own space (a panel is a scaled box, so this is rarely the identity).</summary>
        public Matrix4x4 ToMesh = Matrix4x4.identity;
    }

    /// <summary>
    /// Builds one stretch of palisade from the kit: a post at the joint, logs standing side by side between the
    /// posts, each one reaching from above the top line down into the ground under it, two rails on the camp side
    /// and a rope lashing where a rail crosses a post. Pure: same kit, stretch and seed give the same mesh.
    /// </summary>
    public static class PalisadeMesh
    {
        public const int WOOD = 0, FOOT = 1, ROPE = 2;
        public const int SUBMESHES = 3;

        public const float BURY = 0.7f;              // every log goes this far under its own ground
        public const float SHOULDER_CLEAR = 0.02f;   // a log's lowest shoulder above the top line: the notches clear it too
        private const float RAG = 0.2f;              // tops stand up to this much above each other
        private const float POST_RISE = 0.3f;        // a post's sawn top above the top line
        private const float POST_RAG = 0.12f;
        private const float OVERLAP = 0.9f;          // logs press into each other: an eight-sided, tapering head is narrower than its radius says
        private const float FOOT_HEIGHT = 0.3f;      // the damp, darker part above the ground
        private const float FOOT_RAG = 0.12f;
        private const float FOOT_FLARE = 1.03f;
        private const float LOW_RAIL = 1.25f;        // above the ground
        private const float HIGH_RAIL = 0.75f;       // under the top line
        private const float MIN_RAIL_GAP = 0.9f;     // a wall too low for two rails gets one
        private const float MIN_RAIL = 0.5f;
        private const float MIN_SPAN = 0.02f;        // no room for a log between the posts
        private const float MIN_SHAFT = 0.2f;        // a log's shaft under its head, whatever the ground
        private const float FOOT_MARGIN = 0.05f;     // the foot's edge keeps clear of the head's ring and of the bottom
        private const float RING_TOLERANCE = 0.002f;

        /// <summary>The top line at a distance along the stretch: straight from each end to the middle.</summary>
        public static float TopAt(PalisadeStretch stretch, float along)
        {
            float half = stretch.Length * 0.5f;
            if (half <= 0f) return stretch.TopMid;
            return along < half
                ? Mathf.Lerp(stretch.TopStart, stretch.TopMid, Mathf.Clamp01(along / half))
                : Mathf.Lerp(stretch.TopMid, stretch.TopEnd, Mathf.Clamp01((along - half) / half));
        }

        /// <summary>The vertices of a head's open ring, one per place, in order round the axis.</summary>
        public static int[] FindRing(Vector3[] vertices, float head)
        {
            var ring = new List<int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Mathf.Abs(vertices[i].y + head) > RING_TOLERANCE) continue;
                bool known = false;
                foreach (int other in ring)
                    if ((vertices[other] - vertices[i]).sqrMagnitude < RING_TOLERANCE * RING_TOLERANCE) known = true;
                if (!known) ring.Add(i);
            }
            ring.Sort((a, b) => Mathf.Atan2(vertices[a].z, vertices[a].x).CompareTo(Mathf.Atan2(vertices[b].z, vertices[b].x)));
            return ring.ToArray();
        }

        public static Mesh Build(PalisadeKitData kit, PalisadeStretch stretch)
        {
            var random = new System.Random(stretch.Seed);
            var builder = new Builder(stretch.ToMesh);
            float side = stretch.CampSide >= 0f ? 1f : -1f;
            float logRadius = MaxRadius(kit.Logs);

            if (stretch.PostAtStart) Post(builder, kit, stretch, random, 0f, side);
            if (stretch.PostAtEnd) Post(builder, kit, stretch, random, stretch.Length, side);

            float from = stretch.ClearStart, to = stretch.Length - stretch.ClearEnd;
            if (to - from > MIN_SPAN) Logs(builder, kit, stretch, random, from, to, side, logRadius);

            Rails(builder, kit, stretch, side);
            return builder.ToMesh();
        }

        private static void Post(Builder builder, PalisadeKitData kit, PalisadeStretch stretch, System.Random random, float along, float side)
        {
            PalisadePiece post = kit.Posts[random.Next(kit.Posts.Length)];
            float top = TopAt(stretch, along) + POST_RISE + (float)random.NextDouble() * POST_RAG;
            var at = new Vector3(side * kit.PostOffset, top, along);
            Trunk(builder, kit, post, at, (float)random.NextDouble() * 360f, (float)random.NextDouble(), stretch.Ground(along), stretch.Bottom, random);
        }

        private static void Logs(Builder builder, PalisadeKitData kit, PalisadeStretch stretch, System.Random random,
            float from, float to, float side, float widest)
        {
            // Pick logs until they fill the span when pressed together, then spread them evenly over it.
            var picked = new List<PalisadePiece>();
            float span = to - from, width = 0f;
            while (width * OVERLAP < span)
            {
                PalisadePiece log = kit.Logs[random.Next(kit.Logs.Length)];
                picked.Add(log);
                width += log.Radius * 2f;
            }

            float squeeze = span / width;
            float cursor = from;
            foreach (PalisadePiece log in picked)
            {
                float along = cursor + log.Radius * squeeze;
                cursor += log.Radius * 2f * squeeze;

                float top = TopAt(stretch, along) + log.Shoulder + SHOULDER_CLEAR + (float)random.NextDouble() * RAG;
                // every camp-side face on one line, so the rail lies against all of them
                var at = new Vector3(side * (widest - log.Radius), top, along);
                Trunk(builder, kit, log, at, (float)random.NextDouble() * 360f, (float)random.NextDouble(), stretch.Ground(along), stretch.Bottom, random);
            }
        }

        /// <summary>A head at its top, then its open ring carried down: weathered wood to the damp foot, the foot into the ground.</summary>
        private static void Trunk(Builder builder, PalisadeKitData kit, PalisadePiece piece, Vector3 top, float yaw, float shift,
            float ground, float floor, System.Random random)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            int first = builder.Add(piece, WOOD, v => top + turn * v, n => turn * n, uv => new Vector2(uv.x + shift, uv.y));

            float ringY = top.y - kit.Head;
            float bottomY = Mathf.Min(Mathf.Min(ground - BURY, floor), ringY - MIN_SHAFT);
            float footY = ground + FOOT_HEIGHT + ((float)random.NextDouble() * 2f - 1f) * FOOT_RAG;
            footY = Mathf.Clamp(footY, bottomY + FOOT_MARGIN, ringY - FOOT_MARGIN);

            int count = piece.Ring.Length;
            var upper = new int[count];
            for (int i = 0; i < count; i++) upper[i] = first + piece.Ring[i];

            int[] foot = builder.Drop(upper, top, footY, 1f, ringY, kit.Tile);
            builder.Band(upper, foot, top, WOOD);
            int[] bottom = builder.Drop(foot, top, bottomY, FOOT_FLARE, footY, kit.Tile);
            builder.Band(foot, bottom, top, FOOT);
        }

        private static void Rails(Builder builder, PalisadeKitData kit, PalisadeStretch stretch, float side)
        {
            float from = stretch.PostAtStart ? 0f : stretch.ClearStart;
            float to = stretch.PostAtEnd || stretch.RunsOn ? stretch.Length : stretch.Length - stretch.ClearEnd;
            if (to - from < MIN_RAIL) return;

            Func<float, float> ground = stretch.RailGround ?? stretch.Ground;
            float lowA = ground(from) + LOW_RAIL, lowB = ground(to) + LOW_RAIL;
            // Under the lower top at each joint, never the taller one: the top line may climb to a taller neighbour at
            // an end, and a rail aimed there would cross the middle above the logs' shoulders.
            float highA = Mathf.Min(stretch.TopMid, float.IsNaN(stretch.JointTopStart) ? stretch.TopMid : stretch.JointTopStart) - HIGH_RAIL;
            float highB = Mathf.Min(stretch.TopMid, float.IsNaN(stretch.JointTopEnd) ? stretch.TopMid : stretch.JointTopEnd) - HIGH_RAIL;
            bool two = highA - lowA > MIN_RAIL_GAP && highB - lowB > MIN_RAIL_GAP;

            float x = side * kit.RailOffset;
            foreach ((float a, float b, bool wanted) in new[] { (lowA, lowB, two), (highA, highB, true) })
            {
                if (!wanted) continue;
                var start = new Vector3(x, a, from);
                var end = new Vector3(x, b, to);
                Vector3 run = end - start;
                float length = run.magnitude;
                Quaternion aim = Quaternion.LookRotation(run / length, Vector3.up);
                builder.Add(kit.Rail, WOOD,
                    v => start + aim * new Vector3(v.x, v.y, v.z * length),
                    n => aim * n,
                    uv => new Vector2(uv.x * length, uv.y));

                Quaternion face = Quaternion.Euler(0f, side > 0f ? 0f : 180f, 0f);
                if (stretch.PostAtStart) Lash(builder, kit, new Vector3(side * kit.PostOffset, a, 0f), face);
                if (stretch.PostAtEnd) Lash(builder, kit, new Vector3(side * kit.PostOffset, b, stretch.Length), face);
            }
        }

        private static void Lash(Builder builder, PalisadeKitData kit, Vector3 at, Quaternion face) =>
            builder.Add(kit.Lashing, ROPE, v => at + face * v, n => face * n, uv => uv);

        private static float MaxRadius(PalisadePiece[] pieces)
        {
            float max = 0f;
            foreach (PalisadePiece piece in pieces) max = Mathf.Max(max, piece.Radius);
            return max;
        }

        private sealed class Builder
        {
            private readonly Matrix4x4 _toMesh;
            private readonly Matrix4x4 _normalToMesh;
            private readonly List<Vector3> _wall = new List<Vector3>();     // wall space, for the bands' facing
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _triangles = { new List<int>(), new List<int>(), new List<int>() };

            public Builder(Matrix4x4 toMesh)
            {
                _toMesh = toMesh;
                _normalToMesh = toMesh.inverse.transpose; // normals keep facing out under the panel's uneven scale
            }

            public int Add(PalisadePiece piece, int submesh, Func<Vector3, Vector3> place, Func<Vector3, Vector3> turn, Func<Vector2, Vector2> map)
            {
                int first = _vertices.Count;
                for (int i = 0; i < piece.Vertices.Length; i++)
                    Vertex(place(piece.Vertices[i]), turn(piece.Normals[i]), map(piece.Uvs[i]));
                foreach (int index in piece.Triangles) _triangles[submesh].Add(first + index);
                return first;
            }

            /// <summary>
            /// A copy of <paramref name="ring"/> at height <paramref name="y"/> on the same axis, <paramref name="flare"/>
            /// times as wide, its U moved on by the drop so the grain keeps its scale however long the log is.
            /// </summary>
            public int[] Drop(int[] ring, Vector3 axis, float y, float flare, float fromY, float tile)
            {
                var lower = new int[ring.Length];
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector3 p = _wall[ring[i]];
                    var out2 = new Vector2(p.x - axis.x, p.z - axis.z);
                    Vector2 uv = _uvs[ring[i]];
                    lower[i] = _vertices.Count;
                    Vertex(new Vector3(axis.x + out2.x * flare, y, axis.z + out2.y * flare),
                        new Vector3(out2.x, 0f, out2.y).normalized,
                        new Vector2(uv.x + (fromY - y) / tile, uv.y));
                }
                return lower;
            }

            public void Band(int[] upper, int[] lower, Vector3 axis, int submesh)
            {
                int count = upper.Length;
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    int a = upper[i], b = upper[next], c = lower[next], d = lower[i];
                    Vector3 facing = Vector3.Cross(_wall[b] - _wall[a], _wall[d] - _wall[a]);
                    Vector3 outward = _wall[a] - new Vector3(axis.x, _wall[a].y, axis.z);
                    if (Vector3.Dot(facing, outward) < 0f) (b, d) = (d, b);
                    _triangles[submesh].AddRange(new[] { a, b, c, a, c, d });
                }
            }

            private void Vertex(Vector3 wall, Vector3 normal, Vector2 uv)
            {
                _wall.Add(wall);
                _vertices.Add(_toMesh.MultiplyPoint3x4(wall));
                _normals.Add(_normalToMesh.MultiplyVector(normal).normalized);
                _uvs.Add(uv);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = SUBMESHES;
                for (int i = 0; i < SUBMESHES; i++) mesh.SetTriangles(_triangles[i], i);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
