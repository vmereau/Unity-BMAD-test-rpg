using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public struct InteractionFocusData
    {
        public Component target;  // runtime scene ref passed through Raise() — NOT stored in any SO asset; null = no focus
        public string verb;       // IInteractable.InteractPrompt
        public string name;       // IInteractable.NameTag
        public bool illegal;      // focused interaction is a theft (prompt drawn in red)
    }

    [CreateAssetMenu(menuName = "Game/Events/Interaction Focus", fileName = "NewInteractionFocusEvent")]
    public class GameEventSO_InteractionFocus : GameEventSO<InteractionFocusData> { }
}
