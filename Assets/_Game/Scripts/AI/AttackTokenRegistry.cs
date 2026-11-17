using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// <b>Static</b> attack tokens (spec-ai-combat-tactics-foundation): caps how many tactical attackers swing at one
    /// target at once. A holder owns at most one token; taking one on another target releases the previous one.
    /// Keys are plain objects (target = its <c>FactionMember</c>, holder = the <see cref="EntityCombatTactics"/>), so
    /// the rules are EditMode-testable. Reset on play-mode enter.
    /// </summary>
    public static class AttackTokenRegistry
    {
        private static readonly Dictionary<object, List<object>> _holdersByTarget = new Dictionary<object, List<object>>();
        private static readonly Dictionary<object, object> _targetByHolder = new Dictionary<object, object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            _holdersByTarget.Clear();
            _targetByHolder.Clear();
        }

        public static int CountOn(object target) =>
            target != null && _holdersByTarget.TryGetValue(target, out var holders) ? holders.Count : 0;

        public static bool Holds(object holder, object target) =>
            holder != null && target != null && _targetByHolder.TryGetValue(holder, out var held) && Equals(held, target);

        /// <summary>A token on <paramref name="target"/> could be taken by <paramref name="holder"/> now.</summary>
        public static bool IsAvailable(object target, object holder, int capacity) =>
            Holds(holder, target) || CountOn(target) < capacity;

        /// <summary>Takes (or keeps) a token on <paramref name="target"/>; false when all <paramref name="capacity"/> are held.</summary>
        public static bool TryTake(object target, object holder, int capacity)
        {
            if (target == null || holder == null) return false;
            if (Holds(holder, target)) return true;
            if (CountOn(target) >= capacity) return false;
            Release(holder);
            if (!_holdersByTarget.TryGetValue(target, out var holders))
            {
                holders = new List<object>(2);
                _holdersByTarget.Add(target, holders);
            }
            holders.Add(holder);
            _targetByHolder[holder] = target;
            return true;
        }

        /// <summary>Releases the token <paramref name="holder"/> holds, if any. Idempotent.</summary>
        public static void Release(object holder)
        {
            if (holder == null || !_targetByHolder.TryGetValue(holder, out var target)) return;
            _targetByHolder.Remove(holder);
            if (!_holdersByTarget.TryGetValue(target, out var holders)) return;
            holders.Remove(holder);
            if (holders.Count == 0) _holdersByTarget.Remove(target);
        }
    }
}
