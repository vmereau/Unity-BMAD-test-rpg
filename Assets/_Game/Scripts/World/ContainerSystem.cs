using Game.Core;
using Game.Inventory;
using Game.Progression;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    public class ContainerSystem : MonoBehaviour
    {
        private const string TAG = "[ContainerSystem]";

        [SerializeField] private GameEventSO_ContainerOpenRequest _onContainerOpenRequested;
        [SerializeField] private ContainerUI _containerUI;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private GameEventSO_String _onLockUnlocked;

        [Header("Lockpicking")]
        [SerializeField] private InventorySystem _playerInventory;
        [SerializeField] private ItemSO _lockpickItem;
        [SerializeField] private GameEventSO_String _onLockpickFailed;

        private void OnEnable()
        {
            if (_onContainerOpenRequested == null)
            {
                GameLog.Warn(TAG, "No container open event assigned — ContainerSystem will not respond");
                return;
            }
            _onContainerOpenRequested.AddListener(HandleContainerOpenRequested);
        }

        private void OnDisable()
        {
            if (_onContainerOpenRequested == null) return;
            _onContainerOpenRequested.RemoveListener(HandleContainerOpenRequested);
        }

        private void HandleContainerOpenRequested(ContainerOpenRequestData data)
        {
            if (_containerUI == null)
            {
                GameLog.Warn(TAG, "ContainerUI is not assigned — cannot open container");
                return;
            }

            if (data.isLocked)
            {
                if (data.container == null)
                {
                    GameLog.Warn(TAG, "Locked open request without a container — cannot pick lock");
                    return;
                }
                if (_playerInventory == null)
                {
                    GameLog.Warn(TAG, "Player InventorySystem not assigned — cannot open locked container");
                    return;
                }

                // The key needs neither the skill nor the lockpick wiring, so it is checked first.
                bool hasKey = data.keyItem != null && _playerInventory.CountOf(data.keyItem) > 0;
                if (!hasKey && (_playerSkills == null || _lockpickItem == null))
                {
                    GameLog.Warn(TAG, "PlayerSkills / lockpick item not assigned — cannot pick container lock");
                    return;
                }

                // Empty skill id = misconfigured lock (Lockable warns) — only the lockpick is required.
                bool hasSkill = hasKey || string.IsNullOrEmpty(data.requiredSkillId) || _playerSkills.HasSkill(data.requiredSkillId);
                int lockpicks = hasKey ? 0 : _playerInventory.CountOf(_lockpickItem);
                LockpickResult result = LockpickRules.Evaluate(hasKey, hasSkill, lockpicks);
                if (result != LockpickResult.Success && result != LockpickResult.Key)
                {
                    GameLog.Info(TAG, $"Container stays locked — {result} (skill '{data.requiredSkillId}')");
                    _onLockpickFailed?.Raise(LockpickRules.FailureMessage(result));
                    return;
                }

                // Key: nothing consumed. Pick: consume before unlocking so a failed consume never opens
                // the lock for free.
                if (result == LockpickResult.Success && !_playerInventory.TryConsume(_lockpickItem))
                {
                    GameLog.Warn(TAG, "Lockpick count passed but consume failed — container stays locked");
                    return;
                }

                // Lockpick gate passed on a locked container — this is the "unlocked" moment.
                // ContainerSystem and ContainerInteractable are both Game.World → same-system direct
                // call is allowed. Unlock() persists the open state so this fires only once per chest.
                data.container.Unlock();
                _onLockUnlocked?.Raise("Chest");
            }

            _containerUI.Open(data.containerInventory, data.takeOnly);
        }
    }
}
