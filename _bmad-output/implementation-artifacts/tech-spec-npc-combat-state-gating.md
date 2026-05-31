---
title: 'NPC Combat-State Gating & Interaction Suppression'
slug: 'npc-combat-state-gating'
created: '2026-05-30'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack:
  - 'Unity 6000.3.10f1 (Unity 6.3 LTS)'
  - 'C# / .NET Standard 2.1'
  - 'Unity Test Framework (NUnit) — EditMode tests'
files_to_modify:
  - 'Assets/_Game/Scripts/AI/EntityBrain.cs'
  - 'Assets/_Game/Scripts/AI/NPCPresence.cs'
  - 'Assets/_Game/Scripts/World/IInteractable.cs'
  - 'Assets/_Game/Scripts/World/InteractionSystem.cs'
  - 'Assets/_Game/Scripts/World/InteractableObject.cs'
  - 'Assets/_Game/Scripts/World/ContainerInteractable.cs'
  - 'Assets/_Game/Scripts/Inventory/ItemPickup.cs'
  - 'Assets/Tests/EditMode/InteractionSystemTests.cs'
files_to_create:
  - 'Assets/_Game/Scripts/AI/ICombatStateProvider.cs'
code_patterns:
  - 'Interface-based capability contract (ICombatStateProvider) implemented by EntityBrain; consumers depend on the interface via GetComponent, not the concrete brain'
  - 'Single writer funnel: replace scattered _animationDriver?.SetInCombat(bool) calls with one private SetCombatState(bool) that updates the IsInCombat backing field and drives the animator'
  - 'NPCPresence POLLS IsInCombat in CanInteract — no C# event subscription (project-context.md line 50: cross-system C# event Action is forbidden; GameEventSO is the only cross-boundary channel)'
  - 'IInteractable gains a CanInteract gate read by InteractionSystem before showing the prompt / invoking Interact()'
  - 'GameLog with per-class TAG constant; never Debug.Log'
test_patterns:
  - 'EditMode pure-logic tests mirroring InteractionSystemTests (StubInteractable + ResolvePrompt-style mirror helpers, no MonoBehaviour) for the CanInteract gate'
---

# Tech-Spec: NPC Combat-State Gating & Interaction Suppression

**Created:** 2026-05-30

## Overview

### Problem Statement

The faction-based targeting system (`tech-spec-faction-based-targeting.md`) landed and its
"friendly NPC assist" follow-up is now partially self-solving: by configuring
`Faction_Neutral.hostileFactions = [Faction_Monsters]`, neutral NPCs (e.g. `NPC_Guard`) already
engage monsters via `TargetRegistry.FindClosestHostile`. When a monster chases the player, nearby
NPCs acquire and attack that monster — an "assist" emerges from data alone, no new AI code.

Two gaps remain, both rooted in the same missing concept — **a readable "am I in combat?" state on
the entity**:

1. **No clean combat-state signal.** Combat status lives only as scattered, fire-and-forget
   `_animationDriver?.SetInCombat(bool)` calls inside eight `EntityBrain` transition methods. Nothing
   outside the brain can ask "is this entity fighting right now?". Future assist-AI and UI need this.
2. **Player can talk to an NPC mid-fight.** `NPCPresence` (an `IInteractable`) only blocks
   interaction when the NPC is *dead*. While an NPC guard is actively warning/engaging/attacking a
   monster, the player still gets a "Talk" prompt and can open dialogue — breaking immersion and
   letting the player freeze a combatant into a conversation.

### Solution

Introduce a small **combat-state capability** and gate interaction on it:

1. **`ICombatStateProvider`** interface — `bool IsInCombat { get; }`. `EntityBrain` implements it.
   (No C# event — see Technical Decision 1; `project-context.md` forbids `event Action` across system
   boundaries. Consumers poll.)
2. **Funnel `SetInCombat` through one writer** in `EntityBrain`: a private `SetCombatState(bool)`
   that (a) updates the backing `IsInCombat` field and (b) keeps driving `_animationDriver.SetInCombat`
   exactly as today. All eight existing scattered `_animationDriver?.SetInCombat(...)` calls are
   replaced by `SetCombatState(...)`. In-combat spans **Warning OR Engaging OR Attacking** (verbatim
   with the current `SetInCombat(true)` call placement).
