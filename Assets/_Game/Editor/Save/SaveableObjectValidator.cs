using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Save
{
    /// <summary>
    /// Checks save keys of every <see cref="SaveableObject"/> in the open scenes: assigns missing
    /// <c>_saveId</c>s, reports duplicate keys (and offers to regenerate them), and reports entities
    /// whose PersistentID has no KilledFact. Runs from <c>Tools/Save/Validate Save IDs</c> (fixes) and on
    /// every scene save (report only).
    /// </summary>
    [InitializeOnLoad]
    public static class SaveableObjectValidator
    {
        private const string TAG = "[SaveValidator]";
        public const string SAVE_ID_PROPERTY = "_saveId";

        static SaveableObjectValidator()
        {
            EditorSceneManager.sceneSaving -= HandleSceneSaving;
            EditorSceneManager.sceneSaving += HandleSceneSaving;
        }

        private static void HandleSceneSaving(Scene scene, string path) =>
            Validate(Collect(s => s == scene), fix: false);

        [MenuItem("Tools/Save/Validate Save IDs")]
        public static void ValidateOpenScenes()
        {
            int problems = Validate(Collect(_ => true), fix: true);
            if (problems == 0) GameLog.Info(TAG, "All save IDs are valid.");
        }

        public static string NewSaveId() => System.Guid.NewGuid().ToString("N");

        /// <summary>Writes a new id through SerializedObject so Undo and prefab-instance overrides record it.</summary>
        public static void AssignNewId(SaveableObject saveable)
        {
            var so = new SerializedObject(saveable);
            so.FindProperty(SAVE_ID_PROPERTY).stringValue = NewSaveId();
            so.ApplyModifiedProperties();
            if (saveable.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(saveable.gameObject.scene);
        }

        private static List<SaveableObject> Collect(System.Func<Scene, bool> sceneFilter) =>
            Object.FindObjectsByType<SaveableObject>(FindObjectsInactive.Include)
                .Where(s => s != null
                            && !EditorUtility.IsPersistent(s)
                            && s.gameObject.scene.IsValid()
                            && s.gameObject.scene.isLoaded
                            && !EditorSceneManager.IsPreviewScene(s.gameObject.scene)
                            && sceneFilter(s.gameObject.scene))
                .OrderBy(s => s.gameObject.scene.path)
                .ThenBy(PathOf)
                .ToList();

        /// <returns>Number of problems found (after fixes, if any).</returns>
        private static int Validate(List<SaveableObject> saveables, bool fix)
        {
            int problems = 0;

            foreach (var s in saveables)
            {
                var pid = s.GetComponent<PersistentID>();
                if (pid != null)
                {
                    if (pid.KilledFact == null || string.IsNullOrEmpty(pid.KilledFact.EntityGuid))
                    {
                        problems++;
                        GameLog.Error(TAG, $"'{PathOf(s)}' has a PersistentID without a KilledFact GUID — its state will not be saved.");
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(s.SaveId)) continue;
                if (fix)
                {
                    AssignNewId(s);
                    GameLog.Info(TAG, $"Assigned save ID to '{PathOf(s)}'.");
                }
                else
                {
                    problems++;
                    GameLog.Error(TAG, $"'{PathOf(s)}' has no save ID — run Tools/Save/Validate Save IDs.");
                }
            }

            foreach (var group in saveables.Where(s => !string.IsNullOrEmpty(s.SaveKey)).GroupBy(s => s.SaveKey))
            {
                var list = group.ToList();
                if (list.Count < 2) continue;

                problems++;
                GameLog.Error(TAG, $"Duplicate save key '{group.Key}' on: {string.Join(", ", list.Select(PathOf))}");

                // Only _saveId keys can be regenerated — entity keys come from KilledFact assets.
                var fixable = list.Skip(1).Where(s => s.GetComponent<PersistentID>() == null).ToList();
                if (!fix || fixable.Count == 0) continue;
                if (!EditorUtility.DisplayDialog("Duplicate save ID",
                        $"'{PathOf(list[0])}' shares its save ID with {fixable.Count} other object(s). " +
                        "Regenerate the IDs of the others?", "Regenerate", "Keep"))
                    continue;

                foreach (var s in fixable)
                {
                    AssignNewId(s);
                    GameLog.Info(TAG, $"Regenerated save ID on '{PathOf(s)}'.");
                }
                // Resolved only if every duplicate after the first could be regenerated (entity keys cannot).
                if (fixable.Count == list.Count - 1) problems--;
            }

            return problems;
        }

        private static string PathOf(SaveableObject s)
        {
            var t = s.transform;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return $"{s.gameObject.scene.name}:{path}";
        }
    }
}
