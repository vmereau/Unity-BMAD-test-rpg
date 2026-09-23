# Sprint Change Proposal — 2026-03-22 (rev. 2)
**Project:** Unity-BMAD-test-rpg
**Workflow:** Correct Course
**Scope Classification:** Minor — implemented directly by development team

> **Revision note:** Original proposal used SO timer fields for combo windows and hitbox timing.
> Revised to use Animation Events for all frame-dependent timing — combo windows and hitbox
> windows both depend on animation clip progress and must be driven by the animator, not floats.
> Only `comboSteps` (a design decision, not a timing decision) remains on WeaponSO.

---

## Section 1: Issue Summary

### Problem Statement
Combo behavior (max steps, window timing) and hitbox activation timing are hardcoded globally in
`PlayerCombat` and `CombatConfigSO`. As the game introduces multiple weapon categories, each weapon
type must define its own combat rhythm. The current flat structure cannot support per-weapon
variation without code changes.

Additionally, combo window and hitbox timing are intrinsically frame-dependent — they must open
and close at specific points in the attack animation to feel correct and chain smoothly. Storing
these as SO float values creates a maintenance burden: any animation clip adjustment requires
manually re-tuning matching floats.

### Discovery Context
Identified during post-implementation review of Story 7.9 (Weapon Collider Hit Detection). The
developer left a direct TODO in `PlayerCombat.cs:283`:
```csharp
// TODO "2" might depend on equipped weapon, to update.
if (_comboStep < 2)
```

### Evidence
| Location | Evidence |
|---|---|
| `PlayerCombat.cs:283` | `_comboStep < 2` hardcoded — no weapon query |
| `PlayerCombat.cs:154-175` | Combo window driven by `_comboWindowDelay`/`_comboWindowTimer` floats — not animation-synced |
| `WeaponHitbox.cs:26-33` | `Enable()`/`Disable()` — instant activation, not animation-synced |
| `WeaponSO.cs` | Concrete class with `[CreateAssetMenu]` — no combo fields |

---

## Section 2: Impact Analysis

### Epic Impact
**Epic 7 (Equipment & Economy) — in-progress**
- Stories 7.1–7.9 all done; unaffected
- Two new stories (7.10, 7.11) added within existing epic scope
- Remaining backlog stories (7.5 shop, 7.6 looting, 7.7 gold) independent and unaffected

**All other epics — unaffected**

### Story Impact
| Story | Status | Impact |
|---|---|---|
| 7.9 Weapon Collider Hit Detection | done | Triggers this change |
| **7.10 WeaponSO Combo Steps + Animation Events** | **new → ready-for-dev** | Core abstraction + event bridge |
| **7.11 Hitbox Animation Events** | **new → backlog** | Depends on 7.10 (AnimationEventReceiver exists) |
| 7.5, 7.6, 7.7 | backlog | Unaffected |

### Artifact Conflicts
| Artifact | Change Required |
|---|---|
| `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` | Make abstract; add `comboSteps` only |
| `Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs` | New concrete class |
| `Assets/_Game/Data/Items/Weapon_TestSword.asset` | Migrate `m_Script` GUID to SwordSO |
| `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` | New bridge script on Player root |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` | Drop timer fields/logic; add public event-receiver methods |
| `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` | Remove `comboWindowDelay`/`comboWindowDuration` (now dead) |
| `_bmad-output/epics.md` | Add stories 7.10 and 7.11 to Epic 7 |
| `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` | Update hierarchy (WeaponSO abstract, SwordSO child, Weapons/ folder) |
| `_bmad-output/project-context.md` | Add rule: WeaponSO abstract; concrete types in `ScriptableObjects/Items/Weapons/` |
| `Assets/_Game/Scripts/Combat/CLAUDE.md` | Document AnimationEventReceiver pattern |

### Technical Impact
- `EquipmentSystem.cs:56` uses `item is WeaponSO` — `SwordSO : WeaponSO` keeps this working
- `ItemDetailPanelUI` weapon section check (`item is WeaponSO`) — unchanged
- `EquipmentVisuals.cs:93` cast `weapon as WeaponSO` — still valid
- `WeaponHitbox.cs` — **no changes required**: `Enable()`/`Disable()` already have the right signature
- `CombatConfigSO.comboWindowDelay`/`comboWindowDuration` become dead fields after 7.10 — safe to remove

---

## Section 3: Recommended Approach

**Option 1 — Direct Adjustment** (selected)

Add stories 7.10 and 7.11 within Epic 7. All timing moved to Animation Events; only the
design-level field (`comboSteps`) lives on the SO.

| | |
|---|---|
| **Effort** | Low-Medium |
| **Risk** | Low |
| **Timeline impact** | None |

**Rationale:**
- Animation Events are the canonical Unity approach for frame-synchronised gameplay callbacks
- Single source of truth: animation clip drives timing, not a parallel set of floats that drift
- Adding a new weapon type only requires: a new SO asset + animation events on its override clips
- `WeaponHitbox` requires no changes at all for Story 7.11

---

## Section 4: Detailed Change Proposals

### Story 7.10 — WeaponSO Abstract + comboSteps + Animation Event Bridge

#### Change 1: `WeaponSO.cs` — make abstract, add comboSteps only

```
OLD:
  [CreateAssetMenu(menuName = "Items/Weapon", fileName = "Weapon_")]
  public class WeaponSO : EquipableItemSO

