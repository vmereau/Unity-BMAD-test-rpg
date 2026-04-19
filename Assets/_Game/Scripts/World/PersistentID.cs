using Game.AI;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Marks a world entity as permanently tracked by WorldStateManager.
    /// On Awake: checks if this entity was previously killed and deactivates silently if so.
    /// Call RegisterDeath() when the entity is killed — before playing death effects.
    /// Every world enemy, NPC, and container MUST have this component with a KilledFact assigned.
    /// Story 2.8: Initial implementation.
    /// MIGRATION NOTE: The previous _guid string field has been replaced by _killedFact.
    /// Existing PersistentID instances will need a KilledFact asset created and assigned.
    /// </summary>
    public class PersistentID : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        [SerializeField] private KilledFact _killedFact;
        [SerializeField] private GameEventSO_KilledFact _onEntityKilled;
        
        [SerializeField] private EnemyTypeSO _enemyType;

        private void Awake()
        {
            if (_killedFact == null)
            {
                GameLog.Error(TAG, $"PersistentID on {gameObject.name} has no KilledFact assigned — entity will not be tracked");
                return;
            }
        }

		private void Start()
        {
            if (!enabled) return;

            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, $"WorldStateManager not found — PersistentID check skipped for {gameObject.name}");
                return;
            }

            if (WorldStateManager.Instance.IsKilled(_killedFact))
                gameObject.SetActive(false);
        }

        /// <summary>
        /// Call when this entity is killed. Registers the kill in WorldStateManager
        /// and raises the OnEntityKilled event channel if assigned.
        /// </summary>
        public void RegisterDeath()
        {
            if (_killedFact == null)
            {
                GameLog.Error(TAG, $"RegisterDeath called on {gameObject.name} but KilledFact is not assigned — kill ignored");
                return;
            }

            WorldStateManager.Instance?.RegisterKill(_killedFact);

            if (_onEntityKilled != null)
                _onEntityKilled.Raise(_killedFact);
            else
                GameLog.Warn(TAG, $"OnEntityKilled event not assigned on {gameObject.name} — kill not broadcast");
        }
    }
}
