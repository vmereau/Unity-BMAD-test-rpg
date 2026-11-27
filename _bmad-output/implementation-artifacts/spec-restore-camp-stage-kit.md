---
title: 'Restore the camp-centre stage kit in a hand-owned root'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
baseline_commit: '3cdefcf7'
context:
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The hand-built camp stage (12 `SM_Beam_A_01`, 4 `SM_Floor_A_01`, 1 `SM_Stair_A_03`, 4 `SM_Torch_A`) sat
inside the tool-owned root `Area1_Blockout/CampCentre/Stage`. The Sites run in commit 3cdefcf7 (gang house) replaced
that root, and hand edits can't keep added children, so only the post, pillory, chain and banner came back. The
NavMesh was rebaked without the deck: `WardenStageSpot` / `TamStageSpot` (event-001) are now off the NavMesh.

**Approach:** Recreate the 21 prefab instances from the scene at `3cdefcf7~1`, with the same world transforms, in a new
hand-owned root `Camp/CampCentre/StageKit` outside `Area1_Blockout`. Then rebake the NavMesh and document the root.

## Boundaries & Constraints

**Always:** same source prefabs, same world position / rotation / scale as at `3cdefcf7~1`, plus any per-instance
property overrides they carried. Leave the tool-built `Stage` parts (post, pillory, chain, banner) and `hand-edits.json`
as they are. Rebake the NavMesh through the documented path (`Editor/World/CLAUDE.md`).

**Ask First:** if an instance carried overrides that can't be reproduced (added / removed components, nested
overrides), or if the stage spots still aren't on the NavMesh after the rebake.

**Never:** change `Area1TentCamp` / `Area1SitesBuilder`, re-run Sites or Camp Edge, or touch `Assets/_Recovery/`.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scenes/StartingTown.unity` -- the scene; `3cdefcf7~1` holds the 21 instances parented to `Stage` (transform at local (4.44, 0.507, 31.22))
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs:605` -- `NewRoot(CampCentre)` destroys the root on every run (the cause)
- `docs/World/Areas/area-001/hand-edits.json` -- pins `CampCentre/Stage` + its parts and removes `Platform`/`Step_0..3`
- `ArrivalSequence/WardenStageSpot` (1.2, 23.6), `TamStageSpot` (−0.1, 24.8) -- must sample onto the NavMesh again
- `Assets/_Game/Scenes/STARTING_TOWN.md` -- Camp centre + Arrival sequence sections

## Tasks & Acceptance

**Execution:**
- [x] scratchpad script -- extract each instance's source prefab GUID, local transform under `Stage` and other `m_Modifications` from `git show 3cdefcf7~1:` the scene -- exact source of truth
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- via MCP: create `Camp/CampCentre` (identity) / `StageKit` with `Stage`'s world pose, `PrefabUtility.InstantiatePrefab` each piece with its saved local transform, names and overrides; save the scene -- restores the deck outside tool ownership
- [x] NavMesh -- rebake the Area 1 NavMesh surface the documented way; save -- puts the deck back on the NavMesh
- [x] `Assets/_Game/Scenes/STARTING_TOWN.md` -- Camp centre: the stage's deck, stair and torches are hand-owned under `Camp/CampCentre/StageKit`; the tool builds only post, pillory, chain, banner -- stops the next agent re-adding to the tool root
- [x] `Assets/_Game/Scenes/CLAUDE.md` -- add `Camp/CampCentre/StageKit` to the hand-owned roots line -- index

**Acceptance Criteria:**
- Given the restored scene, when the 21 `StageKit` children are compared with the `3cdefcf7~1` instances, then prefab, world position (≤ 1 mm), rotation and scale match.
- Given the rebake, when `NavMesh.SamplePosition` runs at both stage spots (1 m radius), then both hit within 0.3 m vertically.
- Given a later Sites run, when it replaces `Area1_Blockout/CampCentre`, then `StageKit` is untouched (it lives outside `Area1_Blockout`).

## Verification

**Manual checks:**
- Scene view of the square: the deck, stair and torches stand under the post as before; the post's foot sits on the deck.
- `read_console`: no errors after save and rebake.

## Suggested Review Order

**Cause**

- Sites destroys and rebuilds `CampCentre` on every run; added children are lost.
  [`Area1SitesBuilder.cs:605`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L605)

- Hand edits pin the stage parts and remove the blockout deck, but can't keep added art.
  [`hand-edits.json:676`](../../docs/World/Areas/area-001/hand-edits.json#L676)

**Restore**

- New hand-owned root holding the 21 kit pieces, posed like the tool's `Stage`.
  [`StartingTown.unity:278689`](../../Assets/_Game/Scenes/StartingTown.unity#L278689)

- Rebaked NavMesh: both event-001 stage spots sample onto the deck again (binary).
  [`NavMesh-Terrain_StartingArea.asset`](../../Assets/_Game/Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset)

**Docs**

- Rule: stage deck is hand-owned, never add art under the tool root.
  [`STARTING_TOWN.md:159`](../../Assets/_Game/Scenes/STARTING_TOWN.md#L159)

- Hand-owned roots index.
  [`CLAUDE.md:107`](../../Assets/_Game/Scenes/CLAUDE.md#L107)
