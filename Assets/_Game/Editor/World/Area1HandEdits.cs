using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>A local transform, as stored for a hand edit.</summary>
    public readonly struct Area1Pose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public Area1Pose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    public enum Area1EditKind { Moved, Removed, Added }

    /// <summary>One difference between a fresh build and the live scene.</summary>
    public sealed class Area1EditRow
    {
        public Area1EditKind Kind;
        public string Path;
        public Area1Pose Pose;
        public Vector3 Built;     // where the tool puts it, stored so a run can tell when that has changed
        public bool HeightOnly;   // ground that moved since the last run looks the same as a hand edit, so the user decides
        public bool Ticked;
        public bool InFileOnly;   // saved, but the scene shows no difference: dropped only if the user unticks it
    }

    /// <summary>A saved move. <see cref="Built"/> is the tool's position at capture: generated names shift when a layout changes.</summary>
    public sealed class Area1MovedEdit
    {
        public string Path;
        public Area1Pose Pose;
        public Vector3 Built;
    }

    /// <summary>
    /// Hand edits inside the roots the Area 1 tools rebuild, stored beside layout.json and reapplied at the end of each run.
    /// Paths are relative to Area1_Blockout, with Name[1] for the second sibling of that name.
    /// </summary>
    public sealed class Area1HandEdits
    {
        public const string FILE = "docs/World/Areas/area-001/hand-edits.json";

        private const string TAG = "[Area1HandEdits]";
        private const string CAMP_EDGE = "CampEdge";
        private const char SEPARATOR = '/';
        private const float POSITION_EPSILON = 0.001f;
        private const float RETARGET_EPSILON = 0.01f; // the stored value is rounded
        private const float ROTATION_EPSILON = 0.01f; // degrees
        private const float SCALE_EPSILON = 0.001f;
        private const int DECIMALS = 4;

        private static readonly Regex Ordinal = new Regex(@"^(.*)\[(\d{1,9})\]$"); // bounded, so a hand-edited path can't overflow after the roots are rebuilt

        public readonly List<Area1MovedEdit> Moved = new List<Area1MovedEdit>();
        public readonly List<string> Removed = new List<string>();

        public static string FullPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", FILE));

        public static bool IsCampEdge(string path) => path == CAMP_EDGE || path.StartsWith(CAMP_EDGE + SEPARATOR);

        /// <summary>Every object under <paramref name="tops"/>, by path from <paramref name="root"/>.</summary>
        public static Dictionary<string, Area1Pose> Snapshot(Transform root, IEnumerable<Transform> tops)
        {
            var poses = new Dictionary<string, Area1Pose>();
            foreach (Transform top in tops)
            {
                string path = PathOf(top, root);
                poses[path] = PoseOf(top);
                AddChildren(top, path, poses);
            }
            return poses;
        }

        /// <summary>Moved objects at their live pose. A removed or added parent stands for its children.</summary>
        public static List<Area1EditRow> Diff(Dictionary<string, Area1Pose> baseline, Dictionary<string, Area1Pose> live)
        {
            var rows = new List<Area1EditRow>();
            foreach (KeyValuePair<string, Area1Pose> built in baseline.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!live.TryGetValue(built.Key, out Area1Pose pose))
                {
                    if (!HasAncestorIn(built.Key, baseline, live)) rows.Add(new Area1EditRow { Kind = Area1EditKind.Removed, Path = built.Key, Pose = built.Value });
                    continue;
                }

                if (Differs(built.Value, pose, out bool heightOnly))
                    rows.Add(new Area1EditRow { Kind = Area1EditKind.Moved, Path = built.Key, Pose = pose, Built = built.Value.Position, HeightOnly = heightOnly });
            }

            foreach (KeyValuePair<string, Area1Pose> found in live.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (!baseline.ContainsKey(found.Key) && !HasAncestorIn(found.Key, live, baseline))
                    rows.Add(new Area1EditRow { Kind = Area1EditKind.Added, Path = found.Key, Pose = found.Value });
            return rows;
        }

        /// <summary>
        /// Sets the default ticks and adds saved edits with no difference in the scene, because Save replaces the whole file
        /// and an edit it can't list would vanish unseen.
        /// </summary>
        public static List<Area1EditRow> Merge(List<Area1EditRow> rows, Area1HandEdits saved)
        {
            var known = new HashSet<string>(saved.Moved.Select(e => e.Path).Concat(saved.Removed));
            foreach (Area1EditRow row in rows)
                row.Ticked = row.Kind != Area1EditKind.Added && (known.Contains(row.Path) || !row.HeightOnly);

            var listed = new HashSet<string>(rows.Select(r => r.Path));
            var merged = new List<Area1EditRow>(rows);
            foreach (Area1MovedEdit edit in saved.Moved.Where(e => !listed.Contains(e.Path)))
                merged.Add(new Area1EditRow { Kind = Area1EditKind.Moved, Path = edit.Path, Pose = edit.Pose, Built = edit.Built, Ticked = true, InFileOnly = true });
            foreach (string path in saved.Removed.Where(p => !listed.Contains(p)))
                merged.Add(new Area1EditRow { Kind = Area1EditKind.Removed, Path = path, Ticked = true, InFileOnly = true });
            return merged;
        }

        /// <summary>Reapplies the edits <paramref name="owns"/> accepts, and returns how many paths no longer resolve.</summary>
        public int Apply(Transform root, Func<string, bool> owns)
        {
            // Resolved before anything changes: destroying the first of two same-named siblings would shift the second's ordinal.
            var moved = new List<(Transform target, Area1MovedEdit edit)>();
            var removed = new List<Transform>();
            int missing = 0;
            foreach (Area1MovedEdit edit in Moved.Where(e => owns(e.Path)))
            {
                Transform target = Resolve(root, edit.Path);
                if (target != null) moved.Add((target, edit));
                else missing += WarnMissing(edit.Path);
            }

            foreach (string path in Removed.Where(owns))
            {
                Transform target = Resolve(root, path);
                if (target != null) removed.Add(target);
                else missing += WarnMissing(path);
            }

            foreach ((Transform target, Area1MovedEdit edit) in moved)
            {
                if (Vector3.Distance(target.localPosition, edit.Built) > RETARGET_EPSILON)
                    GameLog.Warn(TAG, $"The tool now builds '{edit.Path}' at {target.localPosition}, not {edit.Built}: the layout or ground changed, or the name now means another object. Rescan and check it.");
                target.localPosition = edit.Pose.Position;
                target.localRotation = edit.Pose.Rotation;
                target.localScale = edit.Pose.Scale;
            }

            foreach (Transform target in removed)
                if (target != null) Remove(target.gameObject); // null if its parent is on the list too

            if (missing > 0) GameLog.Warn(TAG, $"{missing} hand edits in {FILE} no longer match anything.");
            return missing;
        }

        /// <summary>The object at <paramref name="path"/>, or null.</summary>
        public static Transform Resolve(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split(SEPARATOR))
            {
                current = FindChild(current, segment);
                if (current == null) return null;
            }
            return current;
        }

        public string ToJson()
        {
            var moved = new JArray(Moved.Select(e => new JObject
            {
                ["path"] = e.Path,
                ["pos"] = Numbers(e.Pose.Position.x, e.Pose.Position.y, e.Pose.Position.z),
                ["rot"] = Numbers(e.Pose.Rotation.x, e.Pose.Rotation.y, e.Pose.Rotation.z, e.Pose.Rotation.w),
                ["scale"] = Numbers(e.Pose.Scale.x, e.Pose.Scale.y, e.Pose.Scale.z),
                ["built"] = Numbers(e.Built.x, e.Built.y, e.Built.z),
            }));
            return new JObject { ["moved"] = moved, ["removed"] = new JArray(Removed) }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }

        public static bool TryParse(string json, out Area1HandEdits edits, out string problem)
        {
            edits = null;
            try
            {
                var parsed = new Area1HandEdits();
                JObject file = JObject.Parse(json);
                if (!(file["moved"] is JArray moved) || !(file["removed"] is JArray removed))
                {
                    problem = "needs 'moved' and 'removed' lists";
                    return false;
                }

                foreach (JToken row in moved)
                {
                    string path = (row as JObject)?["path"]?.Type == JTokenType.String ? (string)row["path"] : null;
                    if (path == null || !TryFloats(row["pos"], 3, out float[] pos) || !TryFloats(row["rot"], 4, out float[] rot) || !TryFloats(row["scale"], 3, out float[] scale) || !TryFloats(row["built"], 3, out float[] built))
                    {
                        problem = $"bad moved entry {row.ToString(Formatting.None)}";
                        return false;
                    }

                    float magnitude = Mathf.Sqrt(rot.Sum(v => v * v));
                    if (magnitude < Mathf.Epsilon)
                    {
                        problem = $"zero rotation at {path}";
                        return false;
                    }

                    var rotation = new Quaternion(rot[0] / magnitude, rot[1] / magnitude, rot[2] / magnitude, rot[3] / magnitude); // the file holds rounded values
                    parsed.Moved.Add(new Area1MovedEdit
                    {
                        Path = path,
                        Pose = new Area1Pose(new Vector3(pos[0], pos[1], pos[2]), rotation, new Vector3(scale[0], scale[1], scale[2])),
                        Built = new Vector3(built[0], built[1], built[2]),
                    });
                }

                foreach (JToken row in removed)
                {
                    if (row.Type != JTokenType.String)
                    {
                        problem = $"bad removed entry {row.ToString(Formatting.None)}";
                        return false;
                    }
                    parsed.Removed.Add((string)row);
                }

                edits = parsed;
                problem = null;
                return true;
            }
            catch (JsonException e)
            {
                problem = e.Message;
                return false;
            }
        }

        /// <summary>No file means no edits. A broken file is a failure, because building past it would lose them.</summary>
        public static bool TryLoad(out Area1HandEdits edits, out string problem) => TryLoad(FullPath, out edits, out problem);

        public static bool TryLoad(string file, out Area1HandEdits edits, out string problem)
        {
            edits = new Area1HandEdits();
            problem = null;
            if (!File.Exists(file)) return true;

            if (TryParse(File.ReadAllText(file), out edits, out string reason)) return true;
            problem = $"{FILE} is broken ({reason}), so a rebuild would lose the hand edits; nothing changed.";
            return false;
        }

        public void Save() => File.WriteAllText(FullPath, ToJson());

        /// <summary>Unity refuses to destroy a part of a prefab instance, so that stays in the scene, switched off.</summary>
        private static void Remove(GameObject target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target) && !PrefabUtility.IsOutermostPrefabInstanceRoot(target)) target.SetActive(false);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        private static int WarnMissing(string path)
        {
            GameLog.Warn(TAG, $"Hand edit for '{path}' matches nothing.");
            return 1;
        }

        private static void AddChildren(Transform parent, string path, Dictionary<string, Area1Pose> poses)
        {
            var seen = new Dictionary<string, int>();
            foreach (Transform child in parent)
            {
                string childPath = path + SEPARATOR + Segment(child.name, seen);
                poses[childPath] = PoseOf(child);
                AddChildren(child, childPath, poses);
            }
        }

        private static string Segment(string name, Dictionary<string, int> seen)
        {
            seen.TryGetValue(name, out int count);
            seen[name] = count + 1;
            return count == 0 ? name : $"{name}[{count}]";
        }

        private static string PathOf(Transform target, Transform root)
        {
            var segments = new List<string>();
            for (Transform t = target; t != root; t = t.parent)
            {
                int ordinal = 0;
                foreach (Transform sibling in t.parent)
                {
                    if (sibling == t) break;
                    if (sibling.name == t.name) ordinal++;
                }
                segments.Insert(0, ordinal == 0 ? t.name : $"{t.name}[{ordinal}]");
            }
            return string.Join(SEPARATOR, segments);
        }

        private static Transform FindChild(Transform parent, string segment)
        {
            Match match = Ordinal.Match(segment);
            string name = match.Success ? match.Groups[1].Value : segment;
            int wanted = match.Success ? int.Parse(match.Groups[2].Value) : 0;
            int seen = 0;
            foreach (Transform child in parent)
                if (child.name == name && seen++ == wanted) return child;
            return null;
        }

        private static Area1Pose PoseOf(Transform t) => new Area1Pose(t.localPosition, t.localRotation, t.localScale);

        private static bool Differs(Area1Pose built, Area1Pose live, out bool heightOnly)
        {
            bool rotated = AngleBetween(built.Rotation, live.Rotation) > ROTATION_EPSILON;
            bool scaled = Differs(new Vector2(built.Scale.x, built.Scale.z), new Vector2(live.Scale.x, live.Scale.z), SCALE_EPSILON);
            bool stretched = Mathf.Abs(built.Scale.y - live.Scale.y) > SCALE_EPSILON; // a panel's height comes from the ground too
            bool flat = Differs(new Vector2(built.Position.x, built.Position.z), new Vector2(live.Position.x, live.Position.z), POSITION_EPSILON);
            bool raised = Mathf.Abs(built.Position.y - live.Position.y) > POSITION_EPSILON;
            heightOnly = (raised || stretched) && !flat && !rotated && !scaled;
            return raised || stretched || flat || rotated || scaled;
        }

        /// <summary>Degrees. Quaternion.Angle returns 0 below about 0.16 degrees, under the epsilon.</summary>
        private static float AngleBetween(Quaternion a, Quaternion b)
        {
            Quaternion delta = Quaternion.Inverse(a) * b;
            float axis = Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
            return 2f * Mathf.Atan2(axis, Mathf.Abs(delta.w)) * Mathf.Rad2Deg;
        }

        private static bool Differs(Vector3 a, Vector3 b, float epsilon) =>
            Mathf.Abs(a.x - b.x) > epsilon || Mathf.Abs(a.y - b.y) > epsilon || Mathf.Abs(a.z - b.z) > epsilon;

        /// <summary>True when a parent of <paramref name="path"/> is in <paramref name="from"/> but not in <paramref name="other"/>.</summary>
        private static bool HasAncestorIn(string path, Dictionary<string, Area1Pose> from, Dictionary<string, Area1Pose> other)
        {
            for (int end = path.LastIndexOf(SEPARATOR); end > 0; end = path.LastIndexOf(SEPARATOR, end - 1))
            {
                string parent = path.Substring(0, end);
                if (from.ContainsKey(parent) && !other.ContainsKey(parent)) return true;
            }
            return false;
        }

        private static JArray Numbers(params float[] values) =>
            new JArray(values.Select(v => Math.Round((double)v, DECIMALS)));

        private static bool TryFloats(JToken token, int count, out float[] values)
        {
            values = null;
            if (!(token is JArray array) || array.Count != count || !array.All(Area1BlockoutBuilder.IsNumber)) return false;
            values = array.Select(v => (float)v).ToArray();
            return values.All(float.IsFinite);
        }
    }
}
