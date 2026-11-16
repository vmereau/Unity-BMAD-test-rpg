using _Game.ScriptableObjects.Entities;

namespace Game.Core
{
    /// <summary>
    /// Payload raised by WorldStateManager when a tracked entity is killed.
    /// Carries the entity definition (single source of truth for XpOnKill, sourced from
    /// PersistentID.Entity) alongside the KilledFact identity used for persistence and
    /// per-asset bonus-reward matching. Holds only ScriptableObject references — no scene
    /// object refs — so it is safe to pass through SO-adjacent event channels.
    /// </summary>
    [System.Serializable]
    public struct EntityKilled
    {
        public Entity entity;
        public KilledFact fact;
        /// <summary>
        /// The player earned the kill XP: own blow, a Friendly NPC's blow, or an assist (spec-kill-xp-credit-assist).
        /// Bonus rewards matched on <see cref="fact"/> ignore it.
        /// </summary>
        public bool playerCredited;

        public EntityKilled(Entity entity, KilledFact fact, bool playerCredited = false)
        {
            this.entity = entity;
            this.fact = fact;
            this.playerCredited = playerCredited;
        }
    }
}
