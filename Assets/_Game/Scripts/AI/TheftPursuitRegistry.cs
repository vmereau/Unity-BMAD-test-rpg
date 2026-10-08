using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Static claim registry for theft incidents (like <see cref="TargetRegistry"/>). Every witness of a theft chases
    /// the thief; the first to catch them claims the incident (<see cref="TryClaim"/>) and confronts, the others see
    /// <see cref="IsClaimed"/> and give up. Runtime only, reset on play-mode enter.
    /// </summary>
    public static class TheftPursuitRegistry
    {
        private static readonly HashSet<int> _claimed = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _claimed.Clear();

        /// <summary>Claims <paramref name="theftId"/>. True only for the first caller (first catcher wins).</summary>
        public static bool TryClaim(int theftId) => _claimed.Add(theftId);

        public static bool IsClaimed(int theftId) => _claimed.Contains(theftId);
    }
}