NEW:
  public abstract class WeaponSO : EquipableItemSO

ADDED (Header "Combo"):
  [Tooltip("Total number of attacks in the combo chain (e.g. 2 = Attack_1 → finisher).")]
  public int comboSteps = 2;

UNCHANGED:
  public float damageBonus;
  public AnimatorOverrideController animatorOverrideController;
  public override bool CanEquip() => true;

NOT added: comboWindowDelay, comboWindowDuration — timing driven by Animation Events
```

#### Change 2: `SwordSO.cs` — new concrete class
**File:** `Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs` *(new file, new folder)*

```csharp
using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Weapons/Sword", fileName = "Weapon_Sword_")]
    public class SwordSO : WeaponSO
    {
    }
}
```

#### Change 3: `Weapon_TestSword.asset` — migrate m_Script
```
OLD:
  m_Script: {fileID: 11500000, guid: 587b532a887cf794c89473bf6119ca33, type: 3}
  m_EditorClassIdentifier: Game::Game.Inventory.WeaponSO

NEW:
  m_Script: {fileID: 11500000, guid: <guid-of-SwordSO.cs>, type: 3}
  m_EditorClassIdentifier: Game::Game.Inventory.SwordSO
  comboSteps: 2
```
> Implementation note: create `SwordSO.cs` first → read GUID from `.meta` → update asset.

#### Change 4: `AnimationEventReceiver.cs` — new bridge script
**File:** `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` *(new)*

```csharp
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Receives Animation Events fired from attack clips and routes them to PlayerCombat.
    /// Attach to the Player root (same GameObject as the Animator).
    /// Story 7.10: combo window events.
    /// Story 7.11: hitbox enable/disable events.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        [SerializeField] private PlayerCombat _combat;

        // Called from attack animation clips at the frame the combo window opens
        public void ComboWindowOpen() => _combat?.OnComboWindowOpen();

        // Called from attack animation clips at the frame the combo window closes
        public void ComboWindowClose() => _combat?.OnComboWindowClose();

        // Called from attack animation clips at the frame the hit window opens (Story 7.11)
        public void HitboxEnable() => _combat?.OnHitboxEnable();

        // Called from attack animation clips at the frame the hit window closes (Story 7.11)
        public void HitboxDisable() => _combat?.OnHitboxDisable();
    }
}
```

> Wire `_combat` in the Player prefab Inspector. Add this component to the Player root GO.

#### Change 5: `PlayerCombat.cs` — drop timer logic, add event-receiver public methods

```
REMOVED fields:
  private float _comboWindowDelay;
  private float _comboWindowTimer;

REMOVED from Update():
  Phase 1 (comboWindowDelay countdown)
  Phase 2 (comboWindowTimer countdown)
  // Update() may become empty for combat; #if DEVELOPMENT_BUILD block and perfect block
  // countdown remain unchanged

CHANGED IncreaseAttackCombo():
  OLD:
    _comboStep++;
    _comboWindowOpen = false;
    _comboWindowDelay = _currentWeaponSO != null
        ? _currentWeaponSO.comboWindowDelay : _config.comboWindowDelay;
    _comboWindowTimer = 0f;
  NEW:
    _comboStep++;
    _comboWindowOpen = false;
    // Timing now driven by ComboWindowOpen animation event

CHANGED ManageComboStep():
  OLD: if (_comboStep < 2)
  NEW: int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.comboSteps : 3;
       if (_comboStep < maxSteps - 1)

CHANGED HandleVisualsRefreshed() — cache weapon SO (same as original Proposal 4):
  ADDED: _currentWeaponSO = _equipmentSystem?.GetEquipped(EquipmentSlot.Weapon) as WeaponSO;

