---
title: 'Status effects system — food heals over time on the world clock'
type: 'feature'
created: '2026-10-26'
status: 'done'
baseline_commit: 'c6ee8b6'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Core/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/UI/HUD/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every `PotionItemSO` heals instantly, so food is a pocket potion. The game needs timed effects that run on the `GameClock` timeline (deferred from spec-game-clock-and-bed-sleep).

**Approach:** Add a generic status-effect system: a `StatusEffectSO` type, runtime instances stamped `appliedAt` / `expiresAt` in absolute world seconds, and a player host that pays them out from `GameClock.Now`. The host also saves them and raises HUD events. Food is the first user: it applies a shared heal-over-time effect sized per item. The health potion stays instant. The HUD shows active effects, and the health bar shows the HP still to come in a darker red.

## Boundaries & Constraints

**Always:** Effect time is absolute `GameClock.Now`, never a countdown. A time skip (sleep) or a hitch pays out in one pass. Instances of the same effect add up (default stacking). Payout is quantized to the effect's tick interval, and the last tick lands exactly on `expiresAt`, so the sum equals the total. Effects are saved and restored. Restore never re-applies item side effects.

**Ask First:** Adding stat modifiers, damage effects, NPC hosts, or a `Food` item class/category rename.

**Never:** Real-time timers or coroutines for effect duration. A singleton host. Changing `Item_Health_Potion` or `Item_HealingPlant` (both stay instant). Art generation (placeholder tint only).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Eat food | Apple: 8 HP over 20 game min | Heals in ticks over 20 game min (40 real s); total exactly 8 | N/A |
| Two at once | 2 apples eaten back to back | Two instances; heal rates add; HUD shows one icon ×2 | N/A |
| Sleep mid-effect | Skip 8 h | Next frame pays all remaining and expires | N/A |
| At full HP | Heal ticks while HP = max | Ticks are consumed (wasted); pending stays capped visually | N/A |
| Death | Player dies with effects | All effects cleared, changed event raised | N/A |
| Save / load | Save with 1 active, load later | Instance restored with the same times and paid amount; catches up from the restored clock | Unknown effectId → warn, skip |
| No clock / host | TestScene (no `GameClock`) or no host | Food heals its full amount instantly | Warn once |
| Old save | `statusEffects` missing | No effects | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- food/potion `OnUse`; instant `Heal` today
- `Assets/_Game/Scripts/Player/PlayerHealth.cs` -- `Heal`, `IsDead`, `MaxHealth`, `_onPlayerHealthChanged`
- `Assets/_Game/Scripts/Core/Time/GameClock.cs` / `GameTime.cs` -- `Now` (double game s); 1 game h = 2 real min
- `Assets/_Game/Scripts/World/SleepSystem.cs` -- precedent: Player-root component with `GameClock` ref as a Core scene override
- `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs`, `Scripts/Core/Save/SaveData.cs` -- player capture/restore
- `Assets/_Game/Scripts/UI/HUD/HealthBarUI.cs`, `Prefabs/UI/HealthBar.prefab` -- bar (bg Image → `Fill` Image filled)
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- "Restores Health" stat row
- `Assets/_Game/Data/Items/Item_*.asset` -- 9 `PotionItemSO` assets

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/StatusEffects/StatusEffectSO.cs` -- `effectId`, `displayName`, `icon` (nullable) + `iconTint`, `kind` enum (`Heal` only for now), `tickIntervalSeconds` (game s, default 60), `stacking` enum (`Stack` with `maxStacks` 0 = unlimited, `Refresh` = replace existing) -- the generic effect type
- [x] `Assets/_Game/ScriptableObjects/StatusEffects/StatusEffectCatalogSO.cs` -- list + `FindById`; OnValidate warns on duplicate/empty ids -- save resolution
- [x] `Assets/_Game/Scripts/StatusEffects/StatusEffectInstance.cs` + `StatusEffectMath.cs` (pure, `Game.StatusEffects`) -- instance {effect, appliedAt, expiresAt, totalAmount, paidAmount}; `PaidBy(instance, now)` quantized to ticks, full at expiry; `Remaining` -- testable core
- [x] `Assets/_Game/Scripts/StatusEffects/StatusEffectHost.cs` -- on Player root; refs `GameClock` (scene override), `PlayerHealth`, catalog, `GameEventSO_Void _onStatusEffectsChanged`. API: `bool Apply(effect, amount, durationSeconds)` (false when no clock / dead / invalid), `Active`, `PendingHeal`, `Capture()` / `Restore(list)`. `Update`: if dead → clear; else pay `PaidBy(now) − paid` via `Heal`, remove expired; raise changed on apply/expire/clear/restore -- runtime host
- [x] `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- add `_overTimeEffect` + `_durationMinutes`; when both set, `TryGetComponent<StatusEffectHost>` → `Apply`, falling back to instant `Heal` with a warning; expose `HealsOverTime` / `DurationMinutes`; meal bonus unchanged -- food migration
- [x] `Assets/_Game/Scripts/Core/Save/SaveData.cs`, `Scripts/Player/PlayerSaveAdapter.cs` -- `List<StatusEffectSaveData> statusEffects` (effectId, appliedAt, expiresAt, total, paid); capture/restore after health -- persistence
- [x] `Assets/_Game/Scripts/UI/HUD/HealthBarUI.cs` + `HealthBar.prefab` -- add a `PendingFill` Image as a sibling before `Fill` (same rect, draws behind it, dark red ≈ 0.45/0.08/0.08) showing `min(cur + pending, max) / max`; repaint on health change and `_onStatusEffectsChanged` -- incoming-heal preview
- [x] `Assets/_Game/Scripts/UI/HUD/StatusEffectBarUI.cs` + `Prefabs/UI/StatusEffectIcon.prefab` + `UICanvas/Game/StatusEffectBar` (above HealthBar, HorizontalLayoutGroup) -- one icon per effect type: icon/tint, "×N" when stacked, longest remaining game time rounded up to the minute ("20m" / "1h05"); refresh on changed event and every 0.5 real s; pooled entries -- HUD indicator
- [x] `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- over-time food reads "Restores Health: +8 over 20 min"; update matching EditMode tests -- clarity
- [x] Assets: `Data/StatusEffects/SE_WellFed.asset` (Heal, Stack, tint warm amber), `StatusEffectCatalog.asset`; wire the host on `Player.prefab` + Core `GameClock` override + HUD refs; set food: Apple 20, BrownMushroom 20, Bread 30, Trout 30, ReplenishingStew 60, both sandwiches 60 min -- content
- [x] `Assets/Tests/EditMode/StatusEffectMathTests.cs` -- quantized payout, exact total at expiry, skip past expiry, stacking sum, Refresh replace, maxStacks cap -- matrix coverage
- [x] Docs: new `Scripts/StatusEffects/CLAUDE.md`; HUD + Core CLAUDE.md rows; root index row; remove the status-effects bullet from `deferred-work.md` and log what stays deferred (stat modifiers, damage kind, NPC hosts, effect icon art, `Food` category label) -- knowledge capture

**Acceptance Criteria:**
- Given the Core scene with a clock, when the player eats bread at 50/100 HP, then HP rises in steps to 60 over 30 game minutes, the dark-red preview reaches 60 immediately and shrinks as it fills, and a Well Fed icon shows the time left.
- Given the health potion, when used, then 50 HP is restored instantly and no effect appears.
- Given an active effect, when the player sleeps, then on waking it is gone and its HP was paid.

## Spec Change Log

- 2026-10-26 review (patch-level, no loopback): auditor flagged two literal wording gaps, both already implemented the
  better way and documented in HUD / Prefabs CLAUDE.md. `PendingFill` became a sibling drawn behind `Fill`, because a
  child would hide the real fill. The HUD time reads "20m" / "1h05" (game minutes, rounded up), because seconds flicker
  at 30× game speed. KEEP: both behaviours as implemented.

## Design Notes

The item supplies the size (`amount`, `duration`), the SO supplies the behavior. So one `SE_WellFed` serves all food, and the HUD groups by SO. `Stack` keeps one independent instance per application, which gives "they add up". Payout `PaidBy = total × min(1, floor(elapsed / tick) × tick / duration)`, and `total` once `now ≥ expiresAt`. A tick of 60 game s is 2 real s. That keeps health events and logs low-frequency.

## Verification

**Commands:**
- `mcp__UnityMCP__run_tests` (EditMode) -- expected: all pass, incl. new `StatusEffectMathTests` and updated formatter tests
- `mcp__UnityMCP__read_console` -- expected: no compile errors

**Manual checks:**
- Play Core: eat an apple and two breads → stacked icon, preview bar, steps; Skip 1 hour context menu → effects resolve.

## Suggested Review Order

**Clock-driven payout (entry point)**

- Each frame pays what the absolute timeline owes; skips and hitches resolve in one pass
  [`StatusEffectHost.cs:72`](../../Assets/_Game/Scripts/StatusEffects/StatusEffectHost.cs#L72)

- Pure payout formula: whole ticks, exact total at expiry
  [`StatusEffectMath.cs:16`](../../Assets/_Game/Scripts/StatusEffects/StatusEffectMath.cs#L16)

- Paid set, not accumulated, so float drift can't trickle heals every frame
  [`StatusEffectHost.cs:95`](../../Assets/_Game/Scripts/StatusEffects/StatusEffectHost.cs#L95)

- Apply refuses without clock, dead, or mid-load (caller falls back to instant)
  [`StatusEffectHost.cs:48`](../../Assets/_Game/Scripts/StatusEffects/StatusEffectHost.cs#L48)

- Stack / Refresh policies and the oldest-first cap
  [`StatusEffectMath.cs:49`](../../Assets/_Game/Scripts/StatusEffects/StatusEffectMath.cs#L49)

**Effect type and food migration**

- Behaviour-only SO; size comes from the applying item
  [`StatusEffectSO.cs:27`](../../Assets/_Game/ScriptableObjects/StatusEffects/StatusEffectSO.cs#L27)

- Food routes to the host; potions and missing host stay instant
  [`PotionItemSO.cs:84`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L84)

**Persistence**

- Restore validates, drops unknown ids, rebases entries stamped after the restored clock
  [`StatusEffectHost.cs:149`](../../Assets/_Game/Scripts/StatusEffects/StatusEffectHost.cs#L149)

- Restored after the revive, from absolute times
  [`PlayerSaveAdapter.cs:151`](../../Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs#L151)

- Plain save record, null-safe for old saves
  [`SaveData.cs:40`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L40)

**HUD**

- Dark-red pending fill = current + PendingHeal, behind the real fill
  [`HealthBarUI.cs:83`](../../Assets/_Game/Scripts/UI/HUD/HealthBarUI.cs#L83)

- One pooled icon per effect type, stack count, longest expiry
  [`StatusEffectBarUI.cs:63`](../../Assets/_Game/Scripts/UI/HUD/StatusEffectBarUI.cs#L63)

- Detail row "+8 over 20 min"
  [`ItemDetailFormatter.cs:84`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L84)

**Tests**

- Payout, skip, stacking, formatting
  [`StatusEffectMathTests.cs:1`](../../Assets/Tests/EditMode/StatusEffectMathTests.cs#L1)

- Every effect asset is in the catalog with a unique id
  [`StatusEffectCatalogTests.cs:1`](../../Assets/Tests/EditMode/StatusEffectCatalogTests.cs#L1)
