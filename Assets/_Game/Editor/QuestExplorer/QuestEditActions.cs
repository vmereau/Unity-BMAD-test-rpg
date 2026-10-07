using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.NPC;
using Game.Quest;
using Game.UI;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// Undo-aware mutations used by the Quest Explorer. <see cref="QuestPart"/> / <see cref="QuestStep"/>
    /// are structs, so every edit goes through <see cref="SerializedObject"/> — never through a copy.
    /// Each public operation is one named Undo group.
    /// </summary>
    public static class QuestEditActions
    {
        private const string TAG = "[QuestExplorer]";
        private const string UNDO_PREFIX = "Quest Explorer: ";
        public const string FACTS_FOLDER = "Assets/_Game/Data/Facts";
        public const string QUESTS_FOLDER = "Assets/_Game/Data/Quests";
        private const string ENEMIES_FOLDER = "Assets/_Game/Data/Enemies";
        private const string NEW_STEP_TITLE = "New step";
        private const int QUEST_STATE_STEP_OFFSET = 3;

        private static readonly char[] InvalidPathChars = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

        // ── Quest structure ───────────────────────────────────────────────────

        public static bool SetPartFact(QuestSO quest, QuestPartLocation loc, Fact fact) =>
            ModifyQuest(quest, "Set Part Fact", so =>
            {
                var part = PartProp(so, loc);
                if (part == null) return false;
                part.FindPropertyRelative("fact").objectReferenceValue = fact;
                return true;
            });

        public static bool SetPartEntry(QuestSO quest, QuestPartLocation loc, string entry) =>
            ModifyQuest(quest, "Edit Part Entry", so =>
            {
                var part = PartProp(so, loc);
                if (part == null) return false;
                part.FindPropertyRelative("entry").stringValue = entry ?? string.Empty;
                return true;
            });

        public static bool SetTitle(QuestSO quest, string title) =>
            ModifyQuest(quest, "Edit Title", so => SetString(so.FindProperty("title"), title));

        public static bool SetDescription(QuestSO quest, string description) =>
            ModifyQuest(quest, "Edit Description", so => SetString(so.FindProperty("description"), description));

        public static bool SetStepTitle(QuestSO quest, int stepIndex, string title) =>
            ModifyQuest(quest, "Edit Step Title", so => SetString(StepProp(so, stepIndex)?.FindPropertyRelative("title"), title));

        public static bool SetStepDescription(QuestSO quest, int stepIndex, string description) =>
            ModifyQuest(quest, "Edit Step Description", so => SetString(StepProp(so, stepIndex)?.FindPropertyRelative("description"), description));

        public static bool AddPart(QuestSO quest, QuestPartSlot slot, int stepIndex) =>
            ModifyQuest(quest, "Add Part", so =>
            {
                var list = PartsListProp(so, slot, stepIndex);
                if (list == null) return false;
                int i = list.arraySize;
                list.InsertArrayElementAtIndex(i);
                var part = list.GetArrayElementAtIndex(i);
                part.FindPropertyRelative("fact").objectReferenceValue = null;
                part.FindPropertyRelative("entry").stringValue = string.Empty;
                return true;
            });

        /// <summary>Removes a part; the start part is only cleared.</summary>
        public static bool RemovePart(QuestSO quest, QuestPartLocation loc) =>
            ModifyQuest(quest, loc.Slot == QuestPartSlot.Start ? "Clear Start Part" : "Remove Part", so =>
            {
                if (loc.Slot == QuestPartSlot.Start)
                {
                    var start = so.FindProperty("startPart");
                    start.FindPropertyRelative("fact").objectReferenceValue = null;
                    start.FindPropertyRelative("entry").stringValue = string.Empty;
                    return true;
                }
                var list = PartsListProp(so, loc.Slot, loc.StepIndex);
                if (list == null || loc.PartIndex < 0 || loc.PartIndex >= list.arraySize) return false;
                list.DeleteArrayElementAtIndex(loc.PartIndex);
                return true;
            });

        public static bool AddStep(QuestSO quest) =>
            ModifyQuest(quest, "Add Step", so =>
            {
                var steps = so.FindProperty("steps");
                int i = steps.arraySize;
                steps.InsertArrayElementAtIndex(i); // copies the previous element — reset every field
                var step = steps.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("title").stringValue = NEW_STEP_TITLE;
                step.FindPropertyRelative("description").stringValue = string.Empty;
                step.FindPropertyRelative("parts").arraySize = 0;
                return true;
            });

        public static bool RemoveStep(QuestSO quest, int stepIndex, QuestReferenceIndex index) =>
            ApplyStepOp(quest, new StepOp(StepOpKind.Remove, stepIndex), index, "Remove Step");

        public static bool MoveStep(QuestSO quest, int stepIndex, bool up, QuestReferenceIndex index) =>
            ApplyStepOp(quest, new StepOp(up ? StepOpKind.MoveUp : StepOpKind.MoveDown, stepIndex), index,
                up ? "Move Step Up" : "Move Step Down");

        /// <summary>
        /// Removes / moves a step and re-targets every step-state QuestFact so it still points at the
        /// same step. QuestFacts pointing at a removed step are listed in a confirmation dialog first.
        /// </summary>
        private static bool ApplyStepOp(QuestSO quest, StepOp op, QuestReferenceIndex index, string undoName)
        {
            if (quest == null || quest.steps == null || op.Index < 0 || op.Index >= quest.steps.Count) return false;
            if (op.Kind == StepOpKind.MoveUp && op.Index == 0) return false;
            if (op.Kind == StepOpKind.MoveDown && op.Index == quest.steps.Count - 1) return false;

            var map = QuestStepRemap.Compute(quest.steps.Count, op);
            var targeting = index != null ? index.GetQuestFactsTargeting(quest) : (IReadOnlyList<QuestFact>)Array.Empty<QuestFact>();
            var stepFacts = targeting.Where(qf => qf != null && qf.IsStepState && map.ContainsKey(qf.QuestStepIndex)).ToList();

            var orphaned = stepFacts.Where(qf => map[qf.QuestStepIndex] == null).ToList();
            if (orphaned.Count > 0)
            {
                string names = string.Join("\n", orphaned.Select(qf => "• " + qf.name));
                if (!EditorUtility.DisplayDialog("Remove step",
                        $"These QuestFacts point to the removed step and will become invalid:\n{names}\n\nRemove the step anyway?",
                        "Remove", "Cancel"))
                    return false;
            }

            InGroup(undoName, () =>
            {
                // QuestFacts first: QuestFact.OnValidate clamps _questState against the quest's step
                // count, so the facts must be recorded before the quest shrinks (undo replays in reverse).
                foreach (var qf in stepFacts)
                {
                    int oldIndex = qf.QuestStepIndex;
                    if (!(map[oldIndex] is int newIndex) || newIndex == oldIndex) continue;
                    using var factSo = new SerializedObject(qf);
                    factSo.FindProperty("_questState").intValue = newIndex + QUEST_STATE_STEP_OFFSET;
                    factSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(qf);
                }

                using var so = new SerializedObject(quest);
                var steps = so.FindProperty("steps");
                switch (op.Kind)
                {
                    case StepOpKind.Remove: steps.DeleteArrayElementAtIndex(op.Index); break;
                    case StepOpKind.MoveUp: steps.MoveArrayElement(op.Index, op.Index - 1); break;
                    case StepOpKind.MoveDown: steps.MoveArrayElement(op.Index, op.Index + 1); break;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(quest);
            });
            return true;
        }

        // ── Fact creation ─────────────────────────────────────────────────────

        /// <summary>
        /// Uses the selected scene GameObject's PersistentID KilledFact (creating and assigning one if
        /// missing) as the fact of <paramref name="loc"/>.
        /// </summary>
        public static bool AssignKilledFactFromSelection(QuestSO quest, QuestPartLocation loc)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("KilledFact from selection", "Not available in play mode.", "OK");
                return false;
            }

            var go = Selection.activeGameObject;
            PersistentID pid = null;
            // TryGetComponent: in the editor a missing GetComponent result is a fake null, which `??` would not catch.
            if (go != null && !EditorUtility.IsPersistent(go) && go.scene.IsValid() && !go.TryGetComponent(out pid))
                pid = go.GetComponentInParent<PersistentID>(true);
            if (pid == null)
            {
                EditorUtility.DisplayDialog("KilledFact from selection",
                    "Select a GameObject with a PersistentID in a loaded scene (not a prefab asset).", "OK");
                return false;
            }

            var fact = QuestIndexCollector.ReadKilledFact(pid);
            bool created = false;
            if (fact == null)
            {
                string folder = $"{ENEMIES_FOLDER}/{pid.gameObject.scene.name}/Generic";
                string path = $"{folder}/KilledFact_{Sanitize(pid.gameObject.name)}.asset";
                EnsureFolder(folder);
                fact = AssetDatabase.LoadAssetAtPath<KilledFact>(path);
                if (fact != null && IsBoundToOtherPersistentId(fact, pid))
                {
                    // Same GameObject name as an already-bound entity — never share a GUID.
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    fact = null;
                }
                if (fact == null)
                {
                    fact = ScriptableObject.CreateInstance<KilledFact>().Init(Guid.NewGuid().ToString());
                    AssetDatabase.CreateAsset(fact, path);
                    AssetDatabase.SaveAssets();
                    created = true;
                }
            }

            var assigned = fact;
            InGroup("KilledFact From Selection", () =>
            {
                using (var pidSo = new SerializedObject(pid))
                {
                    var prop = pidSo.FindProperty("_killedFact");
                    if (prop.objectReferenceValue != assigned)
                    {
                        prop.objectReferenceValue = assigned; // on a prefab instance this becomes an override
                        pidSo.ApplyModifiedProperties();
                        EditorSceneManager.MarkSceneDirty(pid.gameObject.scene);
                    }
                }
                SetPartFactNoGroup(quest, loc, assigned);
            });

            GameLog.Info(TAG, $"{(created ? "Created" : "Using")} '{assigned.name}' for {pid.gameObject.name} → {quest.name} {loc}");
            return true;
        }

        /// <summary>Prompts for a node id and assigns a (new or reused) DialogueFact to the part.</summary>
        public static bool CreateDialogueFact(QuestSO quest, QuestPartLocation loc)
        {
            if (!QuestExplorerNamePrompt.PromptText("New DialogueFact", "Node id (e.g. Guard_SpiderQuestAccepted)", out string id))
                return false;
            var fact = CreateOrReuse($"{FACTS_FOLDER}/DialogueFact_{id}.asset",
                () => ScriptableObject.CreateInstance<DialogueFact>().Init(id));
            return fact != null && SetPartFact(quest, loc, fact);
        }

        /// <summary>Prompts for an event key and assigns a (new or reused) WorldFact to the part.</summary>
        public static bool CreateWorldFact(QuestSO quest, QuestPartLocation loc)
        {
            if (!QuestExplorerNamePrompt.PromptText("New WorldFact", "Event key (e.g. herbalist_camp_found)", out string key))
                return false;
            var fact = CreateOrReuse($"{FACTS_FOLDER}/WorldFact_{key}.asset",
                () => ScriptableObject.CreateInstance<WorldFact>().Init(key));
            return fact != null && SetPartFact(quest, loc, fact);
        }

        /// <summary>Prompts for a quest + state and assigns a (new or reused) QuestFact to the part.</summary>
        public static bool CreateQuestFact(QuestSO quest, QuestPartLocation loc)
        {
            if (!QuestExplorerNamePrompt.PromptQuestState(quest, out QuestSO target, out int state)) return false;
            if (target == null || string.IsNullOrWhiteSpace(target.questId))
            {
                EditorUtility.DisplayDialog("New QuestFact", "The target quest needs a questId.", "OK");
                return false;
            }
            string path = $"{FACTS_FOLDER}/QuestFact_{Sanitize(target.questId)}_{StateFileSuffix(state)}.asset";
            var fact = CreateOrReuse(path, () => state >= QUEST_STATE_STEP_OFFSET
                ? ScriptableObject.CreateInstance<QuestFact>().InitStep(target, state - QUEST_STATE_STEP_OFFSET)
                : ScriptableObject.CreateInstance<QuestFact>().Init(target, (QuestState)state));
            return fact != null && SetPartFact(quest, loc, fact);
        }

        // ── Headless creation (no dialogs — scripts and Claude via MCP execute_code) ──

        /// <summary>
        /// Creates <c>Data/Quests/{questId}/Quest_{questId}.asset</c> with its id / title / description, or
        /// returns the existing one unchanged. The QuestEventsManager auto-sync registers it; this also adds
        /// it to QuestLogUI. Asset creation is not undoable.
        /// </summary>
        public static QuestSO CreateQuest(string questId, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;
            string id = Sanitize(questId);
            var quest = LoadOrCreate($"{QUESTS_FOLDER}/{id}/Quest_{id}.asset", () =>
            {
                var q = ScriptableObject.CreateInstance<QuestSO>();
                q.questId = id;
                q.title = title ?? string.Empty;
                q.description = description ?? string.Empty;
                return q;
            });
            if (quest != null) AddToQuestLog(quest);
            return quest;
        }

        /// <summary><c>Data/Facts/DialogueFact_{nodeId}.asset</c>, created if missing (no prompt).</summary>
        public static DialogueFact GetOrCreateDialogueFact(string nodeId) =>
            string.IsNullOrWhiteSpace(nodeId) ? null : LoadOrCreate($"{FACTS_FOLDER}/DialogueFact_{Sanitize(nodeId)}.asset",
                () => ScriptableObject.CreateInstance<DialogueFact>().Init(nodeId));

        /// <summary><c>Data/Facts/WorldFact_{eventKey}.asset</c>, created if missing (no prompt).</summary>
        public static WorldFact GetOrCreateWorldFact(string eventKey) =>
            string.IsNullOrWhiteSpace(eventKey) ? null : LoadOrCreate($"{FACTS_FOLDER}/WorldFact_{Sanitize(eventKey)}.asset",
                () => ScriptableObject.CreateInstance<WorldFact>().Init(eventKey));

        /// <summary>
        /// <c>Data/Facts/QuestFact_{questId}_{Started|Completed|Failed|Step{i}}.asset</c>, created if missing.
        /// <paramref name="stepIndex"/> ≥ 0 makes a step-state fact (0-based step); otherwise <paramref name="state"/> is used.
        /// </summary>
        public static QuestFact GetOrCreateQuestFact(QuestSO target, QuestState state, int stepIndex = -1)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.questId)) return null;
            int stateIndex = stepIndex >= 0 ? QUEST_STATE_STEP_OFFSET + stepIndex : (int)state;
            string path = $"{FACTS_FOLDER}/QuestFact_{Sanitize(target.questId)}_{StateFileSuffix(stateIndex)}.asset";
            return LoadOrCreate(path, () => stepIndex >= 0
                ? ScriptableObject.CreateInstance<QuestFact>().InitStep(target, stepIndex)
                : ScriptableObject.CreateInstance<QuestFact>().Init(target, state));
        }

        /// <summary>Popup labels for a QuestFact state index — same wording as QuestFactEditor.</summary>
        internal static string[] QuestStateLabels(QuestSO quest)
        {
            int stepCount = quest != null && quest.steps != null ? quest.steps.Count : 0;
            var labels = new string[QUEST_STATE_STEP_OFFSET + stepCount];
            labels[0] = "IsStarted";
            labels[1] = "IsCompleted";
            labels[2] = "IsFailed";
            for (int i = 0; i < stepCount; i++)
            {
                string title = quest.steps[i].title;
                labels[QUEST_STATE_STEP_OFFSET + i] = string.IsNullOrEmpty(title) ? $"Step {i} (no title)" : $"Step: {title}";
            }
            return labels;
        }

        private static string StateFileSuffix(int state) => state switch
        {
            0 => "Started",
            1 => "Completed",
            2 => "Failed",
            _ => $"Step{state - QUEST_STATE_STEP_OFFSET}"
        };

        // ── Registration ──────────────────────────────────────────────────────

        public static void SyncEventsManager() => QuestEventsManagerAutoSync.SyncQuestsToPrefab();

        public static bool AddToQuestLog(QuestSO quest)
        {
            if (quest == null) return false;
            var root = PrefabUtility.LoadPrefabContents(QuestIndexCollector.QUEST_LOG_PREFAB_PATH);
            if (root == null)
            {
                GameLog.Warn(TAG, $"QuestLogUI prefab not found at '{QuestIndexCollector.QUEST_LOG_PREFAB_PATH}'");
                return false;
            }
            try
            {
                var ui = root.GetComponentInChildren<QuestLogUI>(true);
                if (ui == null)
                {
                    GameLog.Warn(TAG, "QuestLogUI component not found in its prefab");
                    return false;
                }
                using var so = new SerializedObject(ui);
                var list = so.FindProperty("_allQuests");
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == quest) return true;
                int n = list.arraySize;
                list.InsertArrayElementAtIndex(n);
                list.GetArrayElementAtIndex(n).objectReferenceValue = quest;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, QuestIndexCollector.QUEST_LOG_PREFAB_PATH);
                GameLog.Info(TAG, $"Added '{quest.name}' to QuestLogUI._allQuests");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── NPC memory conditions ─────────────────────────────────────────────

        /// <summary>Appends a fact to a memory's condition list. False if null or already listed.</summary>
        public static bool AddMemoryCondition(NPCMemoryEntrySO memory, MemoryConditionList list, Fact fact) =>
            fact != null && Modify(memory, "Add Memory Condition", so =>
            {
                var array = ConditionsProp(so, list);
                if (array == null) return false;
                for (int i = 0; i < array.arraySize; i++)
                    if (array.GetArrayElementAtIndex(i).objectReferenceValue == fact) return false;
                int index = array.arraySize;
                array.InsertArrayElementAtIndex(index);
                array.GetArrayElementAtIndex(index).objectReferenceValue = fact;
                return true;
            });

        /// <summary>Replaces one condition (null allowed). False if the fact is already listed at another index.</summary>
        public static bool SetMemoryCondition(NPCMemoryEntrySO memory, MemoryConditionList list, int index, Fact fact) =>
            Modify(memory, "Set Memory Condition", so =>
            {
                var array = ConditionsProp(so, list);
                if (array == null || index < 0 || index >= array.arraySize) return false;
                if (fact != null)
                    for (int i = 0; i < array.arraySize; i++)
                        if (i != index && array.GetArrayElementAtIndex(i).objectReferenceValue == fact) return false;
                array.GetArrayElementAtIndex(index).objectReferenceValue = fact;
                return true;
            });

        public static bool RemoveMemoryCondition(NPCMemoryEntrySO memory, MemoryConditionList list, int index) =>
            Modify(memory, "Remove Memory Condition", so =>
            {
                var array = ConditionsProp(so, list);
                if (array == null || index < 0 || index >= array.arraySize) return false;
                int size = array.arraySize;
                array.DeleteArrayElementAtIndex(index);
                // Older Unity versions only null a non-null object reference on the first delete; harmless otherwise.
                if (array.arraySize == size) array.DeleteArrayElementAtIndex(index);
                return true;
            });

        private static SerializedProperty ConditionsProp(SerializedObject so, MemoryConditionList list) =>
            so.FindProperty(list == MemoryConditionList.Unlock ? "unlockConditions" : "invalidationConditions");

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Non-interactive: returns the asset at <paramref name="path"/>, or creates it. Null if the path holds another type.</summary>
        private static T LoadOrCreate<T>(string path, Func<T> factory) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                GameLog.Error(TAG, $"'{path}' exists but is not a {typeof(T).Name}");
                return null;
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var asset = factory();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            GameLog.Info(TAG, $"Created {path}");
            return asset;
        }

        private static T CreateOrReuse<T>(string path, Func<T> factory) where T : Fact
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Fact already exists", $"'{path}' already exists. Reuse it?", "Reuse", "Cancel"))
                    return null;
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                EditorUtility.DisplayDialog("Fact already exists", $"'{path}' exists but is not a {typeof(T).Name}.", "OK");
                return null;
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var fact = factory();
            AssetDatabase.CreateAsset(fact, path);
            AssetDatabase.SaveAssets();
            GameLog.Info(TAG, $"Created {path}");
            return fact;
        }

        private static bool ModifyQuest(QuestSO quest, string undoName, Func<SerializedObject, bool> edit) =>
            Modify(quest, undoName, edit);

        private static bool Modify(UnityEngine.Object target, string undoName, Func<SerializedObject, bool> edit)
        {
            if (target == null) return false;
            bool ok = false;
            InGroup(undoName, () =>
            {
                using var so = new SerializedObject(target);
                ok = edit(so);
                if (ok) so.ApplyModifiedProperties();
            });
            if (ok) EditorUtility.SetDirty(target);
            return ok;
        }

        private static bool IsBoundToOtherPersistentId(KilledFact fact, PersistentID self)
        {
            foreach (var other in UnityEngine.Object.FindObjectsByType<PersistentID>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != self && QuestIndexCollector.ReadKilledFact(other) == fact) return true;
            return false;
        }

        private static void SetPartFactNoGroup(QuestSO quest, QuestPartLocation loc, Fact fact)
        {
            using var so = new SerializedObject(quest);
            var part = PartProp(so, loc);
            if (part == null) return;
            part.FindPropertyRelative("fact").objectReferenceValue = fact;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(quest);
        }

        private static void InGroup(string name, Action body)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UNDO_PREFIX + name);
            int group = Undo.GetCurrentGroup();
            try { body(); }
            finally { Undo.CollapseUndoOperations(group); }
        }

        private static bool SetString(SerializedProperty prop, string value)
        {
            if (prop == null) return false;
            prop.stringValue = value ?? string.Empty;
            return true;
        }

        private static SerializedProperty StepProp(SerializedObject so, int stepIndex)
        {
            var steps = so.FindProperty("steps");
            return stepIndex >= 0 && stepIndex < steps.arraySize ? steps.GetArrayElementAtIndex(stepIndex) : null;
        }

        private static SerializedProperty PartsListProp(SerializedObject so, QuestPartSlot slot, int stepIndex) => slot switch
        {
            QuestPartSlot.Step => StepProp(so, stepIndex)?.FindPropertyRelative("parts"),
            QuestPartSlot.Completed => so.FindProperty("completedParts"),
            QuestPartSlot.Failed => so.FindProperty("failedParts"),
            _ => null
        };

        private static SerializedProperty PartProp(SerializedObject so, QuestPartLocation loc)
        {
            if (loc.Slot == QuestPartSlot.Start) return so.FindProperty("startPart");
            var list = PartsListProp(so, loc.Slot, loc.StepIndex);
            if (list == null || loc.PartIndex < 0 || loc.PartIndex >= list.arraySize) return null;
            return list.GetArrayElementAtIndex(loc.PartIndex);
        }

        internal static string Sanitize(string name)
        {
            name = (name ?? string.Empty).Trim();
            foreach (var c in InvalidPathChars) name = name.Replace(c, '_');
            return name;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
