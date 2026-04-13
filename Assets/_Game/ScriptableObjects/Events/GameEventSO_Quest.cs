using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    /// <summary>Typed event channel for quest state transitions (started, completed, failed).</summary>
    [CreateAssetMenu(menuName = "Game/Events/Quest Event", fileName = "OnQuest")]
    public class GameEventSO_Quest : GameEventSO<QuestSO> { }
}
