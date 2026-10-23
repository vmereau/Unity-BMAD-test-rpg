using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using UnityEngine;

namespace Game.UI
{
    public class QuestLogUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[QuestLogUI]";

        [SerializeField] private List<QuestSO> _allQuests = new List<QuestSO>();
        [SerializeField] private QuestListPanelUI _listPanel;
        [SerializeField] private QuestInfoPanelUI _infoPanel;

        [Header("Event Channels (optional — for live refresh)")]
        [SerializeField] private GameEventSO_Quest _onQuestStarted;
        [SerializeField] private GameEventSO_Quest _onQuestCompleted;
        [SerializeField] private GameEventSO_Quest _onQuestFailed;
        [Tooltip("Any world fact change — refreshes parts / steps that raise no quest event.")]
        [SerializeField] private GameEventSO_Fact _onFactChanged;

        private bool _refreshPending;

        private void OnEnable()
        {
            _onQuestStarted?.AddListener(HandleQuestStateChanged);
            _onQuestCompleted?.AddListener(HandleQuestStateChanged);
            _onQuestFailed?.AddListener(HandleQuestStateChanged);
            _onFactChanged?.AddListener(HandleFactChanged);
            if (_listPanel != null) _listPanel.SelectionChanged += HandleSelectionChanged;
        }

        private void OnDisable()
        {
            _onQuestStarted?.RemoveListener(HandleQuestStateChanged);
            _onQuestCompleted?.RemoveListener(HandleQuestStateChanged);
            _onQuestFailed?.RemoveListener(HandleQuestStateChanged);
            _onFactChanged?.RemoveListener(HandleFactChanged);
            if (_listPanel != null) _listPanel.SelectionChanged -= HandleSelectionChanged;
        }

        public void OnScreenOpen()
        {
            if (_listPanel == null) { GameLog.Warn(TAG, "_listPanel is not assigned."); return; }
            if (_infoPanel == null) { GameLog.Warn(TAG, "_infoPanel is not assigned."); return; }
            CursorManager.Unlock();
            RefreshList(); // auto-selects → HandleSelectionChanged shows the quest or the empty state
            GameLog.Info(TAG, "Quest Log opened");
        }

        public void OnScreenClose()
        {
            if (_infoPanel != null) _infoPanel.Hide();
            CursorManager.Lock();
            GameLog.Info(TAG, "Quest Log closed");
        }

        public void SelectQuest(QuestSO quest)
        {
            if (_listPanel == null) { GameLog.Warn(TAG, "_listPanel is not assigned."); return; }
            _listPanel.Select(quest);
        }

        // Coalesces bursts of fact / quest events (e.g. several kills in one frame) into one rebuild
        private void LateUpdate()
        {
            if (_refreshPending) RefreshList();
        }

        private void HandleSelectionChanged(QuestSO quest)
        {
            if (_infoPanel == null) return;
            if (quest != null) _infoPanel.Show(quest);
            else               _infoPanel.Hide();
        }

        private void HandleQuestStateChanged(QuestSO _) => _refreshPending = true;
        private void HandleFactChanged(FactData _) => _refreshPending = true;

        private void RefreshList()
        {
            _refreshPending = false;
            if (_listPanel == null) return;
            _listPanel.Refresh(_allQuests);
        }
    }
}