3. **Add `bool CanInteract { get; }` to `IInteractable`.** `NPCPresence` returns
   `false` while its `ICombatStateProvider.IsInCombat` is true (and keeps its existing dead-check).
   Other implementers (`InteractableObject`, `ContainerInteractable`, `ItemPickup`) return `true`.
4. **`InteractionSystem` honors `CanInteract`** — a candidate with `CanInteract == false` is skipped
   when selecting the prompt target and is never invoked on key-press. The **name-tag scan is left
   untouched**, so an in-combat NPC still shows its floating name but offers no "Talk" prompt.

### Scope

**In Scope:**

- New `ICombatStateProvider` interface in `Game.AI` (`bool IsInCombat { get; }`, no event).
- `EntityBrain` implements `ICombatStateProvider`; add backing `IsInCombat`; add private
  `SetCombatState(bool)` writer; replace all 8 `_animationDriver?.SetInCombat(...)` call sites with it.
- Add `bool CanInteract { get; }` to `IInteractable`.
- `NPCPresence`: cache `ICombatStateProvider` in `Awake`; implement `CanInteract => !dead && !IsInCombat`;
  guard `Interact()` with the same condition (defense in depth).
- `InteractionSystem`: skip `!CanInteract` candidates for the prompt/interaction target; **leave the
  name-tag scan unchanged**.
- Implement `CanInteract => true` on `InteractableObject`, `ContainerInteractable`, `ItemPickup`, and
  the test `StubInteractable`.
- EditMode tests for the `CanInteract` gate (prompt suppression + interaction suppression).

**Out of Scope:**

- Building any new friendly/assist AI behavior — already emergent from faction data; not re-touched here.
- Player-side `PlayerStateManager.IsInCombat` (separate, already-existing concept for weapon draw/sheathe).
- Hiding the floating **name tag** in combat (decision: keep it; only the "Talk" prompt is suppressed).
- New AI states/transitions in `EntityBrain` — only the combat-flag plumbing changes; the state
  machine is untouched.
- Retaliation / aggro-on-attack for neutral NPCs (still deferred from the faction spec).
- Faction-based dialogue gating beyond the in-combat case (e.g. hostile NPCs refusing to talk).

## Context for Development

### Codebase Patterns

- **Single-writer state funnel:** `EntityBrain` currently calls `_animationDriver?.SetInCombat(bool)`
  in 8 places (`TransitionToWarning/Engaging/Attacking`, `TransitionToIdle/Patrol/Dead`,
  `CancelWarning`, `DisengageFromCombat`). These become `SetCombatState(bool)`, which remains the only
  place the animator is told about combat — eliminating drift between the flag and the animation.
- **Interface capability via `GetComponent`** (precedent: `IDamageable` from the faction spec,
  resolved through `GetComponent<IDamageable>()`). `NPCPresence` resolves `ICombatStateProvider` the
  same way. EntityBrain + NPCPresence live on the same root GO (confirmed: both sit beside
  `EntityHealth`/`FactionMember`; both are in the `Game.AI` namespace, so no new `using`).
- **`AIAnimationDriver.SetInCombat(bool active)` is `public virtual`** on the base
  (`AIAnimationDriver.cs:20`), overridden by `HumanoidAIAnimationDriver` (`:74`) → `HumanoidAnimationBridge.SetInCombat`
  → `Animator.SetBool(IsInCombatHash, …)`. The funnel keeps calling `_animationDriver?.SetInCombat(...)`
  unchanged — no animation behavior change. A source comment there already states "the Warning state in
  EntityBrain is handled via SetInCombat(true)", confirming Warning⊆in-combat.
- **`InteractionSystemTests` use pure-logic mirror helpers** (`InteractionSystemTests.cs`): no
  MonoBehaviour, no scene — private helpers re-implement `InteractionSystem` logic (`ResolvePrompt`,
  `ShouldHighlight`, `HasStateChanged`). New gate tests add a `StubInteractable.CanInteract` field and
  a mirror helper for the "skip when `!CanInteract`" selection rule.
- **All non-NPC implementers are trivial:** `InteractableObject`, `ContainerInteractable`, `ItemPickup`
  each just add `public bool CanInteract => true;` — none have a combat concept.
- **`IInteractable` contract** (`Assets/_Game/Scripts/World/IInteractable.cs`): three members today
  (`InteractPrompt`, `NameTag`, `Interact`). Adding `CanInteract` is a breaking interface change —
  every implementer must add it. Implementers found: `NPCPresence`, `InteractableObject`,
  `ContainerInteractable`, `ItemPickup`, plus `StubInteractable` in `InteractionSystemTests`.
