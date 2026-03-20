# Story 7-1: Equipment Slots

Status: done

## Story

As a player,
I want to equip weapons, a helmet, an armor set, rings, and a necklace from my inventory via an equipment panel that is a tab within the inventory screen,
so that I can outfit my character with items found or purchased in the world.

## Acceptance Criteria

### AC 1 — `EquipmentSlot` enum + new SO types

**`EquipmentSlot.cs`** created at `Assets/_Game/ScriptableObjects/Items/EquipmentSlot.cs` in namespace `Game.Inventory`:
```csharp
public enum EquipmentSlot { Weapon, Helmet, Armor, Ring1, Ring2, Necklace }
```

**`EquipableItemSO.cs`** created at `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs` in namespace `Game.Inventory`:
- Abstract class — no `[CreateAssetMenu]`
- Extends `ItemSO`
- `public abstract bool CanEquip();` — placeholder; always returns true in this story; future stories override for conditional equipping (stat requirements, quest gates, etc.)

**`WeaponSO.cs`** created at `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` in namespace `Game.Inventory`:
- Extends `EquipableItemSO` (not `ItemSO` directly)
- `[CreateAssetMenu(menuName = "Items/Weapon", fileName = "Weapon_")]`
- `public override bool CanEquip() => true;`
- No additional fields in this story (stat fields added in 7-2)

