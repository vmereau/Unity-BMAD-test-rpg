using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// World bed. Behaviour only — the look is the prefab's <c>Model</c> child. <see cref="Interact"/> raises the sleep
    /// request; the player-side <see cref="SleepSystem"/> gates it and opens the sleep panel.
    /// </summary>
    public class Bed : MonoBehaviour, IInteractable
    {
        private const string TAG = "[Bed]";

        [SerializeField] private string _interactPrompt = "Sleep";
        [SerializeField] private string _nameTag = "Bed";
        [SerializeField] private GameEventSO_Void _onSleepRequested;

        public string InteractPrompt => _interactPrompt;
        public string NameTag => _nameTag;
        public bool CanInteract => true;

        private void Awake()
        {
            if (_onSleepRequested == null)
                GameLog.Warn(TAG, $"{gameObject.name}: no sleep event assigned — Interact() will do nothing");
        }

        public void Interact()
        {
            _onSleepRequested?.Raise(true);
        }
    }
}
