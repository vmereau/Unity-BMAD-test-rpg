using System.Collections.Generic;
using Game.Core;
using Game.Quest;
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

        private List<QuestSO> _allQuests;
        private QuestTab _activeTab = QuestTab.Started;

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
        }

        private void SwitchTab(QuestTab tab)
        {
            _activeTab = tab;
            RefreshButtons();
            UpdateTabStyles();
        }

        private void RefreshButtons()
        {
            if (_contentRoot == null)       { GameLog.Warn(TAG, "_contentRoot is not assigned."); return; }
            if (_questButtonPrefab == null) { GameLog.Warn(TAG, "_questButtonPrefab is not assigned."); return; }

            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            if (_allQuests == null) return;

            foreach (var quest in _allQuests)
            {
                if (quest == null) continue;
                bool show = _activeTab switch
                {
                    QuestTab.Started   => quest.IsStarted,
                    QuestTab.Completed => quest.IsCompleted,
                    QuestTab.Failed    => quest.IsFailed,
                    _                  => false
                };
                if (!show) continue;

                var go  = Instantiate(_questButtonPrefab, _contentRoot);
                var btn = go.GetComponent<QuestButtonUI>();
                if (btn == null)
                {
                    GameLog.Error(TAG, $"_questButtonPrefab is missing QuestButtonUI component on '{go.name}'.");
                    Destroy(go);
                    continue;
                }
                btn.Bind(quest);
            }
        }

        private void UpdateTabStyles()
        {
            if (_tabStarted   != null) _tabStarted.interactable   = _activeTab != QuestTab.Started;
            if (_tabCompleted != null) _tabCompleted.interactable = _activeTab != QuestTab.Completed;
            if (_tabFailed    != null) _tabFailed.interactable    = _activeTab != QuestTab.Failed;
        }
    }
}
