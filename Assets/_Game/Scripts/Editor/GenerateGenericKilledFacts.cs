#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.World;

namespace Game.Editor
{
    public static class GenerateGenericKilledFacts
    {
        private const string TAG = "[GenerateKilledFacts]";
        private static readonly char[] InvalidPathChars = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

        [MenuItem("Game/World/Generate Missing KilledFacts")]
        public static void Run()
        {
            var activeScene = SceneManager.GetActiveScene();
            var sceneName = activeScene.name;

            var folderPath = $"Assets/_Game/Data/Enemies/{sceneName}/Generic";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                var absolutePath = Path.Combine(Application.dataPath, "..", folderPath);
                Directory.CreateDirectory(absolutePath);
                AssetDatabase.Refresh();
            }

            var persistentIDs = Object.FindObjectsOfType<PersistentID>(true);

            int created = 0, reused = 0, skipped = 0;

            foreach (var pid in persistentIDs)
            {
                var so = new SerializedObject(pid);
                var prop = so.FindProperty("_killedFact");

                if (prop.objectReferenceValue != null)
                {
                    skipped++;
                    continue;
                }

                var sanitizedName = SanitizeName(pid.gameObject.name);
                var assetPath = $"{folderPath}/KilledFact_{sanitizedName}.asset";

                var fact = AssetDatabase.LoadAssetAtPath<KilledFact>(assetPath);
                if (fact != null)
                {
                    reused++;
                }
                else
                {
                    fact = ScriptableObject.CreateInstance<KilledFact>();
                    fact.Init(System.Guid.NewGuid().ToString());
                    AssetDatabase.CreateAsset(fact, assetPath);
                    created++;
                }

                prop.objectReferenceValue = fact;
                so.ApplyModifiedProperties();

                var enemyTypeProp = so.FindProperty("_enemyType");
                if (enemyTypeProp != null)
                {
                    var factSO = new SerializedObject(fact);
                    var factEnemyProp = factSO.FindProperty("_enemyType");
                    if (factEnemyProp != null)
                    {
                        factEnemyProp.objectReferenceValue = enemyTypeProp.objectReferenceValue;
                        factSO.ApplyModifiedProperties();
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);

            if (created == 0 && reused == 0)
                GameLog.Info(TAG, "No unassigned PersistentIDs found — nothing to do.");
            else
                GameLog.Info(TAG, $"Done — {created} created, {reused} reused, {skipped} skipped (already assigned).");
        }

        private static string SanitizeName(string name)
        {
            foreach (var c in InvalidPathChars)
                name = name.Replace(c, '_');
            return name;
        }
    }
}
#endif
