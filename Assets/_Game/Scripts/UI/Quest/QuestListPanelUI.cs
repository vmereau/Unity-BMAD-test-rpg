using System;
using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    public class QuestListPanelUI : MonoBehaviour
    {
        private const string TAG = "[QuestListPanelUI]";

        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _questButtonPrefab;
        [SerializeField] private Button _tabStarted;
        [SerializeField] private Button _tabCompleted;
        [SerializeField] private Button _tabFailed;

        [Header("Tab labels (count appended)")]
        [SerializeField] private TMP_Text _tabStartedLabel;
        [SerializeField] private TMP_Text _tabCompletedLabel;
        [SerializeField] private TMP_Text _tabFailedLabel;
        [SerializeField] private string _startedLabel   = "Started";
        [SerializeField] private string _completedLabel = "Completed";
        [SerializeField] private string _failedLabel    = "Failed";

        /// <summary>Raised after every refresh / tab switch / click with the selected quest (null = none visible).</summary>
        public event Action<QuestSO> SelectionChanged;

        private List<QuestSO> _allQuests;
        private QuestTab _activeTab = QuestTab.Started;
        private QuestSO _selected;
        private readonly List<QuestButtonUI> _buttons = new List<QuestButtonUI>();

        private UnityAction _onTabStarted;
        private UnityAction _onTabCompleted;
        private UnityAction _onTabFailed;

        private void Awake()
        {
            _onTabStarted   = () => SwitchTab(QuestTab.Started);
            _onTabCompleted = () => SwitchTab(QuestTab.Completed);
            _onTabFailed    = () => SwitchTab(QuestTab.Failed);
        }

        private void OnEnable()
        {
            if (_tabStarted   != null) _tabStarted.onClick.AddListener(_onTabStarted);
            if (_tabCompleted != null) _tabCompleted.onClick.AddListener(_onTabCompleted);
            if (_tabFailed    != null) _tabFailed.onClick.AddListener(_onTabFailed);
        }

        private void OnDisable()
        {
            if (_tabStarted   != null) _tabStarted.onClick.RemoveListener(_onTabStarted);
            if (_tabCompleted != null) _tabCompleted.onClick.RemoveListener(_onTabCompleted);
            if (_tabFailed    != null) _tabFailed.onClick.RemoveListener(_onTabFailed);
        }

        public void Refresh(List<QuestSO> allQuests)
        {
            _allQuests = allQuests;
            RefreshButtons();
            UpdateTabStyles();
            UpdateTabCounts();
            EnsureSelection();
        }

        /// <summary>Selects <paramref name="quest"/> if it is in the visible list; ignored otherwise.</summary>
        public void Select(QuestSO quest)
        {
            if (quest == null || quest == _selected || _buttons.Find(b => b.Quest == quest) == null) return;
            _selected = quest;
            ApplySelection();
        }

        private void SwitchTab(QuestTab tab)
        {
            _activeTab = tab;
            RefreshButtons();
            UpdateTabStyles();
            EnsureSelection();
        }

        // Keep the selected quest if still visible, otherwise fall back to the first row (or none)
        private void EnsureSelection()
        {
            if (_selected == null || _buttons.Find(b => b.Quest == _selected) == null)
                _selected = _buttons.Count > 0 ? _buttons[0].Quest : null;
            ApplySelection();
        }

        private void ApplySelection()
        {
            foreach (var btn in _buttons)
                btn.SetSelected(btn.Quest == _selected);
            SelectionChanged?.Invoke(_selected);
        }

        private void RefreshButtons()
        {
            _buttons.Clear();
            if (_contentRoot == null)       { GameLog.Warn(TAG, "_contentRoot is not assigned."); return; }
            if (_questButtonPrefab == null) { GameLog.Warn(TAG, "_questButtonPrefab is not assigned."); return; }

            foreach (Transform child in _contentRoot)
            {
                child.gameObject.SetActive(false); // out of the layout now; Destroy runs at end of frame
                Destroy(child.gameObject);
            }

            if (_allQuests == null) return;

            foreach (var quest in _allQuests)
            {
                if (!IsInTab(quest, _activeTab)) continue;

                var go  = Instantiate(_questButtonPrefab, _contentRoot);
                var btn = go.GetComponent<QuestButtonUI>();
                if (btn == null)
                {
                    GameLog.Error(TAG, $"_questButtonPrefab is missing QuestButtonUI component on '{go.name}'.");
                    Destroy(go);
                    continue;
                }
                if (!btn.Bind(quest)) { Destroy(go); continue; }
                _buttons.Add(btn);
            }
        }

        private static bool IsInTab(QuestSO quest, QuestTab tab)
        {
            if (quest == null) return false;
            return tab switch
            {
                QuestTab.Started   => quest.IsStarted && !quest.IsCompleted && !quest.IsFailed,
                QuestTab.Completed => quest.IsCompleted,
                QuestTab.Failed    => quest.IsFailed,
                _                  => false
            };
        }

        private void UpdateTabCounts()
        {
            int started = 0, completed = 0, failed = 0;
            if (_allQuests != null)
            {
                foreach (var quest in _allQuests)
                {
                    if (IsInTab(quest, QuestTab.Started))   started++;
                    if (IsInTab(quest, QuestTab.Completed)) completed++;
                    if (IsInTab(quest, QuestTab.Failed))    failed++;
                }
            }
            if (_tabStartedLabel   != null) _tabStartedLabel.text   = $"{_startedLabel} ({started})";
            if (_tabCompletedLabel != null) _tabCompletedLabel.text = $"{_completedLabel} ({completed})";
            if (_tabFailedLabel    != null) _tabFailedLabel.text    = $"{_failedLabel} ({failed})";
        }

        private void UpdateTabStyles()
        {
            if (_tabStarted   != null) _tabStarted.interactable   = _activeTab != QuestTab.Started;
            if (_tabCompleted != null) _tabCompleted.interactable = _activeTab != QuestTab.Completed;
            if (_tabFailed    != null) _tabFailed.interactable    = _activeTab != QuestTab.Failed;
        }
    }
}
