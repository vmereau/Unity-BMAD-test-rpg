using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.World
{
    /// <summary>
    /// Compares the live Area 1 blockout with a fresh build in a temporary clone, and saves the ticked differences to
    /// <see cref="Area1HandEdits.FILE"/>, which the tools reapply. A scan leaves the live objects alone.
    /// </summary>
    public sealed class Area1HandEditsWindow : EditorWindow
    {
        private const string TAG = "[Area1HandEditsWindow]";
        private const string BLOCKOUT_ROOT = "Area1_Blockout";
        private const string HEIGHT_NOTE = "height only";
        private const string FILE_NOTE = "in the file, no difference in the scene: untick to drop it";
        private const string LOST_NOTE = "will be lost: move it out of the tool's root";
        private const string IDLE = "Scan compares positions, rotations, scales and which objects exist. Other changes, such as materials or a disabled object, aren't kept.";

        private readonly List<IGrouping<string, Area1EditRow>> _groups = new List<IGrouping<string, Area1EditRow>>();
        private readonly HashSet<string> _open = new HashSet<string>();
        private Dictionary<string, Area1Pose> _scannedLive; // null until a scan succeeds
        private string _scannedScene;
        private Vector2 _scroll;
        private string _status = IDLE;

        [MenuItem("Tools/World/Area 1 Hand Edits")]
        public static void Open() => GetWindow<Area1HandEditsWindow>("Area 1 Hand Edits");

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan")) Scan();
                using (new EditorGUI.DisabledScope(_scannedLive == null)) // an empty scan can still clear stale entries
                    if (GUILayout.Button("Save")) Save();
            }

            EditorGUILayout.HelpBox(_status, MessageType.None);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (IGrouping<string, Area1EditRow> group in _groups) DrawGroup(group);
            EditorGUILayout.EndScrollView();
        }

        private void DrawGroup(IGrouping<string, Area1EditRow> group)
        {
            List<Area1EditRow> savable = group.Where(r => r.Kind != Area1EditKind.Added).ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                bool open = EditorGUILayout.Foldout(_open.Contains(group.Key), $"{group.Key} ({savable.Count(r => r.Ticked)} of {savable.Count} ticked)", true);
                if (open) _open.Add(group.Key);
                else _open.Remove(group.Key);

                bool all = savable.Count > 0 && savable.All(r => r.Ticked);
                bool tick = EditorGUILayout.ToggleLeft("All", all, GUILayout.Width(50f));
                if (tick != all) savable.ForEach(r => r.Ticked = tick);
            }

            if (!_open.Contains(group.Key)) return;
            foreach (Area1EditRow row in group) DrawRow(row);
        }

        private static void DrawRow(Area1EditRow row)
        {
            string label = $"{row.Kind}  {row.Path}";
            if (row.Kind == Area1EditKind.Added)
            {
                EditorGUILayout.LabelField($"{label}  ({LOST_NOTE})");
                return;
            }

            string note = row.InFileOnly ? $"  ({FILE_NOTE})" : row.HeightOnly ? $"  ({HEIGHT_NOTE})" : "";
            row.Ticked = EditorGUILayout.ToggleLeft(label + note, row.Ticked);
        }

        private void Scan()
        {
            _groups.Clear();
            _scannedLive = null;
            if (EditorApplication.isPlaying)
            {
                _status = "Leave play mode first.";
                return;
            }

            GameObject root = GameObject.Find(BLOCKOUT_ROOT);
            if (root == null)
            {
                _status = $"{BLOCKOUT_ROOT} isn't in the open scenes.";
                return;
            }

            if (!Area1HandEdits.TryLoad(out Area1HandEdits saved, out string problem))
            {
                _status = problem;
                return;
            }

            if (!TryBuildBaseline(root, out Dictionary<string, Area1Pose> baseline, out problem))
            {
                _status = $"Nothing scanned: {problem}";
                return;
            }

            _scannedLive = Area1HandEdits.Snapshot(root.transform, OwnedTops(root.transform));
            _scannedScene = root.scene.path;
            List<Area1EditRow> rows = Area1HandEdits.Merge(Area1HandEdits.Diff(baseline, _scannedLive), saved);
            _groups.AddRange(rows.GroupBy(r => r.Path.Split('/')[0]));
            _status = rows.Count == 0
                ? "The scene matches a fresh build, and the file has no edits."
                : $"{rows.Count} rows. New height-only rows start unticked, because stale ground looks the same as a hand edit. If the layout changed, rerun the tools first: a stale object looks like an edit too.";
        }

        private static bool TryBuildBaseline(GameObject root, out Dictionary<string, Area1Pose> baseline, out string problem)
        {
            baseline = null;
            GameObject clone = Instantiate(root);
            try
            {
                SceneManager.MoveGameObjectToScene(clone, root.scene); // the barricades are found through the clone's scene
                if (!Area1SitesBuilder.Run(clone, false, out problem) || !Area1BlockoutBuilder.Run(clone, false, out problem)) return false;
                baseline = Area1HandEdits.Snapshot(clone.transform, OwnedTops(clone.transform));
                return true;
            }
            catch (System.Exception e) // a builder bug must reach the window
            {
                GameLog.Error(TAG, e.ToString());
                problem = e.Message;
                return false;
            }
            finally
            {
                DestroyImmediate(clone);
            }
        }

        private static IEnumerable<Transform> OwnedTops(Transform root) => Area1SitesBuilder.OwnedTops(root).Concat(Area1BlockoutBuilder.OwnedTops(root));

        private void Save()
        {
            GameObject root = GameObject.Find(BLOCKOUT_ROOT);
            if (root == null || root.scene.path != _scannedScene
                || Area1HandEdits.Diff(_scannedLive, Area1HandEdits.Snapshot(root.transform, OwnedTops(root.transform))).Count > 0)
            {
                _status = "The scene changed since the scan, so these rows are out of date. Scan again; nothing saved.";
                return;
            }

            var edits = new Area1HandEdits();
            foreach (Area1EditRow row in _groups.SelectMany(g => g).Where(r => r.Ticked))
            {
                if (row.Kind == Area1EditKind.Moved) edits.Moved.Add(new Area1MovedEdit { Path = row.Path, Pose = row.Pose, Built = row.Built });
                else edits.Removed.Add(row.Path);
            }

            edits.Save();
            _status = $"Saved {edits.Moved.Count} moved and {edits.Removed.Count} removed to {Area1HandEdits.FILE}.";
            GameLog.Info(TAG, _status);
        }
    }
}
