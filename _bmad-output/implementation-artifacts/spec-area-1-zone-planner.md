---
title: 'Area 1 zone planner and layout plan'
type: 'feature'
created: '2026-11-04'
status: 'done'
baseline_commit: '70c3e24'
context:
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Area 1 has a design doc and five artworks, but no agreed geography. The camp fills about 80×100 m of a 500×500 m terrain, and nothing fixes where the river, work sites, ruins or mountains go, so a blockout would be guesswork.

**Approach:** Export the real terrain and camp positions into an interactive top-down planner. Valentin arranges zones, paths and the river over it; the saved result becomes the approved layout that the blockout spec builds from.

## Boundaries & Constraints

**Always:** World units are metres in Unity x/z, on the 500×500 m terrain at origin (−250, −250). The planner shows the live scene's positions, not the doc's. Every proposed feature can be moved, resized or removed. Layout data is plain JSON in the repo. Artworks are reference only: the corner watermark means they never ship in-game.

**Ask First:** Any edit to `narrative-design.md` or `gdd.md`. Any change to the story meaning of a location, such as making the quarry passage publicly visible.

**Never:** Any terrain, scene, prefab or `Core.unity` change: that is the blockout spec. New runtime C#. Building the features themselves. Fixing the magenta tree (logged as deferred work).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Save layout | Zones and paths edited | Saved layout has every feature with id, type, x/z and size or points | N/A |
| Reopen | A saved layout exists | Planner restores it | No saved layout → loads the seed |
| Off-terrain drag | Feature dragged past the edge | Stays inside ±250 m | Clamped |
| Reset | Reset pressed | Seed layout returns | Asks for confirmation first |

</frozen-after-approval>

## Code Map

- `Terrain_StartingArea` -- 500×500 m, heights 0–10 m at 20 m samples; source of the map image
- `StartingTown.unity` `Camp/<Zone>`, `Water_Volume` (75, 50), spiders (62, −35), bandits (−70, 60) -- existing positions, read-only
- `Assets/_Game/Scenes/CLAUDE.md` -- zone table; lake not listed
- `docs/World/Areas/area-001-company-encampment.md` -- design source; gets the approved layout
- `Assets/_Game/Art/Artworks/Starting area/` -- five reference images

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/terrain-map.png` -- export a hillshaded top-down map through `execute_code`, read-only -- planner base
- [x] `docs/World/Areas/area-001/layout.seed.json` -- fixed layer from live scene positions; proposed layer from the Design Notes -- planner starting state
- [x] `docs/World/Areas/area-001/planner.html` -- build the planner: map, draggable zones, editable paths and river, scale in metres, walking distance per path, artwork panel; publish as an Artifact -- the tool
- [x] `docs/World/Areas/area-001/layout.json` -- store the layout Valentin approves -- blockout input
- [x] `docs/World/Areas/area-001-company-encampment.md` -- link the layout and record decisions from the Art Findings -- keeps the design doc honest
- [x] `Assets/_Game/Scenes/CLAUDE.md` -- add the lake to the zone notes -- stale doc
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- log the magenta tree -- out of scope here

**Acceptance Criteria:**
- Given the planner is open, when a viewer compares it with the scene, then fixed features sit within 2 m of their Unity positions.
- Given a path is edited, when its points change, then its displayed length in metres updates.
- Given Valentin approves a layout, when it is stored, then `layout.json` holds every feature and the scene is unchanged.

## Design Notes

**Seed proposal:** river along the north of the tents into the lake, with docks on the camp bank and the hunter's fire opposite; quarry near (−95, −60); ruins on a raised ridge across the river, as in `Master layout 2`; enlarged lake (today's is 20 m wide); wood camp in the west forest; hunting grounds north of the river; Fight Club Hollow in a rock fold north-west of the tents; cave near the spiders; mountain ring at the terrain edges, open to the west for the river and road; a low outer fence with the existing inner fence.

**Art Findings (decide in the planner, record in the doc):**
- **Orientation:** the first art has the river on the right when entering and facing the fort, which doesn't fit the approved river. Seed a north-west arrival gate instead: the road follows the river's south bank and enters facing south-east toward the fort, without crossing the water. The scene's current gate is south, beside the fort.
- **Fence:** the art shows a tall spiked palisade; the doc says a low outer fence, "the wilderness is worse than staying".
- **Company house (decided 2026-11-04):** follow the art: a stone fort with a tower on the outcrop, replacing the scene's hall in a later spec. It matches "repurposed ancient stone structures" in the narrative doc.
- **Quarry (decided 2026-11-04):** follow the doc: a hidden, worker-sealed crack. The first art's chained door is dropped; Valentin is regenerating the image.
- **Banners:** the art shows a raven and a ram; no Company emblem is defined.

**Layout review decisions (2026-11-04, Valentin):**
- The blockout leaves a fence opening wherever a road or path crosses the outer fence.
- The spiders move away from the quarry road.
- The river rises in the eastern mountains as a spring or waterfall, runs through the lake, and leaves by the western pass. There is no exit through the mountains.
- Fight Club Hollow stays near (40, 28): the blockout adds a rock fold that hides it from the fort, checked with a sightline test.
- The ruins' ridge must be tall enough to read from the tents at about 200 m.
- `Ruins 2` is a set of additional ruins in and hidden around the quarry; the sealed passage connects to them.
- The tutorial cave stays in the layout.
- The camp's internal layout is not final: `layout.json` can be approved for the valley without fixing it.

## Verification

**Commands:**
- `git status --short -- Assets/_Game/Scenes/StartingTown.unity Assets/_Game/Art/Terrain` -- expected: no output

**Manual checks (if no CLI):**
- Save, reload and reset the published planner, then confirm a feature dragged past the edge stays on the map.

## Suggested Review Order

**Approved layout**

- Start with the blockout's input: Valentin's decisions and the free-text rules.
  [`layout.json:500`](../../docs/World/Areas/area-001/layout.json#L500)

- The western gap was widened so the river clears the mountain ends.
  [`layout.json:6`](../../docs/World/Areas/area-001/layout.json#L6)

- Live scene positions the planner can't move; spiders and bandits were corrected in review.
  [`layout.seed.json:12`](../../docs/World/Areas/area-001/layout.seed.json#L12)

**Keeping saves safe**

- Newest of store and draft wins; a failed read locks saving.
  [`planner.html:589`](../../docs/World/Areas/area-001/planner.html#L589)

- Saved data is checked feature by feature, and bad entries are dropped.
  [`planner.html:563`](../../docs/World/Areas/area-001/planner.html#L563)

- Save handles double clicks, timeouts and edits made while saving.
  [`planner.html:526`](../../docs/World/Areas/area-001/planner.html#L526)

**Editing on the map**

- A 3 px threshold stops clicks nudging features.
  [`planner.html:451`](../../docs/World/Areas/area-001/planner.html#L451)

- Every feature is kept inside the terrain.
  [`planner.html:396`](../../docs/World/Areas/area-001/planner.html#L396)

**Docs**

- The design doc links the layout and records the art decisions.
  [`area-001-company-encampment.md:20`](../../docs/World/Areas/area-001-company-encampment.md#L20)

- The next spec's full build list.
  [`deferred-work.md:399`](deferred-work.md#L399)
