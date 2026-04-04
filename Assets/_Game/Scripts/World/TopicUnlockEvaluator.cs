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
        /// Returns true if ALL keys are set to true in WorldStateManager.
        /// Empty array = trivially unlocked (always true).
        /// </summary>
        public static bool AllTrue(string[] keys)
        {
            if (keys == null || keys.Length == 0) return true;
            var wsm = WorldStateManager.Instance;
            if (wsm == null)
            {
                GameLog.Warn(TAG, "WorldStateManager not available — conditions evaluated as false");
                return false;
            }
            foreach (var key in keys)
                if (!wsm.GetFact(key)) return false;
            return true;
        }

        /// <summary>
        /// Returns true if ANY key is set to true in WorldStateManager.
        /// Empty array = not invalidated (returns false).
        /// </summary>
        public static bool AnyTrue(string[] keys)
        {
            if (keys == null || keys.Length == 0) return false;
            var wsm = WorldStateManager.Instance;
            if (wsm == null)
            {
                GameLog.Warn(TAG, "WorldStateManager not available — invalidation conditions evaluated as false");
                return false;
            }
            foreach (var key in keys)
                if (wsm.GetFact(key)) return true;
            return false;
        }
    }
}
