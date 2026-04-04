# Tech-Spec: HUD Health Bar & Stamina Bar

**Status:** implementation-complete
**Baseline commit:** 2d56ec80a7eeda7d60ec1a4c35754a9ad00b5073

---

## Overview

This document covers two closely related HUD systems:

1. **HealthBarUI** (existing, documented here for reference) — displays current/max player health.
2. **StaminaBarUI** (new) — displays current/max player stamina, positioned directly below the health bar.

Both bars are anchored to the UICanvas (Screen Space Overlay) and update exclusively via `GameEventSO_Float` channels. No polling in `Update`.

---

## Part 1 — HealthBar (Existing System, Documentation)

### Purpose

Displays the player's current health as a horizontal fill bar above the ActionBar. Updates reactively via the `OnPlayerHealthChanged` GameEventSO channel.

### Files

| File | Status | Purpose |
|------|--------|---------|
| `Assets/_Game/Scripts/UI/HealthBarUI.cs` | exists | MonoBehaviour — subscribes to event, scales fill |
| `Assets/_Game/Prefabs/UI/HealthBar.prefab` | exists | Prefab — background Image + Fill child Image |
| `Assets/_Game/Data/Events/OnPlayerHealthChanged.asset` | exists | `GameEventSO_Float` channel (GUID: d93b6981e4214bd488ec8a72b03d8c60) |

### Prefab Hierarchy

```
HealthBar (root)
├── RectTransform anchors: (0.4, 0.12) → (0.6, 0.14)  [Screen Space, centered horizontally]
├── Image (background): rgba(0.1, 0.1, 0.1, 0.8) — dark semi-transparent bar track
├── HealthBarUI component: _fillImage → Fill/Image, _config → CombatConfig.asset, _onPlayerHealthChanged → OnPlayerHealthChanged.asset
└── Fill (child)
    ├── RectTransform: stretch all, anchoredPosition (2, 0), sizeDelta (-4, -4)
    ├── Pivot: (0, 0.5)  ← left-pivot required for localScale.x fill technique
    └── Image (fill): rgba(0.8, 0.15, 0.15, 1.0) — red
```

### Script: HealthBarUI.cs

```csharp
namespace Game.UI
// [SerializeField] _fillImage: Image (Fill child)
// [SerializeField] _config: CombatConfigSO  →  reads _config.baseHealth as max
// [SerializeField] _onPlayerHealthChanged: GameEventSO_Float
//
// OnEnable  → _onPlayerHealthChanged?.AddListener(HandleHealthChanged)
// OnDisable → _onPlayerHealthChanged?.RemoveListener(HandleHealthChanged)
//
// HandleHealthChanged(float currentHealth):
//   ratio = currentHealth / _config.baseHealth
//   _fillImage.transform.localScale = new Vector3(ratio, 1f, 1f)
```

### Fill Technique

Scale-based fill: `_fillImage.transform.localScale.x` = ratio (0–1).
**Requirement:** `Fill` child pivot must be `(0, 0.5)` (left-edge pivot) so scaling shrinks from the right.
**No `Image.fillAmount`** — scale approach was chosen; do not change to fillAmount.

### Event Channel

`OnPlayerHealthChanged` is a `GameEventSO_Float` raised by `PlayerHealth.cs` (namespace `Game.Combat`) whenever health changes. It fires `float currentHealth` (raw value, not normalized).

`HealthBarUI` divides by `_config.baseHealth` to compute ratio.

**Note:** `_config.baseHealth` is used as a fixed display max. This means the bar could overflow if a future system grants bonus max health without updating `baseHealth`. A `OnPlayerMaxHealthChanged` channel would fix this at that point — out of scope for now.

---

## Part 2 — StaminaBar (New Implementation)

### Goal

A HUD bar visually identical in structure to HealthBar, placed directly below it, displaying the player's current stamina relative to their current `MaxStamina` (including Endurance stat bonus).

### Key Design Decision: Normalized Event

Unlike HealthBarUI (which fires raw `currentHealth` and divides by config max), StaminaBarUI receives a **pre-normalized ratio** (0.0–1.0). This is because stamina's max is dynamic (`baseStaminaPool + endurance bonus`) — `StaminaSystem` is the only component that knows the true current max, so normalization happens there.

The event fires `currentStamina / MaxStamina`.

---

### Files to Create / Modify

| File | Action | Purpose |
|------|--------|---------|
| `Assets/_Game/Scripts/Combat/StaminaSystem.cs` | **modify** | Add event field + raise on change |
| `Assets/_Game/Data/Events/OnPlayerStaminaChanged.asset` | **create** | `GameEventSO_Float` channel (ratio 0–1) |
| `Assets/_Game/Scripts/UI/StaminaBarUI.cs` | **create** | MonoBehaviour — mirrors HealthBarUI |
| `Assets/_Game/Prefabs/UI/StaminaBar.prefab` | **create** | Mirrors HealthBar prefab, yellow fill |
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | **modify** | Add StaminaBar instance below HealthBar |

