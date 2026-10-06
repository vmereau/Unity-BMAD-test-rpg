using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.Progression;
using Game.Quest;
using Game.UI;
using Game.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// Editor I/O half of the index: gathers <see cref="IndexSources"/> from the AssetDatabase, loaded
    /// scenes, prefabs and a text scan of closed scenes. All private fields are read through
    /// <see cref="SerializedObject"/>.
    /// </summary>
    public static class QuestIndexCollector
    {
        private const string TAG = "[QuestExplorer]";
        public const string PREFABS_FOLDER = "Assets/_Game/Prefabs";
        public const string SCENES_FOLDER = "Assets/_Game/Scenes";
        public const string QUEST_EVENTS_PREFAB_PATH = "Assets/_Game/Prefabs/QuestEventsManager.prefab";
        public const string QUEST_LOG_PREFAB_PATH = "Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab";

        private static readonly Regex GuidRegex = new Regex("guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly HashSet<string> s_warnedPrefabs = new HashSet<string>();

        public static QuestReferenceIndex BuildIndex() => QuestReferenceIndex.Build(Collect());

        public static IndexSources Collect()
        {
            var s = new IndexSources
            {
                Quests = LoadAll<QuestSO>("t:QuestSO"),
                Npcs = LoadAll<NPCEntity>("t:NPCEntity"),
                Memories = LoadAll<NPCMemoryEntrySO>("t:NPCMemoryEntrySO"),
                DialogueNodes = LoadAll<DialogueNode>("t:DialogueNode"),
                Rewards = LoadAll<PlayerRewardSO>("t:PlayerRewardSO"),
                AllFacts = LoadAll<Fact>("t:Fact")
            };
            s.QuestFacts = s.AllFacts.OfType<QuestFact>().ToArray();

            var loadedScenePaths = CollectLoadedScenes(s.KilledBindings);
            CollectPrefabs(s.KilledBindings);
            CollectClosedScenes(s, loadedScenePaths);

            s.EventsManagerQuests = ReadQuestList<QuestEventsManager>(QUEST_EVENTS_PREFAB_PATH, "_quests");
            s.QuestLogQuests = ReadQuestList<QuestLogUI>(QUEST_LOG_PREFAB_PATH, "_allQuests");
            return s;
        }

        /// <summary>Reads PersistentID._killedFact (private) — shared with the window's context menu.</summary>
        public static KilledFact ReadKilledFact(PersistentID pid)
        {
            if (pid == null) return null;
            using var so = new SerializedObject(pid);
            return so.FindProperty("_killedFact")?.objectReferenceValue as KilledFact;
        }

        private static T[] LoadAll<T>(string filter) where T : UnityEngine.Object
        {
            var result = new List<T>();
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                if (!seen.Add(guid)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }
            return result.ToArray();
        }

        private static HashSet<string> CollectLoadedScenes(List<KilledFactBinding> bindings)
        {
            var paths = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (!string.IsNullOrEmpty(scene.path)) paths.Add(scene.path);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var pid in root.GetComponentsInChildren<PersistentID>(true))
                    {
                        bindings.Add(new KilledFactBinding
                        {
                            Fact = ReadKilledFact(pid),
                            Entity = pid.Entity,
                            GameObject = pid.gameObject,
                            Component = pid,
                            ScenePath = scene.path,
                            IsPrefab = false
                        });
                    }
                }
            }
            return paths;
        }

        private static void CollectPrefabs(List<KilledFactBinding> bindings)
        {
            if (!AssetDatabase.IsValidFolder(PREFABS_FOLDER)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PREFABS_FOLDER }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var pid in prefab.GetComponentsInChildren<PersistentID>(true))
                {
                    var fact = ReadKilledFact(pid);
                    if (fact == null) continue; // unbound prefab templates are normal (scene instances override)
                    bindings.Add(new KilledFactBinding
                    {
                        Fact = fact,
                        Entity = pid.Entity,
                        GameObject = pid.gameObject,
                        Component = pid,
                        ScenePath = path,
                        IsPrefab = true
                    });
                }
            }
        }

        private static void CollectClosedScenes(IndexSources s, HashSet<string> loadedScenePaths)
        {
            if (!AssetDatabase.IsValidFolder(SCENES_FOLDER)) return;

            var killedByGuid = new Dictionary<string, KilledFact>();
            foreach (var fact in s.AllFacts.OfType<KilledFact>())
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(fact));
                if (!string.IsNullOrEmpty(guid)) killedByGuid[guid] = fact;
            }
            if (killedByGuid.Count == 0) return;

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { SCENES_FOLDER }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (loadedScenePaths.Contains(path)) continue;
                try
                {
                    string text = File.ReadAllText(path);
                    var found = new HashSet<KilledFact>();
                    foreach (Match m in GuidRegex.Matches(text))
                    {
                        if (killedByGuid.TryGetValue(m.Groups[1].Value, out var fact) && found.Add(fact))
                            s.ClosedSceneRefs.Add((fact, path));
                    }
                }
                catch (Exception e)
                {
                    GameLog.Warn(TAG, $"Could not scan closed scene '{path}': {e.Message}");
                }
            }
        }

        private static HashSet<QuestSO> ReadQuestList<T>(string prefabPath, string propertyName) where T : Component
        {
            var set = new HashSet<QuestSO>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var component = prefab != null ? prefab.GetComponentInChildren<T>(true) : null;
            if (component == null)
            {
                if (s_warnedPrefabs.Add(prefabPath))
                    GameLog.Warn(TAG, $"{typeof(T).Name} not found at '{prefabPath}' — registration checks will report every quest as missing");
                return set;
            }

            using var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null || !prop.isArray) return set;
            for (int i = 0; i < prop.arraySize; i++)
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue is QuestSO quest)
                    set.Add(quest);
            return set;
        }
    }
}
