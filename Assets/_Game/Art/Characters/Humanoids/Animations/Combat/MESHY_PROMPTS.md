# Meshy text-to-motion prompts (combat clips)

> How to write Meshy animation prompts that come out usable, plus the log of every prompt sent.
> Clip events / timings: `CLAUDE.md` (this folder). Controller wiring: `../../Controllers/CLAUDE.md`.

Meshy limits: **400 characters** per prompt (count the whole text before sending), no "start from another
clip", no loop toggle. **Duration is set in the Meshy UI**, not in the prompt.

---

## Rules (from the failures of the first 2H batch, 2026-11-03; templates below are untested v1)

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
4. **No steps in the clip.** The forward step is scripted in game (`AttackStep`, `Scripts/Combat/PLAYER_COMBO.md`);
   a stepping clip stacks on it and ends off the start spot. Ask for "shifting weight onto the front foot".
5. **Say where the blade is**, not where it isn't: "blade upright in front of the chest". An unspecified
   hold defaults to a shoulder rest; "never on the shoulder" still names the shoulder.
6. **One action per clip with an explicit path** using plain verbs: start height → end height and side
   ("over the right shoulder … down diagonally to the left hip"). No style words ("epic", "powerful",
   "cinematic", "finisher") — they add spins and jumps.

## Durations (set in the Meshy UI)

| Clip | Duration | Why |
|------|----------|-----|
| Idle guard (loop) | 3.0 s | Long enough to breathe, short loop seam |
| Attack 1 (diagonal cut) | 1.8 s | Similar length to the current `2hand attack` (1.83 s) |
| Attack 2 (vertical chop, last hit) | 1.4 s | Heavier than the current 1.27 s last hit, still snappy |

The current event timings do **not** carry over — re-tune all four events and `attackSteps` after import.

## Two-handed templates (v1, untested)

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

## Unity import checklist (Meshy "animated_mixamo_without_skin" FBX)

- Delete the texture PNGs that ship with the download; Materials: **None**.
- Rig **Humanoid, Create From This Model** (Mixamo bone names map automatically) — check Configure: mapping
  all green. Compare root height with the existing 2H clips (Meshy models have needed Scale Factor 100 before).
- Root Transform **Rotation, Y and XZ all Bake Into Pose, Based Upon Original** — the convention of every
  2H clip. Then check net root XZ travel ≈ 0 (the body may move out and back). If it isn't, regenerate:
  unbaking XZ only turns the travel into foot slide (root motion is off) — last resort.
- Idle: also **Loop Time + Loop Pose**; the loop-match indicators must be green.
- Attacks: add the four events (`CLAUDE.md` → Attack Clip Events) with the Hitbox Tuner, then tune `attackSteps`.

## Prompt log

Record every generation: date, clip, template version, duration, exact prompt, verdict (with units).

| Date | Clip | Template | Duration | Prompt | Verdict |
|------|------|----------|----------|--------|---------|
| 2026-11-03 | 2H idle guard | — | 2.97 s | lost | Near-loop (mean muscle seam 0.03), blade rested on the shoulder → regenerate |
| 2026-11-03 | 2H slash (attack 1) | — | 2.97 s | lost | Body turned 200° and back, travelled 0.8 m → regenerate |
| 2026-11-03 | 2H overhead (attack 2) | — | 2.97 s | lost | Full 365° spin during the strike, travelled 1.6 m → regenerate |
