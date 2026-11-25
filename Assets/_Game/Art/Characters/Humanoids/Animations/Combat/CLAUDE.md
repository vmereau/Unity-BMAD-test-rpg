# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Animations/Combat

> Loaded when Claude accesses files in this folder. Covers the combat clips and their per-clip event
> timings. Event routing / receivers / SMBs: `.claude/rules/attack-pipeline.md`. Controller wiring:
> `../../Controllers/CLAUDE.md`.

---

## Folder Structure

```
Combat/
├── one handed/ — AttackLeft, AttackOverhead, AttackThrust, SwordIdle, Draw Sword, Sheath Sword + Sword_AnimatorOverride;
│                 `sword braced guard source.fbx` (Meshy) → baked `Sword Braced Guard.anim` (block without a shield,
│                 `BAKE_RECIPES.md`)
├── two handed/ — 2hand idle / attack (+ attack refined, Blender) / attack 2 (+ attack 2 refined, Blender) / blocking / stagger, draw 1+2 / sheath 1+2 (sources) → baked `2hand draw.anim` /
│                 `2hand sheath.anim` + TwoHanded_AnimatorOverride (→ `Archetype_TwoHanded`);
│                 `meshy test/` = rejected Meshy text-to-motion tries (`MESHY_PROMPTS.md`)
├── Bow/        — Standing Equip Bow (clip `Draw Bow`), Standing Disarm Bow (clip `Sheathe Bow`) + Bow_AnimatorOverride;
│                 aim: Standing Draw Arrow (`Draw arrow`), Standing Aim Overdraw (`Aim Overdraw`, loopTime on),
│                 Standing Fire Arrow (`Fire arrow`) — no events, sources of the wrist bakes the bow aim states play;
│                 Standing Bow Idle (no skin: avatar copied from `Idle.fbx`) → baked `Bow Idle.anim` (the bow's CombatIdle,
│                 `BAKE_RECIPES.md` → Bow idle)
├── Unarmed/    — lead jab step / hook step / uppercut step (Attack_1 / 2 / 3: Meshy + Blender fit, each carries its own
│                 travel; `meshy smplh rig` = their avatar source), lead jab, Hook, Uppercut (previous Mixamo clips with
│                 Blender leg edits, unreferenced, kept for a revert; untouched sources in `old/`), draw unarmed, sheath unarmed, stagger 1/2,
│                 Unarmed Idle (source) → baked `Unarmed Idle Guard.anim` (the CombatIdle clip, see below)
│                 → baked `Unarmed Block Guard.anim` (Block_State, high guard; `refs/` = the pose reference pictures)
├── Block Idle.fbx — shield guard: `Block_State` blend child at `ShieldInHand` 1 (sword + shield in hand)
├── dodge roll.fbx
├── Dodge back.fbx
├── execute stomp.fbx — clip `Execute Stomp` (player execute, see below)
├── knocked down.fbx, getting up.fbx — knockdown fall / stand-up
└── MESHY_PROMPTS.md — Meshy text-to-motion prompt rules, 2H templates, durations, import checklist, prompt log
```

**Generating a clip with Meshy text-to-motion:** read and follow `MESHY_PROMPTS.md`, and log every prompt there.

---

## Topic Files

| File | Read when |
|------|-----------|
| `BAKE_RECIPES.md` | Re-editing / re-baking a clip: 2H blade-line Blender edit, unarmed foot-step leg edit, unarmed idle / block guard `.anim` bakes, two-part draw / sheathe merge |
| `MESHY_PROMPTS.md` | Generating a clip with Meshy text-to-motion |

---

## Attack Clip Events

Every player attack clip carries **all four** events — `HitboxEnable`, `HitboxDisable`, `ComboWindowOpen`,
`ComboWindowClose` — stored in the FBX `.meta` under `clipAnimations[].events` with **normalized** `time`
(0–1). A missing event is a silent failure: the hitbox stays open, or the combo never chains.

