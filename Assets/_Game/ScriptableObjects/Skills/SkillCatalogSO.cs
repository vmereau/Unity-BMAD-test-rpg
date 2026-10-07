using System.Collections.Generic;
using UnityEngine;

namespace Game.Progression
{
    /// <summary>Ordered list of every skill in the game. Order = display order in the Skills tab.</summary>
    [CreateAssetMenu(menuName = "Game/Skills/Skill Catalog", fileName = "SkillCatalog")]
    public class SkillCatalogSO : ScriptableObject
    {
        [SerializeField] private List<SkillSO> _skills = new List<SkillSO>();

        /// <summary>May contain null entries (unassigned slots) — consumers must skip them.</summary>
        public IReadOnlyList<SkillSO> skills => _skills;
    }
}
