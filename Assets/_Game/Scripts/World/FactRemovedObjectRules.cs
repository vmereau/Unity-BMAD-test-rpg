namespace Game.World
{
    /// <summary>What a <see cref="FactRemovedObject"/> does with its target this check.</summary>
    public enum FactRemovalAction
    {
        /// <summary>Leave the target as it is.</summary>
        None,
        /// <summary>Hide the target (an NPC leaves the world).</summary>
        Hide,
        /// <summary>Show the target again (the fact turned false).</summary>
        Show,
        /// <summary>The target should go but is busy (an NPC fighting, chasing a thief or knocked out): check again later.</summary>
        Wait,
    }

    /// <summary>
    /// <b>Static, pure.</b> Decides whether a <see cref="FactRemovedObject"/> hides or shows its target.
    /// Tested by <c>FactRemovedObjectRulesTests</c>.
    /// </summary>
    public static class FactRemovedObjectRules
    {
        /// <summary>
        /// Fact true → hide, fact false → show; nothing when the target is already in that state. Only a target this
        /// component hid (<paramref name="hiddenByMe"/>) is shown again: another system's hiding is never undone. For an
        /// entity target: a dead one is never touched (its corpse and loot belong to <c>PersistentID</c> /
        /// <c>SaveableObject</c>), and a busy one (in combat, chasing a thief, unconscious) waits before it is hidden.
        /// Showing never waits.
        /// </summary>
        public static FactRemovalAction Decide(bool factTrue, bool isEntity, bool isDead, bool isBusy, bool targetActive,
            bool hiddenByMe)
        {
            if (isEntity && isDead) return FactRemovalAction.None;
            if (!factTrue) return !targetActive && hiddenByMe ? FactRemovalAction.Show : FactRemovalAction.None;
            if (!targetActive) return FactRemovalAction.None;
            return isEntity && isBusy ? FactRemovalAction.Wait : FactRemovalAction.Hide;
        }
    }
}