- **`InteractionSystem` prompt vs name-tag split:** the prompt target (`CurrentInteractable`) is the
  closest-by-angle candidate in the `interactionRange` spherecast; the name-tag list is a separate
  `nameRange` spherecast loop. Gating only the former preserves name tags in combat (the chosen UX).
- **`GameLog` with per-class `TAG` constant** — never `Debug.Log`.
- **OnDisable null-guard rule** (root CLAUDE.md): if `NPCPresence` subscribes to
  `CombatStateChanged` in `OnEnable`, it must unsubscribe in `OnDisable` with a null guard. (Polling
  `IsInCombat` in `CanInteract` avoids subscription entirely — preferred unless an event is needed.)

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | Implements `ICombatStateProvider`; funnels the 8 `SetInCombat` calls through `SetCombatState`. Lines 351, 363, 370, 379, 396, 403, 411, 420 are the call sites. |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Resolve `ICombatStateProvider` in `Awake`; implement `CanInteract`; guard `Interact()`. Already has the dead-check pattern at lines 39–48. |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Add `bool CanInteract { get; }` member. |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Skip `!CanInteract` candidate for prompt/interaction (line 95 selection loop, line 142 invoke). Leave name-tag loop (line 123) unchanged. |
| `Assets/_Game/Scripts/World/InteractableObject.cs` | Add `CanInteract => true`. |
| `Assets/_Game/Scripts/World/ContainerInteractable.cs` | Add `CanInteract => true`. |
| `Assets/_Game/Scripts/Inventory/ItemPickup.cs` | Add `CanInteract => true`. |
| `Assets/Tests/EditMode/InteractionSystemTests.cs` | `StubInteractable` (line 21) needs `CanInteract`; add gate tests near `ResolvePrompt` helpers. |
| `Assets/_Game/Scripts/World/InteractableObject.cs`, `ContainerInteractable.cs`, `Inventory/ItemPickup.cs` | Each adds `public bool CanInteract => true;` — trivial, no behavior change. |
| `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs` (`:20`), `HumanoidAIAnimationDriver.cs` (`:74`), `HumanoidAnimationBridge.cs` (`:41`) | `SetInCombat(bool)` chain the funnel keeps calling. Reference only — no changes. |
| `_bmad-output/project-context.md` (lines 50, 216) | Rule: no C# `event Action` across system boundaries — drives Technical Decision 1 (poll, don't subscribe). |
| `_bmad-output/implementation-artifacts/tech-spec-faction-based-targeting.md` | Predecessor spec; explains the faction foundation this builds on. |
| `_bmad-output/project-context.md` | Authoritative coding rules (57). Mandatory read before implementing. |

### Technical Decisions

1. **`ICombatStateProvider` interface, poll-only — no C# event.** The Step-1 preview showed a
   `CombatStateChanged` event, but `project-context.md` (lines 50, 216) forbids `event Action` across
   system boundaries (`GameEventSO<T>` is the only sanctioned cross-boundary channel). Since
   `NPCPresence` polls `IsInCombat` inside `CanInteract`, no subscriber exists — the event would be
   dead, rule-violating surface. **Decision: interface exposes only `bool IsInCombat { get; }`.** If a
   future story needs push notification across systems, add a `GameEventSO` channel then. The
   interface (vs a concrete `EntityBrain` getter) still decouples readers and is future-proof for
   assist-AI/UI. *(Flagged for user confirmation in Step 2 report.)*
2. **In-combat = Warning OR Engaging OR Attacking.** Reuses the existing `SetInCombat(true)` call
   placement verbatim — Warning already sets it true, so the gate matches today's animation behavior.
3. **Gate via `CanInteract` on `IInteractable`** (not inside `Interact()` alone) so the
   `InteractionSystem` suppresses the *prompt*, satisfying "no prompt in combat" — gating only inside
   `Interact()` would still show the prompt.
4. **Name tag stays visible in combat.** Only the prompt/interaction is gated; the name-tag scan loop
   in `InteractionSystem` is untouched.
5. **`Interact()` keeps its own guard** (defense in depth) even though `InteractionSystem` won't call
   it on an in-combat NPC — external callers shouldn't bypass the rule.
