# Sprint Change Proposal — 2026-03-09

**Project:** Unity-BMAD-test-rpg
**Workflow:** correct-course
**Author:** Valentin
**Status:** Approved — ready for implementation

---

## Section 1: Issue Summary

**Problem statement:**
After completing Stories 2.4 (manual blocking) and 2.5 (perfect block), two related gaps were identified:

1. **Airborne gate missing:** The player can attack and block while jumping or falling. No gate prevents combat input during the airborne state. This conflicts with the GDD's intended grounded, Gothic-style combat feel.

2. **Scalability gap:** Each mutually exclusive state (blocking, attacking, dodging…) is tracked via a local boolean flag in the component that owns it (`_isBlocking` in `PlayerCombat`). As more states accumulate, every new system that needs to query another system's state must directly reference it — leading to tight coupling and scattered guards. Upcoming Story 2-7 (dodge roll) will face the same problem.

**Discovery context:**
Identified proactively between Story 2.5 (done) and Story 2-6 (dodge roll, not yet started) — the cleanest possible insertion point in the sprint.

**Evidence:**
- `PlayerCombat.TryAttack()`: no `IsGrounded` check exists
- `PlayerCombat.OnBlockStarted()`: no `IsGrounded` check exists
- `PlayerController.Update()`: jump is gated by `IsGrounded` only — not by blocking or attacking
- Current `_isBlocking` is a private field in `PlayerCombat`; `PlayerController` cannot query it without a `GetComponent` call and direct coupling

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 2 (in-progress):** New story 2-6 inserted. Existing stories renumbered: 2-6→2-7, 2-7→2-8, 2-8→2-9. Epic goal ("full stamina-based directional combat") remains unchanged — this story strengthens the foundation.
- **Epic 2, Story 2-7 (was 2-6, dodge roll):** Direct benefit — dodge gating (no dodge while airborne, no dodge while attacking) will use the state manager instead of adding yet another ad-hoc check.
- **Epic 3+:** PlayerStateManager is available for future states (stagger, dead) but no changes required now.

### Story Impact
| Story | Impact |
|-------|--------|
| 2-1 through 2-5 | Done — no changes to completed stories |
| **2-6 (NEW)** | PlayerStateManager + airborne gate + PlayerCombat refactor |
| 2-7 (was 2-6) dodge-roll | Renumbered; will consume state manager |
| 2-8 (was 2-7) enemy-ai | Renumbered only |
| 2-9 (was 2-8) health-system | Renumbered only |

### Artifact Conflicts
| Artifact | Impact |
|----------|--------|
| `epics.md` | New story line added to Epic 2; existing stories renumbered |
| `sprint-status.yaml` | New `2-6-player-state-manager: backlog` entry; renumbering |
| `game-architecture.md` | Should note PlayerStateManager as new component (minor, deferred to story dev notes) |
| GDD | No conflict — airborne gating aligns with Gothic combat design intent |
| UI/UX spec | N/A |

### Technical Impact
- `PlayerCombat.cs`: `_isBlocking` field removed; block state ownership moves to `PlayerStateManager`
- `PlayerController.cs`: Jump gate extended; Airborne flag management added
- New file: `Assets/_Game/Scripts/Combat/PlayerStateManager.cs`
- New test file: `Assets/Tests/EditMode/PlayerStateManagerTests.cs`
- No input system changes; no animator changes

---

## Section 3: Recommended Approach

**Option 1 — Direct Adjustment** ✓ Selected

Insert new story 2-6 into Epic 2. Refactor existing combat components to use `PlayerStateManager` in the same story. No rollback needed; no MVP change.

**Rationale:**
- Cleanest insertion point in the sprint (between two done stories and the next not-started story)
- Codebase is small (2 combat files) — refactor risk is low
- Avoids accumulating technical debt that would compound with each new combat story
- `_comboWindowDelay` already tracks the exact animation-lock window needed for the Attacking flag — no new timing logic required

**Effort:** Low-Medium (new component + refactor of 2 existing files + tests)
**Risk:** Low
**Timeline impact:** ~1 story unit inserted; existing stories shifted by 1

---

## Section 4: Detailed Change Proposals

### Change 1 — `epics.md`

**Section:** Epic 2: Combat System → Stories

**OLD:**
```
- As a player, I can perform a perfect block in a tight timing window that
  staggers the attacker and costs no stamina
- As a player, I can dodge roll in any direction with i-frames
- As a player, enemies patrol and engage me when I enter their range
- As a player, enemies and I have health that depletes on hit
```

**NEW:**
```
- As a player, I can perform a perfect block in a tight timing window that
  staggers the attacker and costs no stamina
- As a developer, a PlayerStateManager centralizes player state (airborne,
  blocking, attacking, dodging) so combat actions are correctly gated —
  including preventing all combat while jumping or falling
- As a player, I can dodge roll in any direction with i-frames
- As a player, enemies patrol and engage me when I enter their range
- As a player, enemies and I have health that depletes on hit
```

**Justification:** New story at position 6 between 2.5 (done) and 2.7 (dodge roll, not started).

---

