using System.Collections.Generic;
using Game.Player;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public struct StatRequirement
    {
        public StatType statType;
        public int value;
    }

    /// <summary>
    /// Evaluates whether the player meets all listed stat thresholds (>= check).
    /// NOT stored in WorldStateManager._worldFacts — evaluated at runtime via PlayerStats.GetStat().
    /// Prefix is intentionally not set (not a world-fact key).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Stat Fact", fileName = "StatFact_")]
    public class StatFact : Fact
    {
        [SerializeField] private List<StatRequirement> _requirements = new List<StatRequirement>();

        public IReadOnlyList<StatRequirement> Requirements => _requirements;

        /// <summary>Runtime/test initialiser.</summary>
        public StatFact Init(params StatRequirement[] requirements)
        {
            _requirements = new List<StatRequirement>(requirements);
            return this;
        }

        /// <summary>For debugging only — not used as a world-fact dictionary key.</summary>
        public override string ToString() => $"Stat.Requirements({_requirements?.Count ?? 0})";
    }
}
