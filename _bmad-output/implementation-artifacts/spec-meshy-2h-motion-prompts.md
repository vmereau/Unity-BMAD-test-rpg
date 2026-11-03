---
title: 'Meshy text-to-motion prompt guide for 2H combat clips'
type: 'docs'
created: '2026-11-03'
status: 'done'
route: 'one-shot'
---

# Meshy text-to-motion prompt guide for 2H combat clips

## Intent

**Problem:** The first Meshy text-to-motion 2H batch (idle guard, slash, overhead) was unusable. Measured in Unity: the
idle held the blade near the shoulder, the slash turned 200° and travelled 0.8 m, and the overhead spun 365° and travelled
1.6 m. Every clip was 2.97 s. Meshy can't chain clips and has no loop toggle, and prompts are capped at 400 characters.

**Approach:** A guide (`MESHY_PROMPTS.md`) with rules derived from those failures: a guard sentence shared word for word
by every clip, one positive body-constraint sentence, no in-clip step (`AttackStep` scripts it), and no flashy words.
It also holds v1 templates for idle / attack 1 / attack 2 (318 / 373 / 376 characters), the durations to set in the Meshy
UI, a Unity import checklist (Humanoid, all root transforms baked Based Upon Original) and a prompt log. No clip wiring:
the user regenerates the clips first.

## Suggested Review Order

1. [MESHY_PROMPTS.md](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/MESHY_PROMPTS.md) — rules → templates → import checklist → log
2. [Combat CLAUDE.md](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md) — folder-tree pointer + stale shoulder-rest note flagged
