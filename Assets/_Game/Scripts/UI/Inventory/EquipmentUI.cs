using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Paper-doll equipment section of the Inventory screen (spec-inventory-equipment-paper-doll; behaviour = UX
    /// EXPERIENCE.md). Owns the equipment-side selection and the valid-target highlight, turns slot clicks / drags / drops into
    /// <see cref="EquipmentSystem"/> calls, and reports selection to <see cref="InventoryUI"/>, which owns the shared detail
    /// panel and context menu. Adds no equip rules: legality comes from <see cref="EquipmentRules"/> via the system.
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private GameEventSO_Void _onEquipmentChanged;
        [Tooltip("Item grid panel: an equipment slot dragged and released over it is unequipped.")]
        [SerializeField] private RectTransform _gridDropArea;

        [SerializeField] private EquipmentSlotUI _weaponSlot;
        [SerializeField] private EquipmentSlotUI _offhandSlot;
        [SerializeField] private EquipmentSlotUI _helmetSlot;
        [SerializeField] private EquipmentSlotUI _armorSlot;
        [SerializeField] private EquipmentSlotUI _ring1Slot;
        [SerializeField] private EquipmentSlotUI _ring2Slot;
        [SerializeField] private EquipmentSlotUI _necklaceSlot;

        private InventoryUI _owner;
        private EquipmentSlotUI[] _slots;

        private EquipmentSlot? _selectedSlot;
        private ItemSO _selectedItem;          // item worn in _selectedSlot when selected; a change of item clears the selection
        private ItemSO _heldItem;              // grid selection (valid-target highlight)
        private ItemSO _gridDragItem;          // grid item being dragged
        private EquipmentSlot? _dragFromSlot;  // equipment slot being dragged
        private ItemSO _dragFromItem;          // its item when the drag began (an outside change must not be unequipped)
        private bool _dragDropHandled;

        public EquipmentSystem EquipmentSystem => _equipmentSystem;
        public EquipmentSlot? SelectedSlot => _selectedSlot;
        public ItemSO SelectedItem => _selectedItem;
        private bool IsDragging => _gridDragItem != null || _dragFromSlot.HasValue;

        private void Awake()
        {
            if (_equipmentSystem == null)
            {
                GameLog.Error(TAG, "EquipmentUI: _equipmentSystem is not assigned");
                enabled = false;
                return;
            }
            if (_gridDropArea == null)
                GameLog.Warn(TAG, "EquipmentUI: _gridDropArea is not assigned — dragging equipment onto the grid won't unequip");

            InitializeSlots();
        }

        private void OnEnable()
        {
            _onEquipmentChanged?.AddListener(HandleEquipmentChanged);
            // InventoryUI.OnScreenOpen calls Refresh before activating the panel: on the first open the slots aren't
            // initialized yet (Awake runs on activation), so repaint here as well.
            Refresh();
        }

        private void OnDisable()
        {
            _onEquipmentChanged?.RemoveListener(HandleEquipmentChanged);
            _gridDragItem = null;
            _dragFromSlot = null;
        }

        /// <summary>Called by <see cref="InventoryUI"/> in its Awake: selection and context-menu requests go to it.</summary>
        public void SetOwner(InventoryUI owner) => _owner = owner;

        private void InitializeSlots()
        {
            _slots = new[] { _weaponSlot, _offhandSlot, _helmetSlot, _armorSlot, _ring1Slot, _ring2Slot, _necklaceSlot };
            _weaponSlot?.Initialize(EquipmentSlot.Weapon, this);
            _offhandSlot?.Initialize(EquipmentSlot.Offhand, this);
            _helmetSlot?.Initialize(EquipmentSlot.Helmet, this);
            _armorSlot?.Initialize(EquipmentSlot.Armor, this);
            _ring1Slot?.Initialize(EquipmentSlot.Ring1, this);
            _ring2Slot?.Initialize(EquipmentSlot.Ring2, this);
            _necklaceSlot?.Initialize(EquipmentSlot.Necklace, this);
        }

        private void HandleEquipmentChanged(bool _) => Refresh();

        /// <summary>Repaints every slot from the system; drops the selection when its slot no longer holds the selected item.</summary>
        public void Refresh()
        {
            if (_equipmentSystem == null || _slots == null) return;

            ItemSO weapon = _equipmentSystem.GetEquipped(EquipmentSlot.Weapon);
            ItemSO offhand = _equipmentSystem.GetEquipped(EquipmentSlot.Offhand);
            bool ghost = EquipmentPanelRules.ShowsOffhandGhost(weapon, offhand);
            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                if (slot.Slot == EquipmentSlot.Offhand && ghost) slot.Bind(weapon, true);
                else slot.Bind(_equipmentSystem.GetEquipped(slot.Slot), false);
            }

            // The selection follows its item to another slot (Ring1 → Ring2); it drops when the item is no longer worn.
            if (_selectedSlot.HasValue && _equipmentSystem.GetEquipped(_selectedSlot.Value) != _selectedItem)
            {
                _selectedSlot = null;
                foreach (var pair in _equipmentSystem.Equipped)
                    if (pair.Value == _selectedItem) { _selectedSlot = pair.Key; break; }
                if (!_selectedSlot.HasValue) _selectedItem = null;
            }
            ApplyStates();
        }

        private void ApplyStates()
        {
            if (_slots == null) return;
            ItemSO target = _gridDragItem != null ? _gridDragItem : _dragFromSlot.HasValue ? null : _heldItem;
            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                bool valid = _dragFromSlot.HasValue
                    ? EquipmentPanelRules.IsValidSwapTarget(_equipmentSystem.Equipped, _dragFromSlot.Value, slot.Slot)
                    : target != null && EquipmentPanelRules.IsValidTarget(target, slot.Slot);
                bool selected = _selectedSlot.HasValue && slot.Slot == _selectedSlot.Value;
                slot.SetState(selected, valid, IsDragging);
            }
        }

        // ── Selection (shared with the grid through InventoryUI) ──────────────────────────────

        /// <summary>Grid selection changed: highlight every slot <paramref name="item"/> fits (null clears).</summary>
        public void SetHeldItem(ItemSO item)
        {
            _heldItem = item;
            ApplyStates();
        }

        /// <summary>Grid drag started / ended (null): highlight every slot the dragged item fits.</summary>
        public void SetGridDragItem(ItemSO item)
        {
            _gridDragItem = item;
            ApplyStates();
        }

        public void ClearSelection()
        {
            if (!_selectedSlot.HasValue) return;
            _selectedSlot = null;
            _selectedItem = null;
            ApplyStates();
        }

        /// <summary>Screen closed: no selection, no highlight, no drag.</summary>
        public void ClearAll()
        {
            _selectedSlot = null;
            _selectedItem = null;
            _heldItem = null;
            _gridDragItem = null;
            _dragFromSlot = null;
            _dragFromItem = null;
            ApplyStates();
        }

        private EquipmentSlot ActionSlot(EquipmentSlotUI slotUI) =>
            EquipmentPanelRules.ActionSlot(slotUI.Slot, slotUI.IsGhost);

        public void OnSlotClicked(EquipmentSlotUI slotUI)
        {
            EquipmentSlot slot = ActionSlot(slotUI);
            ItemSO item = _equipmentSystem.GetEquipped(slot);
            if (item == null)
            {
                _selectedSlot = null;
                _selectedItem = null;
                ApplyStates();
                _owner?.OnEquipmentSelectionCleared();
                return;
            }
            _selectedSlot = slot;
            _selectedItem = item;
            _owner?.OnEquipmentSelected(slot, item); // clears the grid selection → SetHeldItem(null) → ApplyStates
            ApplyStates();
        }

        public void OnSlotDoubleClicked(EquipmentSlotUI slotUI)
        {
            EquipmentSlot slot = ActionSlot(slotUI);
            if (_equipmentSystem.GetEquipped(slot) != null)
                _equipmentSystem.Unequip(slot);
        }

        public void OnSlotRightClicked(EquipmentSlotUI slotUI, Vector2 screenPos)
        {
            EquipmentSlot slot = ActionSlot(slotUI);
            if (_equipmentSystem.GetEquipped(slot) == null) return;
            _owner?.ShowEquipmentContextMenu(slot, screenPos);
        }

        // ── Drag & drop ───────────────────────────────────────────────────────────────────

        public void OnSlotDragBegin(EquipmentSlotUI slotUI)
        {
            _dragFromSlot = ActionSlot(slotUI);
            _dragFromItem = _equipmentSystem.GetEquipped(_dragFromSlot.Value);
            _dragDropHandled = false;
            _owner?.HideContextMenu();
            ApplyStates();
        }

        /// <summary>Equipment drag released: on the grid it unequips; anywhere else nothing happens.</summary>
        public void OnSlotDragEnd(EquipmentSlotUI slotUI, Vector2 screenPos)
        {
            EquipmentSlot? from = _dragFromSlot;
            ItemSO item = _dragFromItem;
            _dragFromSlot = null;
            _dragFromItem = null;
            ApplyStates();
            if (_dragDropHandled || !from.HasValue || _gridDropArea == null) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(_gridDropArea, screenPos, null)
                && item != null && _equipmentSystem.GetEquipped(from.Value) == item)
                _equipmentSystem.Unequip(from.Value);
        }

        /// <summary>Equipment slot dropped on another slot: the system swaps them if both items fit (Ring1 ↔ Ring2).</summary>
        public void OnSlotDroppedOnSlot(EquipmentSlotUI source, EquipmentSlotUI target)
        {
            if (!_dragFromSlot.HasValue) return;
            _dragDropHandled = true;
            EquipmentSlot from = _dragFromSlot.Value;
            if (from == target.Slot || _equipmentSystem.GetEquipped(from) != _dragFromItem) return;
            _equipmentSystem.SwapEquipped(from, target.Slot); // refused (logged) when the items don't fit
        }

        /// <summary>Grid item dropped on a slot: the system equips it into that slot or refuses.</summary>
        public void OnGridItemDropped(ItemSlotUI source, EquipmentSlotUI target)
        {
            _gridDragItem = null;
            ApplyStates();
            if (_owner == null || source.GetComponentInParent<InventoryUI>() != _owner) return; // only the inventory grid
            if (source.Item == null) return;
            _equipmentSystem.Equip(source.SlotIndex, target.Slot);
        }
    }
}
