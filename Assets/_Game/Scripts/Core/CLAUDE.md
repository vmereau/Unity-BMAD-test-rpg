# CLAUDE.md — Assets/_Game/Scripts/Core

> Loaded when Claude accesses files in this folder. Project-wide singletons and
> utilities every other system depends on. Namespace: `Game.Core`.

---

## What's here

| File | Role |
|------|------|
| `CursorManager` | **Static.** The ONLY place allowed to touch `Cursor.lockState` / `Cursor.visible`. Use `Lock()` / `Unlock()` / `IsLocked` everywhere else. |
| `GameLog` | **Static.** Project logging wrapper. `Info`/`Warn` stripped in Release; `Error` always writes. Never call `Debug.Log` directly — always `GameLog.*` with a `TAG`. |
| `GameConstants` | **Static.** Compile-time *structural* constants only. Tunable gameplay values belong in config SOs, not here. |
| `SceneLoader` | Scene/additive-scene loading. Loads `_startupScene` in `Start` unless `GameSession.HasPendingLoad` was set at `Awake`; `LoadStartupRegion()` is the New Game boot (also the fallback when a start-menu load can't read its file). |
| `GameSession` | **Static.** Start-menu → `Core` hand-off: `RequestLoad(slot)` / `HasPendingLoad` / `TryTakePendingLoad` (taken by `SaveSystem.Start`); `SetOrigin(id)` / `TakeOriginId()` (New Game origin, taken on the new-game boot), reset on play. See `Scenes/CLAUDE.md` → Boot flow. |
| `PlayScenario` | **Static** `PlayScenarios` + enums. Editor play-scenario hand-off (EditorPrefs read under `#if UNITY_EDITOR`; key includes `Application.dataPath`; `Active` = snapshot taken once per Play at `BeforeSceneLoad`, OpenScenes outside Play / in builds / when `playModeStartScene` is null). `BeforeSceneLoad` sets the Wretch origin for the arrival scenarios; pure `ArrivalOverrideFor` / `OriginFor` (`PlayScenarioTests`). See `Scenes/CLAUDE.md` → Boot flow. |
| `State/WorldStateManager` | Central runtime state singleton (on the `WorldStateManager` GO in `Core.unity`). Kill tracking + flat key/bool world-fact store backed by typed Fact SOs. `CaptureFacts` / `RestoreFacts` for save / load. Also the game time each stored fact turned true (`TryGetFactSetTime`, `Capture` / `RestoreFactSetTimes`, read from `GameClock.Active`) → computed `ElapsedFact` (timed dialogue, `Data/Facts/CLAUDE.md`). `Update` raises `OnFactChanged` once when a pending `ElapsedFact`'s delay ends (deadlines registered by `IsElapsed`, skipped while `SaveSystem.IsLoading`). |
| `Save/` | Save / load (`SaveSystem`, `SaveFileStore`, `SaveData`, converters, `ISaveable`) — `Save/CLAUDE.md`. |
| `Time/` | World clock + day / night (`GameClock`, `GameTime`, `DayNightController`, `DayNightMath`) — `Time/CLAUDE.md`. |
| `State/WorldFactPrefix` | Canonical key prefixes (`enum`). Use the typed setters (`RegisterKill`, `SetWorldEvent`, `SetDialoguePlayed`, `SetRecipeKnown`, `ClaimMealBonus`) — never build fact-key strings by hand at call sites. |
| `GoldLootRequest` | Synchronous gold-pickup request (`OnGoldLootRequested`): player `InventorySystem.AddItem` raises it for a `GoldItemSO`, `GoldSystem` (`Scripts/Economy/`) credits it. `TryAccept()` claims it once so duplicate listeners can't pay twice; unaccepted = addition rejected. Rule: `Scripts/Inventory/CLAUDE.md` → Gold loot. |
| `GoldTheftRequest` | Synchronous gold-theft request (spec-knockout-gold-looting): `EntityBrain` (knockout loot) asks the victim's `GoldSystem` for `BaseAmount + Fraction × gold`; `TryHandle(goldBefore, taken)` fills the result once. Unhandled = no receiver. |
| `StolenGoodsLedger` | **Static, runtime only (not saved).** Per-theft-incident record (spec-theft-return-or-fight, keyed by `TheftCommittedData.theftId`) of items (`StolenItemCount`) and gold taken; written by the player's `StolenGoodsTracker` (`Scripts/World/`), read by the catching NPC. Refused incidents stay marked until recovered or the brawl ends. `ClearAll` on play. |
| `StolenGoodsRequest` | Synchronous query / take request (spec-theft-return-or-fight, `OnStolenGoodsRequested`): `EntityBrain` asks the victim how many stolen items / gold it still carries (`Take` = remove them). The victim's `InventorySystem` fills the item part, `GoldSystem` the gold part; an unhandled part = nothing available. |
| `ContainerItemTakenData` | Payload of `OnContainerItemTaken` (`sourceInventory`, `item`), raised by `ContainerUI` per item moved to the player; read by `ContainerTakeFact` and `StolenGoodsTracker` (`Scripts/World/`). |

---

## Rules (HIGH — enforced project-wide)

- **All cursor changes go through `CursorManager`.** Direct `Cursor.*` / `CursorLockMode` use anywhere else is a HIGH review finding.
- **All logging goes through `GameLog`.** Direct `Debug.Log/LogWarning/LogError` is a finding.
- **World-fact keys are built only via `WorldStateManager`'s typed setters** + `WorldFactPrefix` — no manual string concatenation.
