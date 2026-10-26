# CLAUDE.md — Assets/_Game/Scripts/StatusEffects

> Loaded when Claude accesses files in this folder. Timed effects (heal over time, later poisons / buffs) on the
> `GameClock` timeline. Spec: `_bmad-output/implementation-artifacts/spec-status-effects-food-regen.md`.

---

## Pieces

| File | Role |
|------|------|
| `ScriptableObjects/StatusEffects/StatusEffectSO` | Effect **type** (behavior only): `effectId` (save ID — never change), `displayName`, `icon` / `iconTint` (HUD; no icon → tinted square), `kind` (`Heal` only), `tickIntervalSeconds` (game s, 60 = 2 real s), `stacking` (`Stack` + `maxStacks` 0 = unlimited, drops the oldest past the cap / `Refresh` = replace). Assets `Data/StatusEffects/SE_*.asset` |
| `ScriptableObjects/StatusEffects/StatusEffectCatalogSO` | `Data/StatusEffects/StatusEffectCatalog.asset` — **hand-maintained** list, `FindById` for save restore. A new effect missing here is dropped on load (warn) |
| `StatusEffectInstance` | One application: `AppliedAt` / `ExpiresAt` (absolute `GameClock.Now` seconds), `TotalAmount`, mutable `PaidAmount` |
| `StatusEffectMath` | Pure (`StatusEffectMathTests`): `PaidBy` (quantized to whole ticks, exactly `total` at expiry), `DueNow`, `AddInstance` (stacking), `PendingOf`, `CountOf`, `FormatRemaining` ("20m" / "1h05"), `FormatDuration` ("1 h 30 min") |
| `StatusEffectHost` | Player.prefab root. `Apply(effect, amount, durationSeconds)` (false: no clock / dead / bad values), `Active`, `Now`, `PendingHeal`, `Clear`, `Capture` / `Restore` (via `PlayerSaveAdapter`, after `RestoreHealth`). `Update` pays `DueNow` — one summed `PlayerHealth.Heal` per frame — and removes expired instances. Raises `OnStatusEffectsChanged` (`GameEventSO_Void`) on apply / expire / clear / restore — **not** per tick (health ticks reach the HUD through `OnPlayerHealthChanged`) |

## Rules

- **Size comes from the applier, behavior from the SO.** Food (`PotionItemSO._overTimeEffect` + `_durationMinutes`,
  heal = `_healAmount`) all share `SE_WellFed`, so the HUD groups instances by SO. Don't create one SO per item.
- Never countdowns: everything derives from `GameClock.Now`, so a sleep skip or a hitch pays out in one pass on the
  next frame (no `OnTimeSkipped` listener needed) and a load resumes from the restored clock.
- `_gameClock` is a **Core.unity scene override** on the Player instance (Player.prefab has none, like `SleepSystem`).
  Without it `Apply` refuses (warns once) and food falls back to an instant heal — e.g. `TestScene`.
- The host pauses while `SaveSystem.IsLoading` (clock restored before the region reload, effects after) and clears
  every effect on death. The paid amount is spent even at full HP (no refund).
- New `StatusEffectKind`: add the payout `case` in `StatusEffectHost.Update` and, if it should preview, a `PendingOf`
  reader. New stacking policy: `StatusEffectMath.AddInstance` + tests.
- Cross-system calls (host → `PlayerHealth`, `PotionItemSO` → host, HUD reads `Active` / `PendingHeal`) follow the
  existing `PotionItemSO` / `HealthBarUI` exceptions — keep new ones to reads of a component on the player.
