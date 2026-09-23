# Sprint Change Proposal — Timed Combo Attack System

**Date:** 2026-03-06
**Project:** Echoes of the Fallen (Unity-BMAD-test-rpg)
**Prepared for:** Valentin
**Workflow:** correct-course

---

## Section 1: Issue Summary

**Problem Statement:**
Story 2.3 (Directional Attacks) was implemented as designed — a mouse-delta rolling buffer that resolves LMB input into one of four attack directions (Overhead, Left, Right, Thrust). The system is functional and passes all tests, but the underlying design is not practically implementable as a solo developer.

**Root Cause:**
The directional attack system requires four distinct attack animations (one per direction) to feel meaningful. Sourcing, importing, and tuning four Mixamo FBX attack animations — plus calibrating the `attackDirectionThreshold` and `directionSampleFrames` to feel natural — is disproportionately expensive for a solo developer. The system works in code but cannot deliver on its design promise without significant animation art investment.

**Discovery Context:**
Identified by the developer after Story 2.3 was marked `done`, during reflection on the upcoming animation work required for Stories 2.4–2.8.

**Evidence:**
- `DirectionalAttackSampler.cs` is implemented and tested (6 passing Edit Mode tests)
- `PlayerAnimatorController` has 4 directional attack states — all using placeholder `Idle.fbx` clip
- All 4 states require replacement with real directional Mixamo FBX animations to deliver the intended feel
- Proposed replacement (timed combo system) requires only 3 sequential animations, each independent of mouse direction — significantly easier to source, import, and tune

---

## Section 2: Impact Analysis

### Epic Impact
| Epic | Impact |
|------|--------|
| Epic 2 (Combat System) | Story 2.3 superseded; stories 2.4–2.8 unaffected |
| Epic 3 (Progression) | No impact — progression references "new attacks" generically |
| Epics 4–8 | No impact |

### Story Impact
| Story | Status | Impact |
|-------|--------|--------|
| 2.3 Directional Attacks | `done` → `superseded` | Implementation replaced by new combo story |
| 2.3 Timed Combo (new) | `ready-for-dev` | New story to be created via `create-story` |
| 2.4–2.8 | `backlog` | No impact — all independent of attack direction mechanic |

### Artifact Conflicts
| Artifact | Conflict | Resolution |
|----------|----------|------------|
| `gdd.md` — §Combat | Directional attack description | Rewritten for combo system |
| `gdd.md` — §Controls | "LMB + mouse direction" | Updated to combo input |
| `gdd.md` — §Input Feel | Mouse-directional feel guidance | Rewritten for timing window feel |
| `gdd.md` — §Difficulty Curve | "directional combat" reference | Updated to "combo timing" |
| `game-architecture.md` — Decision 5 | Directional input sampling architecture | Replaced with timed combo state machine decision |
| `epics.md` — Epic 2 | "directional attacks (mouse-driven)" | Updated to "timed combo attacks" |
| `epics.md` — Epic 2 Story 3 | "attack in the direction I move my mouse" | Updated to combo chain story |
| `sprint-status.yaml` | `2-3-directional-attacks: done` | Marked `superseded`; new `2-3-timed-combo: ready-for-dev` added |
| `project-context.md` | `DirectionalAttackSampler` in test targets + gotchas | Updated to combo equivalents |

### Technical Impact
**Code to remove:**
- `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs` (and `.meta`)
- `Assets/_Game/Scripts/Combat/AttackDirection.cs` (and `.meta`)
- `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs` (and `.meta`)

**Code to rework:**
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` — remove `DirectionalAttackSampler` dependency; add combo state machine (step 0–2, window timer, stamina gate per hit)
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller` — replace 4 directional states with 3 combo states (`Attack_1`, `Attack_2`, `Attack_3_Finisher`)

**Code preserved unchanged:**
- `Assets/_Game/Scripts/Combat/StaminaSystem.cs` — no changes
- `Assets/_Game/Scripts/Combat/CombatConfigSO.cs` — add `comboWindowDuration` field; remove `attackDirectionThreshold` and `directionSampleFrames`
- `Assets/Tests/EditMode/PlayerCombatGateTests.cs` — stamina gate tests remain valid

---

## Section 3: Recommended Approach

**Selected Path: Direct Adjustment (Option 1)**

Replace the directional attack implementation in-place while preserving the established stamina gate infrastructure from Story 2.2. No rollback of earlier work required.

**Rationale:**
- Story 2.2 (`PlayerCombat.cs`, stamina gate) is the exact foundation the combo system builds on — the input subscription pattern, stamina `HasEnough`/`Consume` calls, and `[RequireComponent]` structure all carry forward unchanged
- The combo system is simpler to implement than the directional system it replaces — reduced code surface, not increased
- Timeline impact is neutral to positive — fewer animation assets required, faster to achieve playable feel
- Risk is low — the stamina system is proven; the combo window is a small new state machine on top of existing infrastructure

