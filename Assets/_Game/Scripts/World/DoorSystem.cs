using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Player-side resolver for locked-door open requests. Owns PlayerSkills, listens for
    /// GameEventSO_DoorOpenRequest, runs the skill gate, and (on success) unlocks + opens the
    /// specific door. Structurally identical to ContainerSystem.
    /// </summary>
    public class DoorSystem : MonoBehaviour
    {
        private const string TAG = "[DoorSystem]";

        [SerializeField] private GameEventSO_DoorOpenRequest _onDoorOpenRequested;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private GameEventSO_String _onLockUnlocked;

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

            if (data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId))
            {
                if (_playerSkills == null)
                {
                    GameLog.Warn(TAG, "PlayerSkills not assigned — cannot check door skill");
                    return;
                }
                if (!_playerSkills.HasSkill(data.requiredSkillId))
                {
                    GameLog.Info(TAG, $"Door locked — player lacks skill '{data.requiredSkillId}'");
                    return;
                }
            }

            // DoorSystem and DoorInteractable are both Game.World → same-system direct call is allowed.
            data.door.Unlock();
            // Only locked doors reach this system (unlocked ones toggle locally in DoorInteractable),
            // and we are past the skill gate — so this is the unlock success path.
            _onLockUnlocked?.Raise("Door");
            data.door.ToggleOpen();
        }
    }
}
