# Meshy text-to-motion prompts (combat clips)

> How to write Meshy animation prompts that come out usable, plus the log of every prompt sent.
> Clip events / timings: `CLAUDE.md` (this folder). Controller wiring: `../../Controllers/CLAUDE.md`.

Meshy limits: **400 characters** per prompt, **2 s minimum duration** (count the whole text before sending), no "start from another
clip", no loop toggle. **Duration is set in the Meshy UI**, not in the prompt.

## Verdict: not usable for weapon attacks yet

**One-handed (spec-sword-three-hit-combo, 2026-11-25, abandoned):** a three-hit sword combo was generated, fitted with
a wrist pass (`meshy_motion_fit.py` → `blade_aim`) and measured within its targets, then **rejected by the user on
look** ("it does not look good") and reverted. The numbers passed, the result did not: measured travel, blade tip and
clearances do not tell whether a sword swing reads well. Show a clip in game after the first fit, before tuning it.
What was learned is under *One-handed sword* below; the configs and sources are kept in `Tools/Blender/`.

**Two-handed:** Meshy animates **without a weapon**, so nothing keeps a sword path consistent. `TwoHandGripConstraint` aims the blade
along the 15–20 cm left-hand → right-hand line, and in the Meshy attacks that line swings behind the body and runs along
the torso (the blade cut through the player). The rigid right-wrist pose is no better: weaponless wrists are arbitrary.
Mirroring fixed the guard, not the cut. The 2H set went **back to Mixamo**; the v1 clips are kept (tracked) in
`two handed/meshy test/`. Use Meshy for motions where the weapon path doesn't matter (locomotion, reactions, unarmed / NPC
idles). A weapon **hold** works as body only: keep the spine / head and replace the arms with fitted constants
(`BAKE_RECIPES.md` → One-hand braced guard). Two-handed attacks need weapon-aware sources (Mixamo great-sword packs, paid packs, Blender) or a per-clip
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

## Clips that travel (root motion, spec-action-root-motion-jab-trial, spec-action-root-motion-unarmed-combo)

For a clip whose own displacement moves the player. Generated through the Meshy MCP (`meshy_text_to_motion`, `prime`,
10 credits each); general MCP quirks: `.claude/rules/meshy-mcp.md`.

- Describe the lead foot stepping, the action, then **the rear foot following**, and end with "finishes standing in the
  guard, one short step ahead of where he started". "Returns to the guard" makes the character walk back to the start.
