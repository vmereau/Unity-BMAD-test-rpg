---
title: 'NPC sleep routine: sleep in an owned bed, hear-only perception, grumpy wake-up (Hobb)'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '1733c0394030c06287b476199c3332fc9cfa90e1'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/Stealth/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NPC routines can only Stand or Patrol, so at night Hobb stands awake in his hut. Nobody sleeps, so there is no sneaking past a sleeper and no NPC bed to steal from or trespass near.

**Approach:** Add a `Sleep` routine activity that targets a `Bed`. The NPC walks to the bed's approach point, lies on it with the `sleeping` clip, and its perception goes blind: no sight, no proximity circle, only hearing (a sneaking player is silent). Hearing fills the awareness meter as usual. When the meter is full, he wakes. If the player is trespassing in his area, the existing trespass flow runs (scold / chase / fight). Otherwise he shows a disappointed speech bubble, stands by the bed, and lies down again once the player has stayed out of his walk-hearing radius for 10 s. A hit wakes him too. Pressing E on a sleeper shows **Wake**, which wakes him the same way and opens no dialogue. To test it, Hobb's decorative bed becomes a real `Bed` he owns (red outline, theft on use), and his 21–06 block becomes Sleep in it.

## Boundaries & Constraints

**Always:** `EntityBrain` stays the only mover. The sleep pose is entered and left only by the brain. Leaving the pose restores the agent at the approach point. Exceptions: death leaves the body where it lies, and a knockout first returns it to the floor. A sleeper ignores thefts and sight-based witnessing (it can't see). Hearing radii stay the Entity's walk / run radii, with the existing cap and occlusion. The asleep state is not saved: on load or after a time skip, the routine snap warps the NPC to the approach point and he lies down at once. The bed stays usable by the player only while no NPC occupies it. The real Hobb bed lives outside `HomeYardDressing`, and `HomeYardDresser` stops placing the decorative `HobbBed`. Wake lines follow the NPC voice rules (`Data/NPCs/CLAUDE.md`).

**Ask First:** Changing trespass / theft escalation rules. Adding a get-up clip. Moving other `HomeYardDressing` props.

**Never:** Sleeping NPCs other than Hobb in this spec. Saving sleep state. Waking on a sneaking player's proximity. A dialogue on Wake. Sit or other activities.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bedtime | Clock reaches 21:00 | Hobb walks to his bed and lies down (sleeping loop) | Sleep block without a bed → warning, stands at spot |
| Sneak by | Player sneaks into the hut / to the bed | Never wakes; can open HobbsBox unseen | — |
| Walk near, outside hut | Player walks within 4 m (run 8 m; ×0.5 through walls) for about 2 s | Wakes, disappointed bubble, stands by bed facing player; lies down after 10 s with the player beyond walk radius | — |
| Walk in hut | Player walks in his restricted hut | Wakes → trespass flow (alert, chase / scold, or fight on repeat) → back to bed afterward | — |
| Wake prompt | E on sleeping Hobb | Prompt "Wake"; wakes with bubble, no dialogue; Talk once he's up | — |
| Hit | Player punches the sleeper | Sneak-attack bonus applies; wakes and fights back | — |
| Time skip / load | Player sleeps through or loads into 21–06 | Hobb snapped onto his bed, asleep | Warp fails → walks |
| Morning | Block changes to 06 Patrol | Gets up, resumes patrol | — |
| Hobb's bed | Player focuses the bed while Hobb is out | Red outline + prompt; use = theft (Kind Other) | Occupied → not interactable |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs` -- `NpcActivity` enum + block data
- `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs` -- `SendBlock` → `EntityBrain.SetRoutineTarget`
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- state machine; `HandleIdle` / `HandleRoutineArrival`, `ApplyRoutineRequest`, `HandleHealthChanged`, `IsUnawareOf`, `HandleKnockedOut`, `TransitionToDead`, `OnDisable`
- `Assets/_Game/Scripts/AI/EntityPerception.cs` -- `Tick` visibility / hearing, `CanSeeTheft`
- `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` -- prompt / `Interact`, already holds `_brain`
- `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs` -- virtual setters (`SetUnconscious` pattern)
- `Assets/_Game/Scripts/World/Bed.cs` -- player bed `IInteractable`
- `Assets/_Game/ScriptableObjects/Entities/NPC/WitnessProfileSO.cs`, `ScriptableObjects/Config/StealthConfigSO` -- barks / tuning
- `Assets/_Game/Editor/World/HomeYardDresser.cs` -- places `HobbBed` (line ~173)
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`, `Animations/sleeping.fbx`

## Tasks & Acceptance

**Execution:**
- [x] `Animations/sleeping.fbx(.meta)` -- loop on, root rotation / Y / XZ baked into the pose -- in-place loop
- [x] `Humanoid_Template.controller` -- bool `IsSleeping`; Base state `Sleeping` (tag `FullBody`), AnyState → it on `IsSleeping` (no self-transition), exit → `LockOn Locomotion` on false -- full-body pose
- [x] `AIAnimationDriver.cs` / `HumanoidAIAnimationDriver.cs` -- `virtual SetSleeping(bool)` (no-op base) -- polymorphic driver
- [x] `Bed.cs` -- optional `NpcApproach` / `NpcSleepAnchor` child transforms, `Occupant` + `SetOccupant`, `CanInteract` false while occupied -- an NPC bed
- [x] `NpcRoutineBlock.cs` / `NpcRoutine.cs` -- `NpcActivity.Sleep` + `bed` field; send the bed (spot = its approach); missing bed / approach → one warning, stand -- routine data
- [x] `EntityPerception.cs` -- `IsAsleep` (set by the brain): visibility 0, proximity 0, hearing on in every mode incl. witness; `CanSeeTheft` false -- deaf-blind sleeper
- [x] `EntityBrain.cs` -- `EntityState.Sleeping`; lie down on routine arrival at a bed (agent `updatePosition` off, snap to anchor, `SetSleeping`, occupy bed) unless woken and not yet quiet; full awareness / radius hostile / hit / block change / `WakeByPlayer()` → leave pose (Warp approach, free bed); non-trespass wake → `WokenBarks` bubble, wait-quiet flag; `IsUnawareOf` and `HandleHealthChanged` include Sleeping; knockout / death / disable leave the pose; `IsAsleep` getter -- sleep behaviour
- [x] `NPCPresence.cs` -- asleep → prompt "Wake", `Interact` → `_brain.WakeByPlayer()` -- wake by hand
- [x] `WitnessProfileSO.cs` + `StealthConfigSO` -- `WokenBarks`; `sleepResumeDelay` 10 s, wake bubble duration / priority -- data
- [x] `Data/...` -- `Barks_Woken` (4–5 grumbles), assigned on `WitnessProfile_Humanoid` -- content
- [x] `HomeYardDresser.cs` -- drop `HobbBed`; StartingTown: delete the dressed `HobbBed`, add `PlayersZone/HobbsBed` (`Bed_WorkerPallet` at the same pose, `Ownership` → `NPC_TentMate`, approach + anchor children); TentMate routine 21–06 = Sleep (bed); rebake -- Hobb test case
- [x] `Assets/Tests/EditMode/NpcSleepRulesTests.cs` -- pure `NpcSleepRules` (`ShouldLieDown`, `IsQuiet`) -- matrix edge cases
- [x] `Scripts/AI/CLAUDE.md`, `Stealth/CLAUDE.md`, `Controllers/CLAUDE.md`, `World/CLAUDE.md`, `Scenes/CLAUDE.md` -- document

**Acceptance Criteria:**
- Given 21:00 in play, when Hobb reaches his bed, then he lies on it with the sleeping loop and the F3 overlay shows sight range 0 and hearing circles.
- Given Hobb asleep, when the player sneaks right beside him, then awareness never rises.
- Given Hobb woken outside a trespass, when the player walks 6 m away and waits 10 s, then he lies back down.
- Given the edited files, when the EditMode suite runs, then all tests pass and the console has no new errors.

## Design Notes

Pose: the brain sets `_agent.updatePosition/updateRotation = false` and places the root on `NpcSleepAnchor`. To get up, it calls `Warp(approach)` and turns updates back on. One `ExitSleepPose(bool warp)` handles every exit. Tune the anchor visually in play mode.

Woken non-trespass flow: `ResumeNonCombat()` with `_sleepWaitQuiet = true`. Routine arrival at a bed lies down only when `NpcSleepRules.ShouldLieDown(waitQuiet, quietTime, delay)`. `quietTime` grows while the player is beyond the walk-hearing radius and is reset otherwise. While waiting, he faces the player.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- all pass
- `read_console` -- no compile errors

**Manual checks:**
- Play StartingTown at 21–06 (dev skip arrival): bedtime walk, sneak by, walk-in trespass, Wake prompt, morning get-up.

## Suggested Review Order

**Sleep state in the brain**

- Entry point: a routine target with a bed lies the NPC down on arrival.
  [`EntityBrain.cs:666`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L666)

- Lie down: agent updates off, root on the anchor, bed claimed, perception asleep.
  [`EntityBrain.cs:2501`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2501)

- Wake routing: trespass response, grumble, or hostile reaction; monsters by radius.
  [`EntityBrain.cs:2534`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2534)

- Single exit point for every wake: animation, perception, bed, agent restored.
  [`EntityBrain.cs:2606`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2606)

- Woken sleeper waits for 10 s of quiet, facing the player, before lying down again.
  [`EntityBrain.cs:2472`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2472)

- "Wake" by hand; inside the owner's area it becomes the trespass response.
  [`EntityBrain.cs:2564`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2564)

**Other ways out of bed**

- Hit in bed: up first, then the normal damage reaction.
  [`EntityBrain.cs:2092`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2092)

- Stagger / knockdown in bed: up, routine walked to after recovery.
  [`EntityBrain.cs:2169`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2169)

- Block change: same bed sleeps on, otherwise gets up; wait flag cleared per bed.
  [`EntityBrain.cs:2422`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2422)
  [`EntityBrain.cs:2331`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2331)

**Hearing-only perception**

- Asleep: sight and proximity 0, hearing in every mode (witness included).
  [`EntityPerception.cs:218`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L218)
  [`EntityPerception.cs:246`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L246)

**Routine, bed and interaction**

- Sleep block sends the bed; spot becomes its approach point.
  [`NpcRoutine.cs:147`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L147)

- NPC bed points and occupancy; occupied bed not usable by the player.
  [`Bed.cs:43`](../../Assets/_Game/Scripts/World/Bed.cs#L43)

- "Wake" prompt on a sleeper instead of "Talk".
  [`NPCPresence.cs:61`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L61)

**Peripherals**

- Animator bridge for `IsSleeping`.
  [`HumanoidAnimationBridge.cs:269`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L269)

- Tunables: resume delay and woken bubble.
  [`StealthConfigSO.cs:114`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L114)

- F3 overlay: no cones while asleep, "Asleep" label.
  [`StealthDebugOverlay.cs:151`](../../Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs#L151)

- Dresser no longer places the decorative bed.
  [`HomeYardDresser.cs:172`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L172)

- Pure rules tests.
  [`NpcSleepRulesTests.cs:1`](../../Assets/Tests/EditMode/NpcSleepRulesTests.cs#L1)