6. **`SetCombatState(bool)` is idempotent.** It early-returns when the value equals the backing field,
   so it's safe to call from every transition (most transitions already call `SetInCombat` once, but
   the guard keeps the animator/flag write cheap and avoids redundant `SetBool` calls).
7. **`InteractionSystem` gates only the prompt-target selection loop** (`:95`) and the key-press invoke
   (`:142`) — the name-tag scan loop (`:123`) is deliberately left untouched so in-combat NPCs keep
   their floating name. Latency to hide the prompt is bounded by `_config.scanInterval` (existing throttle).

## Implementation Plan

> Ordered by dependency: interface → producer (EntityBrain) → interface change → consumers → gating system → tests.

### Tasks

- [x] **Task 1: Create `ICombatStateProvider` interface**
  - File to create: `Assets/_Game/Scripts/AI/ICombatStateProvider.cs`
  - Action: Write the file with this exact body:
    ```csharp
    namespace Game.AI
    {
        /// <summary>
        /// Read-only capability: "is this entity currently in combat?".
        /// Implemented by EntityBrain. Consumers (e.g. NPCPresence) POLL IsInCombat via
        /// GetComponent&lt;ICombatStateProvider&gt;() — do NOT add a C# event here:
        /// project-context.md (lines 50, 216) forbids `event Action` across system boundaries;
        /// use a GameEventSO channel if push notification is ever needed.
        /// </summary>
        public interface ICombatStateProvider
        {
            bool IsInCombat { get; }
        }
    }
    ```
  - Notes: Lives in `Game.AI` so `EntityBrain` and `NPCPresence` (both `Game.AI`) need no new `using`.

- [x] **Task 2: `EntityBrain` implements `ICombatStateProvider` + `SetCombatState` funnel**
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    1. Change the class declaration:
       `public class EntityBrain : MonoBehaviour` → `public class EntityBrain : MonoBehaviour, ICombatStateProvider`
       (no new `using` — same namespace).
    2. Add the public property + private writer. Place the property near the top with the other public
       surface and the writer alongside the state-transition helpers:
       ```csharp
       public bool IsInCombat { get; private set; }

       // Single writer for combat state. Keeps the readable flag and the animator in lockstep,
       // replacing the previously-scattered _animationDriver?.SetInCombat(...) calls. Idempotent.
       private void SetCombatState(bool inCombat)
       {
           if (IsInCombat == inCombat) return;
           IsInCombat = inCombat;
           _animationDriver?.SetInCombat(inCombat);
       }
       ```
    3. Replace **every** `_animationDriver?.SetInCombat(<x>);` call with `SetCombatState(<x>);`. Exactly
       8 call sites (line numbers from current file):
       - `TransitionToIdle` (351): `SetInCombat(false)` → `SetCombatState(false)`
       - `TransitionToWarning` (363): `SetInCombat(true)` → `SetCombatState(true)` *(leave the adjacent `_animationDriver?.SetWarning(true);` untouched)*
       - `CancelWarning` (370): `SetInCombat(false)` → `SetCombatState(false)` *(leave `SetWarning(false)`)*
       - `TransitionToEngaging` (379): `SetInCombat(true)` → `SetCombatState(true)` *(leave `SetWarning(false)`)*
       - `TransitionToAttacking` (396): `SetInCombat(true)` → `SetCombatState(true)`
       - `TransitionToPatrol` (403): `SetInCombat(false)` → `SetCombatState(false)`
       - `TransitionToDead` (411): `SetInCombat(false)` → `SetCombatState(false)` *(leave `SetWarning(false)`)*
       - `DisengageFromCombat` (420): `SetInCombat(false)` → `SetCombatState(false)`
  - Notes:
    - **Only `SetInCombat` calls move** — all `SetWarning`, `TriggerAttack`, `DriveLocomotion` calls stay
      direct on `_animationDriver`. After this task, grep for `_animationDriver?.SetInCombat` must return zero hits.
    - In-combat therefore spans **Warning OR Engaging OR Attacking**, byte-for-byte with today's animation behavior.
    - The idempotent guard means redundant transitions (e.g. Engaging→Attacking, both true) skip the
      `SetBool` write — a minor improvement, no behavior change.

