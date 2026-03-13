using UnityEngine;
using Game.Core;

namespace Game.World
{
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        private const string TAG = "[Interaction]";

        [SerializeField] private string _promptText = "Press E to interact";

        public string InteractPrompt => _promptText;

        public void Interact()
        {
            GameLog.Info(TAG, $"Interacted with {gameObject.name}");
        }
    }
}
