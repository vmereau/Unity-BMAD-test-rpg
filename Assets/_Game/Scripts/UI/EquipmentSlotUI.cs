using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class EquipmentSlotUI : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private TMP_Text _slotLabelText;
        [SerializeField] private Button _button;

        public EquipmentSlot Slot { get; private set; }
        private EquipmentUI _equipmentUI;

        public void Initialize(EquipmentSlot slot, EquipmentUI equipmentUI)
        {
            Slot = slot;
            _equipmentUI = equipmentUI;

            if (_slotLabelText != null)
                _slotLabelText.text = SlotDisplayName(slot);

            if (_button != null)
                _button.onClick.AddListener(() => _equipmentUI.OnSlotClicked(Slot));
        }

        public void Bind(ItemSO item)
        {
            if (item != null)
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = item.icon;
                    _iconImage.color = item.icon != null ? Color.white : Color.gray;
                }
                if (_button != null) _button.interactable = true;
            }
            else
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = null;
                    _iconImage.color = Color.clear;
                }
                if (_button != null) _button.interactable = false;
            }
        }

        private static string SlotDisplayName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon   => "Weapon",
            EquipmentSlot.Helmet   => "Helmet",
            EquipmentSlot.Armor    => "Armor",
            EquipmentSlot.Ring1    => "Ring 1",
            EquipmentSlot.Ring2    => "Ring 2",
            EquipmentSlot.Necklace => "Necklace",
            _                      => slot.ToString()
        };
    }
}
