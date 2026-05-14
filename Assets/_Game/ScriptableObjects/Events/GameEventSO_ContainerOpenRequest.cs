using Game.Inventory;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public struct ContainerOpenRequestData
    {
        public InventorySystem containerInventory;
        public bool isLocked;
        public string requiredSkillId;
    }

    [CreateAssetMenu(menuName = "Game/Events/Container Open Request", fileName = "NewContainerOpenRequestEvent")]
    public class GameEventSO_ContainerOpenRequest : GameEventSO<ContainerOpenRequestData> { }
}