- [x] **Task 3: Add `CanInteract` to `IInteractable`**
  - File: `Assets/_Game/Scripts/World/IInteractable.cs`
  - Action: Add the member to the interface:
    ```csharp
    namespace Game.World
    {
        public interface IInteractable
        {
            string InteractPrompt { get; }
            string NameTag { get; }
            bool CanInteract { get; }   // NEW — false suppresses the prompt & Interact() in InteractionSystem
            void Interact();
        }
    }
    ```
  - Notes: Breaking change — every implementer (Tasks 4, 5, 7) must add `CanInteract` or the `Game`
    assembly won't compile. Do Tasks 4–5 and 7 in the same change set.

- [x] **Task 4: `NPCPresence` — implement `CanInteract`, poll combat state, guard `Interact()`**
  - File: `Assets/_Game/Scripts/AI/NPCPresence.cs`
  - Action:
    1. Add a cached provider field next to `_entityHealth`:
       ```csharp
       private ICombatStateProvider _combatState;
       ```
       (No new `using` — `ICombatStateProvider` is `Game.AI`, same namespace as `NPCPresence`.)
    2. In `Awake`, after `_entityHealth = GetComponent<EntityHealth>();`, add:
       ```csharp
       _combatState = GetComponent<ICombatStateProvider>(); // null-safe: NPCs without a brain are always interactable
       ```
    3. Implement the interface member (place beside `InteractPrompt`/`NameTag`):
       ```csharp
       public bool CanInteract =>
           (_entityHealth == null || !_entityHealth.IsDead) &&
           (_combatState == null || !_combatState.IsInCombat);
       ```
    4. In `Interact()`, add a combat guard immediately after the existing dead-check block:
       ```csharp
       if (_combatState != null && _combatState.IsInCombat)
       {
           GameLog.Info(TAG, $"{gameObject.name} is in combat — dialogue interaction blocked");
           return;
       }
       ```
  - Notes: `CanInteract` folds in the existing dead-check so a corpse also reports `false` to the
    InteractionSystem (today the corpse still shows a "Talk" prompt that no-ops). The `Interact()` guard
    is defense-in-depth for any non-InteractionSystem caller.

- [x] **Task 5: Add `CanInteract => true` to the non-NPC implementers**
  - Files:
    - `Assets/_Game/Scripts/World/InteractableObject.cs`
    - `Assets/_Game/Scripts/World/ContainerInteractable.cs`
    - `Assets/_Game/Scripts/Inventory/ItemPickup.cs`
  - Action: add to each class, beside its `NameTag` getter:
    ```csharp
    public bool CanInteract => true;
    ```
  - Notes: None of these have a combat concept. Containers/pickups remain always-interactable.

- [x] **Task 6: `InteractionSystem` honors `CanInteract`**
  - File: `Assets/_Game/Scripts/World/InteractionSystem.cs`
  - Action:
    1. In the prompt-target selection loop in `Update()` (the loop at lines 93–105), after the
       `if (candidate == null) continue;` line, add:
       ```csharp
       if (!candidate.CanInteract) continue; // in-combat NPCs (or dead) offer no prompt/interaction
       ```
    2. In `LateUpdate()`, harden the invoke against a stale `CurrentInteractable` (the scan is throttled
       by `_config.scanInterval`, so combat could start between scans):
       ```csharp
       if (CurrentInteractable != null && CurrentInteractable.CanInteract
           && _input.Player.Interact.WasPressedThisFrame())
           CurrentInteractable.Interact();
       ```
    3. **Leave the name-tag scan loop (lines 118–136) untouched** — in-combat NPCs keep their floating name.
  - Notes: Because the gated candidate never becomes `best`, `CurrentInteractable` goes null (or to the
    next valid target) and the crosshair returns to `_defaultColor` automatically via the existing
    `best != _previousInteractable` branch — no extra crosshair handling needed.

