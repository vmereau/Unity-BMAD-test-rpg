using Game.Core;
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
        private const string TAG = "[Inventory]";
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
        [SerializeField] private TMP_Text _weaponDamageBonusText;  // child of WeaponSection
        [SerializeField] private TMP_Text _equipableStatBonusText; // child of EquipableSection

        [Header("Skill Item Section (optional)")]
        [SerializeField] private GameObject _skillSection;
        [SerializeField] private TMP_Text _skillNameText;
        [SerializeField] private TMP_Text _skillLpCostText;
        [SerializeField] private TMP_Text _skillDescriptionText;

        [Header("Actions")]
        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _equipButton;
        
        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private InventorySystem _inventorySystem;

        private CanvasGroup _canvasGroup;
        private static readonly EquipmentSlot[] AllSlots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <param name="onDrop">Called when Drop is clicked. Always provided.</param>
        /// <param name="onUse">Called when Use is clicked. Pass null to disable the Use button.</param>
        public void Show(ItemSO item, System.Action onDrop, System.Action onUse)
        {
            ShowBaseItemDetails(item);
            ShowSections(item);

            ManageDropButton(onDrop, item);
            ManageUseButton(onUse, item);
            ManageEquipButton(item);
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
            gameObject.SetActive(true);
        }

        /// <summary>Shows item details with an equip button (no drop/use). Used by the equipment panel single-click.</summary>
        public void Show(ItemSO item)
        {
            ShowBaseItemDetails(item);
            ShowSections(item);

            ManageDropButton(null, item);
            ManageUseButton(null, item);
            ManageEquipButton(item);

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

        private void OnEquipClicked(ItemSO item)
        {
            if (_inventorySystem == null || _equipmentSystem == null) return;
            for (int i = 0; i < _inventorySystem.Items.Count; i++)
            {
                if (_inventorySystem.Items[i].Item == item)
                {
                    _equipmentSystem.Equip(i);
                    return;
                }
            }
            GameLog.Warn(TAG, "Item not found in inventory for equip");
        }

        private void OnUnequipClicked(ItemSO item)
        {
            if (_equipmentSystem == null) return;
            foreach (var slot in AllSlots)
            {
                if (_equipmentSystem.GetEquipped(slot) == item)
                {
                    _equipmentSystem.Unequip(slot);
                    return;
                }
            }
        }

        private void ManageUseButton(System.Action onUse, ItemSO item)
        {
            if (_useButton == null) return;
            _useButton.onClick.RemoveAllListeners();
            
            if (item is not UsableItemSO)
            {
                _useButton.gameObject.SetActive(false);
                return;
            }
            
            _useButton.gameObject.SetActive(true);
            _useButton.onClick.AddListener(() => onUse?.Invoke());
        }

        private void ManageDropButton(System.Action onDrop, ItemSO item)
        {
            if (_dropButton == null) return;
            _dropButton.onClick.RemoveAllListeners();
            
            bool isEquipped = _equipmentSystem != null && _equipmentSystem.IsEquipped(item);

            if (isEquipped)
            {
                _dropButton.gameObject.SetActive(false);
                return;
            }
            
            _dropButton.gameObject.SetActive(true);
            _dropButton.onClick.AddListener(() => onDrop?.Invoke());
        }

        private void ManageEquipButton(ItemSO item)
        {
            if (_equipButton == null) return;
            _equipButton.onClick.RemoveAllListeners();

            if (item is not EquipableItemSO)
            {
                _equipButton.gameObject.SetActive(false);
                return;
            }
            
            _equipButton.gameObject.SetActive(true);
            bool isEquipped = _equipmentSystem != null && _equipmentSystem.IsEquipped(item);
            
            var labelText = _equipButton.GetComponentInChildren<TMP_Text>();
            if (labelText != null) labelText.text = isEquipped ? "Unequip" : "Equip";
            
            if (isEquipped)
                _equipButton.onClick.AddListener(() => OnUnequipClicked(item));
            else
                _equipButton.onClick.AddListener(() => OnEquipClicked(item));
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
            if (item.strengthBonus  != 0) sb.AppendLine(FormatBonus("STR", item.strengthBonus));
            if (item.dexterityBonus != 0) sb.AppendLine(FormatBonus("DEX", item.dexterityBonus));
            if (item.enduranceBonus != 0) sb.AppendLine(FormatBonus("END", item.enduranceBonus));
            if (item.intelligenceBonus != 0) sb.AppendLine(FormatBonus("INT", item.intelligenceBonus));
            if (item.defenseBonus   != 0) sb.AppendLine(FormatBonus("DEF", item.defenseBonus));

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
            _                                     => slot.ToString()
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
            if (_skillNameText != null) _skillNameText.text = skill.displayName;
            if (_skillLpCostText != null) _skillLpCostText.text = $"LP Cost: {skill.lpCost}";
            if (_skillDescriptionText != null) _skillDescriptionText.text = skill.description;
        }
    }
}
