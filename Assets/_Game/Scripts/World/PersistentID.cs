using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Marks a world entity as permanently tracked by WorldStateManager.
    /// On Awake: checks if this entity was previously killed and deactivates silently if so.
    /// Call RegisterDeath() when the entity is killed — before playing death effects.
    /// Every world enemy, NPC, and container MUST have this component with a unique GUID.
    /// Story 2.8: Initial implementation.
    /// </summary>
    public class PersistentID : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        [SerializeField] private string _guid;
        [SerializeField] private GameEventSO_String _onEntityKilled;

        private void Awake()
        {
            if (string.IsNullOrEmpty(_guid))
            {
                GameLog.Error(TAG, $"PersistentID on {gameObject.name} has no GUID — entity will not be tracked");
                return;
            }

            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, $"WorldStateManager not found — PersistentID check skipped for {gameObject.name}");
                return;
            }

            if (WorldStateManager.Instance.IsKilled(_guid))
            {
                gameObject.SetActive(false); // Silent — no events, no logging
            }
        }

        /// <summary>
        /// Call when this entity is killed. Registers the kill in WorldStateManager
        /// and raises the OnEntityKilled event channel if assigned.
        /// </summary>
        public void RegisterDeath()
        {
            WorldStateManager.Instance?.RegisterKill(_guid);

            if (_onEntityKilled != null)
                _onEntityKilled.Raise(_guid);
            else
                GameLog.Warn(TAG, $"OnEntityKilled event not assigned on {gameObject.name} — kill not broadcast");
        }

#if UNITY_EDITOR
        [ContextMenu("Generate GUID")]
        private void GenerateGUID()
        {
            _guid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
