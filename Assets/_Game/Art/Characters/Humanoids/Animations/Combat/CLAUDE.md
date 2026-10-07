# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Animations/Combat

> Loaded when Claude accesses files in this folder. Covers the combat clips and their per-clip event
> timings. Event routing / receivers / SMBs: `.claude/rules/attack-pipeline.md`. Controller wiring:
> `../../Controllers/CLAUDE.md`.

---

## Folder Structure

```
Combat/
├── attacks/
│   ├── Sword/      — AttackLeft, AttackOverhead, AttackThrust + SwordIdle + Sword_AnimatorOverride
│   └── Unarmed/    — Jab, Uppercut Jab + Unarmed Idle
├── Block Idle.fbx
├── dodge roll.fbx
└── Dodge back.fbx
```

---

## Attack Clip Events

Every player attack clip carries **all four** events — `HitboxEnable`, `HitboxDisable`, `ComboWindowOpen`,
`ComboWindowClose` — stored in the FBX `.meta` under `clipAnimations[].events` with **normalized** `time`
(0–1). A missing event is a silent failure: the hitbox stays open, or the combo never chains.

Author/retime them with the **Hitbox Tuner** (`Tools/Combat/Hitbox Tuner`) — it writes FBX events via
`ModelImporter.clipAnimations`. If editing the `.meta` by hand, copy an existing event block (all fields:
`time`, `functionName`, `data`, `objectReferenceParameter`, `floatParameter`, `intParameter`, `messageOptions`).

### Sword (`attacks/Sword/`)

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `AttackLeft.fbx` | Attack_1 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackOverhead.fbx` | Attack_2 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackThrust.fbx` | Attack_3 | 0.25 | 0.50 | 0.50 | 0.90 |

`HitboxDisable` and `ComboWindowOpen` share 0.50 — they are independent, so dispatch order is irrelevant.

### Unarmed (`attacks/Unarmed/`)

Custom timings tuned to each arc (faster connect, different wind-up) — **never copy the sword timings**.

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `Jab.fbx` | Attack_1 / Attack_3 | 0.211 | 0.332 | 0.356 | 0.924 |
| `Uppercut Jab.fbx` | Attack_2 | 0.339 | 0.572 | 0.628 | 0.928 |

**Tuning:** `HitboxEnable` ≈ when the weapon reaches the target zone (~0.2–0.25), `HitboxDisable` ≈ when it
retracts (~0.35–0.5). Thrust/jab clips need an earlier `HitboxEnable` than overhead swings. Retiming
`ComboWindowOpen` also moves the combo blend start — re-check the transition rule in
`../../Controllers/CLAUDE.md`.

---

## Code Review Checklist — Combat Clips

| Severity | Pattern |
|----------|---------|
| HIGH | Attack clip missing one of the four events |
| HIGH | `functionName` typo in a `.meta` event — silent miss |
| MEDIUM | Unarmed clip given the sword timings (0.25 / 0.50 / 0.50 / 0.90) |