Author/retime them with the **Hitbox Tuner** (`Tools/Combat/Hitbox Tuner`) — it writes FBX events via
`ModelImporter.clipAnimations`. If editing the `.meta` by hand, copy an existing event block (all fields:
`time`, `functionName`, `data`, `objectReferenceParameter`, `floatParameter`, `intParameter`, `messageOptions`).

**Tuning:** `HitboxEnable` ≈ when the weapon reaches the target zone (~0.2–0.25), `HitboxDisable` ≈ when it
retracts (~0.35–0.5). Thrust/jab clips need an earlier `HitboxEnable` than overhead swings. Retiming
`ComboWindowOpen` also moves the combo blend start — re-check the transition rule in
`../../Controllers/CLAUDE.md`.

### One handed (`one handed/`)

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `AttackLeft.fbx` | Attack_1 / Attack_3 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackThrust.fbx` | Attack_2 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackOverhead.fbx` | — (not mapped by `Sword_AnimatorOverride`) | 0.25 | 0.50 | 0.50 | 0.90 |

`HitboxDisable` and `ComboWindowOpen` share 0.50 — they are independent, so dispatch order is irrelevant.

### Two handed (`two handed/`)

2-step combo: `lead jab step` → `2hand attack refined` (Attack_1), `hook step` → `2hand attack 2 refined` (Attack_2, finisher).
They twist the hips up to 112° — fine now that every attack plays full-body on the Base Layer (`Controllers/CLAUDE.md`).

**All weapon attack clips stay in place** (measured: net root XZ travel 0, body offset at most 0.63 m that returns to
the start; the unarmed boxing clips are handled differently, see Unarmed below), so they stay root-baked and the forward step is scripted per archetype (`AttackStep`, `Scripts/Combat/PLAYER_COMBO.md`). Tune
`attackSteps` start / duration in **seconds** against the clip lengths and hit windows (hit time = HitboxEnable × length).

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `2hand attack refined.fbx` (`2hand attack refined`, 1.83 s) | Attack_1 | 0.33 | 0.58 | 0.58 | 0.78 |
| `2hand attack 2 refined.fbx` (`2hand attack 2 refined`, 1.27 s) | Attack_2 (finisher) | 0.42 | 0.63 | — | 0.80 |

The finisher has no combo window (last step); its `ComboWindowClose` only opens the recovery cancel (`PLAYER_COMBO.md`). Its first fast phase (0.08–0.26) is the backswing behind the body; the
strike is the overhead diagonal 0.42–0.61 (blade tip in front from 0.45).

