using UnityEngine;

namespace Game.Core
{
    /// <summary>What was stolen: drives the scold line (item pickups have their own variants).</summary>
    public enum TheftKind { Other, Item, Container, Door }

    [System.Serializable]
    public struct TheftCommittedData
    {
        /// <summary>Identifies one incident — unique per play session; witnesses coordinate their chase on it.</summary>
        public int theftId;
        public Transform thief;               // runtime scene ref passed through Raise() — NOT stored in any SO asset
        public Vector3 position;              // world position of the stolen object
        public Game.NPC.NPCEntity owner;
        public TheftKind kind;
        public string objectName;
    }

    [CreateAssetMenu(menuName = "Game/Events/Theft Committed", fileName = "NewTheftCommittedEvent")]
    public class GameEventSO_TheftCommitted : GameEventSO<TheftCommittedData> { }
}