---

### Task 1 — Modify StaminaSystem.cs

Add a `GameEventSO_Float` field and raise normalized stamina on every change.

**Diff (conceptual):**

```csharp
// Add field:
[SerializeField] private GameEventSO_Float _onPlayerStaminaChanged;

// Add private helper:
private void RaiseStaminaChanged()
{
    float max = MaxStamina;
    float ratio = max > 0f ? _currentStamina / max : 0f;
    _onPlayerStaminaChanged?.Raise(ratio);
}

// Call RaiseStaminaChanged() at the end of Consume() (successful branch only)
// Call RaiseStaminaChanged() inside Update() whenever _currentStamina actually changes
```

**Consume() change:**
```csharp
public bool Consume(float amount)
{
    // ... existing logic ...
    _currentStamina -= amount;
    _currentStamina = Mathf.Max(_currentStamina, 0f);
    _regenCooldown = _config.staminaRegenDelay;
    GameLog.Info(TAG, $"Stamina consumed: -{amount}. Remaining: {_currentStamina:F1}");
    RaiseStaminaChanged();   // ← NEW
    return true;
}
```

**Update() change:** Track whether stamina actually changed during regen to avoid firing every frame when full.

```csharp
private void Update()
{
    if (_config == null) return;

    if (_regenCooldown > 0f)
    {
        _regenCooldown -= Time.deltaTime;
        return;
    }

    float max = MaxStamina;
    if (_currentStamina < max)
    {
        float prev = _currentStamina;
        _currentStamina = Mathf.Min(_currentStamina + _config.staminaRegenRate * Time.deltaTime, max);
        if (!Mathf.Approximately(_currentStamina, prev))
            RaiseStaminaChanged();   // ← NEW (only when value changed)
    }
}
```

**Wire in Player prefab Inspector:** after this story, `StaminaSystem._onPlayerStaminaChanged` → `OnPlayerStaminaChanged.asset`.

**Null guard note:** `_onPlayerStaminaChanged` uses the `?.` null-conditional, so leaving it unassigned only silences the bar — no exception.

---

### Task 2 — Create OnPlayerStaminaChanged.asset

- Type: `GameEventSO_Float`
- Path: `Assets/_Game/Data/Events/OnPlayerStaminaChanged.asset`
- Fires: normalized stamina ratio `float` in range [0.0, 1.0]
- Name convention: matches existing `On + EventName` pattern

---

### Task 3 — Create StaminaBarUI.cs

Path: `Assets/_Game/Scripts/UI/StaminaBarUI.cs`

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays the player's current stamina as a horizontal fill bar below the HealthBar.
    /// Subscribes to OnPlayerStaminaChanged (float = normalized ratio 0–1).
    /// </summary>
    public class StaminaBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private Image _fillImage;
        [SerializeField] private GameEventSO_Float _onPlayerStaminaChanged;

        private void Awake()
        {
            if (_fillImage == null)
            {
                GameLog.Error(TAG, "StaminaBarUI: _fillImage not assigned");
                enabled = false;
                return;
            }
            if (_onPlayerStaminaChanged == null)
                GameLog.Warn(TAG, "StaminaBarUI: _onPlayerStaminaChanged not assigned — bar will not update");
        }

        private void OnEnable()
        {
            _onPlayerStaminaChanged?.AddListener(HandleStaminaChanged);
        }

        private void OnDisable()
        {
            _onPlayerStaminaChanged?.RemoveListener(HandleStaminaChanged);
        }

        private void HandleStaminaChanged(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            _fillImage.transform.localScale = new Vector3(clamped, 1f, 1f);
        }
    }
}
```

**Differences from HealthBarUI:**
- No `_config` field — ratio is pre-normalized by `StaminaSystem`
- Clamps ratio to [0, 1] as a defensive guard

---

### Task 4 — Create StaminaBar.prefab

Path: `Assets/_Game/Prefabs/UI/StaminaBar.prefab`

Mirror the HealthBar prefab structure exactly, with these differences:

| Property | HealthBar | StaminaBar |
|----------|-----------|------------|
| Root name | `HealthBar` | `StaminaBar` |
| Root anchors | `(0.4, 0.12) → (0.6, 0.14)` | `(0.4, 0.09) → (0.6, 0.11)` |
| Fill color | rgba(0.8, 0.15, 0.15, 1.0) — red | rgba(0.9, 0.7, 0.1, 1.0) — yellow |
| Script component | `HealthBarUI` | `StaminaBarUI` |
| Event field | `_onPlayerHealthChanged` | `_onPlayerStaminaChanged` |
| Config field | `_config → CombatConfig.asset` | *(none)* |

**Prefab hierarchy:**

```
StaminaBar (root)
├── RectTransform anchors: (0.4, 0.09) → (0.6, 0.11)
├── Image (background): rgba(0.1, 0.1, 0.1, 0.8) — same dark track as HealthBar
├── StaminaBarUI component: _fillImage → Fill/Image, _onPlayerStaminaChanged → OnPlayerStaminaChanged.asset
└── Fill (child)
    ├── RectTransform: stretch all, anchoredPosition (2, 0), sizeDelta (-4, -4)
    ├── Pivot: (0, 0.5)  ← left-pivot required for localScale.x fill technique
    └── Image (fill): rgba(0.9, 0.7, 0.1, 1.0) — yellow
