# CLAUDE.md — Assets/_Game/Art/Characters/Player/Animations/Combat

> Loaded when Claude accesses files in this folder. Covers attack animation structure, animation events, and hitbox/combo timing per weapon type.

---

## Folder Structure

```
Combat/
├── attacks/
│   ├── Sword/          — Sword attack clips (AttackLeft, AttackOverhead, AttackThrust) + SwordIdle + Sword_AnimatorOverride
│   └── Unarmed/        — Unarmed attack clips (Jab, Uppercut Jab) + Unarmed Idle
├── Block Idle.fbx
├── dodge roll.fbx
└── Dodge back.fbx
```

---

## Animation Events — Attack Clips

All attack clips carry **four animation events** stored in the FBX `.meta` file under `clipAnimations[].events`.
The `time` field is **normalized time** (0.0–1.0).

### Event Routing

```
FBX clip fires event
  → AnimationEventReceiver (MonoBehaviour on Player root GO, same GO as Animator)
    → PlayerCombat public method
```

| Event name | `AnimationEventReceiver` method | `PlayerCombat` method | Effect |
|---|---|---|---|
| `HitboxEnable` | `HitboxEnable()` | `OnHitboxEnable()` | Opens hit window — activates weapon/unarmed collider |
| `HitboxDisable` | `HitboxDisable()` | `OnHitboxDisable()` | Closes hit window — deactivates collider |
| `ComboWindowOpen` | `ComboWindowOpen()` | `OnComboWindowOpen()` | Input accepted for next combo step |
| `ComboWindowClose` | `ComboWindowClose()` | `OnComboWindowClose()` | Combo chain resets; window closed |

> **Exact string match required.** Unity finds receiver methods by function name string — a typo compiles fine but silently does nothing.

---

### Sword Attacks (`attacks/Sword/`)

| Clip | Attack state | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------------|-------------|--------------|-----------------|-----------------|
| `AttackLeft.fbx` | Attack_1 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackOverhead.fbx` | Attack_2 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackThrust.fbx` | Attack_3 | 0.25 | 0.50 | 0.50 | 0.90 |

`HitboxDisable` and `ComboWindowOpen` fire at the same normalized time (0.50). Unity dispatches them in listed order — both are independent so dispatch order is safe.

---

### Unarmed Attacks (`attacks/Unarmed/`)

Unarmed clips have custom timings tuned to their animation arcs (faster connect, different wind-up).

| Clip | Attack state | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------------|-------------|--------------|-----------------|-----------------|
| `Jab.fbx` | Attack_1 | 0.211 | 0.332 | 0.356 | 0.924 |
| `Uppercut Jab.fbx` | Attack_2/3 | 0.339 | 0.572 | 0.628 | 0.928 |

> **Timing tuning:** Edit `time` values directly in the `.meta` file. `HitboxEnable` ~0.2–0.25 = weapon reaches target zone; `HitboxDisable` ~0.35–0.5 = weapon retracts. Thrust/jab clips typically need earlier `HitboxEnable` than overhead swings.

---

## Adding Events to a New Attack Clip

Events are authored directly in the FBX `.meta` file. Each event block under `clipAnimations[].events`:

```yaml
events:
- time: 0.25
  functionName: HitboxEnable
  data:
  objectReferenceParameter: {fileID: 0}
  floatParameter: 0
  intParameter: 0
  messageOptions: 0
- time: 0.5
  functionName: HitboxDisable
  data:
  objectReferenceParameter: {fileID: 0}
  floatParameter: 0
  intParameter: 0
  messageOptions: 0
- time: 0.5
  functionName: ComboWindowOpen
  data:
  objectReferenceParameter: {fileID: 0}
  floatParameter: 0
  intParameter: 0
  messageOptions: 0
- time: 0.9
  functionName: ComboWindowClose
  data:
  objectReferenceParameter: {fileID: 0}
  floatParameter: 0
  intParameter: 0
  messageOptions: 0
```

All four events must be present on every attack clip. If any is missing, the corresponding receiver method silently doesn't fire — the hitbox stays open forever or the combo window never opens.

---

## SMB_AttackState — Animator Wiring

`SMB_AttackState` (`Assets/_Game/Scripts/Combat/SMB_AttackState.cs`) is a `StateMachineBehaviour` attached directly to each attack state in `PlayerAnimatorController`. It provides **guaranteed** enter/exit callbacks that fire regardless of whether animation events ran (e.g. interrupted mid-clip).

**Attachment:** Each of the three attack states (`Attack_1`, `Attack_2`, `Attack_3`) has one `SMB_AttackState` instance with `attackIndex` set to 1, 2, or 3 respectively.

**Setting `attackIndex`:** In the Unity Animator window, select the attack state → Inspector shows the `SMB_AttackState` component → set `Attack Index` to match the state number. This value is passed to `PlayerCombat.OnAttackStateEntered(attackIndex)` and used for logging only.

**Controller YAML reference:** SMBs are stored as `!u!114` MonoBehaviour blocks appended to `PlayerAnimatorController.controller`, and referenced from the state via `m_StateMachineBehaviours`. GUID: `be56e776511a4e3458f4511c065cbeac`.

> **Do NOT use `GetNextAnimatorStateInfo(layerIndex).IsTag(...)` in `OnStateExit` to detect combo chains.** Unity may complete the transition before `OnStateExit` fires, returning empty `AnimatorStateInfo`. Use the `_IsComboAttacking` flag in `PlayerCombat` instead (see `Assets/_Game/Scripts/Combat/CLAUDE.md`).

---

## Code Review Checklist — Combat Animations

| Severity | Pattern |
|----------|---------|
| HIGH | New attack clip missing one or more of the four animation events — silent no-op: hitbox stays open, or combo never chains |
| HIGH | `functionName` typo in `.meta` event — Unity matches by exact string; no compile error, just a silent miss |
| HIGH | `SMB_AttackState` missing from an attack state — no enter/exit guarantee; hitbox may persist across interrupt (dodge/death) |
| MEDIUM | `attackIndex` not set on `SMB_AttackState` in Animator Inspector — defaults to 0; all states report index 0 to `PlayerCombat` (logging only, no gameplay impact) |
| MEDIUM | Unarmed clip events copied verbatim from sword timings (0.25/0.50/0.50/0.90) without tuning — unarmed hits connect earlier; use custom timings per animation arc |
