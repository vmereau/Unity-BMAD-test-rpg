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

            if (data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId))
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
            }

            _containerUI.Open(data.containerInventory, data.takeOnly);
        }
    }
}
