using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Display-only detail panel: header (icon, name, category), stat rows, description, price.
    /// All display decisions come from <see cref="ItemDetailFormatter"/>; this class only paints.
    /// Call Show(item, priceContext) to populate and reveal; Hide() to collapse.
    /// Action buttons are managed by the owner UI via the actions prefab nested under ActionsContainer.
    /// </summary>
    public class ItemDetailPanelUI : MonoBehaviour
    {
        private const string TAG = "[ItemDetailPanelUI]";

        [Header("Header")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _categoryText;

        [Header("Stats")]
        [SerializeField] private GameObject _statsSection;
        [SerializeField] private Transform _statRowsRoot;
        [SerializeField] private ItemStatRowUI _statRowPrefab;
        [SerializeField] private TMP_Text _skillDescriptionText;

        [Header("Description")]
        [SerializeField] private GameObject _descriptionSection;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Price")]
        [SerializeField] private TMP_Text _priceLabelText;
        [SerializeField] private TMP_Text _priceValueText;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.49f, 0.80f, 0.42f);
        [SerializeField] private Color _negativeColor = new(0.88f, 0.42f, 0.42f);
        [SerializeField] private Color _neutralColor = new(0.88f, 0.88f, 0.88f);
        [SerializeField] private Color _priceColor = new(0.90f, 0.76f, 0.36f);

        private readonly List<ItemStatLine> _lines = new();
        private readonly List<ItemStatRowUI> _rows = new();
        private CanvasGroup _canvasGroup;
        private bool _warnedMissingRowRefs;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Show(ItemSO item, ItemPriceContext priceContext = ItemPriceContext.Value)
        {
            if (item == null)
            {
                GameLog.Warn(TAG, "Show: item is null");
                Hide();
                return;
            }

            PaintHeader(item);
            PaintStats(item);
            PaintDescription(item);
            PaintPrice(item, priceContext);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void PaintHeader(ItemSO item)
        {
            if (_icon != null)
            {
                _icon.sprite = item.icon;
                _icon.color = item.icon != null ? Color.white : Color.gray;
            }
            if (_nameText != null) _nameText.text = item.itemName;

            if (_categoryText == null) return;
            string category = ItemDetailFormatter.GetCategory(item);
            bool hasCategory = !string.IsNullOrEmpty(category);
            _categoryText.gameObject.SetActive(hasCategory);
            if (hasCategory) _categoryText.text = category;
        }

        private void PaintStats(ItemSO item)
        {
            if (_statRowPrefab == null || _statRowsRoot == null)
            {
                if (!_warnedMissingRowRefs)
                {
                    GameLog.Warn(TAG, "PaintStats: _statRowPrefab or _statRowsRoot is not assigned; stats hidden");
                    _warnedMissingRowRefs = true;
                }
                if (_statsSection != null) _statsSection.SetActive(false);
                return;
            }

            ItemDetailFormatter.BuildStatLines(item, _lines);

            while (_rows.Count < _lines.Count)
                _rows.Add(Instantiate(_statRowPrefab, _statRowsRoot));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _lines.Count;
                _rows[i].gameObject.SetActive(used);
                if (used) _rows[i].Set(_lines[i].Label, _lines[i].Value, ColorFor(_lines[i].Polarity));
            }

            string skillDesc = ItemDetailFormatter.GetSkillDescription(item);
            if (_skillDescriptionText != null)
            {
                _skillDescriptionText.gameObject.SetActive(skillDesc != null);
                if (skillDesc != null) _skillDescriptionText.text = skillDesc;
            }

            if (_statsSection != null) _statsSection.SetActive(_lines.Count > 0 || skillDesc != null);
        }

        private void PaintDescription(ItemSO item)
        {
            bool hasDescription = !string.IsNullOrWhiteSpace(item.description);
            if (_descriptionSection != null) _descriptionSection.SetActive(hasDescription);
            if (hasDescription && _descriptionText != null) _descriptionText.text = item.description;
        }

        private void PaintPrice(ItemSO item, ItemPriceContext context)
        {
            if (_priceLabelText != null) _priceLabelText.text = ItemDetailFormatter.GetPriceLabel(context);
            if (_priceValueText == null) return;
            _priceValueText.text = $"{ItemDetailFormatter.GetPrice(item, context)}g";
            _priceValueText.color = _priceColor;
        }

        private Color ColorFor(StatPolarity polarity) => polarity switch
        {
            StatPolarity.Positive => _positiveColor,
            StatPolarity.Negative => _negativeColor,
            _ => _neutralColor
        };
    }
}
