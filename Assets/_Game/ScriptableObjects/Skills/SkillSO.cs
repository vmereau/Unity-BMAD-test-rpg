using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Progression
{
    /// <summary>
    /// Data container for a learnable skill. Actual gameplay effects implemented in Story 3.6.
    /// Story 3.5: Initial implementation.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Skills/Skill", fileName = "Skill_")]
    public class SkillSO : ScriptableObject
    {
        [SerializeField] private string _skillId;
        [SerializeField] private string _displayName;
        [TextArea] [SerializeField] private string _description;
        [Min(1)] [SerializeField] private int _lpCost = 1;
        [SerializeField] private List<StatRequirement> _statsRequirements = new List<StatRequirement>();
        [SerializeField] private List<SkillSO> _skillRequirements = new List<SkillSO>();

        public string skillId => _skillId;
        public string displayName => _displayName;
        public string description => _description;
        public int lpCost => _lpCost;
        public IReadOnlyList<StatRequirement> statsRequirements => _statsRequirements;
        public IReadOnlyList<SkillSO> skillRequirements => _skillRequirements;
    }
}
