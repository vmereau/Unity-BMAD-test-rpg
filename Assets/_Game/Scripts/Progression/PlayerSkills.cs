using System.Collections.Generic;
using Game.Core;
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
        [SerializeField] private GameEventSO_String _onSkillLearned;

        private readonly HashSet<string> _learnedSkills = new HashSet<string>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
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

        /// <summary>
        /// Attempts to learn a skill by spending LP. Returns true on success.
        /// </summary>
        public bool LearnSkill(SkillSO skill)
        {
            if (skill == null)
            {
                GameLog.Error(TAG, "LearnSkill called with null skill");
                return false;
            }

            if (HasSkill(skill.skillId))
            {
                GameLog.Warn(TAG, $"Skill already learned: {skill.displayName}");
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 390, 500, 26), $"Skills: {_learnedSkills.Count} learned", _guiStyle);
        }
#endif
    }
}
