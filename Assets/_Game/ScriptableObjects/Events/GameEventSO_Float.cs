using UnityEngine;

namespace Game.Core
{
    /// <summary>Float event channel — used for OnPlayerHealthChanged, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Float Event", fileName = "NewFloatEvent")]
    public class GameEventSO_Float : GameEventSO<float> { }
}
