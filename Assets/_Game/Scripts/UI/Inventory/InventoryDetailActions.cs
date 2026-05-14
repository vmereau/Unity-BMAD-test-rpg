using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class InventoryDetailActions : MonoBehaviour
    {
        private const string TAG = "[InventoryDetailActions]";

        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _equipButton;

        private static readonly EquipmentSlot[] AllSlots =
            (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        public void Bind(InventoryUI owner, int slotIndex, ItemSO item, EquipmentSystem equipmentSystem)
        {
            if (item == null)  { GameLog.Warn(TAG, "Bind: item is null");  return; }
            if (owner == null) { GameLog.Warn(TAG, "Bind: owner is null"); return; }
            ManageDropButton(owner, slotIndex, item, equipmentSystem);
            ManageUseButton(owner, slotIndex, item);
            ManageEquipButton(item, equipmentSystem);
        }

        public void BindForEquipmentSlot(ItemSO item, EquipmentSystem equipmentSystem)
        {
            if (item == null) { GameLog.Warn(TAG, "BindForEquipmentSlot: item is null"); return; }
            if (_dropButton != null) _dropButton.gameObject.SetActive(false);
            if (_useButton  != null) _useButton.gameObject.SetActive(false);
            ManageEquipButton(item, equipmentSystem);
        }

        private void ManageDropButton(InventoryUI owner, int slotIndex, ItemSO item, EquipmentSystem es)
        {
            if (_dropButton == null) return;
            _dropButton.onClick.RemoveAllListeners();

            _dropButton.gameObject.SetActive(true);
            _dropButton.onClick.AddListener(() => owner.DropItem(slotIndex));
        }

        private void ManageUseButton(InventoryUI owner, int slotIndex, ItemSO item)
        {
            if (_useButton == null) return;
            _useButton.onClick.RemoveAllListeners();

            if (item is not UsableItemSO) { _useButton.gameObject.SetActive(false); return; }

            _useButton.gameObject.SetActive(true);
            _useButton.onClick.AddListener(() => owner.UseItem(slotIndex));
        }

        private void ManageEquipButton(ItemSO item, EquipmentSystem es)
        {
            if (_equipButton == null) return;
            _equipButton.onClick.RemoveAllListeners();

            if (item is not EquipableItemSO) { _equipButton.gameObject.SetActive(false); return; }

            _equipButton.gameObject.SetActive(true);
            bool isEquipped = es != null && es.IsEquipped(item);

            var label = _equipButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = isEquipped ? "Unequip" : "Equip";

            if (isEquipped)
                _equipButton.onClick.AddListener(() => OnUnequipClicked(item, es));
            else
                _equipButton.onClick.AddListener(() => OnEquipClicked(item, es));
        }

        private void OnEquipClicked(ItemSO item, EquipmentSystem es)
        {
            if (es == null) { GameLog.Warn(TAG, "OnEquipClicked: equipmentSystem is null"); return; }
            es.Equip(item);
        }

        private void OnUnequipClicked(ItemSO item, EquipmentSystem es)
        {
            if (es == null) return;
            foreach (var slot in AllSlots)
            {
                if (es.GetEquipped(slot) == item) { es.Unequip(slot); return; }
            }
        }
    }
}
