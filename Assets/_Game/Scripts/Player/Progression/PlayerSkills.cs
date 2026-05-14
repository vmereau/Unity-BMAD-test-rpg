using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Progression
{
    /// <summary>
    /// Tracks which skills the player has learned. Skills are learned via TomePickup or other sources.
    /// Actual skill gameplay effects implemented in Story 3.6.
    /// Story 3.5: Initial implementation.
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private GameEventSO_String _onSkillLearned;

        private readonly HashSet<string> _learnedSkills = new HashSet<string>();

#if false // DISABLED: debug OnGUI — to be reworked
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_lpSystem == null)
            {
                GameLog.Error(TAG, "LearningPointSystem not assigned — PlayerSkills disabled.");
                enabled = false;
                return;
            }
        }

        /// <summary>Returns true if the skill with the given id has been learned.</summary>
        public bool HasSkill(string skillId) => _learnedSkills.Contains(skillId);

        public bool CanLearnSkill(SkillSO skill)
        {
            if (skill == null)
            {
                GameLog.Error(TAG, "CanLearnSkill called with null skill");
                return false;
            }

            if (HasSkill(skill.skillId))
            {
                GameLog.Warn(TAG, $"Skill already learned: {skill.displayName}");
                return false;
            }

            // Stat check runs BEFORE LP spend so no LP is lost on a failed stat gate.
            if (skill.statsRequirements.Count > 0)
            {
                if (_playerStats == null)
                {
                    GameLog.Warn(TAG, $"PlayerStats not assigned — cannot validate stat requirements for {skill.displayName}");
                    return false;
                }
                if (!_playerStats.ValidateStats(skill.statsRequirements))
                {
                    GameLog.Warn(TAG, $"Stat requirements not met for {skill.displayName}");
                    return false;
                }
            }

            if (skill.skillRequirements.Count > 0)
            {
                foreach (SkillSO required in skill.skillRequirements)
                {
                    if (required == null) continue;
                    if (!HasSkill(required.skillId))
                    {
                        GameLog.Warn(TAG, $"Skill requirement not met for {skill.displayName}: need {required.displayName}");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to learn a skill by spending LP. Returns true on success.
        /// </summary>
        public bool LearnSkill(SkillSO skill)
        {
            if (!CanLearnSkill(skill))
            {
                return false;
            }

            if (!_lpSystem.TrySpendLP(skill.lpCost))
            {
                GameLog.Warn(TAG, $"Insufficient LP to learn {skill.displayName} (cost: {skill.lpCost}, current: {_lpSystem.CurrentLP})");
                return false;
            }

            _learnedSkills.Add(skill.skillId);
            _onSkillLearned?.Raise(skill.skillId);
            GameLog.Info(TAG, $"Skill learned: {skill.displayName} (id: {skill.skillId}). Total skills: {_learnedSkills.Count}");
            return true;
        }

#if false // DISABLED: debug OnGUI — to be reworked
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 390, 500, 26), $"Skills: {_learnedSkills.Count} learned", _guiStyle);
        }
#endif
    }
}
