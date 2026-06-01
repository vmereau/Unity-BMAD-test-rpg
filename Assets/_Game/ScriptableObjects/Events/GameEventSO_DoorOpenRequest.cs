using Game.World;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public struct DoorOpenRequestData
    {
        public DoorInteractable door;   // runtime scene ref passed through Raise() — NOT stored in any SO asset
        public bool isLocked;
        public string requiredSkillId;
    }

    [CreateAssetMenu(menuName = "Game/Events/Door Open Request", fileName = "NewDoorOpenRequestEvent")]
    public class GameEventSO_DoorOpenRequest : GameEventSO<DoorOpenRequestData> { }
}
