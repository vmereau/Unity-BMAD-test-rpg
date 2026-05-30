using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Factions
{
    [CreateAssetMenu(menuName = "Game/Faction", fileName = "Faction_")]
    public class FactionSO : ScriptableObject
    {
        [Tooltip("Display name shown in debug tools.")]
        public string factionName;

        [Tooltip("Factions this faction will actively engage in combat. Set symmetrically — if A lists B, B should usually list A.")]
        [SerializeField] private List<FactionSO> _hostileFactions = new();

        [Tooltip("Informational for follow-up assist behavior — not consumed in v1.")]
        [SerializeField] private List<FactionSO> _alliedFactions = new();

        public bool IsHostileTo(FactionSO other) => other != null && _hostileFactions.Contains(other);
        public bool IsAlliedWith(FactionSO other) => other != null && _alliedFactions.Contains(other);

        #if UNITY_EDITOR
        private const string TAG = "[Faction]";

        // Test-only chainable setup. Mirrors WorldFact.Init pattern.
        public FactionSO InitForTest(List<FactionSO> hostile, List<FactionSO> allied = null)
        {
            _hostileFactions = hostile ?? new List<FactionSO>();
            _alliedFactions = allied ?? new List<FactionSO>();
            return this;
        }

        private void OnValidate()
        {
            // Self-hostility makes same-faction members target each other (friendly fire) — almost always a misconfig.
            if (_hostileFactions.Contains(this))
                GameLog.Warn(TAG, $"'{name}': lists itself in hostileFactions — same-faction members will target each other.");
            if (_hostileFactions.Contains(null))
                GameLog.Warn(TAG, $"'{name}': hostileFactions contains a null/missing entry — clean it up.");
        }
        #endif
    }
}
