using Game.Core;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    public class ContainerSystem : MonoBehaviour
    {
        private const string TAG = "[ContainerSystem]";

        [SerializeField] private GameEventSO_ContainerOpenRequest _onContainerOpenRequested;
        [SerializeField] private ContainerUI _containerUI;

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
            _containerUI.Open(data.containerInventory);
        }
    }
}
