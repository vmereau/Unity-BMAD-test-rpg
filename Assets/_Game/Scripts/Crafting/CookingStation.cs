using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Crafting
{
    /// <summary>
    /// World crafting station: cooking (fireplace, …) or smithing (anvil) per its <see cref="CraftingKind"/>. Behaviour only — the look is the prefab's <c>Model</c> child, so any model
    /// can host it. <see cref="Interact"/> raises the open request; the player-side <see cref="CookingSystem"/> opens the screen.
    /// </summary>
    public class CookingStation : MonoBehaviour, IInteractable
    {
        private const string TAG = "[CookingStation]";

        [SerializeField] private string _interactPrompt = "Cook";
        [SerializeField] private string _nameTag = "Cooking station";
        [Tooltip("Recipes this station can cook.")]
        [SerializeField] private RecipeSO[] _recipes;
        [Tooltip("Cooking = Experiment + Recipes tabs, ingredients; Smithing = Recipes only, crafting materials.")]
        [SerializeField] private CraftingKind _kind = CraftingKind.Cooking;
        [Tooltip("Skill needed to use the station (checked by the player-side CookingSystem). Null = anyone.")]
        [SerializeField] private SkillFact _requiredSkill;
        [SerializeField] private GameEventSO_CookingStationOpenRequest _onOpenRequested;

        public string InteractPrompt => _interactPrompt;
        public string NameTag => _nameTag;
        public bool CanInteract => true;

        private void Awake()
        {
            if (_recipes == null || _recipes.Length == 0)
                GameLog.Warn(TAG, $"{gameObject.name}: no recipes assigned — nothing can be cooked here");
            else
                foreach (RecipeSO recipe in _recipes)
                {
                    if (recipe == null) continue;
                    if (!recipe.IsComplete)
                        GameLog.Warn(TAG, $"{gameObject.name}: recipe {recipe.name} needs an output and 1–{RecipeSO.MAX_INGREDIENTS} ingredients — skipped");
                    else if (recipe.knownFact == null)
                        GameLog.Warn(TAG, $"{gameObject.name}: recipe {recipe.name} has no knownFact — it can never be learned");
                    else if (System.Array.Exists(recipe.ingredients, item => !CookingRules.IsMaterialFor(_kind, item)))
                        GameLog.Warn(TAG, _kind == CraftingKind.Smithing
                            ? $"{gameObject.name}: recipe {recipe.name} uses an item without isCraftingMaterial — it won't show in the anvil grid"
                            : $"{gameObject.name}: recipe {recipe.name} uses an item without isIngredient — it can't be discovered");
                    else if (!recipe.knownFact.HasRecipeId)
                        GameLog.Warn(TAG, $"{gameObject.name}: {recipe.knownFact.name} has an empty recipe id — every such recipe shares one known key");
                }
            if (_recipes != null)
                for (int i = 0; i < _recipes.Length; i++)
                    for (int j = i + 1; j < _recipes.Length; j++)
                        if (_recipes[i] != null && _recipes[j] != null && _recipes[i] != _recipes[j]
                            && _recipes[i].knownFact != null && _recipes[i].knownFact.ToString() == _recipes[j].knownFact?.ToString())
                            GameLog.Warn(TAG, $"{gameObject.name}: {_recipes[i].name} and {_recipes[j].name} share the known key {_recipes[i].knownFact} — learning one learns both");
            if (_onOpenRequested == null)
                GameLog.Warn(TAG, $"{gameObject.name}: no open event assigned — Interact() will do nothing");
        }

        public void Interact()
        {
            if (_onOpenRequested == null) return;
            _onOpenRequested.Raise(new CookingStationOpenRequestData
            {
                recipes = _recipes,
                stationName = _nameTag,
                kind = _kind,
                requiredSkill = _requiredSkill
            });
        }
    }
}
