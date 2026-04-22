using UnityEngine;

namespace Game.Core
{
    /// <summary>World fact event channel — fires whenever a world fact key/value changes.</summary>
    [CreateAssetMenu(menuName = "Game/Events/WorldFact Event", fileName = "NewWorldFactEvent")]
    public class GameEventSO_Fact : GameEventSO<FactData> { }
}
