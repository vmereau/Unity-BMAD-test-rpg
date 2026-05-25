# CLAUDE.md — Assets/_Game/Scripts/Core/Animations

> Loaded when Claude accesses files in this folder. Covers the AI animation polymorphism
> hierarchy and the Brain → Driver → Bridge contract.

---

## Architecture

```
EntityBrain ─┐                                       ┌─ MonsterAnimationBridge   (pure parameter writes)
             ├─→ AIAnimationDriver ─┬─ MonsterAnimationDriver  ───→ ┤
EntityHealth ┘                      │                              └─ Animator (monster controller)
                                    │
                                    └─ HumanoidAIAnimationDriver ─→ HumanoidAnimationBridge (pure parameter writes)
                                                                                   │
                                                                                   └─→ Animator (Humanoid_Template)
```

`AIAnimationDriver` is the polymorphic seam. `EntityBrain` and `EntityHealth` serialize a
`[SerializeField] AIAnimationDriver _animationDriver` — Unity does not serialize C# interface
fields, so the seam is an abstract MonoBehaviour.

**The Player does NOT flow through this hierarchy.** Player uses
`PlayerAnimationDriver → HumanoidAnimationBridge` directly because the Player is not AI-driven.
Do not put the Player on `AIAnimationDriver`.

---

## Bridge vs Driver

| Layer | Responsibility |
|-------|----------------|
| **Bridge** (`MonsterAnimationBridge`, `HumanoidAnimationBridge`) | Pure animator-parameter wrapper. One method per animator parameter. No lifecycle, no smoothing, no SO logic. |
| **Driver** (`MonsterAnimationDriver`, `HumanoidAIAnimationDriver`, `PlayerAnimationDriver`) | Owns lifecycle (ragdoll, AnimatorOverride application, death-component-disable), smoothing, velocity → animator-parameter math. Calls into the bridge. |

Symmetric on both sides: bridge + driver.

---

## Rules

- **AI code never references concrete bridge or driver types.** `EntityBrain`, `EntityHealth`,
  `SMB_DeathState` must reference `AIAnimationDriver` only. Concrete bindings happen in the
  prefab inspector.
- **One Animator per entity, owned by its matching bridge.** The monster bridge owns the
  monster controller's Animator; the humanoid bridge owns `Humanoid_Template`'s Animator.
  Never write to an Animator from outside its bridge.
- **`MonsterAnimationDriver` script GUID is `bc1ff05bbb035a34cb7a7f54f833aa88`** — preserved
  from the original `EntityAnimationBridge.cs` via file `mv` (file + `.meta` renamed in
  lockstep). Do not regenerate the `.meta` or every monster prefab loses its driver reference.
- **`HumanoidAIAnimationDriver._runSpeed` must match the variant's `Entity.EngageSpeed`** —
  the 2D blend tree normalizes against `_runSpeed`. If a humanoid type uses
  `EngageSpeed = 6` but the driver's `_runSpeed` stays `4`, the blend tree saturates at 1.0
  before the agent reaches full speed.
- **Humanoid AI combat triggers are stubs** — `TriggerAttack` / `TriggerGetHit` /
  `TriggerDeath` / `EnableRagdoll` on `HumanoidAIAnimationDriver` warn-log and no-op. Implement
  when the humanoid AI combat story is created. Until then, `EntityHealth` damage flow to
  NPCs will warn-log without NRE.
- **`SetWarning(bool)` is a held bool, not a trigger.** The warning telegraph must hold for the
  multi-second warning timer and exit cleanly, so the seam method takes a bool (not a one-shot
  trigger). `MonsterAnimationBridge.SetWarning` writes the `IsWarning` bool animator param;
  `MonsterAnimationDriver.SetWarning` forwards to it. `HumanoidAIAnimationDriver.SetWarning`
  warn-logs only on `active == true` (so it logs once on warning enter, silent on the `false`
  exit) — humanoid warning animation is deferred to the humanoid AI combat epic. `EntityBrain`
  owns the warning *logic* (stop, face, timer, escalate) identically for both; only the humanoid
  *animation* is stubbed. `SetBool` on a controller missing the `IsWarning` param is a silent
  no-op in Unity, so the C# is safe to ship before the controller is wired.
- **NavMeshAgent humanoid AI is always grounded** — `HumanoidAIAnimationDriver.DriveLocomotion`
  hard-codes `IsGrounded = true`, `IsRising = false`. Revisit if AI ever leaves the navmesh.

---

## Code Review Checklist — Animations

| Severity | Pattern |
|----------|---------|
| HIGH | `EntityBrain`, `EntityHealth`, or `SMB_DeathState` referencing a concrete driver/bridge type instead of `AIAnimationDriver` |
| HIGH | `Animator.Set*` call from outside the matching bridge component |
| HIGH | Player code routed through `AIAnimationDriver` — Player is not AI |
| MEDIUM | `HumanoidAIAnimationDriver._runSpeed` left at default `4f` on a variant whose `Entity.EngageSpeed > 4` |
| MEDIUM | New monster `Trigger*` parameter added to `MonsterAnimationBridge` but not exposed via `AIAnimationDriver` virtual method — humanoid driver can't no-op-stub it |
| MEDIUM | `IsWarning` animator param missing on a monster controller using warning detection — `SetBool` no-ops silently, so the warning telegraph never plays (no error, no clip). Verify the param name matches exactly (case-sensitive). |
