using Game.Progression;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Evaluates whether the player has learned a specific skill.
    /// NOT stored in WorldStateManager._worldFacts — evaluated at runtime via PlayerSkills.HasSkill().
    /// Prefix is intentionally not set (not a world-fact key).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Skill Fact", fileName = "SkillFact_")]
    public class SkillFact : Fact
    {
        [SerializeField] private SkillSO _skill;

        public SkillSO Skill => _skill;

        /// <summary>Runtime/test initialiser.</summary>
        public SkillFact Init(SkillSO skill)
        {
            _skill = skill;
            return this;
        }

        /// <summary>For debugging only — not used as a world-fact dictionary key.</summary>
        public override string ToString() => $"Skill.{_skill?.skillId ?? "null"}";
    }
}