- Do not try to control the distance or the direction in words (ignored): fit both with `Tools/Blender/meshy_motion_fit.py`.
- **Name the punch with its boxing name and say "punch"** ("throws a fast right hook punch at head height", "a fast
  left uppercut punch: the left fist dips to the waist and punches upward"), in the same sentence as the lead step. A
  described path alone ("throws a horizontal hook … across the front", "drops to waist height then drives up") gave a
  step with no punch: the fist stayed within 9 cm of the guard, or rose at under 1 m/s.
- Every generation picks its own guard: stance length 0.26–0.55 m, width 0.05–0.48 m for the same opening words. Clips
  of one combo never line up as generated — match the starts in Blender (`CLAUDE.md` → *Matching a chained clip's start*).
- Fist speed comes out at about 6–7 m/s (5.8 and 6.7 measured; Mixamo punches: 10–12 m/s): retime with `speed` where the
  hit timing allows (uppercut × 1.8; the hook stays at real time for its 0.47 s hit).
- Avoid "shuffle", "slides", "tiny": they produced a deep crouch and a waist-high punch.
- The MCP output is an SMPL-H skeleton, not the "animated_mixamo" FBX of the web UI: avatar and import settings in
  `CLAUDE.md` → Unarmed → `lead jab step`, `hook step`, `uppercut step`.

## One-handed sword (spec-sword-three-hit-combo, abandoned)

Not in the game (see Verdict). What the prompts and the fit did, 4 generations for 3 clips (`prime`, 2.0 s, 40 credits):

- **Name the weapon and the hand, describe the cut as a path** ("swings the sword down diagonally from his right shoulder
  to his left hip, one-handed"; "thrusts the sword straight ahead at chest height with his right arm"). Both gave a
  one-armed strike at 9.5–10.4 m/s with the lead step in the same sentence.
- **"backhand" gave a two-handed swing** (both hands 3 cm apart, right to left): the word reads as a tennis stroke. Describe
  the arm instead: "sweeps his right arm outward horizontally from his left side to his right side, right arm only".
- **Height words are loose**: "horizontally ... at chest height" came out as a rising arc from the left hip to above the
  right shoulder (kept as a rising backhand).
- **One generation can hold two strikes**: the regenerated backhand first repeats a right-to-left cut with the step
  (frames 5–25), then does the sweep it was asked for standing still (27–45). The fit takes the legs from the first
  stretch and the upper body from the second (`upper_body`).
- **"left arm relaxed at his side" is the wrong ask for a shield.** It works (hands 0.5–1.0 m apart), but a held shield
  only stays upright on a forearm bent forward as in `SwordIdle`; on a hanging arm it tips flat. Ask for "left forearm
  held forward at waist height, as if carrying a shield" next time, or replace the arm with the idle guard pose.
- **A clip must leave room for its combo window**: the cut is over 0.87 s after it starts, and the window has to close
  before the Animator's exit-time transition. Slow the follow-through in the fit (`time_map`) rather than regenerating.
- **A chained clip's strike must come late in the 0.42 s combo crossfade** (linear weight): at 0.33 s the clip weighs
  79 %, at 0.23 s only 55 %. Open the hit window of a chained clip at 0.33 s or later.
- The wrist: a hammer grip holds the blade **across** the forearm (89° from the hand line; Mixamo sword clips: 40–123°,
  mostly 70–110°). Keying the blade along the arm asked for a 75–115° wrist bend. Key it trailing the swing and tilting
  forward at the hit, kept within 60–120° of the forearm (35° for a thrust): bend 11–42° on the cuts, 67° on the thrust
  hold (Mixamo thrust: 82°). The cutting edge then faces along the forearm: about 55° off the tip's path at the hit of
  the down-cut, set by the socket pose, not by the clip.

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
| 2026-11-24 | Unarmed hook with step, v1 (MCP, prime) | — | 2.0 s | `A boxer in a fighting guard, left foot forward, knees bent, fists at chin height, advances straight ahead. The left foot takes one short step forward and the right fist throws a horizontal hook at head height across the front as the foot lands. Then the right foot steps forward the same distance. He keeps facing straight ahead and finishes in the guard, one short step ahead of where he started.` | 1.9 m of travel in a deep lunge (pelvis 0.88 → 0.62 m), the right fist never leaves the guard (≤ 9 cm relative to the pelvis) → **rejected** |
| 2026-11-24 | Unarmed uppercut with step, v1 (MCP, prime) | — | 2.0 s | `A boxer in a fighting guard, left foot forward, knees bent, fists at chin height, advances straight ahead. The left foot takes one short step forward and the left fist drops to waist height then drives up to chin height in front as the foot lands. Then the right foot steps forward the same distance. He faces straight ahead and finishes in the guard, one short step ahead of where he started.` | 1.9 m, two lead steps, the fist only rises with the body (0.9 m/s) → **rejected** |
| 2026-11-24 | Unarmed hook with step, v2 (MCP, prime) | — | 2.0 s | `A boxer in a fighting stance, left foot forward, fists at chin height, steps forward with the left foot and throws a fast right hook punch at head height, the right fist swinging in from the side. Then the right foot steps forward and he stands in his guard again, one step ahead of where he started. He faces forward the whole time.` | Lead step, right fist sweeps 0.9 m across at 6.7 m/s (peak at 0.53 s), hips turn 84°, rear foot follows, lead foot re-sets; 0.71 m of travel 27° to the left, square 0.48 m wide stance → **kept**, fitted in Blender (`hook step`) |
| 2026-11-24 | Unarmed uppercut with step, v2 (MCP, prime) | — | 2.0 s | `A boxer in a fighting stance, left foot forward, fists at chin height, steps forward with the left foot and throws a fast left uppercut punch: the left fist dips to the waist and punches upward to chin height. Then the right foot steps forward and he stands in his guard again, one step ahead of where he started. He faces forward the whole time.` | Lead step, fist dips and rises 0.5 m at 5.8 m/s (peak at 0.87–0.93 s) above head height, rear foot follows twice; 1.10 m of travel 23° to the left, ends turned 21° → **kept**, fitted in Blender (`uppercut step`) |
| 2026-11-25 | Sword down-cut with step, v1 (MCP, prime) | — | 2.0 s | `A swordsman stands with his left foot forward, a sword in his right hand held up beside his right shoulder, left arm relaxed at his side. He steps forward with the left foot and swings the sword down diagonally from his right shoulder to his left hip, one-handed. Then the right foot steps forward and he stands ready, one step ahead of where he started. He faces forward the whole time.` | Lead step, right hand from above the shoulder to the left hip at 9.5 m/s (frames 13–17), shoulders turn 95°, left hand stays at the left side (hands ≥ 0.54 m apart); then walks on (1.96 m in all, 24° to the left) → fitted (frames 1–27), **rejected on look** with the set |
| 2026-11-25 | Sword backhand with step, v1 (MCP, prime) | — | 2.0 s | `A swordsman stands with his left foot forward, a sword in his right hand held low beside his left hip, left arm relaxed at his side. He steps forward with the left foot and swings the sword horizontally from left to right at chest height, a one-handed backhand cut. Then the right foot steps forward and he stands ready, one step ahead of where he started. He faces forward the whole time.` | Both hands together (3–29 cm apart) swing right to left like a bat → **rejected** |
| 2026-11-25 | Sword thrust with step, v1 (MCP, prime) | — | 2.0 s | `A swordsman stands with his left foot forward, a sword in his right hand pulled back beside his right hip, left arm relaxed at his side. He lunges one step forward with the left foot and thrusts the sword straight ahead at chest height with his right arm, one-handed. Then the right foot steps forward and he stands ready, one step ahead of where he started. He faces forward the whole time.` | 13 idle frames, arm shoots forward at 10.4 m/s (frames 17–19) and holds to 33, lead foot lunges and the rear foot follows, 0.86 m of travel 47° to the left with the body turning side-on, then steps back → fitted (frames 7–47), **rejected on look** with the set |
| 2026-11-25 | Sword backhand with step, v2 (MCP, prime) | — | 2.0 s | `A swordsman stands left foot forward, his right hand holding a sword across his body near his left hip, left arm hanging at his side. He steps forward with the left foot and sweeps his right arm outward horizontally from his left side to his right side at chest height, right arm only. Then the right foot steps forward and he stands ready, one step ahead of where he started. He faces forward.` | Frames 5–25: lead step with another right-to-left cut; frames 27–45: right arm alone sweeps from the left hip over head height to the right at 5–6.4 m/s, feet still (0.1 m of travel) → fitted (legs from 3–25, upper body from 25–47), **rejected on look** with the set |
| 2026-11-24 | 1H braced block guard (loop, MCP, prime) | — | 3.0 s | `A swordsman in a braced defensive guard: left foot forward, knees bent, body turned slightly sideways. His right hand holds a sword hilt in front of his right shoulder, the blade angled diagonally up and to the left across his face. His left hand is open, palm pressed flat on the middle of the blade. Seamless looping idle: only slow breathing. Ends in the exact starting pose.` | Bladed torso, near-loop (seam ≤ 0.06), right hand at face height with the blade pointing right (130° off), left hand 21 cm from the blade, arms drift 13 cm → **kept for the body** (spine / head); both arms replaced by fitted constants (`BAKE_RECIPES.md` → One-hand braced guard) |
