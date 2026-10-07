---
paths:
  - "Assets/_Game/Scripts/Combat/**"
  - "Assets/_Game/Scripts/AI/**"
  - "Assets/_Game/Scripts/Core/Animations/**"
  - "Assets/_Game/Art/Characters/**"
  - "Assets/_Game/Editor/HitboxTuner*.cs"
  - "Assets/_Game/Prefabs/Entities/**"
---

# Attack Pipeline — Shared Contract (Player + AI)

Melee attacks for the Player and for AI entities follow one pipeline. Folder files hold the local
details: `Scripts/Combat/CLAUDE.md` (`WeaponHitbox`), `Scripts/Combat/PLAYER_COMBO.md` (player combo),
`Scripts/AI/CLAUDE.md` (`EntityMeleeAttacker`), `Art/Characters/Humanoids/Controllers/CLAUDE.md`
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
| SMB | `SMB_AttackState` | `SMB_EntityAttackState` |

- Unity finds receiver methods **by exact name** — a typo compiles and silently does nothing. Both receivers
  are separate classes, so the parameterless and `string` overloads never clash. A player clip played by an
  NPC (shared controller) delivers `""` → every hitbox opens.
- **FBX importer events use normalized time; `.anim` events use seconds.** Convert through `HitWindowEvents`,
  or author with the Hitbox Tuner (`Tools/Combat/Hitbox Tuner`).
- **Never author events on vendor-pack FBX clips** (gitignored packs, wiped on re-import) — copy the clip into
  the project as `.anim` first (recipe in `Prefabs/Entities/Monsters/CLAUDE.md`).

## State callbacks across crossfades

Unity fires `OnStateEnter(next)` at transition **start** and `OnStateExit(previous)` at transition **end**.
An exit-only "attack ended" signal therefore fires mid-chain on every `Attack_1 → Attack_2` blend.

- Player: `_IsComboAttacking` flag consumed by the next exit (`PLAYER_COMBO.md`).
- AI: `EntityMeleeAttacker` counts active attack states; the attack ends when the count returns to 0.
- **Never** use `GetNextAnimatorStateInfo(layer)` in `OnStateExit` to detect a chain — the transition may
  already be finished and it returns an empty state.

## Shared controller

`Humanoid_Template.controller` is used by the Player **and** humanoid NPCs, so its attack states carry both
SMBs. Each SMB must be a silent no-op when its receiver is absent — explicit `if (receiver != null)`, never
`?.` (Unity fake-null). `EntityBase.controller` (monsters) carries `SMB_EntityAttackState` on `Attack`.
