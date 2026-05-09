using Game.Inventory;
using Game.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Display-only detail panel. Shows icon, name, description, and type-specific sections.
    /// Call Show(item) to populate and reveal; Hide() to collapse.
    /// Action buttons are managed by the owner UI via InventoryDetailActions or TradeDetailActions.
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

        [Header("Equipment Type Label (optional)")]
        [SerializeField] private GameObject _equipableSection;
        [SerializeField] private GameObject _weaponSection;
        [SerializeField] private GameObject _armorSection;
        [SerializeField] private TMP_Text _armorTypeText;
        [SerializeField] private TMP_Text _weaponDamageBonusText;
        [SerializeField] private TMP_Text _equipableStatBonusText;

        [Header("Skill Item Section (optional)")]
        [SerializeField] private GameObject _skillSection;
        [SerializeField] private TMP_Text _skillNameText;
        [SerializeField] private TMP_Text _skillLpCostText;
        [SerializeField] private TMP_Text _skillDescriptionText;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Show(ItemSO item)
        {
            ShowBaseItemDetails(item);
            ShowSections(item);

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

        private void HideTypeSections()
        {
            _usableSection?.SetActive(false);
            _skillSection?.SetActive(false);
            _armorSection?.SetActive(false);
            _weaponSection?.SetActive(false);
            _equipableSection?.SetActive(false);
        }

        private void ShowBaseItemDetails(ItemSO item)
        {
            _icon.sprite = item.icon;
            _icon.color = item.icon != null ? Color.white : Color.gray;
            _nameText.text = item.itemName;
            _descriptionText.text = item.description;
        }

        private void ShowSections(ItemSO item)
        {
            HideTypeSections();

            switch (item)
            {
                case WeaponSO weapon:
                    ShowWeaponSection(weapon);
                    break;
                case ArmorSO armor:
                    ShowArmorSection(armor);
                    break;
                case SkillItemSO skillItem:
                    ShowUsableSection(skillItem);
                    ShowSkillSection(skillItem.Skill);
                    break;
                case PotionItemSO potionItem:
                    ShowUsableSection(potionItem);
                    break;
            }
        }

        private void ShowWeaponSection(WeaponSO item)
        {
            if (_equipableSection == null) return;
            _equipableSection.SetActive(true);
            if (_weaponSection == null) return;
            _weaponSection.SetActive(true);

            if (_weaponDamageBonusText != null)
            {
                bool hasDmgBonus = item.damageBonus > 0f;
                _weaponDamageBonusText.gameObject.SetActive(hasDmgBonus);
                if (hasDmgBonus) _weaponDamageBonusText.text = $"DMG: +{item.damageBonus:F0}";
            }
            ShowEquipableStatBonuses(item);
        }

        private void ShowArmorSection(ArmorSO item)
        {
            if (_equipableSection == null) return;
            _equipableSection.SetActive(true);
            if (_armorSection == null) return;
            _armorSection.SetActive(true);
            if (_armorTypeText != null) _armorTypeText.text = ArmorSlotDisplayName(item.slot);
            ShowEquipableStatBonuses(item);
        }

        private void ShowEquipableStatBonuses(EquipableItemSO item)
        {
            if (_equipableStatBonusText == null) return;

            var sb = new System.Text.StringBuilder();
            if (item.strengthBonus    != 0) sb.AppendLine(FormatBonus("STR", item.strengthBonus));
            if (item.dexterityBonus   != 0) sb.AppendLine(FormatBonus("DEX", item.dexterityBonus));
            if (item.enduranceBonus   != 0) sb.AppendLine(FormatBonus("END", item.enduranceBonus));
            if (item.intelligenceBonus != 0) sb.AppendLine(FormatBonus("INT", item.intelligenceBonus));
            if (item.defenseBonus     != 0) sb.AppendLine(FormatBonus("DEF", item.defenseBonus));

            bool hasAny = sb.Length > 0;
            _equipableStatBonusText.gameObject.SetActive(hasAny);
            if (hasAny) _equipableStatBonusText.text = sb.ToString().TrimEnd();
        }

        private static string FormatBonus(string label, int value)
            => value > 0 ? $"{label}: +{value}" : $"{label}: {value}";

        private static string ArmorSlotDisplayName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Helmet   => "Helmet",
            EquipmentSlot.Armor    => "Armor Set",
            EquipmentSlot.Ring1    => "Ring",
            EquipmentSlot.Ring2    => "Ring",
            EquipmentSlot.Necklace => "Necklace",
            _                      => slot.ToString()
        };

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
            if (_skillNameText != null)        _skillNameText.text = skill.displayName;
            if (_skillLpCostText != null)      _skillLpCostText.text = $"LP Cost: {skill.lpCost}";
            if (_skillDescriptionText != null) _skillDescriptionText.text = skill.description;
        }
    }
}