**Effort:** Low-Medium
**Risk:** Low
**Timeline impact:** Neutral (combo animation sourcing is easier than 4-direction sourcing)

---

## Section 4: Detailed Change Proposals

### GDD Changes (`_bmad-output/gdd.md`)

**1. Combat System — Directional Attacks → Combo Attacks**

OLD:
> `- **Directional Attacks:** Attack direction is determined by mouse movement at the moment of input (left, right, overhead, thrust).`

NEW:
> `- **Combo Attacks:** Attacks are chained in a timed combo sequence via LMB. The first press triggers the opening strike. A timing window opens during the animation — pressing LMB within that window continues the chain to a second strike. A second timing window during the follow-up animation allows a finishing blow. Missing a window resets the combo. All combo hits consume stamina; at zero stamina, the chain cannot continue.`

**2. Controls Table**

OLD: `| Attack (directional) | Left Mouse Button + mouse direction |`
NEW: `| Attack (combo) | Left Mouse Button (tap within window to chain) |`

**3. Input Feel**

OLD: `Mouse-driven directional attacks should feel intuitive with clear visual feedback per direction.`
NEW: `Combo attacks should feel weighty and committed — each hit in the chain has a clear animation window. Timing feedback (visual or audio) communicates when the next input window is open. Missing the window resets the combo naturally without punishment beyond lost momentum.`

**4. Difficulty Curve**

OLD: `Early enemies teach stamina management and directional combat.`
NEW: `Early enemies teach stamina management and combo timing.`

---

### Architecture Changes (`_bmad-output/game-architecture.md`)

**5. Decision 5 — Full replacement**

OLD: Decision 5: Directional Attack Input Sampling — velocity threshold + 3–5 frame buffer
NEW: Decision 5: Timed Combo Attack System — animation-window state machine, per-hit stamina gating, `comboWindowDuration` in `CombatConfigSO`

Decision Summary table row updated accordingly.

---

### Epics Changes (`_bmad-output/epics.md`)

**6. Epic 2 Scope + Story 3**

- Scope: "directional attacks (mouse-driven)" → "timed combo attacks (3-hit chain with animation windows)"
- Story 3: "I attack in the direction I move my mouse" → "I can chain up to 3 attacks by pressing LMB within each combo window"

---

### Sprint Status Changes (`_bmad-output/implementation-artifacts/sprint-status.yaml`)

**7. Story 2.3 entries**

```yaml
# OLD
2-3-directional-attacks: done

# NEW
2-3-directional-attacks: superseded  # replaced by 2-3-timed-combo
2-3-timed-combo: ready-for-dev
```

---

### project-context.md Changes (`_bmad-output/project-context.md`)

**8. Testing Rules — test target updated**

OLD: `` - `DirectionalAttackSampler` direction resolution (buffer averaging, threshold logic) ``
NEW: `` - `ComboController` window timing logic (window open/close, combo step progression, stamina gate per hit) ``

**9. Novel Pattern Gotchas — gotcha updated**

OLD: `` - `DirectionalAttackSampler` buffer must be cleared/reset between attacks — stale buffer causes wrong direction on fast re-clicks ``
NEW: `` - `PlayerCombat` combo window must be explicitly closed when the animator exits an attack state — if the window stays open after exit time, rapid LMB presses can register a hit on the wrong combo step ``

---

## Section 5: Implementation Handoff

**Scope Classification: Minor** — self-contained within Epic 2, no cross-epic dependencies, implementable directly by the development team.

**Handoff: Development Team (solo developer)**

Next steps in order:

1. **Apply all documentation changes** from Section 4 above to the listed files
2. **Run `/bmad:bmgd:workflows:create-story`** to generate the `2-3-timed-combo` story file from the updated epics
3. **Run `/bmad:bmgd:workflows:dev-story`** on the new story to implement the combo system
4. The dev story implementation should:
   - Remove `DirectionalAttackSampler.cs`, `AttackDirection.cs`, and their tests
   - Rework `PlayerCombat.cs` with a 3-step combo state machine + window timer
   - Add `comboWindowDuration` to `CombatConfigSO`; remove directional fields
   - Replace 4 directional animator states with 3 combo states in `PlayerAnimatorController`
   - Keep `PlayerCombatGateTests.cs` — stamina gate tests remain valid

**Success Criteria:**
- Player can press LMB → attack 1 plays; pressing LMB during window → attack 2; pressing LMB during window → attack 3 (finisher)
- Missing any window resets the combo to step 0
- At zero stamina, the next combo hit in the chain is blocked (existing gate behavior preserved)
- All existing Stories 1.1–2.2 behavior unchanged
- No directional attack code or animator parameters remain in the project

---

## Change Log

- 2026-03-06: Proposal created via correct-course workflow
