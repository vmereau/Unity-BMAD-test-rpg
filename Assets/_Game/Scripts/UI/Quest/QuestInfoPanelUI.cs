using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class QuestInfoPanelUI : MonoBehaviour
    {
        private const string TAG = "[QuestInfoPanel]";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private GameObject _headerDivider;
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _questStepPrefab;
        [SerializeField] private GameObject _emptyState;

        [Header("Labels")]
        [SerializeField] private string _inProgressLabel = "In progress";
        [SerializeField] private string _completedLabel  = "Completed";
        [SerializeField] private string _failedLabel     = "Failed";
        [SerializeField] private string _acceptedSectionLabel  = "Quest accepted";
        [SerializeField] private string _completedSectionLabel = "Quest completed";
        [SerializeField] private string _failedSectionLabel    = "Quest failed";

        [Header("Colors")]
        [SerializeField] private Color _inProgressColor = new(0.722f, 0.663f, 0.533f); // #B8A988
        [SerializeField] private Color _completedColor  = new(0.478f, 0.529f, 0.357f); // #5A6346 lightened
        [SerializeField] private Color _failedColor     = new(0.651f, 0.290f, 0.243f); // #7A2A22 lightened

        private QuestSO _shownQuest;
        // Expand state the player picked per step index, with the completion state it was picked in —
        // dropped when the step completes / regresses so it falls back to the new default (reset per quest)
        private readonly Dictionary<int, (bool completed, bool expanded)> _stepOverrides =
            new Dictionary<int, (bool completed, bool expanded)>();

        public void Show(QuestSO quest)
        {
            if (quest == null)
            {
                GameLog.Warn(TAG, "Show called with null quest — hiding panel.");
                Hide();
                return;
            }
            if (_titleText == null || _descriptionText == null || _contentRoot == null || _questStepPrefab == null)
            {
                GameLog.Warn(TAG, "One or more required fields are not assigned — cannot show quest info.");
                return;
            }

            bool questChanged = quest != _shownQuest;
            if (questChanged) _stepOverrides.Clear();
            _shownQuest = quest;

            SetDetailsVisible(true);

            _titleText.text       = quest.title;
            _descriptionText.text = quest.description;
            if (_statusText != null)
            {
                (_statusText.text, _statusText.color) =
                    quest.IsCompleted ? (_completedLabel, _completedColor)
                    : quest.IsFailed  ? (_failedLabel, _failedColor)
                    : (_inProgressLabel, _inProgressColor);
            }
            BuildContent(quest);

            if (questChanged)
            {
                // Content is top-anchored: zero = scrolled to the top
                if (_contentRoot is RectTransform contentRect) contentRect.anchoredPosition = Vector2.zero;
                GameLog.Info(TAG, $"Showing quest: {quest.title}");
            }
        }

        public void Hide()
        {
            _shownQuest = null;
            _stepOverrides.Clear();
            SetDetailsVisible(false);
        }

        private void SetDetailsVisible(bool visible)
        {
            _emptyState?.SetActive(!visible);
            if (_titleText != null)       _titleText.gameObject.SetActive(visible);
            if (_statusText != null)      _statusText.gameObject.SetActive(visible);
            if (_descriptionText != null) _descriptionText.gameObject.SetActive(visible);
            if (_headerDivider != null)   _headerDivider.SetActive(visible);
            if (_contentRoot != null)     _contentRoot.gameObject.SetActive(visible);
        }

        private void BuildContent(QuestSO quest)
        {
            foreach (Transform child in _contentRoot)
            {
                child.gameObject.SetActive(false); // out of the layout now; Destroy runs at end of frame
                Destroy(child.gameObject);
            }

            // ── Accepted section ──────────────────────────────────────────────────
            if (quest.IsStarted
                && quest.startPart.fact != null
                && !string.IsNullOrEmpty(quest.startPart.entry))
            {
                SpawnBlock()?.BindSection(_acceptedSectionLabel, _inProgressColor,
                    new List<QuestPart> { quest.startPart }, showDivider: false);
            }

            // ── Steps: completed collapse, in-progress expand, unless the player chose otherwise ──
            if (quest.steps != null)
            {
                for (int i = 0; i < quest.steps.Count; i++)
                {
                    var step = quest.steps[i];
                    var activeParts = QuestSO.GetActiveParts(step.parts);
                    if (activeParts.Count == 0) continue;

                    bool completed = quest.IsStepCompleted(i);
                    bool expanded  = !completed;
                    if (_stepOverrides.TryGetValue(i, out var choice))
                    {
                        if (choice.completed == completed) expanded = choice.expanded;
                        else _stepOverrides.Remove(i);
                    }
                    int stepIndex = i;
                    SpawnBlock()?.BindStep(step.title, step.description, activeParts, completed, expanded,
                        isExpanded => _stepOverrides[stepIndex] = (completed, isExpanded));
                }
            }

            // ── Outcome section ───────────────────────────────────────────────────
            if (quest.IsCompleted)
            {
                var parts = QuestSO.GetActiveParts(quest.completedParts);
                if (parts.Count > 0)
                    SpawnBlock()?.BindSection(_completedSectionLabel, _completedColor, parts, showDivider: true);
            }
            else if (quest.IsFailed)
            {
                var parts = QuestSO.GetActiveParts(quest.failedParts);
                if (parts.Count > 0)
                    SpawnBlock()?.BindSection(_failedSectionLabel, _failedColor, parts, showDivider: true);
            }
        }

        private QuestStepUI SpawnBlock()
        {
            var go = Instantiate(_questStepPrefab, _contentRoot);
            var stepUI = go.GetComponent<QuestStepUI>();
            if (stepUI == null)
            {
                GameLog.Error(TAG, $"_questStepPrefab is missing QuestStepUI component on '{go.name}'.");
                Destroy(go);
            }
            return stepUI;
        }
    }
}
