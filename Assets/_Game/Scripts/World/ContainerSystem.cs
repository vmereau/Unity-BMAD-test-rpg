using Game.Core;
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

            bool wasLocked = data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId);
            if (wasLocked)
            {
                if (_playerSkills == null)
                {
                    GameLog.Warn(TAG, "PlayerSkills not assigned — cannot check lockpicking skill");
                    return;
                }
                if (!_playerSkills.HasSkill(data.requiredSkillId))
                {
                    GameLog.Info(TAG, $"Container locked — player lacks skill '{data.requiredSkillId}'");
                    return;
                }
                // Skill gate passed on a locked container — this is the "unlocked" moment.
                // ContainerSystem and ContainerInteractable are both Game.World → same-system direct
                // call is allowed. Unlock() persists the open state so this fires only once per chest.
                data.container?.Unlock();
                _onLockUnlocked?.Raise("Chest");
            }

            _containerUI.Open(data.containerInventory, data.takeOnly);
        }
    }
}
