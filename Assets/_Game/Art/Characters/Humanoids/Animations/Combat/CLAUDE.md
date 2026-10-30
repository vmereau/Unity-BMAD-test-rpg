# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Animations/Combat

> Loaded when Claude accesses files in this folder. Covers the combat clips and their per-clip event
> timings. Event routing / receivers / SMBs: `.claude/rules/attack-pipeline.md`. Controller wiring:
> `../../Controllers/CLAUDE.md`.

---

## Folder Structure

```
Combat/
├── one handed/ — AttackLeft, AttackOverhead, AttackThrust, SwordIdle, Draw Sword, Sheath Sword + Sword_AnimatorOverride
├── two handed/ — 2hand idle / attack / attack 2 / blocking / stagger, draw 1+2 / sheath 1+2 (sources) → baked `2hand draw.anim` /
│                 `2hand sheath.anim` + TwoHanded_AnimatorOverride (→ `Archetype_TwoHanded`)
├── Bow/        — Standing Equip Bow (clip `Draw Bow`), Standing Disarm Bow (clip `Sheathe Bow`) + Bow_AnimatorOverride;
│                 aim: Standing Draw Arrow (`Draw arrow`), Standing Aim Overdraw (`Aim Overdraw`, loopTime on),
│                 Standing Fire Arrow (`Fire arrow`) — no events, used directly by the bow aim states
├── Unarmed/    — Jab, Uppercut Jab + Unarmed Idle
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

### One handed (`one handed/`)

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `AttackLeft.fbx` | Attack_1 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackOverhead.fbx` | Attack_2 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackThrust.fbx` | Attack_3 | 0.25 | 0.50 | 0.50 | 0.90 |

`HitboxDisable` and `ComboWindowOpen` share 0.50 — they are independent, so dispatch order is irrelevant.

### Two handed (`two handed/`)

2-step combo: `Unarmed Jab` → `2hand attack` (Attack_1), `Unarmed Uppercut` → `2hand attack 2` (Attack_2, finisher).
Both play **full-body** (FullBody layer, `Controllers/CLAUDE.md`) — they twist the hips up to 112°.

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `2hand attack.fbx` (`2hand attack`, 1.83 s) | Attack_1 | 0.33 | 0.58 | 0.58 | 0.78 |
| `2hand attack 2.fbx` (`2hand attack 2`, 1.27 s) | Attack_2 (finisher) | 0.42 | 0.63 | — | — |

The finisher has no combo window (last step). Its first fast phase (0.08–0.26) is the backswing behind the body; the
strike is the overhead diagonal 0.42–0.61 (blade tip in front from 0.45).

Window from hand-speed sampling: wind-up overhead until ~0.30, downswing 0.33–0.50, horizontal follow-through to ~0.58.
`ComboWindowClose` must stay **below `Attack_1_State → CombatIdle` exit time (0.81)** — a press after it spends stamina
(×2 here) on a trigger the exiting state never takes. 0.58 + the 0.42 s combo blend ends past 0.81, which is safe only
because the combo transition is listed first with `interruptionSource = None`.
`2hand idle` / `2hand blocking` have `loopTime` on. All 2H clips: root rotation / Y / XZ baked into pose.
`2hand idle` is a shoulder-rest pose while attack / stagger / sheathe start from a front guard — a visible blend.

### Unarmed (`Unarmed/`)

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

## Draw / Sheathe Clip Events

Each draw / sheathe clip carries **one** `WeaponSwap` event (normalized, FBX importer) — the frame the hand reaches the
hilt / sheath; the weapon visual changes socket there. First guesses, tune in play mode:

| Clip | State | WeaponSwap |
|------|-------|-----------|
| `one handed/Draw Sword.fbx` (`draw sword`) | Draw_State | 0.40 |
| `one handed/Sheath Sword.fbx` (`sheath sword`) | Sheathe_State | 0.65 |
| `two handed/2hand draw.anim` (1.40 s) | Draw_State (2H override) | 0.55 s (seam) |
| `two handed/2hand sheath.anim` (1.97 s) | Sheathe_State (2H override) | 0.78 s (seam) |
| `Bow/Standing Equip Bow.fbx` (`Draw Bow`) | Draw_State (bow override) | 0.45 |
| `Bow/Standing Disarm Bow.fbx` (`Sheathe Bow`) | Sheathe_State (bow override) | 0.65 |

A missing event is not fatal — the state exit completes the swap, just late (end of clip).

### Two-part draw / sheathe → one baked `.anim`

Draw / sheathe is **one** Attack-layer state with one clip, so a vendor draw split in two parts (reach to the back →
bring forward) is merged, not given a second state. No committed bake tool yet — the clips were baked once from `execute_code`. Recipe: for every
`AnimationUtility.GetCurveBindings(part1)` (Humanoid muscle curves) copy part 1's keys, then part 2's keys shifted by
`part1.length + 0.1 s`; set Auto tangents on the two seam keys (smooths the small pose jump); copy part 1's
`AnimationClipSettings` (loopTime off, `stopTime` = total); one `WeaponSwap` event **in seconds** at the middle of the gap
(the hand is at the back there). Re-bake after changing the source parts' import settings. Keep the longest clip under
`CombatConfigSO.weaponSwitchTimeout` (2.5 s at speed 1).

---

## Code Review Checklist — Combat Clips

| Severity | Pattern |
|----------|---------|
| HIGH | Attack clip missing one of the four events |
| HIGH | `functionName` typo in a `.meta` event — silent miss |
| MEDIUM | Unarmed clip given the sword timings (0.25 / 0.50 / 0.50 / 0.90) |
