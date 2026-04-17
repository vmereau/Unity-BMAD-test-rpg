using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class QuestStepUI : MonoBehaviour
    {
        private const string TAG = "[QuestStepUI]";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Transform _partsRoot;
        [SerializeField] private GameObject _questPartPrefab;

        public void Bind(string title, string description, List<QuestPart> activeParts)
        {
            activeParts ??= new List<QuestPart>();
            if (_titleText == null)       { GameLog.Warn(TAG, "_titleText is not assigned."); return; }
            if (_partsRoot == null)       { GameLog.Warn(TAG, "_partsRoot is not assigned."); return; }
            if (_questPartPrefab == null) { GameLog.Warn(TAG, "_questPartPrefab is not assigned."); return; }

            _titleText.text = title;

            if (_descriptionText != null)
            {
                if (string.IsNullOrEmpty(description))
                {
                    _descriptionText.gameObject.SetActive(false);
                }
                else
                {
                    _descriptionText.gameObject.SetActive(true);
                    _descriptionText.text = description;
                }
            }

            foreach (Transform child in _partsRoot)
                Destroy(child.gameObject);

            foreach (var part in activeParts)
            {
                if (string.IsNullOrEmpty(part.entry)) continue;
                var go = Instantiate(_questPartPrefab, _partsRoot);
                var partUI = go.GetComponent<QuestPartUI>();
                if (partUI == null)
                {
                    GameLog.Error(TAG, $"_questPartPrefab is missing QuestPartUI component on '{go.name}'.");
                    Destroy(go);
                    continue;
                }
                partUI.Bind(part);
            }
        }
    }
}
