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
| `SceneLoader` | Scene/additive-scene loading. |
| `State/WorldStateManager` | Central runtime state singleton (on the `WorldStateManager` GO in `Core.unity`). Kill tracking + flat key/bool world-fact store backed by typed Fact SOs. `CaptureFacts` / `RestoreFacts` for save / load. |
| `Save/` | Save / load — see below. |
| `Time/GameClock` | The single world clock (GO `GameClock` in `Core.unity`) — **not** a singleton (only `WorldStateManager` / `SaveSystem` are): readers hold a serialized ref (scene override on the Core `Player` instance) or listen to its events. `Now` = absolute game seconds since day 1 00:00 (`double`), advanced by scaled `deltaTime` × `GameTime.Rate` (`TimeConfigSO.realMinutesPerGameDay`, 48 → 1 h = 2 real min), so pausing stops it; frozen while `SaveSystem.IsLoading` (restore happens before the async reload). Raises `OnHourChanged` (`GameEventSO_Int`, 0–23) per crossed hour; `bool SkipTo(target)` (false = not in the future, nothing changes) raises `OnTimeSkipped` (`GameEventSO_Float`, seconds) then one `OnHourChanged`. `Restore(double?)` (null / NaN / negative → `startHour`) raises nothing — hour listeners must re-read the time on `OnLoadFinished`. `[ContextMenu] Skip 1 hour` for dev. |
| `Time/GameTime` | **Static, pure.** Day / hour / minute derived from world seconds, `NextHour(now, hour)` (exact HH:00, target = now → +24 h), `CrossedHours` (per-frame hour events, capped at a day), `Format` ("Day 4, 06:00"), `Rate`. Tested by `GameTimeTests`. |
| `State/WorldFactPrefix` | Canonical key prefixes (`enum`). Use the typed setters (`RegisterKill`, `SetWorldEvent`, `SetDialoguePlayed`, `SetRecipeKnown`, `ClaimMealBonus`) — never build fact-key strings by hand at call sites. |

---

## Save / Load (`Save/`)

Spec: `_bmad-output/implementation-artifacts/tech-spec-save-load-system.md`.

- `SaveSystem` — singleton on the `SaveSystem` GO in `Core.unity`. Captures region object state into an in-memory
  cache (on region unload and on save), writes / reads slots, F5 / F9 (own `InputSystem_Actions` enabling only
  `QuickSave` / `QuickLoad`), autosave (region transition, quest completion — deferred until `CanSave`).
  Load = facts → `SceneLoader.ReloadRegion` → objects (`RegionLoaded`, one frame after load so `PersistentID.Start`
  ran) → player → `QuestEventsManager.ReseedState`. Raises `OnLoadStarted` / `OnLoadFinished` (menus, death screen,
  loading overlay listen) and `OnSaveNotification` (toasts). `FinishLoad` / `FailLoad` always reset
  `Time.timeScale = 1` and lock the cursor.
- `CanSave(out reason)` reasons are player-facing (F5 toast, Game Menu Save button subtitle). Loading is allowed
  while dead but not in dialogue / trade / looting.
- `SaveFileStore` — static, synchronous, never throws. Files `Application.persistentDataPath/Saves/save_{slot}.json`
  (slots `quick`, `auto`, `slot_1..10`), written to `*.tmp` then replaced. Error strings are public constants.
- World time: optional `SaveData.worldTimeSeconds` (`double?`, null in older saves → no version bump), captured in
  `BuildSaveData`, restored with the facts in `BeginLoad` (`GameClock.Restore`, no events). Save / load are refused while
  `SleepUI.IsOpen` (panel or fade).
- `SaveData` — plain classes, no Unity types (`SVector3` / `SQuaternion`); Newtonsoft Json.
- **Restore without events:** restore methods set state directly and raise only value-changed events — never
  `_onLevelUp`, `_onSkillLearned`, `_onXPGained`, `RegisterKill` / `RegisterDeath` (they grant LP / XP / toasts).
- Object identity: `SaveableObject` (`Scripts/World/CLAUDE.md`); item identity: `ItemSO.itemId` + `ItemCatalog`
  (`ScriptableObjects/Items/CLAUDE.md`).

---

## Rules (HIGH — enforced project-wide)

- **All cursor changes go through `CursorManager`.** Direct `Cursor.*` / `CursorLockMode` use anywhere else is a HIGH review finding.
- **All logging goes through `GameLog`.** Direct `Debug.Log/LogWarning/LogError` is a finding.
- **World-fact keys are built only via `WorldStateManager`'s typed setters** + `WorldFactPrefix` — no manual string concatenation.
