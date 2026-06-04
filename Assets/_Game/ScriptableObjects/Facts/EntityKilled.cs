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

        public EntityKilled(Entity entity, KilledFact fact)
        {
            this.entity = entity;
            this.fact = fact;
        }
    }
}
