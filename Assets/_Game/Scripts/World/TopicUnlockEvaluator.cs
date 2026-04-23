using Game.Core;

namespace Game.World
{
    /// <summary>
    /// Pure static evaluation logic for NPC memory unlock/invalidation conditions.
    /// Queries WorldStateManager world facts — no MonoBehaviour, no instance.
    /// </summary>
    public static class TopicUnlockEvaluator
    {
        private const string TAG = "[TopicUnlock]";

        /// <summary>
        /// Returns true if ALL facts are set to true in WorldStateManager.
        /// Empty array = trivially unlocked (always true). Null elements are skipped.
        /// </summary>
        public static bool AllTrue(Fact[] facts)
        {
            if (facts == null || facts.Length == 0) return true;
            var wsm = WorldStateManager.Instance;
            if (wsm == null)
            {
                GameLog.Warn(TAG, "WorldStateManager not available — conditions evaluated as false");
                return false;
            }
            foreach (var fact in facts)
            {
                if (fact == null) continue;
                bool result = wsm.GetFact(fact);
                if (!result) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if ANY fact is set to true in WorldStateManager.
        /// Empty array = not invalidated (returns false). Null elements are skipped.
        /// </summary>
        public static bool AnyTrue(Fact[] facts)
        {
            if (facts == null || facts.Length == 0) return false;
            var wsm = WorldStateManager.Instance;
            if (wsm == null)
            {
                GameLog.Warn(TAG, "WorldStateManager not available — invalidation conditions evaluated as false");
                return false;
            }
            foreach (var fact in facts)
            {
                if (fact == null) continue;
                if (wsm.GetFact(fact)) return true;
            }
            return false;
        }
    }
}