```

---

### Task 5 — Update UICanvas.prefab

Add a `StaminaBar` nested prefab instance between `HealthBar` and `ActionBar`.

**Expected sibling order in UICanvas (top-to-bottom in Hierarchy):**
```
Canvas
├── Crosshair
├── HealthBar          ← existing nested prefab
├── StaminaBar         ← NEW nested prefab instance (add here)
├── ActionBar          ← existing
└── InventoryUI        ← existing
```

**MCP prefab workflow note:** Use `manage_prefabs` to add the StaminaBar nested prefab instance.  
Double-check `renderMode = 0` (Screen Space Overlay) on Canvas after any MCP prefab edit — MCP `manage_gameobject(create)` defaults Canvas to `renderMode = 2` (World Space); this doesn't apply to nested prefab additions but verify anyway.

---

### Task 6 — Wire Player Prefab Inspector

After creating the asset and modifying `StaminaSystem.cs`:

1. Open `Assets/_Game/Prefabs/Player/Player.prefab`
2. Select the root GameObject (where `StaminaSystem` is attached)
3. Set `StaminaSystem._onPlayerStaminaChanged` → `Assets/_Game/Data/Events/OnPlayerStaminaChanged.asset`

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | `StaminaSystem.cs` has `[SerializeField] private GameEventSO_Float _onPlayerStaminaChanged` and raises it with a normalized ratio on every stamina change (consume and regen) |
| 2 | `OnPlayerStaminaChanged.asset` exists at `Assets/_Game/Data/Events/` |
| 3 | `StaminaBarUI.cs` exists, compiles, subscribes in `OnEnable`/`OnDisable`, updates fill via `localScale.x` |
| 4 | `StaminaBar.prefab` exists, yellow fill, anchored below HealthBar at `(0.4, 0.09)–(0.6, 0.11)` |
| 5 | `UICanvas.prefab` contains `StaminaBar` as a nested prefab instance, between `HealthBar` and `ActionBar` in sibling order |
| 6 | `StaminaSystem._onPlayerStaminaChanged` is wired to `OnPlayerStaminaChanged.asset` on the Player prefab |
| 7 | In Play Mode: StaminaBar fills as yellow bar; depletes visually when dodge/attack/block consumes stamina; refills during regen |
| 8 | HealthBar behavior is unchanged |
| 9 | No compile errors; no Play Mode console errors |

---

## Architecture Compliance

| Rule | Compliance |
|------|-----------|
| Cross-system event via `GameEventSO<T>` | ✅ `StaminaSystem` (Game.Combat) → `StaminaBarUI` (Game.UI) via `OnPlayerStaminaChanged.asset` |
| Subscribe in `OnEnable`, unsubscribe in `OnDisable` | ✅ `StaminaBarUI` follows this pattern |
| No `Debug.Log` — `GameLog` only | ✅ `StaminaBarUI` uses `GameLog.Error/Warn` with `[UI]` TAG |
| `[SerializeField] private` for Inspector fields | ✅ |
| Null guard in `Awake` | ✅ `_fillImage` is required; disables if missing |
| No `GetComponent` in `Update` | ✅ All refs cached |
| No magic numbers | ✅ Fill color/anchors are prefab data, not code values |
| `OnDisable` null guard for `_input` | N/A — `StaminaBarUI` has no input subscriptions |
| Event SO naming: `On + EventName` | ✅ `OnPlayerStaminaChanged` |

---

## Files Changed

```
Assets/_Game/Scripts/Combat/StaminaSystem.cs                   (modified)
Assets/_Game/Data/Events/OnPlayerStaminaChanged.asset          (new)
Assets/_Game/Data/Events/OnPlayerStaminaChanged.asset.meta     (new)
Assets/_Game/Scripts/UI/StaminaBarUI.cs                        (new)
Assets/_Game/Scripts/UI/StaminaBarUI.cs.meta                   (new)
Assets/_Game/Prefabs/UI/StaminaBar.prefab                      (new)
Assets/_Game/Prefabs/UI/StaminaBar.prefab.meta                 (new)
Assets/_Game/Prefabs/UI/UICanvas.prefab                        (modified)
Assets/_Game/Prefabs/Player/Player.prefab                      (modified — wire event SO)
```
