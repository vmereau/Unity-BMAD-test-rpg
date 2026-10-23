---
title: 'Cooking stations (healing plant → health potion)'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_commit: '64f0ce7'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/UI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/UI/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Harvested ingredients (the Healing Plant) have no use beyond eating them raw. There is no way to turn one item into another.

**Approach:** Add a `RecipeSO` (one input item → one output item), a `CookingStation` interactable that holds a `RecipeSO[]`, and a cooking screen. The screen shows the player inventory and one ingredient slot (filled by drag & drop or double-click), previews the recipe output, and has a Cook button. The station prefab is a behaviour-only base with a swappable model child. The first look is `SM_Fireplace2`, replacing the raw fireplace in StartingTown.

## Boundaries & Constraints

**Always:**
- Prompt "Cook", name tag "Cooking station"; standard outline (`InteractionHighlight`); root on layer Interactable (8).
- The ingredient slot is a **reference** (like the action bar): the item stays in the inventory until Cook. Closing the screen never loses anything, and the slot is empty on every open.
- Cook = consume 1 input, add 1 output, refresh. When the input count reaches 0, the slot clears.
- No matching recipe → Cook disabled + "No recipe" hint. Empty slot → Cook disabled + "Place an ingredient" hint.
- The cooking screen follows the `ContainerUI` lifecycle: cursor via `CursorManager`, Esc / player death closes it, input disposed in `OnDestroy`. Saving and loading are refused while it is open.
- Station look = a variant of `CookingStation_Base` with the model nested under its `Model` child. No code or component change per look.
- Cross-system flow via a `GameEventSO` (station → player-side `CookingSystem` → `CookingUI`), mirroring containers.

**Ask First:** Changing `ItemSlotUI` / `ActionBarSlotUI` drop behaviour beyond ignoring the ingredient slot, the save format, or `InventorySystem`.

**Never:** Multi-ingredient recipes, quantities > 1, cooking time/animation/VFX, skill gates, XP, station ownership/theft, station save state, a global recipe catalog, new UI sprites or `generate_image`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Cook | 3 Healing Plants dragged to the slot, press Cook | 2 plants + 1 Health Potion; slot keeps plants (count 2) | N/A |
| Last one | 1 plant in the slot, Cook | 0 plants, +1 potion, slot clears, Cook disabled | N/A |
| No recipe | Lockpick in the slot | Preview "No recipe", Cook disabled | N/A |
| Clear slot | Right-click the slot or drag it out | Slot empty, inventory untouched | N/A |
| Output refused | `AddItem(output)` returns false | Input refunded, nothing gained | `GameLog.Warn` |
| Misconfigured station | `_recipes` empty / null entries | Opens, every ingredient → "No recipe" | Null recipes skipped; `GameLog.Warn` in `Awake` if empty |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/ContainerInteractable.cs`, `ContainerSystem.cs` -- pattern: interactable raises an open event; the player-side system opens the UI
- `Assets/_Game/ScriptableObjects/Events/GameEventSO_ContainerOpenRequest.cs` -- event struct + SO pattern (`Game.Core`)
- `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs`, `ContainerSideUI.cs` -- screen lifecycle, grid refresh, `IItemSlotContainer` adapter
- `Assets/_Game/Scripts/UI/HUD/ActionBarSlotUI.cs` -- reference-slot drag & drop pattern (drop from `ItemSlotUI`, drag-out clears)
- `Assets/_Game/Prefabs/UI/Items/ItemSlot.prefab`, `Prefabs/UI/Common/ActionButton.prefab` -- reused visuals
- `Assets/_Game/Scripts/Core/Save/SaveSystem.cs` -- `_containerUI.IsOpen` save/load gate (lines 144, 347)
- `Assets/_Game/Prefabs/Player/Player.prefab` -- hosts `ContainerSystem` and `UICanvas`
- `Assets/Hivemind/HDRP(Default)/MedievalFantasyVillage/Art/Prefabs/SM_Fireplace2.prefab` -- model (LODGroup, MeshCollider, URP-compatible materials)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Crafting/RecipeSO.cs` -- `Game.Crafting` SO: `input`, `output` (`ItemSO`); `[CreateAssetMenu]` -- recipe data
- [x] `Assets/_Game/Scripts/Crafting/CookingRules.cs` -- static pure `FindRecipe(IReadOnlyList<RecipeSO>, ItemSO)` (skips null/incomplete) + `Evaluate(recipe, inputCount)` → preview/hint state -- testable logic
- [x] `Assets/_Game/ScriptableObjects/Events/GameEventSO_CookingStationOpenRequest.cs` + `Data/Events/OnCookingStationOpenRequested.asset` -- data `{ RecipeSO[] recipes, string stationName }` -- open channel
- [x] `Assets/_Game/Scripts/Crafting/CookingStation.cs` -- `IInteractable`: `_recipes`, `_nameTag`, event; raises on `Interact()` -- station behaviour
- [x] `Assets/_Game/Scripts/Crafting/CookingSystem.cs` -- player-side listener → `CookingUI.Open(recipes, name)`; add to `Player.prefab` -- mirrors `ContainerSystem`
- [x] `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs`, `CookingInventorySideUI.cs`, `CookingIngredientSlotUI.cs` -- screen, inventory-grid adapter (double-click = set ingredient, swap = no-op), reference slot (`IDropHandler` from `ItemSlotUI`, right-click / drag-out clears) -- cooking screen
- [x] `Assets/_Game/Prefabs/UI/Cooking/CookingUI.prefab` -- inactive, nested in `UICanvas` next to `ContainerUI`: player grid + station name, ingredient slot, output preview (icon + name/hint), Cook button -- UI
- [x] `Assets/_Game/Scripts/Core/Save/SaveSystem.cs` -- refuse save/load while `CookingUI.IsOpen` ("Can't save/load while cooking") -- no mid-cook saves
- [x] `Assets/_Game/Data/Recipes/Recipe_HealthPotion.asset` -- `Item_HealingPlant` → `Item_Health_Potion` -- first recipe
- [x] `Assets/_Game/Prefabs/World/CookingStations/CookingStation_Base.prefab` + `CookingStation_Fireplace.prefab` (variant, `SM_Fireplace2` under `Model`, root BoxCollider fit to the model) -- base `_recipes` = [Recipe_HealthPotion] -- multi-look stations
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- replace the raw `SM_Fireplace2` with `CookingStation_Fireplace` at the same transform -- playable test
- [x] `Assets/Tests/EditMode/CookingRulesTests.cs` -- matrix rows for `FindRecipe` / `Evaluate` -- regressions
- [x] `Scripts/Crafting/CLAUDE.md` (new), `Scripts/UI/CLAUDE.md`, `Prefabs/CLAUDE.md`, `Prefabs/UI/CLAUDE.md`, root `CLAUDE.md` index -- document the system and the prefab contract -- other agents

