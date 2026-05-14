using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.World
{
    public class ContainerInteractable : MonoBehaviour, IInteractable
    {
        private const string TAG = "[ContainerInteractable]";

        [SerializeField] private string _interactPrompt = "Open Container";
        [SerializeField] private string _nameTag = "Chest";
        [SerializeField] private GameEventSO_ContainerOpenRequest _onContainerOpenRequested;

        private InventorySystem _inventory;

        public string InteractPrompt => _interactPrompt;
        public string NameTag => _nameTag;

        private void Awake()
        {
            _inventory = GetComponent<InventorySystem>();
            if (_inventory == null)
            {
                GameLog.Error(TAG, "InventorySystem not found — ContainerInteractable disabled");
                enabled = false;
                return;
            }

            if (_onContainerOpenRequested == null)
                GameLog.Warn(TAG, "No container open event assigned — Interact() will do nothing");
        }

        public void Interact()
        {
            if (_onContainerOpenRequested == null || _inventory == null) return;
            _onContainerOpenRequested.Raise(new ContainerOpenRequestData
            {
                containerInventory = _inventory
            });
        }
    }
}
