using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Crafting
{
    /// <summary>
    /// World cooking station (fireplace, …). Behaviour only — the look is the prefab's <c>Model</c> child, so any model
    /// can host it. <see cref="Interact"/> raises the open request; the player-side <see cref="CookingSystem"/> opens the screen.
    /// </summary>
    public class CookingStation : MonoBehaviour, IInteractable
    {
        private const string TAG = "[CookingStation]";

        [SerializeField] private string _interactPrompt = "Cook";
        [SerializeField] private string _nameTag = "Cooking station";
        [Tooltip("Recipes this station can cook.")]
        [SerializeField] private RecipeSO[] _recipes;
        [SerializeField] private GameEventSO_CookingStationOpenRequest _onOpenRequested;

        public string InteractPrompt => _interactPrompt;
        public string NameTag => _nameTag;
        public bool CanInteract => true;

        private void Awake()
        {
            if (_recipes == null || _recipes.Length == 0)
                GameLog.Warn(TAG, $"{gameObject.name}: no recipes assigned — nothing can be cooked here");
            if (_onOpenRequested == null)
                GameLog.Warn(TAG, $"{gameObject.name}: no open event assigned — Interact() will do nothing");
        }

        public void Interact()
        {
            if (_onOpenRequested == null) return;
            _onOpenRequested.Raise(new CookingStationOpenRequestData
            {
                recipes = _recipes,
                stationName = _nameTag
            });
        }
    }
}