**Acceptance Criteria:**
- Given the fireplace in StartingTown, when the player looks at it, then it gets the outline and the card reads "Cook" / "Cooking station".
- Given the cooking screen is open, when Esc is pressed, then it closes, the cursor re-locks, and the Game Menu does not open.
- Given a new station look, when it is authored, then only a new variant of `CookingStation_Base` with a different `Model` child is needed.

## Design Notes

The prefab is split in two. The root (behaviour, interaction collider, highlight) owns everything the player interacts with. `Model` is pure visuals, so swapping looks never touches behaviour. `InteractionHighlight` collects child renderers in `Awake`, so any model is outlined automatically. The model keeps its own MeshCollider (Default layer) for physical blocking. The root trigger BoxCollider on layer 8 is the interaction target.

The ingredient slot stores an `ItemSO` (not an index), so inventory reorders or count changes after a cook never point it at the wrong item. Each refresh re-reads `CountOf(item)`.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass

**Manual checks (if no CLI):**
- Play StartingTown with Healing Plants: drag one to the slot, cook, check the matrix rows, Esc, try F5 while open.

## Suggested Review Order

**Cook flow**

- Entry point: consume 1 input, add 1 output, refund on failure, notify the action bar.
  [`CookingUI.cs:144`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L144)

- Slot state → preview / hint / Cook button; slot clears when the count hits 0.
  [`CookingUI.cs:193`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L193)

- Pure rules: first complete matching recipe, Empty / NoRecipe / Ready.
  [`CookingRules.cs:31`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L31)

**Ingredient reference slot**

- Drop from the inventory grid or the action bar (marked handled so the hotkey survives).
  [`CookingIngredientSlotUI.cs:49`](../../Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs#L49)

- Drag out anywhere = clear the reference; the inventory is untouched.
  [`CookingIngredientSlotUI.cs:104`](../../Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs#L104)

- Double-click reads the clicked slot's item, so a lagging grid can't select another item.
  [`CookingUI.cs:130`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L130)

- Action bar hotkeys used while open → the grid and count follow.
  [`CookingUI.cs:87`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L87)

**Station → screen routing**

- Station raises its recipes + name; behaviour only, the look is the `Model` child.
  [`CookingStation.cs:33`](../../Assets/_Game/Scripts/Crafting/CookingStation.cs#L33)

- Player-side listener opens the screen (mirrors `ContainerSystem`).
  [`CookingSystem.cs:34`](../../Assets/_Game/Scripts/Crafting/CookingSystem.cs#L34)

- Save / load are refused while cooking.
  [`SaveSystem.cs:146`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L146)

**Peripherals**

- Recipe data: one input → one output.
  [`RecipeSO.cs:11`](../../Assets/_Game/ScriptableObjects/Crafting/RecipeSO.cs#L11)

- Pure rule tests (lookup, null / incomplete recipes, slot states).
  [`CookingRulesTests.cs:1`](../../Assets/Tests/EditMode/CookingRulesTests.cs#L1)

- Station prefab contract (base + `Model` variant).
  [`CLAUDE.md:50`](../../Assets/_Game/Prefabs/CLAUDE.md#L50)
