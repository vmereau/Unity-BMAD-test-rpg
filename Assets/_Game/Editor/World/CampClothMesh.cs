using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Generated camp cloth: sagging tarps strung between four tie points, and ropes between two. Each tarp has its own
    /// shape (eaves, poles, stakes), so it is built from its corners instead of a fixed model, and saved as a mesh asset
    /// under <see cref="FOLDER"/> so the scene references an asset rather than embedding the mesh.
    /// </summary>
    internal static class CampClothMesh
    {
        private const string TAG = "[CampClothMesh]";
        internal const string FOLDER = "Assets/_Game/Art/Environment/Camp/Generated/";
        private const int CELLS = 24;
        private const float PATCH_LIFT = 0.006f;

        /// <summary>
        /// A two-sided sheet. <paramref name="a"/>→<paramref name="b"/> is the top edge and <paramref name="d"/>→
        /// <paramref name="c"/> the bottom edge (positions in the parent's local frame). The middle sags by
        /// <paramref name="sag"/> metres, the cloth wrinkles and its border frays. Submesh 0 is the canvas,
        /// submesh 1 the patches sewn on it. UVs are 1 m per tile.
        /// </summary>
        internal static Mesh Sheet(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float sag, int seed, int patches)
        {
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100f, oz = (float)rng.NextDouble() * 100f;
            float width = (Vector3.Distance(a, b) + Vector3.Distance(d, c)) * 0.5f;
            float length = (Vector3.Distance(a, d) + Vector3.Distance(b, c)) * 0.5f;
            Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
            if (normal.sqrMagnitude < 0.5f)
                GameLog.Warn(TAG, $"{name}: corners are collinear or coincident; the sheet has no surface to wrinkle.");
            if (normal.y < 0f) normal = -normal; // the top side faces up
            // A quad (i,j)(i+1,j)(i+1,j+1)(i,j+1) faces Cross(b−a, d−a); flip the top face's winding when that is down.
            bool flipTop = Vector3.Dot(Vector3.Cross(b - a, d - a), normal) < 0f;

            int n = CELLS + 1;
            var grid = new Vector3[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float u = i / (float)CELLS, v = j / (float)CELLS;
                    float su = Mathf.Sin(Mathf.PI * u), sv = Mathf.Sin(Mathf.PI * v);
                    Vector3 p = Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(d, c, u), v);
                    p.y -= sag * su * sv;
                    // Wrinkles: soft folds running down the sheet, stronger away from the tied edges. Every offset
                    // fades to zero at the four corners, which stay exactly on their tie points.
                    float fold = Mathf.PerlinNoise(ox + u * 6f, oz + v * 2f) - 0.5f;
                    float ripple = Mathf.Sin(u * width * 9f + Mathf.PerlinNoise(ox, oz + v * 3f) * 4f) * 0.008f;
                    p += normal * ((fold * 0.06f + ripple) * sv + fold * 0.015f * Mathf.Min(1f, su + sv));
                    // Frayed border: jitter edge points along their edge.
                    if (i == 0 || i == CELLS || j == 0 || j == CELLS)
                    {
                        float fray = (Mathf.PerlinNoise(ox + u * 20f, oz + v * 20f) - 0.5f) * 0.05f;
                        p += i == 0 || i == CELLS ? (d - a).normalized * (fray * sv) : (b - a).normalized * (fray * su);
                    }
                    grid[i, j] = p;
                }

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var canvas = new List<int>();
            var patch = new List<int>();
            // Top and bottom faces have their own vertices at the same positions; the bottom winds the other way so
            // both sides render and light correctly.
            for (int side = 0; side < 2; side++)
            {
                int start = vertices.Count;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        vertices.Add(grid[i, j]);
                        uvs.Add(new Vector2(i / (float)CELLS * width, j / (float)CELLS * length));
                    }
                for (int i = 0; i < CELLS; i++)
                    for (int j = 0; j < CELLS; j++)
                        Quad(canvas, start + i * n + j, start + (i + 1) * n + j, start + (i + 1) * n + j + 1, start + i * n + j + 1, (side == 1) != flipTop);
            }

            // Patches: a few rectangles of cells, lifted off both faces along the local surface normal (a sagging
            // sheet leans far from its average plane, where a single lift would sink into the canvas).
            for (int k = 0; k < patches; k++)
            {
                int w = 3 + rng.Next(3), h = 3 + rng.Next(4);
                int i0 = 2 + rng.Next(CELLS - w - 3), j0 = 2 + rng.Next(CELLS - h - 3);
                for (int side = 0; side < 2; side++)
                {
                    int start = vertices.Count;
                    float lift = side == 0 ? PATCH_LIFT : -PATCH_LIFT;
                    for (int i = 0; i <= w; i++)
                        for (int j = 0; j <= h; j++)
                        {
                            vertices.Add(grid[i0 + i, j0 + j] + LocalNormal(grid, i0 + i, j0 + j, normal) * lift);
                            uvs.Add(new Vector2(i * 0.04f, j * 0.04f));
                        }
                    for (int i = 0; i < w; i++)
                        for (int j = 0; j < h; j++)
                        {
                            int s = start + i * (h + 1) + j;
                            Quad(patch, s, s + h + 1, s + h + 2, s + 1, (side == 1) != flipTop);
                        }
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(canvas, 0);
            mesh.SetTriangles(patch, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // the canvas materials carry a normal map
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The sheet's surface normal at a grid point, on the side of <paramref name="up"/>.</summary>
        private static Vector3 LocalNormal(Vector3[,] grid, int i, int j, Vector3 up)
        {
            int last = grid.GetLength(0) - 1;
            Vector3 alongU = grid[Mathf.Min(i + 1, last), j] - grid[Mathf.Max(i - 1, 0), j];
            Vector3 alongV = grid[i, Mathf.Min(j + 1, last)] - grid[i, Mathf.Max(j - 1, 0)];
            Vector3 n = Vector3.Cross(alongU, alongV).normalized;
            return Vector3.Dot(n, up) < 0f ? -n : n;
        }

        /// <summary>A rope between two points (parent's local frame), hanging <paramref name="sag"/> metres at its middle.</summary>
        internal static Mesh Rope(string name, Vector3 from, Vector3 to, float sag, float radius = 0.009f)
        {
            const int SEGMENTS = 16, SIDES = 6;
            if ((to - from).sqrMagnitude < 1e-6f)
            {
                GameLog.Warn(TAG, $"{name}: rope ends coincide; nudged 1 cm so the tube has a direction.");
                to = from + Vector3.right * 0.01f;
            }
            var points = new Vector3[SEGMENTS + 1];
            for (int i = 0; i <= SEGMENTS; i++)
            {
                float t = i / (float)SEGMENTS;
                points[i] = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float along = 0f;
            for (int i = 0; i <= SEGMENTS; i++)
            {
                Vector3 dir = (points[Mathf.Min(i + 1, SEGMENTS)] - points[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                side.Normalize();
                Vector3 up = Vector3.Cross(side, dir);
                if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                for (int k = 0; k <= SIDES; k++)
                {
                    float angle = k / (float)SIDES * Mathf.PI * 2f;
                    Vector3 radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    vertices.Add(points[i] + radial * radius);
                    normals.Add(radial); // set, not recalculated: the ring's repeated seam vertex would shade as a crease
                    uvs.Add(new Vector2(k / (float)SIDES * 0.1f, along * 4f));
                }
            }
            // The ring turns from `side` toward `up`, which makes Cross(b−a, c−a) point into the tube: wind the
            // other way so the outside is the front face.
            for (int i = 0; i < SEGMENTS; i++)
                for (int k = 0; k < SIDES; k++)
                {
                    int s = i * (SIDES + 1) + k;
                    Quad(triangles, s, s + 1, s + SIDES + 2, s + SIDES + 1, true);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Writes the mesh to <see cref="FOLDER"/><paramref name="fileName"/>.asset, reusing an existing asset so its GUID
        /// (and every scene reference to it) stays the same across rebuilds. Returns the saved asset.
        /// </summary>
        internal static Mesh Save(Mesh mesh, string fileName)
        {
            string folder = FOLDER.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            string path = FOLDER + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            // Write it now: the scene is saved against this asset, and a stale file on disk would ship the old shape.
            AssetDatabase.SaveAssetIfDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void Quad(List<int> triangles, int a, int b, int c, int d, bool flip)
        {
            if (flip) triangles.AddRange(new[] { a, c, b, a, d, c });
            else triangles.AddRange(new[] { a, b, c, a, c, d });
        }
    }
}
