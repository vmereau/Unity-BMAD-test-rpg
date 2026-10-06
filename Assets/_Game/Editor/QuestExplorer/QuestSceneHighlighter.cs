using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// Scene-view overlay for the selected quest: a ring + label on every scene object linked to one
    /// of its parts (KilledFact PersistentIDs, NPCs that set its DialogueFacts). Subscribed to
    /// <see cref="SceneView.duringSceneGui"/> only while enabled.
    /// </summary>
    public static class QuestSceneHighlighter
    {
        private const float RING_RADIUS = 1f;
        private const float LABEL_HEIGHT = 2.2f;
        private const float LABEL_STACK_OFFSET = 0.4f;
        private const float INACTIVE_ALPHA = 0.5f;

        private static readonly Color EditColor = Color.cyan;
        private static readonly Color TrueColor = Color.green;
        private static readonly Color FalseColor = Color.yellow;

        private sealed class Target
        {
            public GameObject GameObject;
            public readonly List<(string label, Fact fact)> Labels = new List<(string label, Fact fact)>();
        }

        private static readonly List<Target> s_targets = new List<Target>();
        private static bool s_enabled;
        private static GUIStyle s_labelStyle;
        private static Texture2D s_labelBackground;

        public static bool IsEnabled => s_enabled;

        public static void Enable(QuestReferenceIndex index, QuestSO quest)
        {
            BuildTargets(index, quest);
            if (!s_enabled)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                s_enabled = true;
            }
            SceneView.RepaintAll();
        }

        public static void Disable()
        {
            s_targets.Clear();
            if (s_enabled)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                s_enabled = false;
            }
            SceneView.RepaintAll();
        }

        private static void BuildTargets(QuestReferenceIndex index, QuestSO quest)
        {
            s_targets.Clear();
            if (index == null || quest == null) return;

            var byObject = new Dictionary<GameObject, Target>();
            string questId = string.IsNullOrEmpty(quest.questId) ? quest.name : quest.questId;

            foreach (var (loc, part) in index.GetParts(quest))
            {
                if (part.fact == null) continue;
                string label = $"{questId} · {loc} · {part.fact.name}";

                foreach (var setter in index.GetSetters(part.fact))
                {
                    if (setter.Source == FactLinkSource.ScenePersistentID && setter.Owner is Component c && c != null)
                        AddTarget(byObject, c.gameObject, label, part.fact);
                    else if (setter.Npc != null)
                        foreach (var go in index.GetSceneObjectsForNpc(setter.Npc))
                            AddTarget(byObject, go, label, part.fact);
                }
            }
        }

        private static void AddTarget(Dictionary<GameObject, Target> byObject, GameObject go, string label, Fact fact)
        {
            if (go == null) return;
            if (!byObject.TryGetValue(go, out var target))
            {
                target = new Target { GameObject = go };
                byObject[go] = target;
                s_targets.Add(target);
            }
            foreach (var existing in target.Labels)
                if (existing.label == label) return;
            target.Labels.Add((label, fact));
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyle();
            bool playing = EditorApplication.isPlaying && WorldStateManager.Instance != null;

            foreach (var target in s_targets)
            {
                if (target.GameObject == null) continue; // destroyed / unloaded
                Vector3 pos = target.GameObject.transform.position;
                float alpha = target.GameObject.activeInHierarchy ? 1f : INACTIVE_ALPHA;

                bool allTrue = true;
                if (playing)
                    foreach (var (_, fact) in target.Labels)
                        allTrue &= WorldStateManager.Instance.GetFact(fact);

                Handles.color = WithAlpha(playing ? (allTrue ? TrueColor : FalseColor) : EditColor, alpha);
                Handles.DrawWireDisc(pos, Vector3.up, RING_RADIUS);

                for (int i = 0; i < target.Labels.Count; i++)
                {
                    var (label, fact) = target.Labels[i];
                    Color color = playing ? (WorldStateManager.Instance.GetFact(fact) ? TrueColor : FalseColor) : Color.white;
                    s_labelStyle.normal.textColor = WithAlpha(color, alpha);
                    Handles.Label(pos + Vector3.up * (LABEL_HEIGHT + i * LABEL_STACK_OFFSET), label, s_labelStyle);
                }
            }
        }

        private static void EnsureStyle()
        {
            if (s_labelStyle != null && s_labelBackground != null) return;
            s_labelBackground = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            s_labelBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
            s_labelBackground.Apply();
            s_labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                padding = new RectOffset(4, 4, 2, 2),
                normal = { background = s_labelBackground, textColor = Color.white }
            };
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);
    }
}
