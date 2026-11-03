# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Animations/Combat

> Loaded when Claude accesses files in this folder. Covers the combat clips and their per-clip event
> timings. Event routing / receivers / SMBs: `.claude/rules/attack-pipeline.md`. Controller wiring:
> `../../Controllers/CLAUDE.md`.

---

## Folder Structure

```
Combat/
├── one handed/ — AttackLeft, AttackOverhead, AttackThrust, SwordIdle, Draw Sword, Sheath Sword + Sword_AnimatorOverride
├── two handed/ — 2hand idle / attack (+ attack refined, Blender) / attack 2 (+ attack 2 refined, Blender) / blocking / stagger, draw 1+2 / sheath 1+2 (sources) → baked `2hand draw.anim` /
│                 `2hand sheath.anim` + TwoHanded_AnimatorOverride (→ `Archetype_TwoHanded`)
├── Bow/        — Standing Equip Bow (clip `Draw Bow`), Standing Disarm Bow (clip `Sheathe Bow`) + Bow_AnimatorOverride;
│                 aim: Standing Draw Arrow (`Draw arrow`), Standing Aim Overdraw (`Aim Overdraw`, loopTime on),
│                 Standing Fire Arrow (`Fire arrow`) — no events, used directly by the bow aim states
├── Unarmed/    — Jab, Uppercut Jab + Unarmed Idle
├── Block Idle.fbx
├── dodge roll.fbx
├── Dodge back.fbx
└── MESHY_PROMPTS.md — Meshy text-to-motion prompt rules, 2H templates, durations, import checklist, prompt log
```

**Generating a clip with Meshy text-to-motion:** read and follow `MESHY_PROMPTS.md`, and log every prompt there.

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

2-step combo: `Unarmed Jab` → `2hand attack refined` (Attack_1), `Unarmed Uppercut` → `2hand attack 2 refined` (Attack_2, finisher).
They twist the hips up to 112° — fine now that every attack plays full-body on the Base Layer (`Controllers/CLAUDE.md`).

**All attack clips are in place** (measured: net root XZ travel 0, body offset at most 0.63 m that returns to the start), so
they stay root-baked and the forward step is scripted per archetype (`AttackStep`, `Scripts/Combat/PLAYER_COMBO.md`). Tune
`attackSteps` start / duration in **seconds** against the clip lengths and hit windows (hit time = HitboxEnable × length).

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `2hand attack refined.fbx` (`2hand attack refined`, 1.83 s) | Attack_1 | 0.33 | 0.58 | 0.58 | 0.78 |
| `2hand attack 2 refined.fbx` (`2hand attack 2 refined`, 1.27 s) | Attack_2 (finisher) | 0.42 | 0.63 | — | — |

The finisher has no combo window (last step). Its first fast phase (0.08–0.26) is the backswing behind the body; the
strike is the overhead diagonal 0.42–0.61 (blade tip in front from 0.45).

Attack_1 window from hand-speed sampling (original clip): wind-up overhead until ~0.30, downswing 0.33–0.50, follow-through to ~0.58.
In the refined clip the blade is horizontal and in front 0.44–0.55, inside the unchanged 0.33–0.58 window.
`ComboWindowClose` must stay **below `Attack_1_State → CombatIdle` exit time (0.81)** — a press after it spends stamina
(×2 here) on a trigger the exiting state never takes. 0.58 + the 0.42 s combo blend ends past 0.81, which is safe only
because the combo transition is listed first with `interruptionSource = Destination` (the outgoing exit can't cut it).
`2hand idle` / `2hand blocking` have `loopTime` on. All 2H clips: root rotation / Y / XZ baked into pose.
`2hand idle` (Mixamo) is a front guard like the attacks. Meshy text-to-motion replacements were tried and rejected
(`MESHY_PROMPTS.md` → Verdict).

**Attack_1 plays `2hand attack refined.fbx`** (take `Scene`, frames 1–56, same 1.83 s, avatar / root-bake / events as
`2hand attack.fbx`). The original is unreferenced but kept as the edit source; revert = point the override slot back to it.
Blender edit (2026-11-03): only the left arm, 0.38–0.62, so the blade (left → right hand line, `TwoHandGripConstraint`)
runs horizontal through the cut: pitch +18…+35° → +1…+7°, tip 1.23–1.58 → 0.91–1.11 m. Body and right hand within 2.4 mm.

**Attack_2 plays `2hand attack 2 refined.fbx`** (take `Scene`, frames 1–39, same 1.27 s, avatar / root-bake / events as
`2hand attack 2.fbx`; the original is kept as the edit source, revert = point the override slot back to it).
Blender edit (2026-11-03, `configs/2hand_attack_2.json`, `pitch_curve`): left arm only, 0.18–0.69, yaw kept. The source swing was a near-flat descending sweep (blade never raised, flat behind the head at 0.42,
plane tilted 21°, tip above the hands +11…+19° at 0.45–0.50). Refined: blade lifts over the right shoulder from 0.21
(+48° at 0.42, tip 2.6 m), pitch falls monotonically to −38° at 0.605, then eases back to the source by 0.69; plane tilt
42°. Body and right socket within 2.7 mm. Known limit: the hands are at 0.88 m when the blade points forward (0.55), so
the tip passes the centre low (~0.7 m); raising it needs the body / right arm.

### Blender edit recipe (weapon line of a 2H clip)

Claude Code Blender MCP (`mcp__blender__*`, Blender 5.2). Script + per-clip configs: `Tools/Blender/` (repo root,
`twohand_blade_line.py`, `configs/2hand_attack_1.json` / `2hand_attack_2.json`, README with the Unity sampling snippet, the
measure → edit loop and the constant vs `pitch_curve` target).
1. Sample the clip in Unity (PlayableGraph on `Player.prefab`, `WeaponSocket` / `LeftWeaponSocket`, per frame); set
   `animator.fireEvents = false` first or the clip events throw NREs into the uninitialised instance (the one NRE from
   `EquipmentVisuals.OnEnable` → `PlayerAnimationDriver.SetInCombat` on instantiation is expected and harmless).
2. Import the FBX in Blender (`automatic_bone_orientation=False`; armature scale 0.01, armature space +Y up, +Z forward,
   −X right); wrap import / export in `temp_override(window=…)` — after `read_homefile` the context has no active object.
3. Key an empty (parented to the armature, armature space) as the left-hand IK target `R − d′ × dist`, `d′` = Unity-measured
   correction applied to the Middle1→Middle1 line; IK on `LeftForeArm` (chain 2, pole from the original elbow, pole angle
   calibrated ≈ 1.5°), influence keyed with a smoothstep ramp; bake LOCAL quats (`convert_space`) only in the window.
4. Hand bones are only a proxy for the Unity sockets (retarget + palm offsets ≈ 10° pitch, distances off by up to 70 %):
   re-measure in Unity and correct per frame — 3 passes reached 0.022 m on the hand distance. Attack_1 final values:
   target pitch +4°, pole angle 1.5°, distance scale per Blender frame 24: 1.69, 25: 1.36, 26–31: 1.07–1.28, others ≈ 1.
5. Export armature-only FBX (baked, no leaf bones, no simplify) at scene fps 30. Copy the source root-bake flags, avatar
   setup, compression and events, but keep the new take (`Scene`) and its frame range (1–56), never the source's.

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

Draw / sheathe is **one** UpperBody-layer state with one clip, so a vendor draw split in two parts (reach to the back →
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
