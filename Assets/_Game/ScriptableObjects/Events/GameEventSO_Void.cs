using UnityEngine;

namespace Game.Core
{
    /// <summary>Void/signal event channel — used for OnPlayerDied, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Void Event", fileName = "NewVoidEvent")]
    public class GameEventSO_Void : GameEventSO<bool> { }
}