- [x] **Task 7: EditMode tests for the `CanInteract` gate**
  - File: `Assets/Tests/EditMode/InteractionSystemTests.cs`
  - Action:
    1. Update `StubInteractable` to satisfy the new interface member, defaulting to interactable:
       ```csharp
       private class StubInteractable : IInteractable
       {
           private readonly string _prompt;
           public StubInteractable(string prompt = "Test Prompt", bool canInteract = true)
           {
               _prompt = prompt;
               CanInteract = canInteract;
           }
           public string InteractPrompt => _prompt;
           public string NameTag => "";
           public bool CanInteract { get; }
           public void Interact() { }
       }
       ```
    2. Add a mirror helper next to `ResolvePrompt` that re-implements the new selection skip
       (`InteractionSystem.cs:96`):
       ```csharp
       // Mirrors InteractionSystem.Update() prompt-candidate gate (skip when !CanInteract)
       private bool IsPromptCandidate(IInteractable interactable) =>
           interactable != null && interactable.CanInteract;
       ```
    3. Add tests under a new section header:
       ```csharp
       // ── CanInteract gate tests ─────────────────────────────────────────────

       [Test]
       public void Gate_CanInteractTrue_IsPromptCandidate()
       {
           var stub = new StubInteractable("Talk", canInteract: true);
           Assert.IsTrue(IsPromptCandidate(stub));
       }

       [Test]
       public void Gate_CanInteractFalse_IsNotPromptCandidate()
       {
           var stub = new StubInteractable("Talk", canInteract: false);
           Assert.IsFalse(IsPromptCandidate(stub));
       }

       [Test]
       public void Gate_NullCandidate_IsNotPromptCandidate()
       {
           Assert.IsFalse(IsPromptCandidate(null));
       }

       [Test]
       public void Gate_DefaultStub_IsInteractable()
       {
           // Regression guard: existing stubs must default to interactable so prior tests stay valid.
           Assert.IsTrue(new StubInteractable().CanInteract);
       }
       ```
  - Notes: Pure-logic mirror style (no MonoBehaviour) matches the file's existing pattern. The mirror
    helper carries the documented caveat already at the top of the file — if `InteractionSystem.Update`
    changes its gate, this helper must track it.

### Acceptance Criteria

- [ ] **AC1 — Combat state is readable and tracks the state machine**
  - **Given:** an `EntityBrain` on a hostile entity.
  - **When:** it transitions Idle/Patrol → Warning → Engaging → Attacking and back via `DisengageFromCombat`.
  - **Then:** `ICombatStateProvider.IsInCombat` reads `true` for the Warning/Engaging/Attacking span and
    `false` in Idle/Patrol/Dead, matching the `_animationDriver` `IsInCombat` animator bool at every step.

- [ ] **AC2 — `SetInCombat` is funneled through one writer (regression gate)**
  - **Given:** the refactored `EntityBrain.cs`.
  - **When:** running `Grep` for `_animationDriver?.SetInCombat` across `Assets/_Game/Scripts/`.
  - **Then:** zero matches in `EntityBrain` — every former call site routes through `SetCombatState`;
    the only remaining `_animationDriver?.SetInCombat` literal call lives inside `SetCombatState`.

- [ ] **AC3 — NPC offers no "Talk" prompt while in combat**
  - **Given:** an `NPC_Guard` whose `Entity.Faction = Faction_Neutral` (hostile to `Faction_Monsters`),
    engaged with a monster so its `EntityBrain.IsInCombat == true`.
  - **When:** the player aims the crosshair at the guard within `interactionRange`.
  - **Then:** `InteractionSystem` shows **no** interaction prompt and the crosshair does not highlight;
    pressing Interact does nothing.

- [ ] **AC4 — NPC name tag stays visible while in combat**
  - **Given:** the same in-combat `NPC_Guard` within `nameRange`.
  - **When:** the player looks at it.
  - **Then:** the floating name tag still renders (name-tag scan is untouched); only the actionable prompt is gone.

- [ ] **AC5 — Interaction resumes after disengage**
  - **Given:** the `NPC_Guard` kills/loses its target and `DisengageFromCombat` runs (`IsInCombat → false`).
  - **When:** the player aims at it within `interactionRange` (after at most one `_config.scanInterval`).
  - **Then:** the "Talk" prompt reappears and `Interact()` opens dialogue as before.

- [ ] **AC6 — Non-NPC interactables are unaffected (regression gate)**
  - **Given:** a `ContainerInteractable`, an `ItemPickup`, and an `InteractableObject` in a scene.
  - **When:** the player aims at each.
  - **Then:** each reports `CanInteract == true`; prompts and `Interact()` behave exactly as before this change.

- [ ] **AC7 — Dead NPC reports `CanInteract == false`**
  - **Given:** an NPC whose `EntityHealth.IsDead == true`.
  - **When:** the player aims at the corpse.
  - **Then:** `CanInteract` is `false`; no "Talk" prompt shows (today it shows a no-op prompt). `Interact()`
    remains guarded for the future loot-corpse hook noted in `NPCPresence`.

