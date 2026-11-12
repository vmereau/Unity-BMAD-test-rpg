using Game.Inventory;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Channel carrying an <see cref="InventorySystem"/> (e.g. OnLootSourceLost: a looted body got back up).</summary>
    [CreateAssetMenu(menuName = "Game/Events/Inventory", fileName = "OnInventoryEvent")]
    public class GameEventSO_Inventory : GameEventSO<InventorySystem> { }
}
