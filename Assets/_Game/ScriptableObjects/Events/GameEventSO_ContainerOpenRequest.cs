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
        public bool takeOnly; // true for corpse loot (no deposit); false for world containers
    }

    [CreateAssetMenu(menuName = "Game/Events/Container Open Request", fileName = "NewContainerOpenRequestEvent")]
    public class GameEventSO_ContainerOpenRequest : GameEventSO<ContainerOpenRequestData> { }
}
