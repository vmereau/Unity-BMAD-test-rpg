using System;
using System.Collections.Generic;
using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Who the protagonist was before the Company contract, picked at New Game (docs/World/player-origins.md).
    /// The stat bonuses and skill are applied once on the new-game boot. The belongings (items + gold) are only
    /// shown on the pick screen: they never reach the inventory.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Player/Origin", fileName = "Origin_")]
    public class OriginSO : ScriptableObject
    {
        [Serializable]
        public struct StatBonus
        {
            public StatType stat;
            [Min(1)] public int points;
        }

        [SerializeField] private string _originId;
        [SerializeField] private string _displayName;
        [TextArea(2, 6)] [SerializeField] private string _description;
        [SerializeField] private List<StatBonus> _statBonuses = new List<StatBonus>();
        [Tooltip("Optional skill known from the start. No LP spent, no skill-learned event.")]
        [SerializeField] private SkillSO _skill;

        [Header("Belongings (shown on the pick screen only)")]
        [SerializeField] private bool _hasBelongings;
        [SerializeField] private List<string> _promisedItems = new List<string>();
        [Min(0)] [SerializeField] private int _promisedGold;

        [Header("Pick screen")]
        [Tooltip("3:4 illustration of the hero in his past, shown beside the details. Optional: hidden when empty.")]
        [SerializeField] private Sprite _artwork;

        [Header("Facts")]
        [Tooltip("WorldFact_Origin_<Id>, set on the new-game boot.")]
        [SerializeField] private WorldFact _originFact;

        public string originId => _originId;
        public string displayName => _displayName;
        public string description => _description;
        public IReadOnlyList<StatBonus> statBonuses => _statBonuses;
        public SkillSO skill => _skill;
        public bool hasBelongings => _hasBelongings;
        public IReadOnlyList<string> promisedItems => _promisedItems;
        public int promisedGold => _promisedGold;
        public WorldFact originFact => _originFact;
        public Sprite artwork => _artwork;

        /// <summary>Test / tooling setup.</summary>
        public OriginSO Init(string originId, string displayName, List<StatBonus> statBonuses = null, SkillSO skill = null,
            bool hasBelongings = false, List<string> promisedItems = null, int promisedGold = 0, WorldFact originFact = null,
            Sprite artwork = null)
        {
            _originId = originId;
            _displayName = displayName;
            _statBonuses = statBonuses ?? new List<StatBonus>();
            _skill = skill;
            _hasBelongings = hasBelongings;
            _promisedItems = promisedItems ?? new List<string>();
            _promisedGold = promisedGold;
            _originFact = originFact;
            _artwork = artwork;
            return this;
        }
    }
}
