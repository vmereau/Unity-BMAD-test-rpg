---
title: 'Area 1 warehouse concept pack: reference captures, artwork prompts and build brief'
type: 'feature'
created: '2026-11-26'
status: 'done'
route: 'one-shot'
---

# Area 1 warehouse concept pack: reference captures, artwork prompts and build brief

## Intent

**Problem:** The warehouse over the starting corner is a graybox box on thin posts, and its final look is undecided (stone base or posts, how it meets the palisade and the rocks), so it cannot be rebuilt in Blender the way the huts were.

**Approach:** Fix the direction (a timber hall on braced posts, open underneath, with a rubble wall only on the fence line, better built than the huts, one line with the palisade, ramps as trestle carpentry), then give the user what they need to generate the artworks themselves: six 1920 × 1080 captures of the placeholder from fixed viewpoints, one image-to-image prompt per capture written to the art direction, and a build brief listing the dimensions and gameplay constraints the rebuild must keep. The rebuild (hall, both ramps, landing, hoist, palisade junction, rock clump dressing; not the interior) is a follow-up spec once the artworks are judged.

## Suggested Review Order

- Entry point: the chosen direction, in four lines.
  [`WAREHOUSE_CONCEPTS.md:12`](../../Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md#L12)

- How to generate: W1 first, it sets the look for the other five.
  [`WAREHOUSE_CONCEPTS.md:29`](../../Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md#L29)

- The shared template; each view pastes one paragraph into `{VIEW}`.
  [`WAREHOUSE_CONCEPTS.md:45`](../../Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md#L45)

- The six view paragraphs, W1 first.
  [`WAREHOUSE_CONCEPTS.md:61`](../../Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md#L61)

- Build brief: sizes, route, fence line, tool ownership, the dresser's ramp tie.
  [`WAREHOUSE_CONCEPTS.md:113`](../../Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md#L113)

- The six input captures.
  [`refs/`](../../Assets/_Game/Art/Artworks/Starting%20area/Camp/Warehouse/refs/)

- Pointer from the camp guide.
  [`Camp/CLAUDE.md:5`](../../Assets/_Game/Art/Environment/Camp/CLAUDE.md#L5)