Attack_1 window from hand-speed sampling (original clip): wind-up overhead until ~0.30, downswing 0.33–0.50, follow-through to ~0.58.
In the refined clip the blade is horizontal and in front 0.44–0.55, inside the unchanged 0.33–0.58 window.
`ComboWindowClose` must stay **below the `Attack_1_State → LockOn Locomotion` exit-time transition (≈ 0.8065)** — a press
after it spends stamina (×2 here) on a trigger the exiting state never takes. 0.58 + the 0.42 s combo blend ends past
0.8065, which is safe only because the combo transition (`Attack_2`) is listed **before** the exit-time transition (after
the `IsBlocking` exit and the `AttackCancel` transitions) with `interruptionSource = Destination` (the outgoing exit
can't cut it).
`2hand idle` / `2hand blocking` have `loopTime` on. All 2H clips: root rotation / Y / XZ baked into pose.
`2hand idle` (Mixamo) is a front guard like the attacks. Meshy text-to-motion replacements were tried and rejected
(`MESHY_PROMPTS.md` → Verdict).

**Attack_1 plays `2hand attack refined.fbx`** (take `Scene`, frames 1–56, same 1.83 s, avatar / root-bake / events as
`2hand attack.fbx`). The original is unreferenced but kept as the edit source; revert = point the override slot back to it. Blender edit: `BAKE_RECIPES.md` → *2H blade line*.

**Attack_2 plays `2hand attack 2 refined.fbx`** (take `Scene`, frames 1–39, same 1.27 s, avatar / root-bake / events as
`2hand attack 2.fbx`; the original is kept as the edit source, revert = point the override slot back to it). Blender edit: `BAKE_RECIPES.md` → *2H blade line*.

### Unarmed (`Unarmed/`)

Custom timings tuned to each arc (faster connect, different wind-up) — **never copy the sword timings**.

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `lead jab step.fbx` (`lead jab step`, 1.07 s, frames 1–33) | Attack_1 — left hand | 0.27 | 0.41 | 0.41 | 0.78 |
| `hook step.fbx` (`hook step`, 1.63 s, frames 1–50) | Attack_2 — right hand | 0.288 (0.47 s) | 0.386 (0.63 s) | 0.386 | 0.704 (1.15 s) |
| `uppercut step.fbx` (`uppercut step`, 1.07 s, frames 1–33) | Attack_3 — left hand, finisher | 0.375 (0.40 s) | 0.516 (0.55 s) | — | 0.703 (0.75 s) |
| `lead jab.fbx` (`lead jab`, 1.80 s, frames 1–55) | — (unreferenced since the root motion trial) | 0.185 (f11) | 0.315 (f18) | 0.315 | 0.741 (f41) |
| `Hook.fbx` (`Hook`, 1.83 s, frames **11–66**) | — (unreferenced since spec-action-root-motion-unarmed-combo) | 0.309 (f28) | 0.436 (f35) | 0.436 | 0.764 (f53) |
| `Uppercut.fbx` (`Uppercut`, 1.33 s, frames 1–41) | — (unreferenced, same spec) | 0.275 (f12) | 0.400 (f17) | — | 0.70 (f29) |

Spec: `spec-unarmed-combo-rework`. Frame numbers are source frames of the Blender re-export (importer take `Scene`, numbering starts at 1);
normalized = (frame − firstFrame) / clip frames.
Measured on the Player rig (fist hitbox, player-local) — the three previous Mixamo clips:

- **lead jab** — left fist speeds up from f11 (peak 4.8 m/s at f13), full extension f15–16 at (0.20, 0.96, 0.79).
- **Hook** — right fist swings wide from f27 (10 m/s at f29–30), crosses the front f30–34, ends at (0.25, 1.45, 0.70).
  The first 10 source frames (f1–10, slow guard-to-wind-up) are trimmed in the importer: the trimmed start has hip yaw 78°,
  the lead jab's yaw at its combo window (77°), and the hit lands 0.57 s after the chain instead of 0.90 s.
- **Uppercut** — left fist drops back f8–10, drives up f12–17 (12 m/s at f15), in front at f13–16 (z 0.36–0.65), then
  goes overhead. Its start is not trimmed: the 0.42 s combo blend covers the drop-back.

**`lead jab step`, `hook step`, `uppercut step` (spec-action-root-motion-jab-trial, spec-action-root-motion-unarmed-combo)** —
the clips that move the player by their own root motion (`Scripts/Combat/PLAYER_COMBO.md` → Action root motion). Meshy
text-to-motion sources fitted by `Tools/Blender/meshy_motion_fit.py`, one config per clip (`configs/unarmed/meshy_*.json`).
The points below hold for all three; the jab's numbers come first.

- `lead jab step`: Meshy jab v2, travel straightened, stride 1.22 m → 0.42 m, retimed × 1.4, 1.07 s.
- Skeleton is **SMPL-H**, not Mixamo: its avatar is copied from `meshy smplh rig.fbx` (T-pose export, explicit human
  mapping `Pelvis` → Hips, `L_Hip` → upper leg, `L_Knee` → lower leg, `L_Ankle` → foot, `L_Foot` → toes, `L_Collar` →
  shoulder, `L_Shoulder` → upper arm, `L_Elbow` → lower arm, `L_Wrist` → hand). A Blender-exported clip can't build the
  avatar itself: its model pose is the first animated frame, not a T-pose.
- Import: root rotation baked (Original), root Y baked (Original) with **Offset 0.042** (the SMPL-H ankle sits higher
  than the player's, so without it the feet float 4 cm; a positive offset lowers the body), root **XZ not baked**.
- On the Player: fist furthest at 0.40 s, 0.63 m ahead of the root; lead foot planted from 0.45 s; the trailing foot
  drags 5–7 cm above the ground until the end of the clip (source behaviour, scaled down).
- Timing against the old jab: clip 1.80 s → 1.07 s, hit window opens at 0.33 s → 0.29 s, combo window 0.57–1.33 s →
  0.44–0.83 s (about half as long). NPCs get the same timings through the shared controller.
- Revert (one clip): point its state back to the old clip, re-key the two weapon overrides, set its
  `unarmedAttacks[i].step` back with `rootMotionScale` 0 (asset and the `CombatConfigSO` default: jab 0.30 / 0.07 / 0.43,
  hook 0.35 / 0.27 / 0.37, uppercut 0.50 / 0.10 / 0.37). Revert all three together: a scripted step inside the chain cuts
  the travel at its crossfades.

**`hook step`** — Meshy hook v2 (0.71 m of travel 27° to the left, a square 0.48 m wide stance), fitted with `travel_m` 0.33 (0.42 m on the Player, its settle back scaled down), real time.

- Starts in the jab's stance at the middle of its combo window (0.63 s): lead foot planted, rear foot still in the air
  0.5 m behind, landing in the first 0.17 s. The rear foot's lane is moved 0.27 m inward for the whole clip (the jab
  stands almost in line) and lands 0.12 m further forward, where `uppercut step` starts.
- On the Player: right fist crosses the front 0.47–0.63 s, furthest at 0.55 s, 0.61 m ahead of the root, 1.47 m high.
  Root travel 0.46 m at 0.9 s, 0.42 m at the end (the settle back is scaled to 40 %); in a chain 0.33–0.40 m.
- The body leans into the punch: the root goes 6 cm to the left and comes back (centre of mass). The travel line is
  turned 8° to the right so the lean sits on the line when the uppercut is chained.
- Timing against the old Hook: clip 1.83 s → 1.63 s, hit window 0.57–0.80 s → 0.47–0.63 s, combo window 0.80–1.40 s →
  0.63–1.15 s.

**`uppercut step`** — Meshy uppercut v2 (1.10 m of travel 23° to the left), fitted to 0.57 m, retimed × 1.8.

- Starts close to the hook's stance over its whole window without a start offset (lead foot within 7 cm, rear foot on
  the hook's landing spot). Its lunge starts at 0.13 s, a third of the way into the 0.42 s combo blend, which still eats 0.08–0.15 m of it: a lunge from 0 s is half
  lost in the crossfade (measured 0.37 m instead of 0.50 m with the lunge at 0–0.4 s).
- Ends in the jab's start stance (lead foot within 2 cm; rear foot 6 cm short, its leg is at full reach), for the
  recovery restart and the exit.
- On the Player: left fist drops to 1.16 m at 0.33 s and drives up 0.40–0.50 s, furthest at 0.49 s, 0.59 m ahead of the
  root, 1.78 m high. Root travel 0.60 m alone, 0.45–0.52 m in a chain.
- Timing against the old Uppercut: clip 1.33 s → 1.07 s, hit window 0.37–0.53 s → 0.40–0.55 s, recovery opens
  (`ComboWindowClose`) 0.93 s → 0.75 s.

**Matching a chained clip's start** (Meshy cannot start from a pose): what Unity blends is the pose **relative to the
root**, so sample both clips with `Tools/Blender/meshy_motion_measure.cs` and compare each foot's root-relative position
— the previous clip inside its combo window against the new clip's frame 0. One start pose cannot match a whole
window (the root keeps moving over a planted foot: 11 cm across the jab's window). Rules that worked: match the middle
of the window; a foot that is in the air for the whole window starts in the air and lands fast; a foot that lands
inside the window starts planted on the landing spot (early presses then show a landing, late presses nothing — the
other way round a planted foot is pulled back, measured 7 cm).


**Hit event ids:** the combo clips' `HitboxEnable` / `HitboxDisable` carry a string — `UnarmedLeft` (lead jab step,
uppercut step) or `Unarmed` (hook step) — the NPC hitbox id. The player ignores it and reads the hand from
`CombatConfigSO.unarmedAttacks` (`Scripts/Combat/PLAYER_COMBO.md`).

**Import settings of the previous Mixamo clips (`lead jab`, `Hook`, `Uppercut`; Loop Pose off):** root rotation baked, **Original** orientation; root Y baked, Original; root **XZ not
baked**. The Mixamo boxing clips lunge up to 0.5 m and step back; with XZ unbaked and `applyRootMotion` off the Animator
drops that travel, so the punch is truly in place and the scripted `AttackStep` is the only movement (the three `* step` clips are the opposite case: the player reads their travel, see above). Orientation based
on **Body** turned the bladed stance square and sent the jab 35° to the left.

**Foot step (previous clips):** `lead jab`, `Hook` and `Uppercut` are Blender re-exports whose legs follow the scripted `AttackStep`; the untouched
Mixamo sources are in `Unarmed/old/` — recipe, per-clip foot plan and gotchas in `BAKE_RECIPES.md` → *Unarmed foot step*.

**Guard poses:** `CombatIdle` plays the baked `Unarmed Idle Guard.anim` (from `Unarmed Idle.fbx`, kept as the bake
source) and `Block_State` plays `Unarmed Block Guard.anim` (baked from the idle guard) — offsets and re-bake recipe in
`BAKE_RECIPES.md`.

---

## Draw / Sheathe Clip Events

Each draw / sheathe clip carries **one** `WeaponSwap` event (normalized, FBX importer) — the frame the hand reaches the
hilt / sheath; the weapon visual changes socket there. First guesses, tune in play mode:

| Clip | State | WeaponSwap |
|------|-------|-----------|
| `Unarmed/draw unarmed.fbx` (`Draw unarmed`, 1.00 s) | Draw_State (base — unarmed) | 0.50 (fists reach the guard at f15–18) |
| `Unarmed/sheath unarmed.fbx` (`sheath unarmed`, 1.07 s) | Sheathe_State (base — unarmed) | 0.50 (hands down by f15–18) |
| `one handed/Draw Sword.fbx` (`draw sword`) | Draw_State (sword override) | 0.40 |
| `one handed/Sheath Sword.fbx` (`sheath sword`) | Sheathe_State (sword override) | 0.65 |
| `two handed/2hand draw.anim` (1.40 s) | Draw_State (2H override) | 0.55 s (seam) |
| `two handed/2hand sheath.anim` (1.97 s) | Sheathe_State (2H override) | 0.78 s (seam) |
| `Bow/Standing Equip Bow.fbx` (`Draw Bow`) | Draw_State (bow override) | 0.45 |
| `Bow/Standing Disarm Bow.fbx` (`Sheathe Bow`) | Sheathe_State (bow override) | 0.65 |

A missing event is not fatal — the state exit completes the swap, just late (end of clip). Unarmed has no weapon
visual: its `WeaponSwap` only releases the switch lock, so attacks unlock there.

**Two-part vendor draws** (2H) are merged into one baked `.anim` per state (one UpperBody state, one clip): recipe in
`BAKE_RECIPES.md` → *Two-part draw / sheathe*.

---

## Execute Stomp (`execute stomp.fbx`, spec-execute-unconscious-npc)

Mixamo, Humanoid (own avatar), 56 frames @ 30 fps = **1.867 s**, root rotation / Y / XZ baked into pose, no loop. Events
(normalized): `ExecuteImpact` **0.393** (frame 22 — the right foot touches down; it rests on the body until frame 28) and
`ExecuteEnd` 0.95. Landing point ≈ **(−0.45, 0, 0.88) m** in player-local space, mirrored in
`CombatConfigSO.executeFootOffset` / `executeClipDuration` — re-measure both (sample `RightFoot` / `RightToes` with an
`AnimationClipPlayable` on the FBX avatar, × 100: the avatar is in cm) if the clip or its trim changes.

---

## Code Review Checklist — Combat Clips

| Severity | Pattern |
|----------|---------|
| HIGH | Attack clip missing one of the four events |
| HIGH | `functionName` typo in a `.meta` event — silent miss |
| MEDIUM | Unarmed clip given the sword timings (0.25 / 0.50 / 0.50 / 0.90) |