- [ ] **AC8 — `Game` assembly compiles; all `IInteractable` implementers updated (regression gate)**
  - **Given:** the change set.
  - **When:** Unity recompiles.
  - **Then:** zero compile errors; `NPCPresence`, `InteractableObject`, `ContainerInteractable`,
    `ItemPickup`, and the test `StubInteractable` all define `CanInteract`.

- [ ] **AC9 — EditMode tests pass**
  - **Given:** Tasks 1–7 complete.
  - **When:** the Unity Test Runner runs the Edit Mode tab.
  - **Then:** the 4 new `Gate_*` tests pass and all pre-existing `InteractionSystemTests` still pass
    (the `StubInteractable` default keeps them valid); no other test class regresses.

## Additional Context

### Dependencies

- **No new packages.** Builds on the completed faction-targeting work; the "assist" behavior is already
  live because the user wired `Faction_Neutral.hostileFactions = [Faction_Monsters]`.
- **No upstream blockers.** All touched files exist on `main`.
- **Single change set required:** Task 3 (interface change) breaks compilation until Tasks 4, 5, and 7
  land — implement them together before recompiling.

### Testing Strategy

- **EditMode (primary):** the `CanInteract` gate is pure logic — covered by 4 new `Gate_*` tests in
  `InteractionSystemTests` using the mirror-helper pattern already established in that file.
- **No automated test for `EntityBrain.IsInCombat`** beyond the existing brain coverage — the funnel is a
  mechanical call-site swap; AC1/AC2 are verified by grep + manual play. (A PlayMode test would need a
  full NavMesh scene; out of proportion for v1.)
- **Manual scene verification (regression gates AC3–AC5):**
  1. Load `StartingTown` / `BanditCamp`; position so an `NPC_Guard` engages a monster.
  2. Aim at the guard mid-fight → confirm **no** "Talk" prompt, crosshair un-highlighted, name tag still shown.
  3. Let the guard win/disengage → confirm the prompt returns and dialogue opens.
  4. Aim at a chest / item pickup → confirm unchanged behavior (AC6).

### Notes

- **Pre-mortem — highest-risk failure modes:**
  - **Missed `IInteractable` implementer → compile break.** Adding `CanInteract` is a breaking change.
    Mitigation: AC8 enumerates all 5 implementers (4 prod + 1 test stub); grep `: IInteractable` /
    `IInteractable\b` before compiling.
  - **Mirror-helper drift.** `InteractionSystemTests` re-implements `InteractionSystem` logic in helpers.
    If Task 6's gate is later moved/altered, `IsPromptCandidate` silently diverges. Mitigation: the
    file's top-of-class caveat already warns this; AC9 + the `Gate_DefaultStub_IsInteractable` regression
    test catch the common default-flip mistake.
  - **Stale `CurrentInteractable` across a scan interval.** Combat can begin between throttled scans, so
    for up to `_config.scanInterval` the cached target could still be the now-in-combat NPC. Mitigation:
    Task 6 step 2 re-checks `CanInteract` at the `LateUpdate` invoke site.
  - **Idempotent guard hides a desired re-fire.** `SetCombatState` early-returns on equal values. This is
    safe today (nothing depends on repeated `SetInCombat(true)`); if a future consumer needs edge events,
    add a `GameEventSO` rather than removing the guard.
- **Why poll instead of event?** `project-context.md` (50, 216) bans cross-system C# `event Action`.
  Polling `IsInCombat` in `CanInteract` (evaluated only during the throttled interaction scan) is cheap
  and rule-clean. Push notification, if ever needed, goes through a typed `GameEventSO` channel.
- **Why fold the dead-check into `CanInteract`?** It unifies "not targetable for talk" in one place and
  fixes a latent oddity (a corpse currently still shows a no-op "Talk" prompt). The `Interact()` dead
  TODO (future loot-corpse hook) is preserved.
- **CLAUDE.md candidates to flag after implementation:**
  - `ICombatStateProvider` poll-not-event decision (cross-system event ban) → `Assets/_Game/Scripts/AI/CLAUDE.md`.
  - `IInteractable.CanInteract` gate + "name tag survives, prompt does not" rule → `Assets/_Game/Scripts/World/` notes.
  - Single-writer `SetCombatState` funnel pattern (flag + animator in lockstep) → `Assets/_Game/Scripts/AI/CLAUDE.md`.
