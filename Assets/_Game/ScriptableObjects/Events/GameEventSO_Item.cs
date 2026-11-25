using Game.Inventory;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Channel carrying an <see cref="ItemSO"/> (e.g. OnEquipSwapRequested: action bar → PlayerCombat sheathe / equip / draw).</summary>
    [CreateAssetMenu(menuName = "Game/Events/Item", fileName = "OnItemEvent")]
    public class GameEventSO_Item : GameEventSO<ItemSO> { }
}
