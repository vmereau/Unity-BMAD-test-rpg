using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.UI
{
    public class EquipmentUI : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private GameEventSO_Void _onEquipmentChanged;

        [SerializeField] private ItemDetailPanelUI _itemDetailPanel;

        [SerializeField] private EquipmentSlotUI _weaponSlot;
        [SerializeField] private EquipmentSlotUI _helmetSlot;
        [SerializeField] private EquipmentSlotUI _armorSlot;
        [SerializeField] private EquipmentSlotUI _ring1Slot;
        [SerializeField] private EquipmentSlotUI _ring2Slot;
        [SerializeField] private EquipmentSlotUI _necklaceSlot;

        private void Awake()
        {
            if (_equipmentSystem == null)
            {
                GameLog.Error(TAG, "EquipmentUI: _equipmentSystem is not assigned");
                enabled = false;
                return;
            }

            InitializeSlots();
        }

        private void OnEnable()
        {
            _onEquipmentChanged?.AddListener(HandleEquipmentChanged);
        }

        private void OnDisable()
        {
            _onEquipmentChanged?.RemoveListener(HandleEquipmentChanged);
        }

        private void InitializeSlots()
        {
            _weaponSlot?.Initialize(EquipmentSlot.Weapon, this);
            _helmetSlot?.Initialize(EquipmentSlot.Helmet, this);
            _armorSlot?.Initialize(EquipmentSlot.Armor, this);
            _ring1Slot?.Initialize(EquipmentSlot.Ring1, this);
            _ring2Slot?.Initialize(EquipmentSlot.Ring2, this);
            _necklaceSlot?.Initialize(EquipmentSlot.Necklace, this);
        }

        private void HandleEquipmentChanged(bool _) => Refresh();

        public void Refresh()
        {
            if (_equipmentSystem == null) return;
            _weaponSlot?.Bind(_equipmentSystem.GetEquipped(EquipmentSlot.Weapon));
            _helmetSlot?.Bind(_equipmentSystem.GetEquipped(EquipmentSlot.Helmet));
            _armorSlot?.Bind(_equipmentSystem.GetEquipped(EquipmentSlot.Armor));
            _ring1Slot?.Bind(_equipmentSystem.GetEquipped(EquipmentSlot.Ring1));
            _ring2Slot?.Bind(_equipmentSystem.GetEquipped(EquipmentSlot.Ring2));
            _necklaceSlot?.Bind(_equipmentSystem.GetEquipped(EquipmentSlot.Necklace));
        }

        public void OnSlotDoubleClicked(EquipmentSlot slot)
        {
            _equipmentSystem.Unequip(slot);
        }

        public void OnSlotClicked(EquipmentSlot slot, ItemSO item)
        {
            _itemDetailPanel?.Show(item);
        }
    }
}
