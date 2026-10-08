using System.Collections.Generic;
using Game.Factions;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Static runtime registry of all live FactionMember components.
    /// Queried by EntityBrain to find targets without GameObject.FindGameObjectWithTag.
    /// Reset on play-mode enter via SubsystemRegistration; not persisted across scenes intentionally
    /// (FactionMember.OnEnable re-registers on additive scene loads automatically).
    /// </summary>
    public static class TargetRegistry
    {
        private static readonly HashSet<FactionMember> _members = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _members.Clear();

        public static void Register(FactionMember member)
        {
            if (member == null) return;
            _members.Add(member);
        }

        public static void Unregister(FactionMember member)
        {
            if (member == null) return;
            _members.Remove(member);
        }

        /// <summary>
        /// Returns the closest live member whose faction is hostile to <paramref name="myFaction"/>
        /// and within <paramref name="maxRange"/> of <paramref name="origin"/>, or null.
        /// With <paramref name="skipStealthTargets"/>, members with a <see cref="FactionMember.StealthTarget"/>
        /// (the player) are ignored — they are acquired through EntityPerception instead.
        /// </summary>
        public static FactionMember FindClosestHostile(FactionSO myFaction, Vector3 origin, float maxRange,
            bool skipStealthTargets = false) =>
            FindClosest(myFaction, origin, maxRange, skipStealthTargets ? StealthFilter.Exclude : StealthFilter.Any, true);

        /// <summary>
        /// Like <see cref="FindClosestHostile"/>, but only members with a <see cref="FactionMember.StealthTarget"/>.
        /// </summary>
        public static FactionMember FindClosestHostileStealthTarget(FactionSO myFaction, Vector3 origin, float maxRange) =>
            FindClosest(myFaction, origin, maxRange, StealthFilter.Only, true);

        /// <summary>
        /// Closest live stealth target whose faction is <b>not</b> hostile to <paramref name="myFaction"/> — used by
        /// non-hostile witnesses (EntityPerception witness mode).
        /// </summary>
        public static FactionMember FindClosestNonHostileStealthTarget(FactionSO myFaction, Vector3 origin, float maxRange) =>
            FindClosest(myFaction, origin, maxRange, StealthFilter.Only, false);

        private enum StealthFilter { Any, Exclude, Only }

        private static FactionMember FindClosest(FactionSO myFaction, Vector3 origin, float maxRange, StealthFilter filter,
            bool wantHostile)
        {
            if (myFaction == null) return null;
            float bestSqr = maxRange * maxRange;
            FactionMember best = null;
            foreach (var m in _members)
            {
                if (m == null) continue;
                if (m.Faction == null) continue;
                bool isStealthTarget = m.StealthTarget != null;
                if (filter == StealthFilter.Exclude && isStealthTarget) continue;
                if (filter == StealthFilter.Only && !isStealthTarget) continue;
                if (myFaction.IsHostileTo(m.Faction) != wantHostile) continue;
                // m.Damageable is an interface ref — cast to Object so the null check honors Unity's
                // destroyed-object semantics (a destroyed health component is not C#-null but is Unity-null).
                if (m.Damageable == null || (Object)m.Damageable == null || m.Damageable.IsDead) continue;
                float sqr = (m.Transform.position - origin).sqrMagnitude;
                if (sqr > bestSqr) continue;
                // Deterministic tie-break on exact-distance ties (HashSet iteration order is not stable).
                if (best == null || sqr < bestSqr || m.GetEntityId().CompareTo(best.GetEntityId()) < 0)
                {
                    bestSqr = sqr;
                    best = m;
                }
            }
            return best;
        }
    }
}
