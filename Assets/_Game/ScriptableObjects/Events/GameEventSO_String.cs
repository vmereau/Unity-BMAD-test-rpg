using UnityEngine;

namespace Game.Core
{
    /// <summary>String event channel — used for OnEntityKilled, OnNPCDied, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/String Event", fileName = "NewStringEvent")]
    public class GameEventSO_String : GameEventSO<string> { }
}