### Change 2 — `sprint-status.yaml`

**Section:** `development_status` — Epic 2 block

**OLD:**
```yaml
  2-6-dodge-roll: backlog
  2-7-enemy-ai-patrol-engage: backlog
  2-8-health-system: backlog
```

**NEW:**
```yaml
  2-6-player-state-manager: backlog
  2-7-dodge-roll: backlog
  2-8-enemy-ai-patrol-engage: backlog
  2-9-health-system: backlog
```

**Justification:** Story inserted; existing stories renumbered by 1.

---

### Change 3 — New Story 2-6 Definition (input for `create-story`)

**Story ID:** 2-6-player-state-manager
**Epic:** Epic 2 — Combat System

**User Story:**
> As a developer, I want a centralized PlayerStateManager that tracks the player's mutually exclusive action states, so that combat inputs are cleanly gated without scattered boolean checks — including blocking all combat actions while the player is airborne.

**Scope:**

**New: `PlayerStateManager` MonoBehaviour**
- Location: `Assets/_Game/Scripts/Combat/PlayerStateManager.cs`, namespace `Game.Combat`
- `[Flags] public enum PlayerStateFlags { None = 0, Airborne = 1<<0, Blocking = 1<<1, Attacking = 1<<2, Dodging = 1<<3 }`
- Public API: `AddState(flag)`, `RemoveState(flag)`, `HasState(flag)`, `CurrentState` property
- Gate methods:
  - `CanAttack()` → `!HasState(Airborne | Attacking | Blocking | Dodging)`
  - `CanBlock()` → `!HasState(Airborne | Attacking | Dodging)`
  - `CanJump()` → `!HasState(Attacking | Blocking | Airborne | Dodging)`
- `OnGUI()` debug line showing active flags (e.g. `"State: Airborne | Blocking"`)

**Refactor: `PlayerCombat.cs`**
- `GetComponent<PlayerStateManager>` in `Awake()`
- `_isBlocking` private field **removed** — ownership moves to state manager
- `OnBlockStarted()`: gate with `_stateManager.CanBlock()`; on success call `_stateManager.AddState(Blocking)`
- `OnBlockCanceled()`: call `_stateManager.RemoveState(Blocking)` (keep existing `if (!_isBlocking)` guard logic adapted)
- `TryAttack()`: gate with `_stateManager.CanAttack()` (replaces existing `_isBlocking` check + new Airborne check)
- Attacking flag lifecycle:
  - `AddState(Attacking)` when `_comboWindowDelay` is armed (after each attack hit 1 or 2)
  - `RemoveState(Attacking)` in `Update()` when `_comboWindowDelay` reaches 0 (window opens — player free to chain OR cancel into block/jump)
  - `RemoveState(Attacking)` on finisher (step 3) and on combo timeout/reset
- `TryReceiveHit()` and `_isPerfectBlockWindowOpen` logic: unchanged (internal to PB; reads `HasState(Blocking)` instead of `_isBlocking`)
- `OnGUI()`: remove Block line (now shown by PlayerStateManager debug); or update to read from state manager

**Refactor: `PlayerController.cs`**
- `GetComponent<PlayerStateManager>` in `Awake()`
- Each `Update()` frame: if `IsGrounded` → `RemoveState(Airborne)`, else `AddState(Airborne)`
- Jump input: gate with `_stateManager.CanJump()` (in addition to existing `IsGrounded` check)

**Edit Mode tests: `PlayerStateManagerTests.cs`**
- `AddState` / `RemoveState` / `HasState` flag operations
- `CanAttack()` false when Airborne, Attacking, Blocking, or Dodging
- `CanBlock()` false when Airborne, Attacking, or Dodging
- `CanJump()` false when Attacking, Blocking, Airborne, or Dodging
- Attacking flag clears when combo window opens (formula)

**Out of scope:**
- Dodging flag lifecycle (Story 2-7 sets/clears it)
- Attacking flag from animation events (deferred — `_comboWindowDelay` is sufficient proxy)
- Stagger / Dead states (Epic 3+)

---

## Section 5: Implementation Handoff

**Change scope classification:** Minor — direct implementation by development team.

**Handoff:** Run `/bmad:bmgd:workflows:create-story` to generate the full story 2-6 file from this proposal, then `/bmad:bmgd:workflows:dev-story` to implement it.

**Prerequisites before create-story:**
1. Apply Change 1 to `epics.md`
2. Apply Change 2 to `sprint-status.yaml`

**Success criteria:**
- Player cannot attack or block while airborne (jumping/falling)
- Player cannot jump while blocking or in combo animation lock
- `PlayerCombat` contains no `_isBlocking` field — all state is in `PlayerStateManager`
- All existing Edit Mode tests (26+) remain green
- New `PlayerStateManagerTests` pass

**Dependencies for Story 2-7 (dodge roll):**
- Story 2-7 will call `_stateManager.AddState(Dodging)` at dodge start and `RemoveState(Dodging)` at dodge end
- `CanAttack()` / `CanBlock()` / `CanJump()` already gate on Dodging — no changes needed to those methods in 2-7
