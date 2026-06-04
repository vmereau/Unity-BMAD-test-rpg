using Game.Inventory;
using Game.World;
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
        public ContainerInteractable container; // runtime scene ref passed through Raise() — NOT stored in any SO asset; null for corpse loot
    }

    [CreateAssetMenu(menuName = "Game/Events/Container Open Request", fileName = "NewContainerOpenRequestEvent")]
    public class GameEventSO_ContainerOpenRequest : GameEventSO<ContainerOpenRequestData> { }
}
