# CLAUDE.md — Assets/_Game/Scripts/UI/HUD

> In-game overlay under `UICanvas/Game`: drawn over the character tabs, hidden while a world screen
> (trade / container / crafting) is open — see `HudVisibilityUI`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `HealthBarUI` | Horizontal fill bar showing player current/max HP. Subscribes to `GameEventSO_Float _onPlayerHealthChanged`; reads max from `_playerHealth.MaxHealth` (base + first-meal bonuses; wired as a `Player.prefab` override), falling back to `CombatConfigSO.baseHealth`. `PendingFill` (dark red, sibling **before** `Fill` so it draws behind) previews `min(current + StatusEffectHost.PendingHeal, max)`; repainted on health changes and `OnStatusEffectsChanged` (`_statusEffects` is a `Player.prefab` override). |
| `StatusEffectBarUI` | `UICanvas/Game/StatusEffectBar` (HorizontalLayoutGroup, anchored on top of the HealthBar). One pooled `StatusEffectIcon.prefab` (`StatusEffectIconUI`: tinted icon, "×N" stack label, remaining game time via `StatusEffectMath.FormatRemaining`) per effect type, longest expiry shown. Rebuilt on `OnStatusEffectsChanged`; a 0.5 s unscaled timer only refreshes the countdown labels while something is shown. `_host` is a `Player.prefab` override. |
| `StaminaBarUI` | Horizontal fill bar showing player stamina. Subscribes to `GameEventSO_Float _onPlayerStaminaChanged` (value is already a normalized 0–1 ratio — no config reference needed). |
| `ExperienceBarUI` | Thin bar (`ExperienceBar.prefab`, anchors y 0.08–0.086) under the StaminaBar showing progress toward the next level. Subscribes to `GameEventSO_Float _onPlayerXPProgressChanged` (normalized 0–1 within the current level, raised by `LevelSystem`). Fill is authored at `localScale.x = 0` because the bar starts empty. No text label. |
| `ActionBarUI` | Manages 6 `ActionBarSlotUI` children. Refreshes on `GameEventSO_Int _onActionBarUsed` and `GameEventSO_Void _onInventoryChanged` (`OnPlayerInventoryChanged` — catches non-UI changes like a lockpick consumed by `DoorSystem` / `ContainerSystem`). Requires exactly 6 slots wired in Inspector. |
| `ActionBarSlotUI` | Individual action-bar slot. Supports drag-and-drop between inventory and action bar, hover highlight, key-label display, and stack count. |
| `InteractionPromptUI` | Prompt card (`InteractionPrompt.prefab`, `UICanvas/Game/InteractionPrompt`, own nested Canvas) reading `[E]` + verb + target name. Subscribes to `OnInteractionFocusChanged`; texts change **only** in the handler. `LateUpdate` anchors the card (pivot bottom-center) above the target collider's top via `WorldToScreenPoint`, clamps it inside `_screenMargin` (16 px, static `ClampToScreen`, tested by `InteractionPromptUITests`), and fades `CanvasGroup.alpha`. Hidden while `CursorManager.IsLocked` is false (menus) or the target is behind the camera / destroyed. Fixed pixel sizes (UICanvas has no CanvasScaler). The `E` key label is authored text. `data.illegal` (theft) tints verb + name `_illegalColor` (defaults cached from the texts in `Awake`); the key label keeps its colour. |
| `SpeechBubbleUI` | Generic speech bubbles (`UICanvas/Game/SpeechBubbleLayer`, own Canvas, before `InteractionPrompt` so the prompt draws on top). Subscribes to `OnSpeechBubbleRequested`. Pool of `SpeechBubble.prefab` entries (`SpeechBubbleEntryUI`: black 60 % card, white 18 pt TMP, hugs short lines, wraps past 320 px), prewarmed 4, max 6 (oldest recycled). One bubble per `speaker` transform: a request with equal / higher priority replaces the text in place (`SpeechBubbleRules.RestartElapsed` keeps the current alpha — a fading-out bubble fades back in), lower is ignored. Anchor = `speaker.position + up × anchorHeight`, pivot bottom-centre + 8 px, clamped with `InteractionPromptUI.ClampToScreen`. Fade in / hold / fade out via pure `SpeechBubbleRules` (`SpeechBubbleRulesTests`), scaled time (pauses with the game). Hidden (alpha 0) behind the camera, beyond 25 m or while the cursor is unlocked; released when the speaker is destroyed / inactive. **Make anything say something:** raise a `SpeechBubbleRequest` on `OnSpeechBubbleRequested` (lines usually from a `BarkSetSO`). |
| `AimSpreadUI` | Bow accuracy ring (`UICanvas/Game/AimSpreadRing`). **Polls** `PlayerCombat.TryGetAimSpread` in `LateUpdate` (continuous value — the accepted exception to "no polling", like `InteractionPromptUI`); hidden unless aiming a bow. Diameter = max(`_minDiameter` 18, 2 × `BowSpreadMath.ScreenRadius(angle, fov, pixelHeight)` ÷ canvas `scaleFactor`) using `PlayerCombat.AimCamera` — the same camera the shot is cast from — so it tracks the aim zoom. Resizes only past 0.25 px; own nested Canvas (per-frame resize while shrinking rebatches only the ring). Drawn by `AimSpreadRingGraphic` (`MaskableGraphic` ring mesh, constant 2 px stroke + 1 px feather, no texture — a scaled ring sprite would thicken as it grows). `_playerCombat` is a `Player.prefab` override. |
| `HudVisibilityUI` / `HudHiderSet` | On `UICanvas/Game` (+ `CanvasGroup`). Hides the whole HUD (alpha 0, no raycasts / interaction) while ≥ 1 hider is registered (`AddHider` / `RemoveHider`, counted by the pure `HudHiderSet`, `HudHiderSetTests`). HUD components keep running while hidden — never `SetActive(false)` the HUD (they'd miss event raises). `NotificationContainer` has a `CanvasGroup` with `ignoreParentGroups` so toasts stay visible. |
| `HideHudWhileOpen` | On the root of each world screen that toggles its own GameObject (`ContainerUI`, `NPCTradeUI`, `CookingUI`; `_hud` → `Game`): registers in `OnEnable`, unregisters in `OnDisable`. **A new world screen opts in by adding this component** — no screen-type references in the HUD. Character tabs deliberately don't hide the HUD (action bar is a drag target). |
| `NotificationToastUI` | Transient toast/notification stack on the HUD (`UICanvas/Game/NotificationContainer`). Subscribes to `OnXPGained`, `OnLevelUp`, `OnLockUnlocked` and formats a short message per event (`"Experience +N"`, `"Level up!"`, `"Door/Chest unlocked!"`); `OnSaveNotification` (from `SaveSystem`) and `OnLockpickFailed` (from `DoorSystem` / `ContainerSystem`) are shown verbatim. Reward toasts from `PlayerRewards`: `OnRewardGoldGained` → `FormatGold` ("Gold +N") and `OnRewardItemReceived` (verbatim "Received: …"); channels wired on `UICanvas.prefab`. `NotificationContainer` has its own Canvas (override sorting 50) so toasts draw above the Game Menu. Instantiates `NotificationToast.prefab` entries under a `VerticalLayoutGroup`; max 5 visible (FIFO eviction), each fades in → holds `_holdSeconds` (3) → fades out via `CanvasGroup.alpha`. **Dedup:** a message equal to the newest visible toast restarts that toast's hold (no new entry) — applies to every channel, so identical XP gains within the hold merge. The hold is a per-toast unscaled timer loop, never a shared cached `WaitForSecondsRealtime` (its deadline would be shared by concurrent toasts and survive a restart). Errors+disables if `_container`/`_toastEntryPrefab` unassigned; warns (continues) on a missing channel. |

---

## Event Channels

- `GameEventSO_Float` — `OnPlayerHealthChanged` (raised by `PlayerHealth`, current HP value)
- `GameEventSO_Void` — `OnStatusEffectsChanged` (raised by `StatusEffectHost` on apply / expire / clear / restore — not per tick; consumed by `HealthBarUI`, `StatusEffectBarUI`)
- `GameEventSO_Float` — `OnPlayerStaminaChanged` (raised by `StaminaSystem`, normalized 0–1)
- `GameEventSO_Float` — `OnPlayerXPProgressChanged` (raised by `LevelSystem` in `Start` and after each XP gain; normalized 0–1 within the current level, 1 at max level)
- `GameEventSO_Int` — `OnActionBarUsed` (raised externally when a slot is activated by key)
- `GameEventSO_Void` — `OnPlayerInventoryChanged` (raised by the player `InventorySystem` after every successful mutation; consumed by `ActionBarUI` and the open `InventoryUI`)
- `GameEventSO_Int` — `OnXPGained` / `OnLevelUp` (consumed by `NotificationToastUI` for toasts)
- `GameEventSO_InteractionFocus` — `OnInteractionFocusChanged` (raised by `InteractionSystem` on focus or verb/name/legality change; payload `target` is a runtime scene `Component` ref, null = no focus; `illegal` = theft → red prompt — consumed by `InteractionPromptUI`)
- `GameEventSO_TheftCommitted` — `OnTheftCommitted` (raised by `InteractionSystem` before an illegal interaction; consumed by `EntityBrain` witnesses — not by the HUD; theft alerts reach the HUD as speech bubbles)
- `GameEventSO_SpeechBubbleRequest` — `OnSpeechBubbleRequested` (payload `speaker` runtime Transform + `text`, `duration` (≤ 0 = default 3 s), `priority`, `anchorHeight`; raised by `EntityBrain` witness warnings and theft alerts — consumed by `SpeechBubbleUI`)
- `GameEventSO_String` — `OnLockUnlocked` (payload is the noun `"Door"`/`"Chest"`; raised by `DoorSystem` after `Unlock()`, and by `ContainerSystem` only on the **locked-container success path** — never for an already-unlocked container)
- `GameEventSO_Int` / `GameEventSO_String` — `OnRewardGoldGained` / `OnRewardItemReceived` (raised by `PlayerRewards` when a `PlayerRewardSO` grants gold / items; reward-only — other gold changes have no toast)
- `GameEventSO_String` — `OnCombatNotification` (verbatim toast text, e.g. "Out of arrows"; raised by `PlayerCombat` — wired on `Player.prefab` and `UICanvas.prefab`)
- `GameEventSO_String` — `OnCraftingRefused` (verbatim, "You need the Smithing skill"; raised by `CookingSystem` when a station's required skill is missing — wired on `Player.prefab` and `UICanvas.prefab`)
- `GameEventSO_String` — `OnLockpickFailed` (payload is the full toast text from `LockpickRules.FailureMessage`; raised by `DoorSystem` / `ContainerSystem` when a locked attempt lacks the skill or a lockpick)

---

## ActionBarSlotUI — Drag & Drop

- Implements all five drag interfaces: `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler`, `IPointerClickHandler`.
- Ghost image is parented to the root Canvas and has `raycastTarget = false`.
- `_dropHandled` flag prevents double-destroy when both source `OnEndDrag` and target `OnDrop` fire in the same frame.
- `SlotIndex` (action bar position) and `InventoryIndex` (backing inventory slot, -1 if empty) are set via `Initialize()`.

---

## Gotchas

- `ActionBarUI` sets `enabled = false` in `Awake` if dependencies are missing — any `OnDisable` path that touches `_actionBarSystem` must null-guard first.
- `HealthBarUI`, `StaminaBarUI` and `ExperienceBarUI` all set `enabled = false` in `Awake` if `_fillImage` is null — guard accordingly.
- `OnPlayerXPProgressChanged` is fire-and-forget (no last-value query). `ExperienceBarUI` gets its initial value only because `UICanvas` is nested in `Player.prefab`, so its `OnEnable` runs before `LevelSystem.Start` raises. If the HUD moves to a separate scene/prefab, or a new listener subscribes late, it stays empty until the next XP gain. Add a re-raise or a current-value getter first.
