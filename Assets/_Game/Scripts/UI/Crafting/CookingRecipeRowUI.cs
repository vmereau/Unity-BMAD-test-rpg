using Game.Crafting;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One known recipe in the cooking screen's Recipes tab: output icon + name. Clicking selects it
    /// (<see cref="CookingUI.SelectRecipe"/>); uncookable recipes are dimmed.
    /// </summary>
    public class CookingRecipeRowUI : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Color _normalColor = new Color(0f, 0f, 0f, 0.25f);
        [SerializeField] private Color _selectedColor = new Color(0.85f, 0.65f, 0.3f, 0.45f);
        [SerializeField] private Color _unavailableTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private CookingUI _owner;
        private RecipeSO _recipe;
        private Color _availableTextColor = Color.white;

        private void Awake()
        {
            _owner = GetComponentInParent<CookingUI>();
            if (_nameText != null) _availableTextColor = _nameText.color;
            _button?.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveListener(HandleClick);
        }

        public void Bind(RecipeSO recipe, bool selected, bool canCook)
        {
            _recipe = recipe;
            ItemSO output = recipe != null ? recipe.output : null;
            if (_iconImage != null)
            {
                _iconImage.sprite = output != null ? output.icon : null;
                _iconImage.color = output != null && output.icon == null ? Color.gray : Color.white; // like ItemSlotUI
                _iconImage.enabled = output != null;
            }
            if (_nameText != null)
            {
                _nameText.text = output != null ? output.itemName : recipe != null ? recipe.name : string.Empty;
                _nameText.color = canCook ? _availableTextColor : _unavailableTextColor;
            }
            if (_background != null) _background.color = selected ? _selectedColor : _normalColor;
        }

        private void HandleClick()
        {
            if (_recipe != null) _owner?.SelectRecipe(_recipe);
        }
    }
}
