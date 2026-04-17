using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class EquipmentSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _slotLabelText;
        [SerializeField] private Button _button;

        public EquipmentSlot Slot { get; private set; }
        private EquipmentUI _equipmentUI;
        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;
        private ItemSO _currentItem;

        public void Initialize(EquipmentSlot slot, EquipmentUI equipmentUI)
        {
            Slot = slot;
            _equipmentUI = equipmentUI;

            if (_slotLabelText != null)
                _slotLabelText.text = SlotDisplayName(slot);

            if (_button != null)
                _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnButtonClicked()
        {
            if (Time.unscaledTime - _lastClickTime <= DoubleClickThreshold)
                _equipmentUI.OnSlotDoubleClicked(Slot);
            else if (_currentItem != null)
                _equipmentUI.OnSlotClicked(Slot, _currentItem);
            _lastClickTime = Time.unscaledTime;
        }

        public void Bind(ItemSO item)
        {
            _currentItem = item;
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
