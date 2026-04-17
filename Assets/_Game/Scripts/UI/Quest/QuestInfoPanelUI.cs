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
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _questStepPrefab;
        [SerializeField] private GameObject _emptyState;

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

            _emptyState?.SetActive(false);
            _titleText.gameObject.SetActive(true);
            _descriptionText.gameObject.SetActive(true);
            _contentRoot.gameObject.SetActive(true);

            _titleText.text       = quest.title;
            _descriptionText.text = quest.description;
            BuildContent(quest);

            GameLog.Info(TAG, $"Showing quest: {quest.title}");
        }

        public void Hide()
        {
            _emptyState?.SetActive(true);
            if (_titleText != null)       _titleText.gameObject.SetActive(false);
            if (_descriptionText != null) _descriptionText.gameObject.SetActive(false);
            if (_contentRoot != null)     _contentRoot.gameObject.SetActive(false);
        }

        private void BuildContent(QuestSO quest)
        {
            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            // ── Start part ────────────────────────────────────────────────────────
            if (quest.IsStarted
                && quest.startPart.fact != null
                && !string.IsNullOrEmpty(quest.startPart.entry))
            {
                SpawnStep("Start", null, new List<QuestPart> { quest.startPart });
            }

            // ── Numbered active steps ─────────────────────────────────────────────
            if (quest.steps != null)
            {
                int displayNumber = 1;
                foreach (var step in quest.steps)
                {
                    var activeParts = QuestSO.GetActiveParts(step.parts);
                    if (activeParts.Count == 0) continue;
                    SpawnStep($"{displayNumber}. {step.title}", step.description, activeParts);
                    displayNumber++;
                }
            }

            // ── Completion / failure footer ────────────────────────────────────────
            if (quest.IsCompleted)
            {
                var parts = QuestSO.GetActiveParts(quest.completedParts);
                if (parts.Count > 0) SpawnStep("Completed", null, parts);
            }
            else if (quest.IsFailed)
            {
                var parts = QuestSO.GetActiveParts(quest.failedParts);
                if (parts.Count > 0) SpawnStep("Failed", null, parts);
            }
        }

        private void SpawnStep(string title, string description, List<QuestPart> activeParts)
        {
            var go = Instantiate(_questStepPrefab, _contentRoot);
            var stepUI = go.GetComponent<QuestStepUI>();
            if (stepUI == null)
            {
                GameLog.Error(TAG, $"_questStepPrefab is missing QuestStepUI component on '{go.name}'.");
                Destroy(go);
                return;
            }
            stepUI.Bind(title, description, activeParts);
        }
    }
}
