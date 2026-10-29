using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>One icon tab of <see cref="InventoryFilterBarUI"/>. View only: click / hover are forwarded to the bar.</summary>
    public class InventoryFilterTabUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _iconImage;
        [Tooltip("Shown instead of the icon when the entry has no sprite.")]
        [SerializeField] private TMP_Text _fallbackLabel;
        [SerializeField] private Color _normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color _hoverColor = new Color(1f, 0.95f, 0.85f, 1f);
        [SerializeField] private Color _activeColor = new Color(1f, 0.82f, 0.5f, 1f);
        [Tooltip("Icon alpha when the category holds no slot.")]
        [SerializeField, Range(0f, 1f)] private float _emptyAlpha = 0.35f;

        public ItemFilter Filter { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        private InventoryFilterBarUI _bar;
        private bool _isActive;
        private bool _isHovered;

        public void Init(InventoryFilterBarUI bar, ItemFilter filter, Sprite icon)
        {
            _bar = bar;
            Filter = filter;
            bool hasIcon = icon != null;
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.gameObject.SetActive(hasIcon);
            }
            if (_fallbackLabel != null)
            {
                _fallbackLabel.text = ItemFilterRules.Label(filter);
                _fallbackLabel.gameObject.SetActive(!hasIcon);
            }
            Repaint();
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            Repaint();
        }

        public void SetCount(int count)
        {
            float alpha = count > 0 || Filter == ItemFilter.All ? 1f : _emptyAlpha;
            if (_iconImage != null) _iconImage.color = new Color(1f, 1f, 1f, alpha);
            if (_fallbackLabel != null) _fallbackLabel.alpha = alpha;
        }

        private void Repaint()
        {
            if (_backgroundImage == null) return;
            _backgroundImage.color = _isActive ? _activeColor : _isHovered ? _hoverColor : _normalColor;
        }

        // Leaving the screen never delivers OnPointerExit — drop the hover state here.
        private void OnDisable()
        {
            _isHovered = false;
            Repaint();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            Repaint();
            _bar?.ShowTooltip(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            Repaint();
            _bar?.HideTooltip();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _bar?.Select(Filter);
        }
    }
}
