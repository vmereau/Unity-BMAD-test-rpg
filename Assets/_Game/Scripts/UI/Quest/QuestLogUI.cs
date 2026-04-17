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

        private void OnEnable()
        {
            _onQuestStarted?.AddListener(HandleQuestStateChanged);
            _onQuestCompleted?.AddListener(HandleQuestStateChanged);
            _onQuestFailed?.AddListener(HandleQuestStateChanged);
        }

        private void OnDisable()
        {
            _onQuestStarted?.RemoveListener(HandleQuestStateChanged);
            _onQuestCompleted?.RemoveListener(HandleQuestStateChanged);
            _onQuestFailed?.RemoveListener(HandleQuestStateChanged);
        }

        public void OnScreenOpen()
        {
            if (_listPanel == null) { GameLog.Warn(TAG, "_listPanel is not assigned."); return; }
            if (_infoPanel == null) { GameLog.Warn(TAG, "_infoPanel is not assigned."); return; }
            CursorManager.Unlock();
            RefreshList();
            _infoPanel.Hide();
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
            if (_infoPanel == null) { GameLog.Warn(TAG, "_infoPanel is not assigned."); return; }
            _infoPanel.Show(quest);
        }

        private void HandleQuestStateChanged(QuestSO _) => RefreshList();

        private void RefreshList()
        {
            if (_listPanel == null) return;
            _listPanel.Refresh(_allQuests);
        }
    }
}
