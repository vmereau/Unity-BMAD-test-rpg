# Project Overview
- Game Title: Unity-BMAD-test-rpg
- High-Level Concept: Action RPG with dialogue and inventory systems.
- Players: Single player
- Inspiration / Reference Games: Classic RPGs (Gothic, Witcher)
- Tone / Art Direction: Stylized 3D
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
The shop system integrates with the existing Dialogue and Inventory systems. Players interact with NPCs to open a dialogue, and through specific dialogue nodes, they can transition to a trade interface where they can buy and sell items using gold.

## Controls and Input Methods
- **Mouse**: Left-click to select items, right-click to open context menus (Buy/Sell). Hover to see basic info.
- **Keyboard**: Escape to close the trade UI and exit dialogue.

# UI
## NPCTradeUI Layout
- **Left Panel**: NPC's inventory grid.
- **Middle Panel**: Item detail view (reuses `ItemDetailPanelUI`).
- **Right Panel**: Player's inventory grid.
- **Hover Tooltip**: Floating panel showing Name, Price, and short Description.

# Key Asset & Context
- `ShopDialogueNode`: New `DialogueNode` type.
- `NPCTradeUI`: New UI component managing the trade session.
- `IItemSlotContainer`: Interface to allow `ItemSlotUI` to communicate with either `InventoryUI` or `NPCTradeUI`.
- `NPCDialogueRequestData`: Updated to include `InventorySystem` reference.
- `GoldSystem`: Used for transaction processing.

# Implementation Steps
## Part 1: Data Structures and NPC Setup
1. **Create `ShopDialogueNode`**:
   - Inherit from `DialogueNode`.
   - Add fields for NPC greeting text (though `DialogueNode.text` can be used).
   - Dependency: `DialogueNode.cs`
2. **Update `NPCDialogueRequestData`**:
   - Add `public InventorySystem npcInventory;`.
   - Dependency: `NPCDialogueRequestData.cs`
3. **Update `NPCPresence`**:
   - In `Interact()`, get the `InventorySystem` component (if present) and pass it in the request.
   - Dependency: `NPCPresence.cs`

## Part 2: UI Foundation and Refactoring
1. **Create `IItemSlotContainer`**:
   - Interface with methods: `OnSlotSelected(int index, IItemSlotContainer source)`, `OnSlotRightClicked(int index, Vector2 pos, IItemSlotContainer source)`.
2. **Refactor `ItemSlotUI`**:
   - Change `_inventoryUI` reference to `IItemSlotContainer`.
   - Update `OnPointerClick` and `OnPointerEnter/Exit` to use the interface.
   - Dependency: `ItemSlotUI.cs`
3. **Update `InventoryUI`**:
   - Implement `IItemSlotContainer`.
   - Dependency: `InventoryUI.cs`

## Part 3: NPC Trade UI Implementation
1. **Create `NPCTradeUI`**:
   - Implement `IItemSlotContainer`.
   - Add logic to open/close the panel.
   - Add Buy/Sell transaction logic using `GoldSystem`.
   - Wire `ItemDetailPanelUI` for item selection.
2. **Handle Context Menu**:
   - Create a generic context menu or adapt the one in `InventoryUI` to show "Buy" for NPC items and "Sell" for Player items.
3. **Integrate with `DialogueSystem`**:
   - In `AdvanceToNode`, if node is `ShopDialogueNode`:
     - Show text in `DialogueUI`.
     - Open `NPCTradeUI` with the captured NPC inventory.
   - Dependency: `DialogueSystem.cs`

# Verification & Testing
1. **Dialogue Transition**: Start a dialogue with an NPC, select the shop topic, and verify the trade UI opens.
2. **Buy Action**:
   - Select an item from the NPC list.
   - Right-click -> Buy.
   - Verify gold is deducted from `GoldSystem`.
   - Verify item is moved from NPC `InventorySystem` to Player `InventorySystem`.
   - Verify Buy is disabled if player has insufficient gold.
3. **Sell Action**:
   - Select an item from the Player list in the trade UI.
   - Right-click -> Sell.
   - Verify gold is added to `GoldSystem`.
   - Verify item is moved from Player to NPC.
4. **UI Closure**: Press Escape. Verify both Trade UI and Dialogue UI close, and player is no longer "Busy" (cursor locks).
