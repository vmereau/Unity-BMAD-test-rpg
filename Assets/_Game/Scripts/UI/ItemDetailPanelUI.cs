using Game.Inventory;
using Game.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Manages the inventory detail panel. Call Show(item) to populate and reveal the panel;
    /// call Hide() to collapse it. Add type-specific sub-sections in the Inspector — any section
    /// left unassigned is simply skipped for that item type.
    /// </summary>
    public class ItemDetailPanelUI : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Usable Item Section (optional)")]
        [SerializeField] private GameObject _usableSection;
        [SerializeField] private TMP_Text _consumableLabel;

        [Header("Skill Item Section (optional)")]
        [SerializeField] private GameObject _skillSection;
        [SerializeField] private TMP_Text _skillNameText;
        [SerializeField] private TMP_Text _skillLpCostText;
        [SerializeField] private TMP_Text _skillDescriptionText;

        [Header("Actions")]
        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _useButton;

        /// <param name="onDrop">Called when Drop is clicked. Always provided.</param>
        /// <param name="onUse">Called when Use is clicked. Pass null to disable the Use button.</param>
        public void Show(ItemSO item, System.Action onDrop, System.Action onUse)
        {
            _icon.sprite = item.icon;
            _icon.color = item.icon != null ? Color.white : Color.gray;
            _nameText.text = item.itemName;
            _descriptionText.text = item.description;

            HideTypeSections();

            switch (item)
            {
                case SkillItemSO skillItem:
                    ShowUsableSection(skillItem);
                    ShowSkillSection(skillItem.Skill);
                    break;
            }

            BindButtons(onDrop, onUse);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void BindButtons(System.Action onDrop, System.Action onUse)
        {
            if (_dropButton != null)
            {
                _dropButton.onClick.RemoveAllListeners();
                _dropButton.onClick.AddListener(() => onDrop?.Invoke());
            }

            if (_useButton != null)
            {
                _useButton.onClick.RemoveAllListeners();
                _useButton.interactable = onUse != null;
                if (onUse != null)
                    _useButton.onClick.AddListener(() => onUse.Invoke());
            }
        }

        private void HideTypeSections()
        {
            _usableSection?.SetActive(false);
            _skillSection?.SetActive(false);
        }

        private void ShowUsableSection(UsableItemSO usable)
        {
            if (_usableSection == null) return;
            _usableSection.SetActive(true);
            if (_consumableLabel != null)
                _consumableLabel.text = usable.consumable ? "Consumable" : "Reusable";
        }

        private void ShowSkillSection(SkillSO skill)
        {
            if (_skillSection == null) return;
            _skillSection.SetActive(true);
            if (skill == null) return;
            if (_skillNameText != null) _skillNameText.text = skill.displayName;
            if (_skillLpCostText != null) _skillLpCostText.text = $"LP Cost: {skill.lpCost}";
            if (_skillDescriptionText != null) _skillDescriptionText.text = skill.description;
        }
    }
}
