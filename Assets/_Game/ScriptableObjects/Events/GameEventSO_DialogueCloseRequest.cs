using Game.AI;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// An NPC asks to close the dialogue it opened (payload: the requesting NPC). DialogueSystem closes only when that
    /// NPC's request opened the current dialogue (spec-npc-dialogue-close-request).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Events/Dialogue Close Request", fileName = "OnDialogueCloseRequested")]
    public class GameEventSO_DialogueCloseRequest : GameEventSO<NPCPresence> { }
}
