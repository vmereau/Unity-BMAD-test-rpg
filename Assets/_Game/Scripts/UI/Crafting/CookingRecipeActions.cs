using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// "Requires" block of the crafting Recipes tab card, nested under the card's <see cref="ItemDetailPanelUI"/>
    /// ActionsContainer: ingredients have/need, craft count and the Cook / Forge button.
    /// Paints only — <see cref="CookingUI"/> formats the texts and listens to <see cref="CraftButton"/>.
    /// </summary>
    public class CookingRecipeActions : MonoBehaviour
    {
        [SerializeField] private TMP_Text _ingredientsText;
        [Tooltip("\"Cooked N times\" (WorldStateManager craft count, saved).")]
        [SerializeField] private TMP_Text _craftCountText;
        [SerializeField] private Button _craftButton;
        [Tooltip("Label of the craft button — \"Cook\" or \"Forge\" per station kind.")]
        [SerializeField] private TMP_Text _craftLabel;

        public Button CraftButton => _craftButton;

        public void Show(string ingredients, string craftCount, string craftLabel, bool canCraft)
        {
            if (_ingredientsText != null) _ingredientsText.text = ingredients;
            if (_craftCountText != null) _craftCountText.text = craftCount;
            if (_craftLabel != null) _craftLabel.text = craftLabel;
            if (_craftButton != null) _craftButton.interactable = canCraft;
        }
    }
}
