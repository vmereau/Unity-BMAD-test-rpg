---
title: 'Ancient civilization ruins — design language and look-test prompt pack'
type: 'feature'
created: '2026-11-10'
status: 'in-review'
baseline_commit: '40ed2862633b212d9d8e7daaecfa5f58261e45b6'
context:
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/_bmad-output/narrative-design.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The vanished civilization's visual language is still "spirals, circles, geometric motifs (TBD)". The only ruins artwork (`Art/Artworks/Starting area/Ruins.png`) shows generic mossy medieval ruins, which says nothing about a people who mastered magic by means no human can reach.

**Approach:** Write a design document for the lost civilization and its architecture (weird-fiction / brutalist, Lovecraftian by geometry only), then derive a small prompt pack — mood concepts plus image-to-3D input sheets — that Valentin generates himself to test the feel in 2D and in 3D.

## Boundaries & Constraints

**Always:**
- Three approved pillars: (1) **impossible local stone** — the territory's own grey stone, seamless, jointless, no tool marks, perfect circles and arcs, as if cast or grown from bedrock; (2) **pure geometry** — dread from scale, non-human proportions, precision and notation-like symbols; (3) **intact but displaced** — ancient stone does not erode (sharp edges, true circles) but is tilted, half-sunk, sheared or buried; only nature and human additions on top are worn.
- Stay inside `ART_DIRECTION.md`: muted earthy palette, banned prompt words, magic expressed by physical cues only, never glow or particles.
- Respect canon in `narrative-design.md`: destroyed by magic itself, cause unknown to the living, temporal layering (factions build on top of ancient foundations), ruins "wrong in ways that are hard to name".
- Prompts are deliverables only: give prompt + save path; Valentin runs image and Meshy generation.

**Ask First:**
- Any new lore fact beyond canon (the civilization's name, what they looked like, what the containment mechanism is).
- Rewriting GDD or narrative text beyond replacing the "(TBD in detail)" motif line with a pointer.
- Replacing or deleting the existing `Ruins.png` artwork.

**Never:**
- Creatures, eyes, tentacles, idols, faces or figurative carvings; glowing runes, crystals, floating stones, sci-fi panels or metal tech.
- Calling `generate_image` / `generate_model` or any paid generation API.
- Unity scene, prefab or code changes; importing 3D results (follow-up once the look is validated).
- Naming or imitating a specific game's assets in prompts (Control is a mood reference only).

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/ART_DIRECTION.md` -- shared art rules; Environment section and Template Index need a ruins entry
- `_bmad-output/narrative-design.md` -- canon; line ~832 holds the "(TBD in detail)" motif line
- `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` -- model for a verbatim prompt template with slots and Meshy image-to-3D input rules
- `Assets/_Game/Art/Artworks/Starting area/Ruins.png` -- current off-direction ruins concept, kept as the "before"
- `Assets/_Game/Art/Environment/` -- empty folder (`.gitkeep`) that will own the new docs
- `CLAUDE.md` -- root Folder CLAUDE.md Index needs an Environment row

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Art/Environment/ANCIENT_CIVILIZATION.md` -- create the design document: who they were as far as canon allows, design principles (the three pillars), form vocabulary (circle, arc, cylinder, monolith, cut plane), non-human proportions and metrics, symbol / notation system, material and surface rules, displacement types, what nature and each faction add on top, site tiers (outpost → deep site), audio/light notes, do / never list, prompt vocabulary -- single source of truth for the civilization's look
- [x] `Assets/_Game/Art/Environment/CLAUDE.md` -- create the folder file: two verbatim prompt templates with slots (mood concept, image-to-3D input sheet), Meshy prop pipeline notes, the look-test batch (3 mood concepts + 3 3D test pieces, full prompts and save paths), and an empty verdict log -- makes the test reproducible
- [x] `Assets/_Game/Art/ART_DIRECTION.md` -- extend the Environment section with a short ancient-ruins paragraph and add the template to the Template Index -- keeps the art entry point complete
- [x] `_bmad-output/narrative-design.md` -- replace the "(TBD in detail)" Ancient Civilization motif line with a one-line summary pointing to the design doc -- removes the stale TBD
- [x] `CLAUDE.md` -- add an Environment row to the Folder CLAUDE.md Index -- discoverability
- [x] `Assets/_Game/Art/Environment/*.meta` -- let Unity generate metas (refresh), never hand-write them; remove `.gitkeep` -- git convention

**Acceptance Criteria:**
- Given the design doc, when an artist or agent reads only it and `ART_DIRECTION.md`, then they can decide for any ruin element its material, proportions, condition and symbols without further questions (no "TBD" left in the doc).
- Given any prompt in the test batch, when it is checked against `ART_DIRECTION.md`, then it contains no banned word, opens with the style anchor, restates the palette and specifies the lighting for its asset type.
- Given the 3D input prompts, when read, then each describes one isolated object on a plain light gray background with flat shadowless light and the whole object in frame.
- Given the batch, when Valentin generates all six, then each prompt names its exact save path and the verdict log has a row ready for his keep / reject note.
- Given the docs, when searched for creature, eye, tentacle or glow imagery, then they appear only in "never" lists.

## Design Notes

The contradiction to resolve is Gothic's "Worn" pillar versus un-weathered ancient stone. Resolution: wear is the *human and natural* signature; its absence is the ancient one. A ruin reads as a sharp, perfect form wearing a coat of moss, soot, scaffolding and banners — the contrast is the horror, and it keeps every other asset on-pillar.

Mood-concept prompts are full scenes with in-world lighting; 3D inputs follow the character-sheet rules (isolated, flat light, gray background) because Meshy needs readable geometry.

## Verification

**Manual checks (if no CLI):**
- `grep -n -i -E "magical|legendary|epic|glowing|mystical|ornate|heroic|shiny|vibrant|sparkle" Assets/_Game/Art/Environment/CLAUDE.md` -- expected: matches only inside negative "no …" clauses or the never list
- `grep -n "TBD" Assets/_Game/Art/Environment/*.md` -- expected: no matches
- Unity console after refresh -- expected: `.meta` files generated for both new `.md` files, no errors
