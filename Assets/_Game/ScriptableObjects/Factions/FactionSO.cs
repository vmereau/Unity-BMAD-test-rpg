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

        [Tooltip("Factions this one never fights (either direction). Allied with Faction_Player = Friendly tier; with " +
                 "_membersDefendEachOther, allies are defended like own members.")]
        [SerializeField] private List<FactionSO> _alliedFactions = new();

        [Tooltip("Members join a fight a fellow (or allied) member is in, against its opponent. Off for workers.")]
        [SerializeField] private bool _membersDefendEachOther;

        public bool IsHostileTo(FactionSO other) => other != null && _hostileFactions.Contains(other);
        public bool IsAlliedWith(FactionSO other) => other != null && _alliedFactions.Contains(other);
        public bool MembersDefendEachOther => _membersDefendEachOther;

        /// <summary>
        /// True when an entity of (<paramref name="self"/>, <paramref name="selfGroup"/>) defends one of
        /// (<paramref name="other"/>, <paramref name="otherGroup"/>): a shared non-null aid group, or self's faction
        /// defends its members and other is that faction or allied with it (either direction).
        /// </summary>
        public static bool Defends(FactionSO self, AidGroupSO selfGroup, FactionSO other, AidGroupSO otherGroup)
        {
            if (selfGroup != null && selfGroup == otherGroup) return true;
            if (self == null || other == null || !self.MembersDefendEachOther) return false;
            return other == self || self.IsAlliedWith(other) || other.IsAlliedWith(self);
        }

        /// <summary>True when either faction lists the other as hostile (null-safe). Shared rule for player-facing
        /// hostility checks (lock-on targets, NPC interaction gating).</summary>
        public static bool AreHostile(FactionSO a, FactionSO b) =>
            a != null && b != null && (a.IsHostileTo(b) || b.IsHostileTo(a));

        #if UNITY_EDITOR
        private const string TAG = "[Faction]";

        // Test-only chainable setup. Mirrors WorldFact.Init pattern.
        public FactionSO InitForTest(List<FactionSO> hostile, List<FactionSO> allied = null, bool defendMembers = false)
        {
            _hostileFactions = hostile ?? new List<FactionSO>();
            _alliedFactions = allied ?? new List<FactionSO>();
            _membersDefendEachOther = defendMembers;
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
