# CLAUDE.md — Assets/_Game/Scripts/Core/Save

> Loaded when Claude accesses files in this folder. Save / load: `SaveSystem`, `SaveFileStore`, `SaveData`,
> `ISaveable`, `ItemStackConverter`, `EquipmentSaveConverter`. Namespace: `Game.Core`. Core-wide rules: `Scripts/Core/CLAUDE.md`.

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
- **New-game origin:** `HandleRegionLoaded(isStartup)` (not loading) resolves `GameSession.TakeOriginId()` through
  `_originCatalog` (`Data/Origins/OriginCatalog`, unknown / none → Wretch) and calls `PlayerSaveAdapter.ApplyOrigin`
  (`PlayerStats.AddBaseStat`, `PlayerSkills.GrantSkillSilently`, `WorldFact_Origin_<Id>` + `_hadBelongingsFact`) **before**
  `BuildSaveData` — so Restart keeps it. This one gate covers menu New Game, Play in Core, failed menu loads and dev skips.
- `SaveSystem.BlockSaving(owner)` / `UnblockSaving(owner)` (static, reset on play): `CanSave` refuses ("Can't save right
  now") while any owner holds a block — for region-scene story sequences that can't be saved mid-way (`ArrivalSequence`).
  Loading stays allowed; the owner must unblock from `OnDisable` too.
- `CanSave(out reason)` reasons are player-facing (F5 toast, Game Menu Save button subtitle). Loading is allowed
  while dead but not in dialogue / trade / looting.
- `SaveFileStore` — static, synchronous, never throws. Files `Application.persistentDataPath/Saves/save_{slot}.json`
  (slots `quick`, `auto`, `slot_1..10`), written to `*.tmp` then replaced. Error strings are public constants.
- World time: optional `SaveData.worldTimeSeconds` (`double?`, null in older saves → no version bump), captured in
  `BuildSaveData`, restored with the facts in `BeginLoad` (`GameClock.Restore`, no events). Save / load are refused while
  `SleepUI.IsOpen` (panel or fade).
- Fact times: optional `SaveData.factSetTimes` (fact key → game seconds it turned true; null in older saves → every true
  fact counts as long elapsed), restored with the facts in `BeginLoad`, cleared on a failed start-menu load.
- Status effects: `PlayerSaveData.statusEffects` (`StatusEffectSaveData`: effectId, absolute appliedAt / expiresAt,
  total, paid; missing in older saves → none), restored by `PlayerSaveAdapter` after `RestoreHealth` via
  `StatusEffectHost.Restore` — they resume from the clock restored in `BeginLoad`. See `Scripts/StatusEffects/CLAUDE.md`.
- `SaveData` — plain classes, no Unity types (`SVector3` / `SQuaternion`); Newtonsoft Json.
- **Restore without events:** restore methods set state directly and raise only value-changed events — never
  `_onLevelUp`, `_onSkillLearned`, `_onXPGained`, `RegisterKill` / `RegisterDeath` (they grant LP / XP / toasts).
- Object identity: `SaveableObject` (`Scripts/World/CLAUDE.md` → Persistence and save ids); item identity: `ItemSO.itemId` + `ItemCatalog`
  (`ScriptableObjects/Items/CLAUDE.md`).
