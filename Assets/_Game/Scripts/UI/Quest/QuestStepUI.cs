using System;
using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One block of the quest info panel. Either a collapsible step (header button toggles the body)
    /// or a fixed section ("Quest accepted" / outcome) with a tinted label and no toggle.
    /// </summary>
    public class QuestStepUI : MonoBehaviour
    {
        private const string TAG = "[QuestStepUI]";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Transform _partsRoot;
        [SerializeField] private GameObject _questPartPrefab;

        [Header("Layout")]
        [SerializeField] private Button _headerButton;
        [SerializeField] private Image _marker;
        [SerializeField] private TMP_Text _expandHintText;
        [SerializeField] private Image _accentBar;
        [SerializeField] private GameObject _body;
        [SerializeField] private GameObject _topDivider;

        [Header("Colors")]
        [SerializeField] private Color _inProgressTitleColor  = new(0.847f, 0.800f, 0.690f);     // #D8CCB0
        [SerializeField] private Color _completedTitleColor   = new(0.502f, 0.486f, 0.455f);     // #807C74
        [SerializeField] private Color _inProgressMarkerColor = new(0.549f, 0.451f, 0.263f);     // #8C7343
        [SerializeField] private Color _completedMarkerColor  = new(0.478f, 0.529f, 0.357f);     // #5A6346 lightened

        private bool _expanded;
        private Action<bool> _onToggle;
        private UnityEngine.Events.UnityAction _toggleAction;

        private void Awake()
        {
            _toggleAction = Toggle;
            if (_headerButton != null) _headerButton.onClick.AddListener(_toggleAction);
        }

        private void OnDestroy()
        {
            if (_headerButton != null) _headerButton.onClick.RemoveListener(_toggleAction);
        }

        /// <summary>Collapsible step. <paramref name="onToggle"/> receives the new expanded state after a click.</summary>
        public void BindStep(string title, string description, List<QuestPart> activeParts,
                             bool completed, bool expanded, Action<bool> onToggle)
        {
            if (!Populate(title, description, activeParts)) return;

            _titleText.color = completed ? _completedTitleColor : _inProgressTitleColor;
            if (_marker != null)
            {
                _marker.gameObject.SetActive(true);
                _marker.color = completed ? _completedMarkerColor : _inProgressMarkerColor;
            }
            if (_accentBar != null)   _accentBar.gameObject.SetActive(!completed);
            if (_topDivider != null)  _topDivider.SetActive(false);
            if (_headerButton != null) _headerButton.interactable = true;
            if (_expandHintText != null) _expandHintText.gameObject.SetActive(true);

            _onToggle = onToggle;
            SetExpanded(expanded);
        }

        /// <summary>Fixed section: tinted label, always expanded, no marker / toggle.</summary>
        public void BindSection(string label, Color labelColor, List<QuestPart> activeParts, bool showDivider)
        {
            if (!Populate(label, null, activeParts)) return;

            _titleText.color = labelColor;
            if (_marker != null)         _marker.gameObject.SetActive(false);
            if (_accentBar != null)      _accentBar.gameObject.SetActive(false);
            if (_topDivider != null)     _topDivider.SetActive(showDivider);
            if (_headerButton != null)   _headerButton.interactable = false;
            if (_expandHintText != null) _expandHintText.gameObject.SetActive(false);

            _onToggle = null;
            SetExpanded(true);
        }

        private bool Populate(string title, string description, List<QuestPart> activeParts)
        {
            activeParts ??= new List<QuestPart>();
            if (_titleText == null)       { GameLog.Warn(TAG, "_titleText is not assigned."); return false; }
            if (_partsRoot == null)       { GameLog.Warn(TAG, "_partsRoot is not assigned."); return false; }
            if (_questPartPrefab == null) { GameLog.Warn(TAG, "_questPartPrefab is not assigned."); return false; }

            _titleText.text = title;

            if (_descriptionText != null)
            {
                bool hasDescription = !string.IsNullOrEmpty(description);
                _descriptionText.gameObject.SetActive(hasDescription);
                if (hasDescription) _descriptionText.text = description;
            }

            foreach (Transform child in _partsRoot)
            {
                child.gameObject.SetActive(false); // out of the layout now; Destroy runs at end of frame
                Destroy(child.gameObject);
            }

            int spawned = 0;
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
                spawned++;
            }
            // No blank gap when every entry is empty (e.g. kill-count parts without text)
            _partsRoot.gameObject.SetActive(spawned > 0);
            return true;
        }

        private void Toggle()
        {
            SetExpanded(!_expanded);
            _onToggle?.Invoke(_expanded);
        }

        private void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_body != null) _body.SetActive(expanded);
            if (_expandHintText != null) _expandHintText.text = expanded ? "-" : "+";

            if (transform.parent is RectTransform parentRect)
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }
    }
}
