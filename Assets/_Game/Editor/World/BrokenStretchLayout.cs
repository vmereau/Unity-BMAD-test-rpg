using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>How a part of the broken stretch meets the ground (the `fit` of the parts file).</summary>
    public enum StretchFit
    {
        Ground,  // a height above the terrain
        Surface, // a height above the terrain or a camp rock, whichever is higher
        Span,    // a log lying between two points, each end resting on the ground
        Strut,   // a unit pole stretched between two points, each a height above its ground
    }

    public sealed class StretchCollider
    {
        public bool Capsule;
        public Vector3 A, B;      // capsule: the two ends of its axis. Box: A is the centre, B the size
        public float Radius;
        public float Yaw;
    }

    public sealed class StretchPart
    {
        public string Name, Mesh;
        public StretchFit Fit;
        public bool OnSurface;    // spans and struts: ends rest on rocks as well as terrain
        public Vector3 Pos;       // ground / surface: x, height, z
        public Vector3 Forward = Vector3.forward;
        public float Roll;
        public Vector3 A, B;      // span: x, -, z. Strut: x, height, z
        public float Lift;
        public StretchCollider[] Colliders = Array.Empty<StretchCollider>();
    }

    public struct StretchPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    /// <summary>
    /// The stretch's own frame on the wall line: x runs along the wall, z out of the camp, y up. Same frame as
    /// build_broken_stretch.py writes its parts in.
    /// </summary>
    public readonly struct StretchFrame
    {
        public readonly Vector2 Origin, Along, Outward;

        /// <param name="campSide">Any point inside the camp: z points away from it.</param>
        public StretchFrame(Vector2 origin, Vector2 along, Vector2 campSide)
        {
            Origin = origin;
            Vector2 line = along.normalized;
            var left = new Vector2(-line.y, line.x);
            Outward = Vector2.Dot(left, campSide - origin) > 0f ? -left : left;
            // x is taken from z, not from the way the line happens to be listed: a line read the other way round would
            // otherwise mirror the layout while the meshes stay as they are.
            Along = new Vector2(Outward.y, -Outward.x);
        }

        public Vector2 ToWorld(float x, float z) => Origin + Along * x + Outward * z;

        public Vector3 Direction(Vector3 local) =>
            new Vector3(Along.x * local.x + Outward.x * local.z, local.y, Along.y * local.x + Outward.y * local.z);
    }

    /// <summary>
    /// Pure placement of the broken stretch's parts: the parts file in, world poses out. No scene, no assets, so the
    /// same file, frame and ground always give the same poses.
    /// </summary>
    public static class BrokenStretchLayout
    {
        private const float STRAIGHT_UP = 0.99f;   // a forward this close to vertical has no heading to keep level

        public static bool TryParse(JObject file, out StretchPart[] parts, out string problem)
        {
            parts = null;
            if (!(file["parts"] is JArray list) || list.Count == 0)
            {
                problem = "no list 'parts'";
                return false;
            }

            var found = new List<StretchPart>();
            var names = new HashSet<string>();
            foreach (JToken token in list)
            {
                if (!(token is JObject))
                {
                    problem = "a part is not an object";
                    return false;
                }

                var part = new StretchPart { Name = Text(token["name"]), Mesh = Text(token["mesh"]) };
                if (string.IsNullOrEmpty(part.Name) || string.IsNullOrEmpty(part.Mesh) || !names.Add(part.Name))
                {
                    problem = $"a part has no name or mesh, or its name '{part.Name}' is used twice";
                    return false;
                }

                string fit = Text(token["fit"]);
                switch (fit)
                {
                    case "ground": part.Fit = StretchFit.Ground; break;
                    case "surface": part.Fit = StretchFit.Surface; break;
                    case "span": part.Fit = StretchFit.Span; break;
                    case "strut": part.Fit = StretchFit.Strut; break;
                    default:
                        problem = $"part '{part.Name}' has an unknown fit '{fit}'";
                        return false;
                }

                bool twoPoints = part.Fit == StretchFit.Span || part.Fit == StretchFit.Strut;
                bool read = part.Fit == StretchFit.Span
                    ? TryFlat(token["a"], out part.A) && TryFlat(token["b"], out part.B)
                    : part.Fit == StretchFit.Strut
                        ? TryVector(token["a"], out part.A) && TryVector(token["b"], out part.B)
                        : TryVector(token["pos"], out part.Pos);
                if (!read || (twoPoints && (part.A - part.B).sqrMagnitude < 1e-6f))
                {
                    problem = $"part '{part.Name}' has no usable position";
                    return false;
                }

                if (TryVector(token["forward"], out Vector3 forward) && forward.sqrMagnitude > 1e-6f) part.Forward = forward.normalized;
                part.Roll = Number(token["roll"]);
                part.Lift = Number(token["lift"]);
                string on = Text(token["on"]);
                if (token["on"] != null && on != "surface" && on != "ground")
                {
                    problem = $"part '{part.Name}' rests on an unknown '{token["on"]}'";
                    return false;
                }
                part.OnSurface = on == "surface";

                if (token["col"] != null && !(token["col"] is JArray))
                {
                    problem = $"part '{part.Name}' has a 'col' that is not a list";
                    return false;
                }

                var colliders = new List<StretchCollider>();
                foreach (JToken c in token["col"] as JArray ?? new JArray())
                {
                    if (!(c is JObject))
                    {
                        problem = $"part '{part.Name}' has a collider that is not an object";
                        return false;
                    }

                    string shape = Text(c["t"]);
                    if (shape != "capsule" && shape != "box")
                    {
                        problem = $"part '{part.Name}' has a collider of an unknown shape '{c["t"]}'";
                        return false;
                    }

                    var collider = new StretchCollider { Capsule = shape == "capsule", Radius = Number(c["r"]), Yaw = Number(c["yaw"]) };
                    bool ok = collider.Capsule
                        ? TryVector(c["a"], out collider.A) && TryVector(c["b"], out collider.B) && collider.Radius > 0f
                        : TryVector(c["c"], out collider.A) && TryVector(c["s"], out collider.B);
                    ok = ok && (collider.Capsule
                        ? (collider.A - collider.B).sqrMagnitude > 1e-6f
                        : collider.B.x > 0f && collider.B.y > 0f && collider.B.z > 0f);
                    if (!ok)
                    {
                        problem = $"part '{part.Name}' has a collider that cannot be read";
                        return false;
                    }
                    colliders.Add(collider);
                }
                part.Colliders = colliders.ToArray();
                found.Add(part);
            }

            parts = found.ToArray();
            problem = null;
            return true;
        }

        /// <param name="ground">Terrain height at a world (x, z).</param>
        /// <param name="surface">The highest of the terrain and the camp's rocks at a world (x, z).</param>
        public static StretchPose Place(StretchPart part, StretchFrame frame, Func<Vector2, float> ground, Func<Vector2, float> surface)
        {
            switch (part.Fit)
            {
                case StretchFit.Span:
                {
                    Func<Vector2, float> rest = part.OnSurface ? surface : ground;
                    Vector2 a = frame.ToWorld(part.A.x, part.A.z), b = frame.ToWorld(part.B.x, part.B.z);
                    var from = new Vector3(a.x, rest(a) + part.Lift, a.y);
                    var to = new Vector3(b.x, rest(b) + part.Lift, b.y);
                    return new StretchPose { Position = from, Rotation = Aim(to - from, part.Roll, frame), Scale = Vector3.one };
                }
                case StretchFit.Strut:
                {
                    Func<Vector2, float> rest = part.OnSurface ? surface : ground;
                    Vector2 a = frame.ToWorld(part.A.x, part.A.z), b = frame.ToWorld(part.B.x, part.B.z);
                    var from = new Vector3(a.x, rest(a) + part.A.y, a.y);
                    var to = new Vector3(b.x, rest(b) + part.B.y, b.y);
                    return new StretchPose { Position = from, Rotation = Aim(to - from, 0f, frame), Scale = new Vector3(1f, 1f, Vector3.Distance(from, to)) };
                }
                default:
                {
                    Vector2 at = frame.ToWorld(part.Pos.x, part.Pos.z);
                    float height = (part.Fit == StretchFit.Surface ? surface(at) : ground(at)) + part.Pos.y;
                    return new StretchPose
                    {
                        Position = new Vector3(at.x, height, at.y),
                        Rotation = Aim(frame.Direction(part.Forward), part.Roll, frame),
                        Scale = Vector3.one,
                    };
                }
            }
        }

        /// <summary>Turns a mesh's +z along <paramref name="forward"/> and then about it by <paramref name="roll"/> degrees.</summary>
        private static Quaternion Aim(Vector3 forward, float roll, StretchFrame frame)
        {
            forward.Normalize();
            Vector3 up = Mathf.Abs(forward.y) > STRAIGHT_UP ? new Vector3(frame.Along.x, 0f, frame.Along.y) : Vector3.up;
            return Quaternion.LookRotation(forward, up) * Quaternion.AngleAxis(roll, Vector3.forward);
        }

        private static string Text(JToken token) => token != null && token.Type == JTokenType.String ? (string)token : null;

        private static float Number(JToken token) => Finite(token) ? (float)token : 0f;

        private static bool TryVector(JToken token, out Vector3 value)
        {
            value = default;
            if (!(token is JArray a) || a.Count != 3) return false;
            foreach (JToken n in a)
                if (!Finite(n)) return false;
            value = new Vector3((float)a[0], (float)a[1], (float)a[2]);
            return true;
        }

        private static bool TryFlat(JToken token, out Vector3 value)
        {
            value = default;
            if (!(token is JArray a) || a.Count != 2 || !Finite(a[0]) || !Finite(a[1])) return false;
            value = new Vector3((float)a[0], 0f, (float)a[1]);
            return true;
        }

        /// <summary>A JSON number that is a real one: the parser also reads NaN and Infinity as floats.</summary>
        private static bool Finite(JToken token)
        {
            if (!Area1BlockoutBuilder.IsNumber(token)) return false;
            double value = (double)token;
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
