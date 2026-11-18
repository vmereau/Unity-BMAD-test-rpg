using Game.Core;
using Game.Inventory;
using Game.Progression;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Reusable lock-data holder + Unlock() operation. The single source of truth for the
    /// "locked" concept, shared by DoorInteractable and ContainerInteractable via GetComponent.
    /// Absence of this component on an interactable means "never locked".
    /// Picking a lock needs the required skill + one lockpick (LockpickRules, resolved by DoorSystem /
    /// ContainerSystem); holding the optional key item opens it outright, nothing consumed. Unlock() is idempotent — calling it when already unlocked is harmless.
    /// </summary>
    public class Lockable : MonoBehaviour
    {
        private const string TAG = "[Lockable]";

        [SerializeField] private bool _isLocked = false;
        [SerializeField] private SkillSO _requiredSkill;
        [SerializeField] private string _lockedPrompt = "Locked";
        [Tooltip("Optional. A player holding this item opens the lock with no skill and no lockpick.")]
        [SerializeField] private ItemSO _keyItem;

        public bool IsLocked => _isLocked;
        public string RequiredSkillId => _requiredSkill != null ? _requiredSkill.skillId : null;
        public string LockedPrompt => _lockedPrompt;
        public ItemSO KeyItem => _keyItem;

        private void Awake()
        {
            // Every lock must define its skill; resolvers fall back to "lockpick only" when it is missing.
            if (_isLocked && _requiredSkill == null)
                GameLog.Warn(TAG, $"{gameObject.name} is locked but has no required skill — assign one");
        }

        /// <summary>Save/load: sets the lock state directly (can re-lock). No log, no event.</summary>
        public void RestoreLocked(bool locked) => _isLocked = locked;

        public void Unlock()
        {
            if (!_isLocked) return;
            _isLocked = false;
            GameLog.Info(TAG, $"{gameObject.name} unlocked");
        }
    }
}
