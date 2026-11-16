using UnityEngine;

namespace Game.Core
{
    /// <summary>What was stolen: drives the scold line (item pickups have their own variants). <c>Trespass</c> = an
    /// owner caught the player inside its <c>RestrictedArea</c> (never raised on the theft channel — pursuit kind only).</summary>
    public enum TheftKind { Other, Item, Container, Door, Trespass }

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
        /// <summary>Item thefts (pickup / harvest): the item taken. Null otherwise.</summary>
        public Game.Inventory.ItemSO item;
        /// <summary>Units of <see cref="item"/> taken (pickup 1, harvest its yield).</summary>
        public int itemCount;
        /// <summary>Containers: the opened container's inventory — every unit taken from it counts as stolen
        /// (StolenGoodsTracker). Runtime scene ref, never stored in an SO. Null otherwise.</summary>
        public Game.Inventory.InventorySystem sourceInventory;
    }

    [CreateAssetMenu(menuName = "Game/Events/Theft Committed", fileName = "NewTheftCommittedEvent")]
    public class GameEventSO_TheftCommitted : GameEventSO<TheftCommittedData> { }
}
