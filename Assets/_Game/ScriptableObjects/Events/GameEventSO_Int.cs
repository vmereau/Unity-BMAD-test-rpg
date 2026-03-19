using UnityEngine;

namespace Game.Core
{
    /// <summary>Int event channel — used for OnLevelUp, OnActAdvanced, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Int Event", fileName = "NewIntEvent")]
    public class GameEventSO_Int : GameEventSO<int> { }
}
