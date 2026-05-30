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
        /// </summary>
        public static FactionMember FindClosestHostile(FactionSO myFaction, Vector3 origin, float maxRange)
        {
            if (myFaction == null) return null;
            float bestSqr = maxRange * maxRange;
            FactionMember best = null;
            foreach (var m in _members)
            {
                if (m == null) continue;
                if (m.Faction == null) continue;
                if (!myFaction.IsHostileTo(m.Faction)) continue;
                // m.Damageable is an interface ref — cast to Object so the null check honors Unity's
                // destroyed-object semantics (a destroyed health component is not C#-null but is Unity-null).
                if (m.Damageable == null || (Object)m.Damageable == null || m.Damageable.IsDead) continue;
                float sqr = (m.Transform.position - origin).sqrMagnitude;
                if (sqr > bestSqr) continue;
                // Deterministic tie-break on exact-distance ties (HashSet iteration order is not stable).
                if (best == null || sqr < bestSqr || m.GetInstanceID() < best.GetInstanceID())
                {
                    bestSqr = sqr;
                    best = m;
                }
            }
            return best;
        }
    }
}
