---
title: 'World screens take precedence over the HUD'
type: 'bugfix'
created: '2026-10-29'
status: 'done'
baseline_commit: 'd9cc84d'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In `UICanvas`, `ContainerUI` / `NPCTradeUI` / `CookingUI` are siblings **before** `Game` (HUD), so the HUD draws on top of them and steals clicks: Buy / Sell and Take / Put (y 46–126 @1080p) sit under the ActionBar (raycast slots, y 20–84) and the stamina / XP bars; the Recipes-tab Craft button (y 80–160) sits under the health / stamina bars. `Crosshair` is also a raycast target at screen centre (blocks an 8 px spot of the inventory grid).

**Approach:** Hide the HUD (alpha 0, no raycasts) while any world screen is open, keep notification toasts visible, and move the three world screens after `Game` so they win even if the HUD shows. Crosshair stops being a raycast target.

## Boundaries & Constraints

**Always:** User decision: HUD fully hidden (health, stamina, XP, status effects, action bar, crosshair) while Trade / Container / Crafting is open; toasts (`NotificationContainer`) stay visible. HUD components keep running while hidden (no `SetActive(false)` — they would miss `GameEventSO` raises, e.g. inventory changes from a purchase). Character tabs (Inventory etc.) keep the HUD on top — the action bar is a drag target there. Multiple hiders are counted (HUD shows again only when all are closed).

**Ask First:** Hiding the HUD for dialogue, sleep or the Game Menu; changing the HUD layout.

**Never:** Referencing `NPCTradeUI` / `ContainerUI` / `CookingUI` types from the HUD code; polling in `Update`; static global state; editing the nested world-screen prefabs' children.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Open trade | Talk to a merchant → trade | HUD hidden; Buy / Sell fully clickable | N/A |
| Close trade | Esc / close | HUD visible again, values current | N/A |
| Craft | Anvil → Recipes tab, select recipe | Craft button fully visible / clickable | N/A |
| Container / loot | Open chest or corpse | HUD hidden; Take / Put clickable | N/A |
| Toast during trade | Buy item raising a toast | Toast visible | N/A |
| Inventory tab | Press I | HUD still shown; drag to action bar works | N/A |
| Hider unwired | `_hud` null on a world screen | Screen works, HUD stays visible | Warn once in `Awake` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- sibling order; `Game` (HUD root); `Game/Crosshair`; `Game/NotificationContainer` (own Canvas, sort 50)
- `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`, `ContainerUI.cs`, `Crafting/CookingUI.cs` -- `Open` / `Close` toggle their own root GO (no change needed)
- `Assets/_Game/Scripts/UI/HUD/` -- new HUD visibility scripts live here
- `Assets/Tests/EditMode/` -- `Tests.EditMode` asmdef

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/UI/HUD/HudVisibilityUI.cs` -- new, `[RequireComponent(typeof(CanvasGroup))]`; `AddHider(Object)` / `RemoveHider(Object)` over the pure `HudHiderSet` (own file, so EditMode tests stay MonoBehaviour-free); applies `alpha` 0/1, `blocksRaycasts` / `interactable`; `IsHidden` property; lazy CanvasGroup fetch -- counted hide
- [x] `Assets/_Game/Scripts/UI/HUD/HideHudWhileOpen.cs` -- new; `[SerializeField] HudVisibilityUI _hud`; `OnEnable` → `AddHider(this)`, `OnDisable` → `RemoveHider(this)`; warn in `Awake` if null -- generic, no screen-type coupling
- [x] `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- `Game`: add `CanvasGroup` + `HudVisibilityUI`; `NotificationContainer`: add `CanvasGroup` with `ignoreParentGroups = true`; add `HideHudWhileOpen` (wired to `Game`) on the `ContainerUI` / `NPCTradeUI` / `CookingUI` instance roots; move those three after `Game` (before `DialoguePanel`); `Crosshair` `raycastTarget = false` -- wiring + precedence
- [x] `Assets/Tests/EditMode/HudHiderSetTests.cs` (tests `HudHiderSet`) -- one hider hides; two hiders need both removed; double add / remove of same hider is idempotent; removing unknown hider no-op -- counted logic
- [x] `Assets/_Game/Scripts/UI/HUD/CLAUDE.md`, `Assets/_Game/Prefabs/UI/CLAUDE.md` -- document the scripts, new sibling order, the `ignoreParentGroups` toast rule -- doc
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- log the UX / readability suggestions -- user request

**Acceptance Criteria:**
- Given all EditMode tests, when run, then they pass and the console has no compile errors.
- Given a play session, when each matrix scenario is played, then behavior matches the matrix.

## Design Notes

World screens toggle their own root GameObject in `Open` / `Close`, so a component on that root sees `OnEnable` / `OnDisable` exactly when the screen shows / hides — zero changes to the three screen scripts, and future world screens opt in by adding the component. `CanvasGroup` alpha propagates into nested Canvases, hence `ignoreParentGroups` on the toast container.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all pass
- MCP `read_console` (errors) -- expected: none after compile

**Manual checks (if no CLI):**
- Play mode: open trade / container / cooking, screenshot — HUD absent, buttons clear; Inventory tab still shows the HUD.

## Suggested Review Order

**Hide mechanism**

- Entry point: world-screen root registers / unregisters with the HUD on enable / disable.
  [`HideHudWhileOpen.cs:22`](../../Assets/_Game/Scripts/UI/HUD/HideHudWhileOpen.cs#L22)

- Counted hide; logs only on real state changes.
  [`HudVisibilityUI.cs:29`](../../Assets/_Game/Scripts/UI/HUD/HudVisibilityUI.cs#L29)

- CanvasGroup alpha / raycasts / interactable; components keep running.
  [`HudVisibilityUI.cs:48`](../../Assets/_Game/Scripts/UI/HUD/HudVisibilityUI.cs#L48)

- Pure set: state-change return drives Apply.
  [`HudHiderSet.cs:16`](../../Assets/_Game/Scripts/UI/HUD/HudHiderSet.cs#L16)

**Prefab wiring**

- New sibling order, toast `ignoreParentGroups`, crosshair raycast off, hider components on screen roots.
  [`Prefabs/UI/CLAUDE.md:19`](../../Assets/_Game/Prefabs/UI/CLAUDE.md#L19)

**Peripherals**

- Counted-set edge cases.
  [`HudHiderSetTests.cs:27`](../../Assets/Tests/EditMode/HudHiderSetTests.cs#L27)
