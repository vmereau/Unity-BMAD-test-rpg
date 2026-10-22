using Game.Core;
using Game.Inventory;
using Game.Progression;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Player-side resolver for locked-door open requests. Owns PlayerSkills + the player
    /// InventorySystem, listens for GameEventSO_DoorOpenRequest, runs LockpickRules (skill + one
    /// lockpick), and on success consumes a lockpick, unlocks + opens the specific door.
    /// Structurally identical to ContainerSystem.
    /// </summary>
    public class DoorSystem : MonoBehaviour
    {
        private const string TAG = "[DoorSystem]";

        [SerializeField] private GameEventSO_DoorOpenRequest _onDoorOpenRequested;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private GameEventSO_String _onLockUnlocked;

        [Header("Lockpicking")]
        [SerializeField] private InventorySystem _playerInventory;
        [SerializeField] private ItemSO _lockpickItem;
        [SerializeField] private GameEventSO_String _onLockpickFailed;

        private void OnEnable()
        {
            if (_onDoorOpenRequested == null)
            {
                GameLog.Warn(TAG, "No door open event assigned — DoorSystem will not respond");
                return;
            }
            _onDoorOpenRequested.AddListener(HandleDoorOpenRequested);
        }

        private void OnDisable()
        {
            if (_onDoorOpenRequested == null) return;
            _onDoorOpenRequested.RemoveListener(HandleDoorOpenRequested);
        }

        private void HandleDoorOpenRequested(DoorOpenRequestData data)
        {
            if (data.door == null) return;

            if (data.isLocked)
            {
                if (_playerSkills == null || _playerInventory == null || _lockpickItem == null)
                {
                    GameLog.Warn(TAG, "PlayerSkills / player InventorySystem / lockpick item not assigned — cannot pick door lock");
                    return;
                }

                // Empty skill id = misconfigured lock (Lockable warns) — only the lockpick is required.
                bool hasSkill = string.IsNullOrEmpty(data.requiredSkillId) || _playerSkills.HasSkill(data.requiredSkillId);
                LockpickResult result = LockpickRules.Evaluate(hasSkill, _playerInventory.CountOf(_lockpickItem));
                if (result != LockpickResult.Success)
                {
                    GameLog.Info(TAG, $"Door stays locked — {result} (skill '{data.requiredSkillId}')");
                    _onLockpickFailed?.Raise(LockpickRules.FailureMessage(result));
                    return;
                }

                // Consume before unlocking so a failed consume never opens the lock for free.
                if (!_playerInventory.TryConsume(_lockpickItem))
                {
                    GameLog.Warn(TAG, "Lockpick count passed but consume failed — door stays locked");
                    return;
                }

                // DoorSystem and DoorInteractable are both Game.World → same-system direct call is allowed.
                data.door.Unlock();
                _onLockUnlocked?.Raise("Door");
            }

            data.door.ToggleOpen();
        }
    }
}
