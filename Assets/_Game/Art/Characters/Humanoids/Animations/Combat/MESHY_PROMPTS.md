# Meshy text-to-motion prompts (combat clips)

> How to write Meshy animation prompts that come out usable, plus the log of every prompt sent.
> Clip events / timings: `CLAUDE.md` (this folder). Controller wiring: `../../Controllers/CLAUDE.md`.

Meshy limits: **400 characters** per prompt, **2 s minimum duration** (count the whole text before sending), no "start from another
clip", no loop toggle. **Duration is set in the Meshy UI**, not in the prompt.

## Verdict: not usable for weapon attacks yet

Meshy animates **without a weapon**, so nothing keeps a sword path consistent. `TwoHandGripConstraint` aims the blade
along the 15–20 cm left-hand → right-hand line, and in the Meshy attacks that line swings behind the body and runs along
the torso (the blade cut through the player). The rigid right-wrist pose is no better: weaponless wrists are arbitrary.
Mirroring fixed the guard, not the cut. The 2H set went **back to Mixamo**; the v1 clips are kept (tracked) in
`two handed/meshy test/`. Use Meshy for motions where the weapon path doesn't matter (locomotion, reactions, unarmed / NPC
idles). Two-handed attacks need weapon-aware sources (Mixamo great-sword packs, paid packs, Blender) or a per-clip
blade-direction track. Retry when Meshy can take a prop or a reference clip.

---

## Rules (from the failures of the first 2H batch; templates below are v1, see Verdict)

The banned words below apply to **prompt text only** — labels in this file may use them.

1. **Shared guard sentence.** Meshy can't chain clips, so every clip of a weapon set **opens with the same
   guard sentence, word for word**, and every clip ends with "Ends in the exact starting pose". That is the
   only way idle → attack → idle start and end from matching hands. Check the guard against the first frame
   of the clips that stay (2H block, stagger, draw, sheathe) — a mismatch just moves the visible blend there.
2. **Loops are asked for in words**: "Seamless looping …" after the guard sentence, "Ends in the exact
   starting pose" at the end. Keep the motion tiny (breathing, weight shift). Unity's Loop Pose only smooths
   a small seam — a visible one means regenerate.
3. **One shared body-constraint sentence, positive phrasing**, verbatim in every attack:
   `Feet stay planted, faces forward the whole time, torso turns less than 90 degrees.`
   Flashy words ("slash", "overhead", "finisher", "spin") pulled in a 200° turn and a 365° spin with
   0.8–1.6 m of travel. Avoid negations like "no spin": text encoders handle negation poorly and the word
   can *trigger* the motion. If a clip still turns, tighten to "less than 45 degrees".
4. **No steps in the clip** — for clips moved by the scripted step. A clip read by `ActionRootMotion` is the opposite
   case (see *Clips that travel*). The forward step is scripted in game (`AttackStep`, `Scripts/Combat/PLAYER_COMBO.md`);
   a stepping clip stacks on it and ends off the start spot. Ask for "shifting weight onto the front foot".
5. **Say where the blade is**, not where it isn't: "blade upright in front of the chest". An unspecified
   hold defaults to a shoulder rest; "never on the shoulder" still names the shoulder.
6. **One action per clip with an explicit path** using plain verbs: start height → end height and side
   ("over the right shoulder … down diagonally to the left hip"). No style words ("epic", "powerful",
   "cinematic", "finisher") — they add spins and jumps.
7. **Hand order decides the blade direction.** `TwoHandGripConstraint` aims the blade from the left hand to the right
   hand (Mixamo: right hand on top, nearest the crossguard). Meshy puts the **left hand on top**, so its clips are
   imported **mirrored**. A clip whose hands cross (hands < 5 cm apart, then the order swaps) can't be fixed by
   mirroring — say "left hand stays above the right on the hilt the whole time" and regenerate.

## Clips that travel (root motion, spec-action-root-motion-jab-trial)

For a clip whose own displacement moves the player. Generated through the Meshy MCP (`meshy_text_to_motion`, `prime`,
10 credits each); general MCP quirks: `.claude/rules/meshy-mcp.md`.

- Describe the lead foot stepping, the action, then **the rear foot following**, and end with "finishes standing in the
  guard, one short step ahead of where he started". "Returns to the guard" makes the character walk back to the start.
- Do not try to control the distance or the direction in words (ignored): fit both with `Tools/Blender/meshy_jab_fit.py`.
- Avoid "shuffle", "slides", "tiny": they produced a deep crouch and a waist-high punch.
- The MCP output is an SMPL-H skeleton, not the "animated_mixamo" FBX of the web UI: avatar and import settings in
  `CLAUDE.md` → Unarmed → `lead jab step`.

## Durations (set in the Meshy UI)

| Clip | Duration | Why |
|------|----------|-----|
| Idle guard (loop) | 3.0 s | Long enough to breathe, short loop seam |
| Attack 1 (diagonal cut) | 2.0 s (minimum) | Meshy minimum; the Mixamo clip was 1.83 s |
| Attack 2 (vertical chop, last hit) | 2.0 s (minimum) | Meshy minimum; the Mixamo clip was 1.27 s |

The current event timings do **not** carry over — re-tune all four events and `attackSteps` after import.

## Two-handed templates (v1 — rejected: see Verdict)

Guard sentence (shared, verbatim): `A warrior holds a heavy two-handed sword in a front guard: left foot forward,
knees bent, both hands on the hilt at waist height, blade upright in front of the chest.`

**Idle guard (loop)** — 318 chars

