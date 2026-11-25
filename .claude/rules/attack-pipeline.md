---
paths:
  - "Assets/_Game/Scripts/Combat/**"
  - "Assets/_Game/Scripts/AI/**"
  - "Assets/_Game/Scripts/Core/Animations/**"
  - "Assets/_Game/Art/Characters/Humanoids/Animations/**"
  - "Assets/_Game/Art/Characters/Humanoids/Controllers/**"
  - "Assets/_Game/Art/Characters/Monsters/**"
  - "Assets/_Game/Editor/HitboxTuner*.cs"
  - "Assets/_Game/Prefabs/Entities/**"
  - "Assets/_Game/Prefabs/Player/**"
  - "Assets/_Game/Scripts/Player/PlayerAttackStep.cs"
  - "Assets/_Game/Scripts/Player/ActionRootMotion*.cs"
  - "Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs"
  - "Assets/_Game/ScriptableObjects/Items/Weapons/**"
---

# Attack Pipeline — Shared Contract (Player + AI)

Melee attacks for the Player and for AI entities follow one pipeline. Folder files hold the local
details: `Scripts/Combat/CLAUDE.md` (`WeaponHitbox`), `Scripts/Combat/PLAYER_COMBO.md` (player combo),
`Scripts/AI/COMBAT.md` (`EntityMeleeAttacker`), `Art/Characters/Humanoids/Controllers/CLAUDE.md`
(controller authoring), `Art/Characters/Humanoids/Animations/Combat/CLAUDE.md` (player clip timings).

```
clip event ─→ receiver on the Animator GO ─→ owner (PlayerCombat | EntityMeleeAttacker) ─→ WeaponHitbox.Enable/Disable
SMB on attack state ─→ same receiver ─→ owner        (guaranteed enter/exit, even when the clip is interrupted)
WeaponHitbox.OnHit ─→ owner: TryReceiveHit → TakeDamage only on NotBlocked
```

## Events

| | Player | AI entity |
|---|---|---|
| Receiver (on the **Animator GO**) | `AnimationEventReceiver` (Player root) | `EntityAnimationEventReceiver` (`Character` / `CreatureVisual`) |
| Hit window | `HitboxEnable()` / `HitboxDisable()` | `HitboxEnable(string id)` / `HitboxDisable(string id)` — `""` = all hitboxes |
| Combo | `ComboWindowOpen()` / `ComboWindowClose()` | `ComboWindowOpen()` advances the plan; `ComboWindowClose()` no-op |
| SMB | `SMB_AttackState` (`attackIndex`) | `SMB_EntityAttackState` (`attackIndex`, 0 = no step) |

- Unity finds receiver methods **by exact name** — a typo compiles and silently does nothing. Both receivers
  are separate classes, so the parameterless and `string` overloads never clash. A player clip played by an
  NPC (shared controller) delivers `""` → every hitbox opens.
- **FBX importer events use normalized time; `.anim` events use seconds.** Convert through `HitWindowEvents`,
  or author with the Hitbox Tuner (`Tools/Combat/Hitbox Tuner`).
- **Never author events on vendor-pack FBX clips** (gitignored packs, wiped on re-import) — copy the clip into
  the project as `.anim` first (recipe in `Prefabs/Entities/Monsters/CLAUDE.md`).

## Swing movement (same rules for both)

Rooted for the whole attack (no locomotion input / no agent walking) except the per-state forward step, resolved by
`CombatConfigSO.ResolveAttackStep(weapon, index)` (archetype `attackSteps` / `unarmedAttacks` / `defaultAttackStep`).
Player: `PlayerAttackStep` + wind-up steering until `HitboxEnable`. AI: `EntityAttackStep` (`NavMeshAgent.Move`) + brain
facing until `EntityMeleeAttacker.IsAttackCommitted`. AI SMB carries `attackIndex` (0 = no step) like `SMB_AttackState`.

**Player exception:** a step with `AttackStep.rootMotionScale` > 0 moves the player by the clip's own root displacement
through `ActionRootMotion` (`OnAnimatorMove`) instead of the scripted step — the whole unarmed combo (`lead jab step`,
`hook step`, `uppercut step`) and the one-handed sword combo (Mixamo sword-and-shield clips), whose travel carries on
through the attack → attack crossfades
(`Scripts/Combat/PLAYER_COMBO.md` → Action root motion). AI entities ignore the scale and keep the scripted step, tuned to
the travel the player gets in a chain; `applyRootMotion` stays off on every Animator.

## State callbacks across crossfades

Unity fires `OnStateEnter(next)` at transition **start** and `OnStateExit(previous)` at transition **end**.
An exit-only "attack ended" signal therefore fires mid-chain on every `Attack_1 → Attack_2` blend.

- Player: `PlayerAttackTracker` counts active attack states, plus a stall watchdog (`PLAYER_COMBO.md`).
- AI: `EntityMeleeAttacker` counts active attack states; the attack ends when the count returns to 0.
- **Never** use `GetNextAnimatorStateInfo(layer)` in `OnStateExit` to detect a chain — the transition may
  already be finished and it returns an empty state.

## Shared controller

`Humanoid_Template.controller` is used by the Player **and** humanoid NPCs, so its attack states (Base Layer, full-body,
tag `Attack`) carry both SMBs. One layer plays each attack clip, so clip events fire once — no dedupe. Each SMB must be a silent no-op when its receiver is absent — explicit `if (receiver != null)`, never
`?.` (Unity fake-null). `EntityBase.controller` (monsters) carries `SMB_EntityAttackState` on `Attack`.
