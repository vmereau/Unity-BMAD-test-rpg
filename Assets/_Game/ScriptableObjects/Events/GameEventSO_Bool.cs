using UnityEngine;

namespace Game.Core
{
    /// <summary>Bool event channel — used for OnDayNightChanged, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Bool Event", fileName = "NewBoolEvent")]
    public class GameEventSO_Bool : GameEventSO<bool> { }
}
