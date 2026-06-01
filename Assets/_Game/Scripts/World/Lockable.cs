using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Reusable lock-data holder + Unlock() operation. The single source of truth for the
    /// "locked" concept, shared by DoorInteractable and ContainerInteractable via GetComponent.
    /// Absence of this component on an interactable means "never locked".
    /// Skill locks are idempotent (no consumable) — calling Unlock() when already unlocked is harmless.
    /// </summary>
    public class Lockable : MonoBehaviour
    {
        private const string TAG = "[Lockable]";

        [SerializeField] private bool _isLocked = false;
        [SerializeField] private SkillSO _requiredSkill;
        [SerializeField] private string _lockedPrompt = "Locked";

        public bool IsLocked => _isLocked;
        public string RequiredSkillId => _requiredSkill != null ? _requiredSkill.skillId : null;
        public string LockedPrompt => _lockedPrompt;

        public void Unlock()
        {
            if (!_isLocked) return;
            _isLocked = false;
            GameLog.Info(TAG, $"{gameObject.name} unlocked");
        }
    }
}