CHANGED UnbindWeaponHitbox() — clear weapon SO (same as original Proposal 4):
  ADDED (first line): _currentWeaponSO = null;

ADDED public methods (called by AnimationEventReceiver):
  public void OnComboWindowOpen()
  {
      _comboWindowOpen = true;
      GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready");
  }

  public void OnComboWindowClose()
  {
      if (!_comboWindowOpen) return; // guard against stale events after manual reset
      ResetAttackCombo();
      GameLog.Info(TAG, "Combo window closed — chain reset");
  }

  public void OnHitboxEnable()  => _activeHitbox?.Enable();   // Story 7.11
  public void OnHitboxDisable() => _activeHitbox?.Disable();  // Story 7.11
```

> **Dev story investigation required:** The original `_comboWindowDelay > 0f` guard in `TryAttack`
> blocked rapid double-click input before the combo window opened. With Animation Events this guard
> is removed. The dev story must verify whether `_stateManager.IsAttacking && !_comboWindowOpen`
> is a sufficient replacement, or whether a dedicated `_isComboInputBlocked` bool is needed.

#### Change 6: `CombatConfigSO.cs` — remove dead timing fields

```
REMOVED:
  public float comboWindowDelay = 0.3f;
  public float comboWindowDuration = 0.18f;
```

> These fields are no longer read anywhere after the PlayerCombat changes above.
> The [Header("Combo Attack")] group is removed entirely.

---

### Story 7.11 — Hitbox Animation Events

#### Change 7: `PlayerCombat.cs` — `OnHitboxEnable`/`OnHitboxDisable` already added in Change 5

No additional PlayerCombat changes needed — the public methods are defined in Story 7.10.

#### Change 8: `WeaponHitbox.cs` — no changes required

`WeaponHitbox.Enable()` / `Disable()` already have the correct instant-activation signature.
Animation Events drive the timing; WeaponHitbox simply toggles colliders on command.

#### Change 9: Attack animation clips — add Animation Events

For each attack clip used by the player (Attack_1, Attack_2, Attack_3 — and any future
weapon override clips):

| Event | Suggested frame timing |
|---|---|
| `ComboWindowOpen` | ~50% through clip (attack motion committed, blend acceptable) |
| `ComboWindowClose` | ~90% through clip (too late to chain cleanly) |
| `HitboxEnable` | Frame the weapon strikes (~20-30% through clip) |
| `HitboxDisable` | Frame the weapon retracts (~50% through clip) |

> Exact frame placement is tuned by the developer in the Animation window by playing the clip
> and moving event markers until chaining and hit registration feel correct.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by development team

### Story 7.10 — Implementation Order
1. Create `Assets/_Game/ScriptableObjects/Items/Weapons/` folder
2. Create `SwordSO.cs`
3. Update `WeaponSO.cs` (abstract + `comboSteps`)
4. Read `SwordSO.cs.meta` GUID → update `Weapon_TestSword.asset`
5. Create `AnimationEventReceiver.cs`; add to Player root prefab; wire `_combat`
6. Update `PlayerCombat.cs` (drop timer fields, add public methods, update ManageComboStep)
7. Update `CombatConfigSO.cs` (remove dead combo fields)
8. Add `ComboWindowOpen` / `ComboWindowClose` events to Attack_1, Attack_2, Attack_3 clips
9. Playtest combo chaining; adjust event frame positions until feel is correct
10. Update `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` and `project-context.md`

### Story 7.11 — Implementation Order (after 7.10 done)
1. Add `HitboxEnable` / `HitboxDisable` events to Attack_1, Attack_2, Attack_3 clips
2. For each weapon `AnimatorOverrideController`, add the same events to its override clips
3. Playtest hit registration; adjust frame positions until hits register at the right moment

### Success Criteria — Story 7.10
- `Weapon_TestSword.asset` opens in Inspector with no missing script errors; shows `comboSteps`
- Equipping the test sword: 2-hit combo fires Attack_1 → Attack_2
- Combo window opens/closes at the animation-event frames; no timer drift
- Unarmed fallback: `maxSteps = 3` keeps 3-hit sphere combo unchanged
- `EquipmentSystem`, `ItemDetailPanelUI`, `InventoryUI` require zero changes

### Success Criteria — Story 7.11
- Weapon hitbox activates at the exact frame `HitboxEnable` is placed on the clip
- Hitbox deactivates at the exact frame `HitboxDisable` is placed
- `WeaponHitbox.cs` unchanged from Story 7.9 implementation

---

*Generated by Correct Course workflow — 2026-03-22 (rev. 2)*