```
A warrior holds a heavy two-handed sword in a front guard: left foot forward, knees bent, both hands on the hilt at waist height, blade upright in front of the chest. Seamless looping idle: only slow breathing and a slight weight shift. Feet stay planted, faces forward the whole time. Ends in the exact starting pose.
```

**Attack 1: diagonal cut** — 373 chars

```
A warrior holds a heavy two-handed sword in a front guard: left foot forward, knees bent, both hands on the hilt at waist height, blade upright in front of the chest. He raises the blade over the right shoulder and brings it down diagonally to the left hip. Feet stay planted, faces forward the whole time, torso turns less than 90 degrees. Ends in the exact starting pose.
```

**Attack 2: vertical chop** — 376 chars

```
A warrior holds a heavy two-handed sword in a front guard: left foot forward, knees bent, both hands on the hilt at waist height, blade upright in front of the chest. He lifts the blade straight above the head and chops vertically down to knee height in front. Feet stay planted, faces forward the whole time, torso turns less than 90 degrees. Ends in the exact starting pose.
```

**Attack 2: vertical chop, v2** (after v1's hands crossed mid-clip) — 382 chars

```
A warrior holds a heavy two-handed sword in a front guard: left foot forward, knees bent, both hands on the hilt at waist height, blade upright in front of the chest. He lifts the blade straight above the head and chops vertically down to knee height in front. Feet planted, faces forward, left hand stays above the right on the hilt the whole time. Ends in the exact starting pose.
```

## Unity import checklist (Meshy "animated_mixamo_without_skin" FBX)

- Delete the texture PNGs that ship with the download; Materials: **None**.
- Rig **Humanoid, Create From This Model** (Mixamo bone names map automatically) — check Configure: mapping
  all green. Compare root height with the existing 2H clips (Meshy models have needed Scale Factor 100 before).
- Root Transform **Rotation, Y and XZ all Bake Into Pose, Based Upon Original** — the convention of every
  2H clip. Then check net root XZ travel ≈ 0 (the body may move out and back). If it isn't, regenerate:
  unbaking XZ only turns the travel into foot slide (root motion is off) — last resort.
- Idle: also **Loop Time + Loop Pose**; the loop-match indicators must be green.
- **Mirror** on when the left hand is on top (Meshy so far). Check on the Player rig with a PlayableGraph: blade tip
  = right socket + normalize(right − left) × 1.1 m must point forward at the guard and the hit. `AnimationMode`
  sampling **ignores the importer Mirror flag** — use `AnimationClipPlayable` + `graph.Evaluate`.
- Attacks: add the four events (`CLAUDE.md` → Attack Clip Events) with the Hitbox Tuner, then tune `attackSteps`.

## Prompt log

Record every generation: date, clip, template version, duration, exact prompt, verdict (with units).

| Date | Clip | Template | Duration | Prompt | Verdict |
|------|------|----------|----------|--------|---------|
| 2026-11-03 | 2H idle guard | — | 2.97 s | lost | Near-loop (mean muscle seam 0.03), blade rested on the shoulder → regenerate |
| 2026-11-03 | 2H slash (attack 1) | — | 2.97 s | lost | Body turned 200° and back, travelled 0.8 m → regenerate |
| 2026-11-03 | 2H overhead (attack 2) | — | 2.97 s | lost | Full 365° spin during the strike, travelled 1.6 m → regenerate |
| 2026-11-03 | 2H idle guard (loop) | v1 | 2.97 s | v1 template | Loop seam 0.016, yaw ±4°, front guard → mirrored, looked right; dropped with the set |
| 2026-11-03 | 2H attack 1, diagonal cut | v1 | 1.97 s | v1 template | Torso to −105° and back, travel ≤ 0.09 m, starts 0.09 from the idle guard → unmirrored: blade over the head, no hits; mirrored: blade swept behind and through the body → **rejected** |
| 2026-11-03 | 2H attack 2, vertical chop | v1 | 1.97 s | v1 template | Torso to −72°, travel ≤ 0.10 m, clean chop → hands cross at 0.64 (grip flips), start / end guard points backward → **rejected** (v2 not generated) |
| 2026-11-23 | Unarmed jab with step, v1 (MCP, prime) | — | 2.0 s | `A boxer stands in a fighting guard: left foot forward, knees bent, both fists raised at chin height. He takes one short step forward with the left foot and throws a straight left jab at head height, then the right foot follows and he returns to the guard. Faces forward the whole time, torso turns less than 45 degrees. Ends in the same guard pose, about 0.3 m ahead of the start.` | Lead foot lunges 0.65 m on a 45° diagonal and comes back, rear foot never follows, net travel 0.12 m → **rejected** |
| 2026-11-23 | Unarmed jab with step, v2 (MCP, prime) | — | 2.0 s | `A boxer in a fighting guard, left foot forward, knees bent, fists at chin height, advances straight ahead. The left foot takes one short step straight forward and the left fist jabs straight ahead at head height as the foot lands. Then the right foot steps forward the same distance. He keeps facing straight ahead and finishes standing in the guard, one short step ahead of where he started.` | Step, jab at head height, rear foot follows; 1.22 m of travel 24° to the left, hit at 0.70 s, last 8 frames lift the lead foot again → **kept**, fitted in Blender (`lead jab step`) |
| 2026-11-23 | Unarmed jab with step, v3 (MCP, prime) | — | 2.0 s | `A boxer in a fighting guard, left foot forward, knees bent, fists at chin height. He shuffles forward a tiny step: the left foot slides forward half a foot length and the left fist snaps a quick straight jab at head height, then the right foot slides forward the same small distance. He stays upright, faces straight ahead and finishes in the same guard, barely ahead of where he started.` | 0.9 m at 45°, pelvis drops to 0.59 m, jab at 0.93 m height → **rejected** |