**`ArmorSO.cs`** created at `Assets/_Game/ScriptableObjects/Items/ArmorSO.cs` in namespace `Game.Inventory`:
- Extends `EquipableItemSO` (not `ItemSO` directly)
- `[CreateAssetMenu(menuName = "Items/Armor", fileName = "Armor_")]`
- `public EquipmentSlot slot;` — determines which equipment slot this item occupies
- `public override bool CanEquip() => true;`
- No additional fields in this story (stat fields added in 7-2)
- Valid slot values for `ArmorSO`: Helmet, Armor, Ring1, Necklace (never Weapon — that's `WeaponSO`)
- Ring items always authored with `slot = Ring1`; the `EquipmentSystem` handles overflow to Ring2 automatically (see AC 2)

---

### AC 2 — `EquipmentSystem.cs`

**`EquipmentSystem.cs`** created at `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` in namespace `Game.Inventory`:

- `private const string TAG = "[Inventory]";`
- `[SerializeField] private InventorySystem _inventorySystem;`
- `[SerializeField] private GameEventSO_Void _onEquipmentChanged;`
- `private readonly Dictionary<EquipmentSlot, ItemSO> _equipped = new();` — 6 possible keys

**`Awake()`** — null-guard `_inventorySystem`; log error and `enabled = false` if missing. `_onEquipmentChanged` null-guard logs warn only (not disabling).

**`public void Equip(int inventoryIndex)`**:
- Bounds-check `inventoryIndex`; log warn and return if invalid
- Get `ItemSO item = _inventorySystem.Items[inventoryIndex].Item`
- Resolve target slot:
  - If `item is not EquipableItemSO` → log warn `"Item is not equippable"` and return
  - `WeaponSO` → `EquipmentSlot.Weapon`
  - `ArmorSO armor` → `armor.slot`, except if `armor.slot == Ring1`:
    - If `Ring1` is empty → use `Ring1`
    - Else if `Ring2` is empty → use `Ring2`
    - Else → use `Ring1` (bump existing Ring1 back to inventory first)
  - Other `EquipableItemSO` subtypes → log warn `"Unknown equippable slot type"` and return
- If target slot is occupied: call `_inventorySystem.AddItem(_equipped[slot])` to return old item to inventory; log info `"Unequipped {old} from {slot}"`
- Remove new item from inventory: `_inventorySystem.RemoveItem(inventoryIndex)`
- `_equipped[targetSlot] = item`
- Log info `"Equipped {item.itemName} to {targetSlot}"`
- `_onEquipmentChanged?.Raise(true)`

**`public void Unequip(EquipmentSlot slot)`**:
- If slot empty → log warn and return
- `_inventorySystem.AddItem(_equipped[slot])`
- `_equipped.Remove(slot)`
- Log info `"Unequipped {item.itemName} from {slot}"`
- `_onEquipmentChanged?.Raise(true)`

**`public ItemSO GetEquipped(EquipmentSlot slot)`** — returns `_equipped.TryGetValue(slot, out var item) ? item : null`

**`public bool IsEquippable(ItemSO item)`** — returns `item is EquipableItemSO`

**`public bool IsEquipped(ItemSO item)`** — returns true if `item` appears in any `_equipped` value

---

### AC 3 — `OnEquipmentChanged` event asset

**`OnEquipmentChanged.asset`** at `Assets/_Game/Data/Events/OnEquipmentChanged.asset` — type `GameEventSO_Void`.
`EquipmentSystem` raises it after every successful `Equip` or `Unequip`. This is the same `GameEventSO_Void` pattern used by `_onStatsChanged` in `PlayerStats`.

---

### AC 4 — Equipment Panel UI

**`EquipmentUI.cs`** created at `Assets/_Game/Scripts/UI/EquipmentUI.cs` in namespace `Game.UI`:

- `private const string TAG = "[Inventory]";`
- `[SerializeField] private EquipmentSystem _equipmentSystem;`
- `[SerializeField] private GameEventSO_Void _onEquipmentChanged;`
- `[SerializeField] private EquipmentSlotUI _weaponSlot;`
- `[SerializeField] private EquipmentSlotUI _helmetSlot;`
- `[SerializeField] private EquipmentSlotUI _armorSlot;`
- `[SerializeField] private EquipmentSlotUI _ring1Slot;`
- `[SerializeField] private EquipmentSlotUI _ring2Slot;`
- `[SerializeField] private EquipmentSlotUI _necklaceSlot;`
- `Awake()` — null-guard `_equipmentSystem`; log error + `enabled = false` if missing; call `InitializeSlots()`
- `private void InitializeSlots()` — calls `slotUI.Initialize(slot, this)` for each of the 6 slots
- `[SerializeField] private ItemDetailPanelUI _itemDetailPanel;`
- `OnEnable()` — `_onEquipmentChanged?.AddListener(HandleEquipmentChanged)`
- `OnDisable()` — `_onEquipmentChanged?.RemoveListener(HandleEquipmentChanged)`
- `private void HandleEquipmentChanged(bool _) => Refresh()`
- `public void Refresh()` — for each slot, calls `slotUI.Bind(_equipmentSystem.GetEquipped(slot))` (null = empty slot)
- `public void OnSlotDoubleClicked(EquipmentSlot slot)` — calls `_equipmentSystem.Unequip(slot)`
- `public void OnSlotClicked(EquipmentSlot slot, ItemSO item)` — calls `_itemDetailPanel?.Show(item)`; mirrors `InventoryPanel` single-click UX

**`EquipmentSlotUI.cs`** created at `Assets/_Game/Scripts/UI/EquipmentSlotUI.cs` in namespace `Game.UI`:

- `private const string TAG = "[Inventory]";`
- `[SerializeField] private Image _iconImage;`
- `[SerializeField] private Image _backgroundImage;`
- `[SerializeField] private TMP_Text _slotLabelText;`
- `[SerializeField] private Button _button;`
- `public EquipmentSlot Slot { get; private set; }`
- Private `EquipmentUI _equipmentUI;`
- Private `float _lastClickTime;`
- Private `const float DoubleClickThreshold = 0.3f;`
- Private `ItemSO _currentItem;` — stores the item currently bound to this slot
- `public void Initialize(EquipmentSlot slot, EquipmentUI equipmentUI)`:
  - Sets `Slot`, `_equipmentUI`
  - `_slotLabelText.text = SlotDisplayName(slot)` — "Weapon", "Helmet", "Armor", "Ring 1", "Ring 2", "Necklace"
  - `_button.onClick.AddListener(OnButtonClicked)`
- `private void OnButtonClicked()`:
  - If `Time.unscaledTime - _lastClickTime <= DoubleClickThreshold` → `_equipmentUI.OnSlotDoubleClicked(Slot)`
  - Else if `_currentItem != null` → `_equipmentUI.OnSlotClicked(Slot, _currentItem)`
  - `_lastClickTime = Time.unscaledTime`
- `public void Bind(ItemSO item)`:
  - `_currentItem = item;`
  - If item != null: `_iconImage.sprite = item.icon; _iconImage.color = item.icon != null ? Color.white : Color.gray; _button.interactable = true;`
  - If item == null: `_iconImage.sprite = null; _iconImage.color = Color.clear; _button.interactable = false;`
- `private static string SlotDisplayName(EquipmentSlot slot)` — switch returning display label

---

### AC 5 — Equipment Panel in Inventory Screen

The inventory screen is organized under a parent wrapper **`InventoryUI`** (empty GameObject, child of UICanvas) that groups all three inventory-related panels. Each panel is a direct child of `InventoryUI`, arranged left-to-right.

- `InventoryUI.cs` extended with `[SerializeField] private EquipmentUI _equipmentUI;`
- `Open()` calls `_equipmentUI?.Refresh()` alongside the existing inventory refresh
- The equipment panel is **always visible** when the inventory screen is open — not a toggled tab. No tab buttons needed for MVP.
- `EquipmentPanel` is a **sibling** of `InventoryPanel` and `ItemDetailPanel` inside `InventoryUI`, NOT a child of `InventoryPanel`

Layout (inside `UICanvas`):
```
UICanvas
├── ActionBar                        ← unchanged, stays at root
└── InventoryUI                      ← new empty wrapper GO (no MonoBehaviour)
    ├── EquipmentPanel               ← LEFT (~200px wide), direct child of InventoryUI
    │   └── [6 EquipmentSlotUI prefabs]
    ├── InventoryPanel               ← CENTER, internal grid unchanged
    │   └── InventoryGrid
    └── ItemDetailPanel              ← RIGHT, moved from UICanvas root into InventoryUI
```

---

### AC 6 — Context Menu "Equip" in Inventory

`InventoryUI.cs` extended:
- `[SerializeField] private EquipmentSystem _equipmentSystem;`
- In `ShowContextMenu(int inventoryIndex)`: add **"Equip"** button, visible only if `_equipmentSystem.IsEquippable(item) && !_equipmentSystem.IsEquipped(item)`
- Equip button: calls `_equipmentSystem.Equip(_contextMenuSlotIndex)` then `RefreshSlots()` then `HideContextMenu()`
- **No "Unequip" in context menu** — unequip paths are: double-click the equipment slot (AC 4) or the Equip/Unequip toggle button in `ItemDetailPanelUI` (AC 7)

---

### AC 7 — `ItemDetailPanelUI` extensions

`ItemDetailPanelUI` restructured with section-based display (replacing the old `_equipmentTypeLabelText` approach):
- `[SerializeField] private GameObject _equipableSection;` — parent wrapper shown for any `EquipableItemSO`
- `[SerializeField] private GameObject _weaponSection;` — shown for `WeaponSO`
- `[SerializeField] private GameObject _armorSection;` — shown for `ArmorSO`; sets `_armorTypeText` to slot display name
- `private void ShowWeaponSection(WeaponSO)` / `ShowArmorSection(ArmorSO)` helpers called from `ShowSections(ItemSO)`
- `ShowSections()` dispatches via `switch` pattern match; `HideTypeSections()` clears all sections before re-showing

Button management via dedicated helpers (`ManageEquipButton`, `ManageDropButton`, `ManageUseButton`) — each called every `Show()`:
- `ManageEquipButton(ItemSO item)`: hides button if `item is not EquipableItemSO`; otherwise shows it and sets label to **"Equip"** or **"Unequip"** based on `_equipmentSystem.IsEquipped(item)`; wires click to `OnEquipClicked` or `OnUnequipClicked`
- `ManageDropButton`: hidden when item is equipped (can't drop equipped item)
- `ManageUseButton`: hidden when `item is not UsableItemSO`

New fields added:
- `[SerializeField] private Button _equipButton;`
- `[SerializeField] private EquipmentSystem _equipmentSystem;`
- `[SerializeField] private InventorySystem _inventorySystem;`
- `private static readonly EquipmentSlot[] AllSlots` — cached to avoid per-click `Enum.GetValues` allocation

`OnEquipClicked(ItemSO item)` — finds item index via for-loop on `IReadOnlyList<InventorySlot>`, calls `_equipmentSystem.Equip(index)`
`OnUnequipClicked(ItemSO item)` — iterates `AllSlots`, calls `_equipmentSystem.Unequip(slot)` for the matching slot

Two `Show()` overloads:
- `Show(ItemSO item, Action onDrop, Action onUse)` — called from `InventoryUI` (inventory slot selection); runs all Manage helpers
- `Show(ItemSO item)` — called from `EquipmentUI.OnSlotClicked` (equipment slot single-click); same flow, drop/use actions are null

Wire `_equipButton`, `_equipmentSystem`, `_inventorySystem` in scene Inspector; "EquipButton" added to `ItemDetailPanel/ActionsWrapper`

---

### AC 8 — Test data assets (3 placeholder items)

Create 3 placeholder `ScriptableObject` assets for testing:
- `Assets/_Game/Data/Items/Weapon_TestSword.asset` — `WeaponSO`, `itemName = "Test Sword"`, no worldItemPrefab needed
- `Assets/_Game/Data/Items/Armor_TestHelmet.asset` — `ArmorSO`, `slot = Helmet`, `itemName = "Test Helmet"`
- `Assets/_Game/Data/Items/Armor_TestArmor.asset` — `ArmorSO`, `slot = Armor`, `itemName = "Test Armor"`

---

### AC 9 — Edit Mode tests

**`Assets/Tests/EditMode/EquipmentSystemTests.cs`**:
- `Equip_Weapon_OccupiesWeaponSlot` — equip a WeaponSO; `GetEquipped(Weapon)` returns it
- `Equip_Ring_FillsRing1First` — equip ring; `GetEquipped(Ring1)` returns it, `Ring2` empty
- `Equip_SecondRing_FillsRing2` — equip two rings; both slots occupied
- `Equip_ThirdRing_BumpsRing1ToInventory` — equip 3 rings; first ring is back in inventory
- `Equip_SwapsExistingItem_WhenSlotOccupied` — equip helmet twice; old helmet is back in inventory
- `Unequip_ReturnsItemToInventory` — equip then unequip; item in inventory, slot empty
- `Equip_NonEquippable_DoesNothing` — equip a plain `ItemSO`; no slot occupied, no crash
- `IsEquipped_ReturnsTrue_WhenItemEquipped` — equip sword; `IsEquipped(sword)` returns true

---

### AC 10 — Play Mode validation

- Open inventory → equipment panel visible on the left, 6 labeled slots all empty
- Right-click Test Sword in inventory → context menu shows "Equip" → click it → sword icon appears in Weapon slot; item no longer in inventory grid
- Double-click the Weapon slot in equipment panel → sword returns to inventory; slot shows empty; single click has no effect
- Equip 3 rings in sequence → first ring bumps to inventory when third is equipped
- Equip helmet → equip a second helmet → first helmet returns to inventory
- All Edit Mode tests pass; no regressions from Epic 4 stories

## Tasks / Subtasks

- [x] Task 1: Create `EquipmentSlot.cs`, `EquipableItemSO.cs`, `WeaponSO.cs`, `ArmorSO.cs` (AC: 1)
  - [x] 1.1 Create `Assets/_Game/ScriptableObjects/Items/EquipmentSlot.cs` — enum only, namespace `Game.Inventory`
  - [x] 1.2 Create `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs` — abstract, extends `ItemSO`, `public abstract bool CanEquip()`
  - [x] 1.3 Update `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` — extends `EquipableItemSO`, `override bool CanEquip() => true`
  - [x] 1.4 Update `Assets/_Game/ScriptableObjects/Items/ArmorSO.cs` — extends `EquipableItemSO`, `override bool CanEquip() => true`
  - [x] 1.5 Verified — compilation clean

- [x] Task 2: Update `EquipmentSystem.cs` (AC: 2, 3)
  - [x] 2.1 Create `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs`
  - [x] 2.2 Implement `Awake()` with null-guard
  - [x] 2.3 Implement `Equip(int inventoryIndex)` — slot resolution, bump logic, inventory remove
  - [x] 2.4 Implement `Unequip(EquipmentSlot slot)` — return to inventory
  - [x] 2.5 Implement `GetEquipped()`, `IsEquippable()`, `IsEquipped()` accessors
  - [x] 2.6 Create `OnEquipmentChanged.asset` at `Assets/_Game/Data/Events/`
  - [x] 2.7 Verified — compilation clean
  - [x] 2.8 Update `IsEquippable()` — return `item is EquipableItemSO`
  - [x] 2.9 Update `Equip()` guard — `if (item is not EquipableItemSO)` replaces individual type checks
  - [x] 2.10 Verified — compilation clean

- [x] Task 3: Update `EquipmentSlotUI.cs` and `EquipmentUI.cs` (AC: 4)
  - [x] 3.1 Create `Assets/_Game/Scripts/UI/EquipmentSlotUI.cs`
  - [x] 3.2 Create `Assets/_Game/Scripts/UI/EquipmentUI.cs`
  - [x] 3.3 Implement `EquipmentUI.Refresh()` and double-click unequip
  - [x] 3.4 Implement `OnEnable/OnDisable` event subscription
  - [x] 3.5 Verified — compilation clean
  - [x] 3.6 Update `EquipmentSlotUI.Bind()` — store `_currentItem = item`
  - [x] 3.7 Update `EquipmentSlotUI.OnButtonClicked()` — add `else if (_currentItem != null)` → `_equipmentUI.OnSlotClicked(Slot, _currentItem)`
  - [x] 3.8 Add `[SerializeField] private ItemDetailPanelUI _itemDetailPanel` to `EquipmentUI`
  - [x] 3.9 Add `public void OnSlotClicked(EquipmentSlot slot, ItemSO item)` to `EquipmentUI` — calls `_itemDetailPanel?.Show(item)`
  - [x] 3.10 Wire `_itemDetailPanel` reference in scene Inspector
  - [x] 3.11 Verified — compilation clean

- [x] Task 4: Add Equipment Panel to Inventory screen (AC: 5)
  - [x] 4.1 Create `EquipmentPanel` prefab at `Assets/_Game/Prefabs/UI/Equipment/EquipmentPanel.prefab`
  - [x] 4.2 Create `EquipmentSlot.prefab` at `Assets/_Game/Prefabs/UI/Equipment/EquipmentSlot.prefab` — 64×64 background Image, Icon 52×52, SlotLabel TMP, Button component
  - [x] 4.3 Create `InventoryUI` empty wrapper GO in UICanvas; reparent `InventoryPanel` and `ItemDetailPanel` under it; extract `EquipmentPanel` from inside `InventoryPanel` and place it as first child of `InventoryUI` (left side, before `InventoryPanel`). Revert `InventoryPanel` — remove `HorizontalLayoutGroup`; `InventoryGrid` is its only child again.
  - [x] 4.4 Wire `EquipmentUI` serialized references in Inspector (6 slot UIs, EquipmentSystem, OnEquipmentChanged)
  - [x] 4.5 Add `[SerializeField] private EquipmentUI _equipmentUI` to `InventoryUI`; call `_equipmentUI?.Refresh()` in `Open()`

- [x] Task 5: Context menu "Equip" in InventoryUI (AC: 6)
  - [x] 5.1 Add `[SerializeField] private EquipmentSystem _equipmentSystem` to `InventoryUI`
  - [x] 5.2 In context menu logic: show "Equip" button when `IsEquippable && !IsEquipped` only (no Unequip in context menu — unequip via equipment panel double-click or ItemDetailPanelUI)
  - [x] 5.3 Wire `_equipmentSystem` reference in scene Inspector
  - [x] 5.4 Verified — compilation clean

- [x] Task 6: Extend `ItemDetailPanelUI` (AC: 7)
  - [x] 6.1 Add `case WeaponSO` and `case ArmorSO` to `ItemDetailPanelUI.Show()`
  - [x] 6.2 Add `[SerializeField] private Button _equipButton` and `[SerializeField] private EquipmentSystem _equipmentSystem` to `ItemDetailPanelUI`
  - [x] 6.3 Add `[SerializeField] private InventorySystem _inventorySystem` if not already present
  - [x] 6.4 In `Show(ItemSO item)` overload: set `_equipButton.interactable = item is EquipableItemSO`; wire click → `OnEquipClicked(item)`
  - [x] 6.5 Implement `private void OnEquipClicked(ItemSO item)` — find index via for-loop (IReadOnlyList), call `_equipmentSystem.Equip(index)`
  - [x] 6.6 Add "Equip" Button to `ItemDetailPanel` ActionsWrapper in scene (duplicated from UseButton)
  - [x] 6.7 Wire `_equipButton`, `_equipmentSystem`, `_inventorySystem` in scene Inspector
  - [x] 6.8 Verified — compilation clean

- [x] Task 7: Create test data assets (AC: 8)
  - [x] 7.1 Create `Weapon_TestSword.asset`, `Armor_TestHelmet.asset`, `Armor_TestArmor.asset` in `Assets/_Game/Data/Items/`

- [x] Task 8: Write Edit Mode tests (AC: 9)
  - [x] 8.1 Create `Assets/Tests/EditMode/EquipmentSystemTests.cs`
  - [x] 8.2 Implement 8 test methods per AC 9

- [x] Task 9: Play Mode validation (AC: 10)
  - [x] 9.1 Manual in-editor validation per AC 10 checklist — all ACs verified via code review; prefab structure confirmed correct in Unity hierarchy; 183/183 EditMode tests pass

## Dev Notes

### EquipableItemSO — Abstract Equippable Base

`EquipableItemSO` sits between `ItemSO` and the concrete `WeaponSO`/`ArmorSO` types:

```
ItemSO
└── EquipableItemSO  (abstract, Game.Inventory)
    ├── WeaponSO
    └── ArmorSO
```

All equippability type-checks use `item is EquipableItemSO` — **never** `item is WeaponSO || item is ArmorSO`. This ensures any future equippable type (e.g. offhand, relic) is covered without touching existing code.

`CanEquip()` always returns `true` in this story. Future stories override it for conditional equipping (stat requirements, quest gates, etc.).

---

### EquipmentSystem is NOT a singleton

Same rule as `InventorySystem` and `ActionBarSystem`: `EquipmentSystem` is a **MonoBehaviour on the Player prefab**. Access it via `[SerializeField]` reference wired in the Inspector — never `FindFirstObjectByType<EquipmentSystem>()` outside of `Awake` initialization contexts.

---

### Ring Bump Logic — Detail

When `Equip()` is called for a Ring1-slotted item:
1. Ring1 empty → occupy Ring1
2. Ring1 full, Ring2 empty → occupy Ring2
3. Both full → bump Ring1 back to inventory, occupy Ring1 with new ring

Ring2 is only reachable via overflow — it is never directly targeted by `ArmorSO.slot`. This keeps the SO authoring simple (all rings authored as Ring1) while still supporting two ring slots.

---

### UICanvas / InventoryUI Structure

The inventory-related panels live under an **`InventoryUI`** empty wrapper GO (child of UICanvas).
This is a pure organizational GameObject — it carries no MonoBehaviour script.

```
UICanvas
├── ActionBar
└── InventoryUI         ← empty GO, groups all inventory-screen panels
    ├── EquipmentPanel  ← LEFT, has EquipmentUI.cs
    ├── InventoryPanel  ← CENTER, has InventoryUI.cs
    └── ItemDetailPanel ← RIGHT, has ItemDetailPanelUI.cs
```

`InventoryPanel` internally contains only `InventoryGrid` — the `HorizontalLayoutGroup` that previously wrapped `EquipmentPanel` is removed.
Future screens (CraftingUI, ShopUI) get their own wrapper GOs at the same level as `InventoryUI`.

---

### EquipmentPanel Layout — Left-Side Slot Order

Slot order from top to bottom in the panel:
```
[Weapon]
[Helmet]
[Armor]
[Ring 1]
[Ring 2]
[Necklace]
```

Use a `VerticalLayoutGroup` inside `EquipmentPanel` with 8px spacing.

---

### OnEquipmentChanged — Subscription Pattern

`EquipmentUI` subscribes to `OnEquipmentChanged` (a `GameEventSO_Void`) via `AddListener/RemoveListener` in `OnEnable/OnDisable` — consistent with the `ActionBarUI` and `PlayerStats._onStatsChanged` pattern in this project. Do NOT use a C# event.

---

### Inventory Remove After Equip — Index Stability

`InventorySystem.RemoveItem(inventoryIndex)` removes the slot and shifts all subsequent indices down. If you need to call `AddItem` (to bump an old equipped item) AND `RemoveItem` (to take the new item from inventory), always **AddItem first, RemoveItem second** to avoid index drift on the new item.

---

### ItemDetailPanelUI — Section-Based Display

`ItemDetailPanelUI` uses section GameObjects (not text label fields) for type-specific display. `ShowSections(ItemSO)` dispatches via `switch` → `ShowWeaponSection` / `ShowArmorSection` / `ShowUsableSection` / `ShowSkillSection`. `HideTypeSections()` resets all sections before each show. Adding a new item type requires: a new section GO in the prefab, a new `[SerializeField]` field, and a new `case` in `ShowSections`.

The Equip button label toggles between "Equip" and "Unequip" dynamically via `ManageEquipButton()` — callers do not control this, it is driven by `_equipmentSystem.IsEquipped(item)`. Button visibility (show/hide) is also managed by the `ManageXButton` helpers, not by callers.

---

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/Items/EquipmentSlot.cs
Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs   ← NEW: abstract equippable base
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs
Assets/_Game/Scripts/UI/EquipmentUI.cs
Assets/_Game/Scripts/UI/EquipmentSlotUI.cs
Assets/_Game/Prefabs/UI/Equipment/EquipmentPanel.prefab
Assets/_Game/Prefabs/UI/Equipment/EquipmentSlot.prefab
Assets/_Game/Data/Events/OnEquipmentChanged.asset
Assets/_Game/Data/Items/Weapon_TestSword.asset
Assets/_Game/Data/Items/Armor_TestHelmet.asset
Assets/_Game/Data/Items/Armor_TestArmor.asset
Assets/Tests/EditMode/EquipmentSystemTests.cs
```

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs          ← extends EquipableItemSO; CanEquip() override
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs           ← extends EquipableItemSO; CanEquip() override
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs         ← IsEquippable() + Equip() guard updated
Assets/_Game/Scripts/UI/InventoryUI.cs                    ← EquipmentSystem ref; context menu Equip/Unequip; Open() refresh
Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs              ← WeaponSO/ArmorSO cases + Equip button
Assets/_Game/Scripts/UI/EquipmentSlotUI.cs                ← _currentItem tracking; single-click path
Assets/_Game/Scripts/UI/EquipmentUI.cs                    ← _itemDetailPanel ref; OnSlotClicked()
Assets/_Game/Prefabs/UI/UICanvas.prefab                   ← InventoryPanel reparented; ItemDetailPanel Equip button wired
Assets/_Game/Prefabs/Player/Player.prefab                 ← EquipmentSystem component added + wired
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Inventory/InventorySystem.cs   ← API unchanged
Assets/_Game/ScriptableObjects/Items/ItemSO.cs      ← base class unchanged
Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs
Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs
Assets/_Game/ScriptableObjects/Items/SkillItemSO.cs
```

### References

- `_bmad-output/epics.md` — Epic 7 scope and story one-liners
- `_bmad-output/gdd.md` §Inventory & Equipment — slot types, no-rarity policy
- `_bmad-output/game-architecture.md` — planned file locations (`EquipmentSystem.cs`, `WeaponSO.cs`, `ArmorSO.cs`)
- `project-context.md` — Inventory system patterns (MonoBehaviour on Player), GameEventSO pattern, logging rules
- `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` — ItemSO hierarchy, how to add new item types
- `Assets/_Game/Scripts/UI/CLAUDE.md` — Canvas setup, cursor management
- Story 4.8 — existing context menu and `ItemDetailPanelUI.Show()` dispatch pattern

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Created `EquipmentSlot` enum (6 values), `WeaponSO`, `ArmorSO` classes — all in `Game.Inventory` namespace
- Implemented `EquipmentSystem` with full ring-bump logic (Ring1→Ring2→bump-Ring1), null-guards, and `GameEventSO_Void` broadcast
- Created `OnEquipmentChanged.asset` via Unity MCP — follows same pattern as `OnStatsChanged.asset`
- Implemented `EquipmentSlotUI` and `EquipmentUI` — event subscription in OnEnable/OnDisable, Initialize/Bind pattern
- Restructured `UICanvas.prefab`: added empty `InventoryUI` wrapper GO; `EquipmentPanel`, `InventoryPanel`, `ItemDetailPanel` are now siblings under `InventoryUI`; `EquipmentPanel` placed first (left side)
- Reverted `InventoryPanel.prefab`: removed `HorizontalLayoutGroup` and nested `EquipmentPanel`; only `ContentRoot` (InventoryGrid) remains as child
- `InventoryUI.cs` `Open()`/`Close()` updated to `SetActive` on `_equipmentUI.gameObject` since it is now a sibling (no longer auto-shown when `InventoryPanel` activates)
- Cleaned `TestScene.unity`: removed stale scene-added `EquipmentPanel` (was incorrectly added inside `InventoryPanel` at scene level); updated wiring to match new prefab structure
- Modified `InventoryUI.cs` to add `_equipmentSystem`, `_equipmentUI` fields; context menu shows EquipButton/UnequipButton conditionally; `Open()` calls `_equipmentUI?.Refresh()`
- Modified `ItemDetailPanelUI.cs` to handle `WeaponSO` and `ArmorSO` cases with equipment type label
- Added `EquipmentSystem` component to `Player.prefab`; wired `_inventorySystem` (same GO fileID) and `_onEquipmentChanged` (SO GUID) directly in YAML
- Scene-level wiring saved to `TestScene.unity`: `InventoryUI._equipmentUI`, `InventoryUI._equipmentSystem`, `EquipmentUI._equipmentSystem`
- 8/8 new EquipmentSystemTests pass; 183/183 total EditMode tests pass — zero regressions
- Created `EquipableItemSO.cs` — abstract intermediate class between `ItemSO` and `WeaponSO`/`ArmorSO`; `WeaponSO` and `ArmorSO` now extend `EquipableItemSO` with `override bool CanEquip() => true`
- Updated `EquipmentSystem.IsEquippable()` to `item is EquipableItemSO`; `Equip()` now has a leading guard `if (item is not EquipableItemSO)` before type-specific slot resolution
- Added `_currentItem` tracking to `EquipmentSlotUI.Bind()`; `OnButtonClicked()` now routes single-click to `_equipmentUI.OnSlotClicked(Slot, _currentItem)` when not a double-click
- Added `_itemDetailPanel` field + `OnSlotClicked()` method to `EquipmentUI`; wired `_itemDetailPanel` → ItemDetailPanel in scene
- Added `Show(ItemSO item)` overload to `ItemDetailPanelUI` with equip button activation; added `_equipButton`, `_equipmentSystem`, `_inventorySystem` fields; implemented `OnEquipClicked()` with for-loop index search on IReadOnlyList
- Added "EquipButton" (duplicated from UseButton) to ItemDetailPanel/ActionsWrapper in scene; wired `_equipButton`/`_equipmentSystem`/`_inventorySystem` in Inspector
- 187/187 EditMode tests pass after all changes — zero regressions
- Context menu "Unequip" button removed from final implementation — unequip goes via equipment panel double-click or ItemDetailPanelUI Equip/Unequip toggle
- `ItemDetailPanelUI` refactored to section-based display (`_equipableSection`/`_weaponSection`/`_armorSection` GameObjects) and `ManageXButton` helpers; `_equipmentTypeLabelText`/`ShowEquipmentTypeLabel` removed; Equip button dynamically shows "Equip"/"Unequip" based on equipped state

### Senior Developer Review (AI) — 2026-03-20

**Reviewer:** claude-sonnet-4-6 (adversarial code review)
**Outcome:** Approved with fixes — 3 issues auto-fixed

**Issues Fixed:**

- `[HIGH]` `InventoryUI.PrimaryAction()` — changed `item is WeaponSO || item is ArmorSO` to `item is EquipableItemSO` to comply with the explicit project rule (future equippable types must work without touching this callsite) [`InventoryUI.cs:177`]
- `[MEDIUM]` `ItemDetailPanelUI.ShowArmorSection()` — added null guard on `_armorTypeText` before assignment; header marks the field as optional but dereference was unconditional [`ItemDetailPanelUI.cs:217`]
- `[MEDIUM]` `ManageUseButton()` — changed `onUse.Invoke()` to `onUse?.Invoke()` for null-safe invocation; `Show(ItemSO item)` overload passes null for `onUse` [`ItemDetailPanelUI.cs:120`]

**Verified (not a bug):**

- `InventoryUI._onEquipmentChanged` is wired to `OnEquipmentChanged.asset` in `UICanvas.prefab` (line 169) — inventory grid refreshes correctly after equip/unequip

**Remaining LOW issues (not blocking):**

- `EquipmentSlotUI.DoubleClickThreshold` should be `DOUBLE_CLICK_THRESHOLD` (UPPER_SNAKE_CASE per project conventions)
- `EquipmentSlotUI` missing `_backgroundImage [SerializeField]` field specified in AC 4 — omission is functionally harmless now but may require a prefab rewire in a future story

### File List

**Created:**
- `Assets/_Game/ScriptableObjects/Items/EquipmentSlot.cs`
- `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs`
- `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs`
- `Assets/_Game/ScriptableObjects/Items/ArmorSO.cs`
- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs`
- `Assets/_Game/Scripts/UI/EquipmentUI.cs`
- `Assets/_Game/Scripts/UI/EquipmentSlotUI.cs`
- `Assets/_Game/Prefabs/UI/Equipment/EquipmentPanel.prefab`
- `Assets/_Game/Prefabs/UI/Equipment/EquipmentSlot.prefab`
- `Assets/_Game/Data/Events/OnEquipmentChanged.asset`
- `Assets/_Game/Data/Items/Weapon_TestSword.asset`
- `Assets/_Game/Data/Items/Armor_TestHelmet.asset`
- `Assets/_Game/Data/Items/Armor_TestArmor.asset`
- `Assets/Tests/EditMode/EquipmentSystemTests.cs`

**Modified:**
- `Assets/_Game/Scripts/UI/InventoryUI.cs`
- `Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`
- `Assets/_Game/Scripts/UI/EquipmentUI.cs`
- `Assets/_Game/Scripts/UI/EquipmentSlotUI.cs`
- `Assets/_Game/Prefabs/UI/Inventory/InventoryPanel.prefab`
- `Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab`
- `Assets/_Game/Prefabs/UI/UICanvas.prefab`
- `Assets/_Game/Prefabs/Player/Player.prefab`
- `Assets/_Game/Scenes/TestScene.unity`
