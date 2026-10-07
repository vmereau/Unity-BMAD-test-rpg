using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Skills tab: lists every skill of the <see cref="SkillCatalogSO"/> (unlearned grayed) and shows the selected one
    /// in a <see cref="SkillDetailPanelUI"/>. Read-only — skills are not learned from here.
    /// </summary>
    public class SkillsUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[UI]";

        [SerializeField] private SkillCatalogSO _catalog;

        // Cross-system MonoBehaviour refs (prototype shortcut, same as CharacterStatsUI) — read-only queries.
        // Wired as Player.prefab overrides on the nested UICanvas.
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private PlayerStats _playerStats;

        [SerializeField] private Transform _listRoot;
        [SerializeField] private SkillSlotUI _slotPrefab;
        [SerializeField] private SkillDetailPanelUI _detailPanel;

        // Event SOs
        [SerializeField] private GameEventSO_String _onSkillLearned;
        [SerializeField] private GameEventSO_Void _onStatsChanged;

        private readonly List<SkillSlotUI> _slots = new();
        private bool _slotsBuilt;
        private SkillSO _selectedSkill;
        private Func<string, bool> _hasSkill;
        private Func<StatType, int> _getStat;
        private Action<SkillSlotUI> _onSlotClicked;

        private void Awake()
        {
            if (_catalog == null || _playerSkills == null || _listRoot == null || _slotPrefab == null || _detailPanel == null)
            {
                GameLog.Error(TAG, "SkillsUI: missing required reference — disabled");
                enabled = false;
                return;
            }

            if (_playerStats == null)    GameLog.Warn(TAG, "SkillsUI: _playerStats not assigned — stat requirements shown uncolored");
            if (_onSkillLearned == null) GameLog.Warn(TAG, "SkillsUI: _onSkillLearned not assigned — learned state won't be live");
            if (_onStatsChanged == null) GameLog.Warn(TAG, "SkillsUI: _onStatsChanged not assigned — requirement colors won't be live");

            _hasSkill = _playerSkills.HasSkill;
            _getStat = _playerStats != null ? _playerStats.GetStat : null;
            _onSlotClicked = HandleSlotClicked;
        }

        private void OnEnable()
        {
            _onSkillLearned?.AddListener(HandleSkillLearned);
            _onStatsChanged?.AddListener(HandleStatsChanged);
            Refresh();
        }

        private void OnDisable()
        {
            _onSkillLearned?.RemoveListener(HandleSkillLearned);
            _onStatsChanged?.RemoveListener(HandleStatsChanged);
        }

        // IScreenPanel — cursor is handled by UIScreenManager; OnEnable already refreshes.
        public void OnScreenOpen()  => GameLog.Info(TAG, "Skills opened");
        public void OnScreenClose() => GameLog.Info(TAG, "Skills closed");

        // Event handlers
        private void HandleSkillLearned(string _) => Refresh();
        private void HandleStatsChanged(bool _)   => Refresh();

        private void HandleSlotClicked(SkillSlotUI slot)
        {
            if (slot == null || slot.Skill == null) return;
            _selectedSkill = slot.Skill;
            ApplySelection();
        }

        private void Refresh()
        {
            BuildSlotsOnce();

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetLearned(_hasSkill(_slots[i].Skill.skillId));

            if (!HasSlotFor(_selectedSkill))
                _selectedSkill = _slots.Count > 0 ? _slots[0].Skill : null;

            ApplySelection();
        }

        // The catalog is static at runtime, so slots are built on first refresh and never rebuilt.
        private void BuildSlotsOnce()
        {
            if (_slotsBuilt) return;
            _slotsBuilt = true;

            var seenIds = new HashSet<string>();
            foreach (SkillSO skill in _catalog.skills)
            {
                if (skill == null) continue;
                if (string.IsNullOrWhiteSpace(skill.skillId))
                {
                    GameLog.Warn(TAG, $"SkillsUI: skill '{skill.name}' has no skillId — skipped");
                    continue;
                }
                if (!seenIds.Add(skill.skillId))
                {
                    GameLog.Warn(TAG, $"SkillsUI: duplicate skillId '{skill.skillId}' in catalog ('{skill.name}') — skipped");
                    continue;
                }
                SkillSlotUI slot = Instantiate(_slotPrefab, _listRoot);
                slot.Bind(skill, _onSlotClicked);
                _slots.Add(slot);
            }
        }

        private bool HasSlotFor(SkillSO skill)
        {
            if (skill == null) return false;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Skill == skill) return true;
            return false;
        }

        private void ApplySelection()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetSelected(_slots[i].Skill == _selectedSkill);

            if (_selectedSkill == null)
            {
                _detailPanel.Hide();
                return;
            }
            _detailPanel.Show(_selectedSkill, _hasSkill(_selectedSkill.skillId), _getStat, _hasSkill);
        }
    }
}
