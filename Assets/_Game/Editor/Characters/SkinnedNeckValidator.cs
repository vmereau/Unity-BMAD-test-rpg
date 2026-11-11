using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Read-only authoring checks. All mesh positions and bind poses must already be
    /// in canonical mesh space; posed coverage arrays must share one coordinate space.
    /// An exact skin join and a garment overlapping skin are deliberately separate checks.
    /// </summary>
    public static class SkinnedNeckValidator
    {
        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public bool IsValid => Errors.Count == 0;
            public int Samples;
            public int FailedSamples;
            public int ExaminedTriangles;
            public int DegenerateUvTriangles;
            public float MaxGap;
            public float MaxNormalDegrees;
            public float MaxWeightL1;
            public float MaxUvDistance;
            public float MaxBindPoseDelta;
            public float MaxTangentDegrees;
            public int TangentHandednessMismatches;
            public float MaxCoverageDistance;

            internal void Fail(string message)
            {
                FailedSamples++;
                if (Errors.Count < 20) Errors.Add(message);
            }
        }

        /// <summary>Matches bind poses by unique bone name, not array order. No assets are modified.</summary>
        public static Report ValidateBindSpace(Mesh part, IReadOnlyList<string> partNames,
            Mesh canonical, IReadOnlyList<string> canonicalNames, float tolerance = .00001f)
        {
            var report = new Report();
            if (!Positive(tolerance) || part == null || canonical == null)
            {
                report.Fail("Provide two meshes and a finite positive bind-pose tolerance.");
                return report;
            }
            var partMap = BoneMap(part.bindposes, partNames, "Part", report);
            var canonicalMap = BoneMap(canonical.bindposes, canonicalNames, "Canonical", report);
            if (!report.IsValid) return report;
            var source = part.bindposes;
            var target = canonical.bindposes;
            foreach (var pair in partMap)
            {
                report.Samples++;
                if (!canonicalMap.TryGetValue(pair.Key, out int index))
                {
                    report.Fail("Canonical skeleton lacks bone '" + pair.Key + "'.");
                    continue;
                }
                float delta = 0;
                for (int k = 0; k < 16; k++) delta = Mathf.Max(delta, Mathf.Abs(source[pair.Value][k] - target[index][k]));
                report.MaxBindPoseDelta = Mathf.Max(report.MaxBindPoseDelta, delta);
                if (delta > tolerance)
                    report.Fail($"Bone '{pair.Key}' bind pose differs by {delta:G6}; tolerance {tolerance:G6}. Bake against the canonical skeleton.");
            }
            return report;
        }

        /// <summary>
        /// Validates both sides of an exposed skin join at an explicit canonical Y.
        /// Coincident UV-split vertices are alternatives: any candidate satisfying all
        /// tolerances is a match. Unrelated face/skin edits away from the ring are ignored.
        /// </summary>
        public static Report ValidateExactJoin(Mesh first, IReadOnlyList<string> firstNames,
            Mesh second, IReadOnlyList<string> secondNames, float seamHeight,
            float heightTolerance = .00001f, float gapTolerance = .0001f,
            float normalToleranceDegrees = 1f, float weightTolerance = .0001f, float uvTolerance = .0001f,
            float tangentToleranceDegrees = 1f)
        {
            var report = ValidateBindSpace(first, firstNames, second, secondNames);
            report.Samples = 0;
            if (!report.IsValid) return report;
            if (!Finite(seamHeight) || !Positive(heightTolerance) || !Positive(gapTolerance) ||
                !Positive(normalToleranceDegrees) || !Positive(weightTolerance) || !Positive(uvTolerance) || !Positive(tangentToleranceDegrees))
            {
                report.Fail("Join height and tolerances must be finite; tolerances must be positive.");
                return report;
            }
            if (!SurfaceAttributes(first, report) || !SurfaceAttributes(second, report)) return report;
            var a = new JoinData(first, firstNames, seamHeight, heightTolerance, report);
            var b = new JoinData(second, secondNames, seamHeight, heightTolerance, report);
            if (!report.IsValid) return report;
            Compare(a, b, report, gapTolerance, normalToleranceDegrees, weightTolerance, uvTolerance, tangentToleranceDegrees);
            Compare(b, a, report, gapTolerance, normalToleranceDegrees, weightTolerance, uvTolerance, tangentToleranceDegrees);
            return report;
        }

        /// <summary>Checks nonzero-area triangles whose centroid lies in a supplied neck band/cylinder.</summary>
        public static Report ValidateNeckUV(Mesh mesh, float minY, float maxY, float radius,
            float minimumUvArea = 1e-10f)
        {
            var report = new Report();
            if (mesh == null || !Finite(minY) || !Finite(maxY) || minY >= maxY ||
                !Positive(radius) || !Positive(minimumUvArea))
            {
                report.Fail("Provide a mesh, ordered neck heights, positive radius and UV-area threshold.");
                return report;
            }
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            if (uv.Length != vertices.Length)
            {
                report.Fail("Mesh has no complete UV0 channel.");
                return report;
            }
            var indices = mesh.triangles;
            if (!Triangles(vertices, indices, report)) return report;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3;
                if (center.y < minY || center.y > maxY || center.x * center.x + center.z * center.z > radius * radius) continue;
                if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude < 1e-16f) continue;
                report.ExaminedTriangles++;
                var ab = uv[b] - uv[a];
                var ac = uv[c] - uv[a];
                float area = Mathf.Abs(ab.x * ac.y - ab.y * ac.x) * .5f;
                if (!Finite(area) || area < minimumUvArea)
                {
                    report.DegenerateUvTriangles++;
                    report.Fail($"Neck triangle {i / 3} has UV area {area:G6}; unwrap it rather than copying UV (0,0).");
                }
            }
            if (report.ExaminedTriangles == 0) report.Fail("No nondegenerate triangles fall inside the requested neck region.");
            return report;
        }

        /// <summary>
        /// Casts from each hidden skin-boundary point along its outward normal into
        /// the posed garment. Tests triangle faces from either side (no Physics/scene).
        /// Caller chooses the maximum acceptable overlap depth, in the arrays' units.
        /// This is a boundary coverage check, not a certificate of all surface collisions.
        /// </summary>
        public static Report ValidateCoverage(Vector3[] points, Vector3[] outwardNormals,
            Vector3[] garmentVertices, int[] garmentTriangles, float maxDistance)
        {
            var report = new Report();
            if (points == null || outwardNormals == null || points.Length == 0 ||
                points.Length != outwardNormals.Length || !Positive(maxDistance))
            {
                report.Fail("Provide nonempty corresponding boundary points/normals and a finite positive coverage distance.");
                return report;
            }
            if (!Triangles(garmentVertices, garmentTriangles, report)) return report;
            for (int i = 0; i < points.Length; i++)
            {
                report.Samples++;
                if (!Finite(points[i]) || !Finite(outwardNormals[i]) || outwardNormals[i].sqrMagnitude < 1e-12f)
                {
                    report.Fail($"Boundary sample {i} has a nonfinite position or invalid outward normal.");
                    continue;
                }
                Vector3 direction = outwardNormals[i].normalized;
                float nearest = float.PositiveInfinity;
                for (int t = 0; t < garmentTriangles.Length; t += 3)
                {
                    if (RayTriangle(points[i], direction, garmentVertices[garmentTriangles[t]],
                        garmentVertices[garmentTriangles[t + 1]], garmentVertices[garmentTriangles[t + 2]], out float distance))
                        nearest = Mathf.Min(nearest, distance);
                }
                if (nearest > maxDistance)
                    report.Fail($"Boundary sample {i} at {points[i]} is not covered within {maxDistance:G6}; nearest outward hit {nearest:G6}.");
                else report.MaxCoverageDistance = Mathf.Max(report.MaxCoverageDistance, nearest);
            }
            return report;
        }

        private sealed class JoinData
        {
            internal readonly string Name;
            internal readonly Vector3[] Positions, Normals;
            internal readonly Vector2[] UV;
            internal readonly Vector4[] Tangents;
            internal readonly List<int> Ring = new List<int>();
            internal readonly Dictionary<string, float>[] Weights;

            internal JoinData(Mesh mesh, IReadOnlyList<string> names, float height, float tolerance, Report report)
            {
                Name = mesh.name;
                Positions = mesh.vertices;
                Normals = mesh.normals;
                UV = mesh.uv;
                Tangents = mesh.tangents;
                Weights = new Dictionary<string, float>[Positions.Length];
                var counts = mesh.GetBonesPerVertex();
                var weights = mesh.GetAllBoneWeights();
                if (counts.Length != Positions.Length)
                {
                    report.Fail("Mesh '" + Name + "' has no complete skin weights.");
                    return;
                }
                int cursor = 0;
                for (int i = 0; i < Positions.Length; i++)
                {
                    bool onRing = Mathf.Abs(Positions[i].y - height) <= tolerance;
                    var influence = onRing ? new Dictionary<string, float>() : null;
                    if (onRing) Ring.Add(i);
                    float total = 0;
                    for (int k = 0; k < counts[i]; k++)
                    {
                        var weight = weights[cursor++];
                        if (!onRing) continue;
                        if (weight.boneIndex < 0 || weight.boneIndex >= names.Count || !Finite(weight.weight) || weight.weight < 0)
                        {
                            report.Fail($"Mesh '{Name}' vertex {i} has an invalid bone influence.");
                            continue;
                        }
                        string bone = names[weight.boneIndex];
                        influence.TryGetValue(bone, out float old);
                        influence[bone] = old + weight.weight;
                        total += weight.weight;
                    }
                    if (onRing && Mathf.Abs(total - 1) > .001f) report.Fail($"Mesh '{Name}' ring vertex {i} weights sum to {total:G6}, expected 1.");
                    Weights[i] = influence;
                }
                if (Ring.Count == 0) report.Fail($"Mesh '{Name}' has no vertices at neck join Y={height:G6}.");
            }
        }

        private static void Compare(JoinData a, JoinData b, Report report, float gapTol, float normalTol, float weightTol, float uvTol, float tangentTol)
        {
            foreach (int i in a.Ring)
            {
                float nearest = float.PositiveInfinity, bestScore = float.PositiveInfinity;
                float bestGap = 0, bestNormal = 0, bestWeight = 0, bestUv = 0;
                float bestTangent = 0;
                bool bestHandedness = false;
                bool match = false;
                foreach (int j in b.Ring)
                {
                    float gap = Vector3.Distance(a.Positions[i], b.Positions[j]);
                    nearest = Mathf.Min(nearest, gap);
                    if (gap > gapTol) continue;
                    float normal = Vector3.Angle(a.Normals[i], b.Normals[j]);
                    float uv = Vector2.Distance(a.UV[i], b.UV[j]);
                    float weight = WeightDifference(a.Weights[i], b.Weights[j]);
                    float tangent = Vector3.Angle(a.Tangents[i], b.Tangents[j]);
                    bool handedness = Mathf.Sign(a.Tangents[i].w) != Mathf.Sign(b.Tangents[j].w);
                    float score = Mathf.Max(gap / gapTol, normal / normalTol, weight / weightTol, uv / uvTol, tangent / tangentTol, handedness ? 2 : 0);
                    if (score < bestScore)
                    {
                        bestScore = score; bestGap = gap; bestNormal = normal; bestWeight = weight; bestUv = uv;
                        bestTangent = tangent; bestHandedness = handedness;
                    }
                    if (score <= 1) match = true;
                }
                report.Samples++;
                report.MaxGap = Mathf.Max(report.MaxGap, float.IsPositiveInfinity(bestScore) ? nearest : bestGap);
                report.MaxNormalDegrees = Mathf.Max(report.MaxNormalDegrees, bestNormal);
                report.MaxWeightL1 = Mathf.Max(report.MaxWeightL1, bestWeight);
                report.MaxUvDistance = Mathf.Max(report.MaxUvDistance, bestUv);
                report.MaxTangentDegrees = Mathf.Max(report.MaxTangentDegrees, bestTangent);
                if (bestHandedness) report.TangentHandednessMismatches++;
                if (!match)
                    report.Fail($"'{a.Name}' ring vertex {i}: gap {nearest * 1000:G6} mm, normal {bestNormal:G6} deg, tangent {bestTangent:G6} deg, flipped tangent {bestHandedness}, weight L1 {bestWeight:G6}, UV delta {bestUv:G6}. No compatible opposite vertex.");
            }
        }

        private static float WeightDifference(Dictionary<string, float> a, Dictionary<string, float> b)
        {
            float sum = 0;
            foreach (var pair in a) { b.TryGetValue(pair.Key, out float other); sum += Mathf.Abs(pair.Value - other); }
            foreach (var pair in b) if (!a.ContainsKey(pair.Key)) sum += Mathf.Abs(pair.Value);
            return sum;
        }

        private static Dictionary<string, int> BoneMap(Matrix4x4[] poses, IReadOnlyList<string> names, string label, Report report)
        {
            var map = new Dictionary<string, int>();
            if (names == null || poses.Length == 0 || poses.Length != names.Count)
            {
                report.Fail(label + " bone-name/bind-pose counts are absent or inconsistent.");
                return map;
            }
            for (int i = 0; i < names.Count; i++)
            {
                if (string.IsNullOrEmpty(names[i]) || map.ContainsKey(names[i])) report.Fail(label + " has an empty or duplicate bone name at index " + i + ".");
                else map.Add(names[i], i);
                for (int k = 0; k < 16; k++) if (!Finite(poses[i][k])) { report.Fail(label + " has a nonfinite bind pose at index " + i + "."); break; }
                if (Mathf.Abs(poses[i].determinant) < 1e-12f) report.Fail(label + " has a singular bind pose at index " + i + ".");
            }
            return map;
        }

        private static bool SurfaceAttributes(Mesh mesh, Report report)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var tangents = mesh.tangents;
            if (vertices.Length == 0 || normals.Length != vertices.Length || uv.Length != vertices.Length || tangents.Length != vertices.Length)
            {
                report.Fail("Mesh '" + mesh.name + "' needs positions, normals, tangents and UV0 for every vertex.");
                return false;
            }
            for (int i = 0; i < vertices.Length; i++)
                if (!Finite(vertices[i]) || !Finite(normals[i]) || normals[i].sqrMagnitude < 1e-12f || !Finite(uv[i].x) || !Finite(uv[i].y) ||
                    !Finite((Vector3)tangents[i]) || ((Vector3)tangents[i]).sqrMagnitude < 1e-12f || !Finite(tangents[i].w) || Mathf.Abs(tangents[i].w) < .5f)
                {
                    report.Fail($"Mesh '{mesh.name}' vertex {i} has nonfinite attributes or a zero normal.");
                    return false;
                }
            return true;
        }

        private static bool Triangles(Vector3[] vertices, int[] indices, Report report)
        {
            if (vertices == null || vertices.Length == 0 || indices == null || indices.Length == 0 || indices.Length % 3 != 0)
            { report.Fail("Provide vertices and a nonempty triangle index array (length divisible by three)."); return false; }
            foreach (var vertex in vertices) if (!Finite(vertex)) { report.Fail("Triangle surface contains a nonfinite vertex."); return false; }
            foreach (int index in indices) if (index < 0 || index >= vertices.Length) { report.Fail("Triangle index is outside the vertex array: " + index + "."); return false; }
            return true;
        }

        private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0;
            var ab = b - a; var ac = c - a;
            var p = Vector3.Cross(direction, ac);
            float determinant = Vector3.Dot(ab, p);
            if (Mathf.Abs(determinant) < 1e-10f) return false;
            float inverse = 1 / determinant;
            var delta = origin - a;
            float u = Vector3.Dot(delta, p) * inverse;
            if (u < -1e-6f || u > 1 + 1e-6f) return false;
            var q = Vector3.Cross(delta, ab);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < -1e-6f || u + v > 1 + 1e-6f) return false;
            distance = Vector3.Dot(ac, q) * inverse;
            return distance >= 0;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Positive(float value) => Finite(value) && value > 0;
    }
}
