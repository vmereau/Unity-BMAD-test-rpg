---
title: 'Area 1 blockout: remote sites and enemies'
type: 'feature'
created: '2026-11-05'
status: 'done'
baseline_commit: 'e5b772e'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json', '{project-root}/Assets/_Game/Scenes/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Area 1 still lacks its remote sites, the dock deck is cut off from the NavMesh, and the enemies stand in old spots: on or near roads, or floating over the sculpted ground.

**Approach:** Extend `Area1SitesBuilder` with one graybox root per remote site and an invisible walkable deck surface, move the enemies, rebake, then refresh the map and planner and record walk times between all sites.

## Boundaries & Constraints

**Always:** A tool re-run replaces only the roots it owns. In `Docks` it keeps both `SM_WoodPlatformFloorKit_01a` pieces and replaces only `DeckFloor_*`, `Boat`, `MooringPost` and `MooringRope`. Sites are simple placeholder shapes. Positions come from `layout.json`. The deck stays free of visible boards. Runtime `NavMeshObstacle`s stay disabled in the scene. Every sightline is confirmed by a screenshot with a marker, with the camera above ground. Keep the quarry road end, the cleft and the path points (127, 143) and (226, −96) clear. Commit `.meta` files with assets. `ProjectSettings.asset` ends unchanged.

**Ask First:** Any terrain change or tree removal. Moving a Company house gameplay object. Ruin towers above 18 m. Any change to `CleftBlocker` or `RuinsRing` behaviour.

**Never:** Moving tents, center stage, inner fence or palisade. Breaking `Fort`, `BridgeRails`, `Barricades`, `Hollow` or `HuntersFire`. Rebuilding `Walkway_*`. Baking from physics colliders, or joining the deck with NavMesh links. Using the disabled `Ground` surface. Terrain holes. Slope-slide or mountain-wall work. Touching `Core.unity`, the TMP font, Shimmer2 meta, `.lighting` files or the meshy test folder. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Re-run | Sites tool run twice | Same result; hand-built objects and enemies untouched | N/A |
| Broken layout | Missing or malformed site feature | Logs the id, changes nothing | Abort before writing |
| Missing prefab | Rock prefab or material missing | Logs it, changes nothing | Abort before writing |
| Blocker in place | Play mode, fact false | Ruins 2 unreachable | N/A |
| Blocker removed | Play mode, fact true | Ruins 2 reachable through the cleft | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- tool to extend; `Build` validates first, `NewRoot`, `DockParts`, `BuildDocks`, `PlaceRock`
- `Assets/_Game/Editor/World/Area1RemoteSites.cs` -- new: the remote sites' shapes; `Area1SitesBuilder` owns the roots and validation
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `Box`, `Ground`, `GroundUnder`, `Yaw`, `TryReadLayout`
- `docs/World/Areas/area-001/layout.json` -- `ruins-arch` (added), `zone-4`, `wood-camp`, `hunting-camp`, `cave`
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- read only: `hunting-camp` and `cave` centre tree clearings of 22 m and 20 m
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `_waypoints`
- `Assets/_Game/Scenes/StartingTown.unity`, `Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset` -- enemies, roots, bake
- `docs/World/Areas/area-001/terrain-map.png`, `planner.html`, `layout.seed.json` -- 1000 × 1000 map; planner fetches the map and seed, and its `layouts/current` database document wins over `layout.json`

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- move `hunting-camp` to (32, 222) -- (32, 214) is the trail's steep end
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- add `Ruins`, `QuarryDressing`, `Ruins2Dressing`, `WoodCamp`, `HuntingCamp` and `Cave` roots; give `DeckFloor_*` the walkable surface; drop `Walkway_` from `DockParts`; validate before replacing anything
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run Sites; move `Bandit` root and snap its waypoints; wire all five into the child `bandit`'s `EntityBrain._waypoints`; move and snap the three spiders; bake `Terrain_StartingArea`; save; only then run tests -- enemies aren't tool-owned, so a re-run leaves them alone
- [x] `docs/World/Areas/area-001/terrain-map.png`, planner artifact -- recapture at 1000 × 1000 from the current terrain and republish to the existing URL with `layout.json` and the map
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `Scripts/AI/CLAUDE.md` if needed, `area-001-company-encampment.md`, `deferred-work.md` -- record roots, new palisade height, enemy spots and walk times; close the deferred item

**Acceptance Criteria:**
- Given a marker on the ruin tops, when viewed by day from about (15, 28), then a screenshot shows the ruins over the palisade.
- Given the player and a NavMesh agent, then both reach the whole dock deck, all four openings, the fort through its gate, both wade lanes and every new site, and the deck shows no boards in a screenshot.
- Given the rebake, then the hollow stays hidden from the fort, the fire shows at night from (11, 35) and the docks, and the waterfall is intact in-game.
- Given the spiders and bandit, then each stands on the NavMesh and off every road, and in play mode the bandit walks all five waypoints.
- Given NavMesh paths at 6 m/s from (−0.45, 0.50, 16.06), then the six baselines (36.1, 44.1, 30.5, 35.3, 33.3, 18.3 s) are compared and a table between all sites records exact start and end points. Any change over 1 s is explained.
- Given eye-level screenshots against `Master layout 2`, `Ruins` and `Quarry`, then the sites read as the art's massing and nothing is magenta.

- Review pass 1: three independent reviewers. Patches only; no code was reverted. Fixed: a float loop that dropped the cave's last rock row (25 of 27); floating top scaffold ledgers; quarry positions now offset from the `quarry` feature, which is validated; Ruins 2 pulled inside 10 m; hunting-camp props moved off the slope, small props buried; `Wall_1` cleared from the lane behind the arch; the arch turned square to the tents and moved 2 m north, because on the world axes only about 0.8 m of its opening faced them. Rebaked and retested on this geometry. KEEP: `ShadowsOnly` deck floors; enemies as one-off scene edits, not tool-owned.

## Design Notes

**Docks:** `DeckFloor_*` get a cube mesh at their existing size, with the `MeshRenderer` on `ShadowsOnly`. Render-mesh bakes collect that, not disabled renderers. If its shadow shows in the gaps, use a zero-alpha material. NavMesh stops one agent radius (0.3 m) short of the deck edges.

**Ruins:** on the lip around z 142–148, with the arch near (127, 148), square to the tents, and (127, 143) walkable, as in `Ruins`; low walls and rubble run behind it to about z 156. The palisade now hides below y 40–44 at x 118–138 (41 at x 126), and the ground is y 35. Use 18 m towers at x 118–134 and a lower piece near x 140, which foliage partly screens.

**Quarry:** scaffolds along the south and east faces, as in `Quarry`. **Ruins 2:** ruin shapes within 10 m of (272, −147), because the pad's outer edge slopes; leave the cleft mouth (260, −134) open.

**Wood camp:** on the y 6 pad; leave the road end (−182, 3) clear. **Hunting camp:** within 14 m of (32, 222).

**Cave:** a box chamber about 10 × 8 m and 4 m tall sits on the ground at (85, −45), with a 3 m wide mouth facing the camp. Rock meshes cover the outside and must stay out of the chamber, and the box walls set the interior. Path to the chamber floor.

**Enemies:** the `Bandit` root moves by about (−40, −30), keeping the waypoint pattern, so the nearest waypoint is about 12 m off the wood road (bandit detection 8 m). If a waypoint lands in a tree, nudge it. Put the spiders in the chamber and at its mouth; they detect at 9 m and the quarry road is 25 m away. Spider pivots sit at ground level.

## Walk Times

Paths are measured on the NavMesh at 6 m/s in edit mode, from the saved bake.

Start: camp (−0.45, 0.50, 16.06). Ends: quarry road end (193.00, 21.13, −66.00), pad centre (226.00, 25.05, −96.00), wood road end (−182.00, 6.35, 3.00), wood camp (−224.00, 6.05, −8.00), trail end (31.70, 11.89, 208.00), hunting camp (32.00, 7.41, 222.00), ruins lip (127.00, 35.01, 143.00), cave chamber (85.00, 0.65, −45.00), Ruins 2 (272.00, 30.05, −147.00), bandit site (−110.00, 7.10, 30.00), deck head (6.03, 0.85, 71.39).

| Baseline from camp | Before | After |
|--------------------|--------|-------|
| Quarry road end | 36.1 | 36.6 |
| Quarry pad centre | 44.1 | 44.0 |
| Wood road end | 30.5 | 30.5 |
| Trail end | 35.3 | 35.3 |
| Ruins lip | 33.3 | 33.3 |
| Cave | 18.3 | 18.4 |

Seconds between sites (row to column; the small differences each way come from path corners). Ruins 2 is an edit-mode path, through the cleft: the blocker only carves at runtime.

| From | Quarry road | Pad | Wood road | Wood camp | Trail end | Hunting | Ruins lip | Cave | Ruins 2 | Bandit |
|------|-------------|-----|-----------|-----------|-----------|---------|-----------|------|---------|--------|
| Camp | 36.6 | 44.0 | 30.5 | 37.6 | 35.3 | 37.7 | 33.3 | 18.4 | 57.0 | 19.6 |
| Quarry road | -- | 7.5 | 68.0 | 75.1 | 63.2 | 65.5 | 61.4 | 24.2 | 20.5 | 56.5 |
| Pad | 7.5 | -- | 75.1 | 82.0 | 70.3 | 72.7 | 68.3 | 32.2 | 12.3 | 66.7 |
| Wood road | 66.4 | 73.8 | -- | 7.2 | 65.1 | 67.5 | 62.5 | 48.1 | 86.3 | 12.8 |
| Wood camp | 73.5 | 79.9 | 7.2 | -- | 72.2 | 74.6 | 69.8 | 55.2 | 91.7 | 20.3 |
| Trail end | 62.8 | 70.1 | 65.0 | 72.1 | -- | 2.5 | 21.6 | 44.7 | 83.1 | 50.5 |
| Hunting | 65.5 | 72.8 | 66.5 | 74.4 | 2.5 | -- | 22.1 | 47.0 | 85.8 | 53.1 |
| Ruins lip | 63.1 | 70.4 | 62.4 | 69.6 | 20.8 | 22.1 | -- | 43.2 | 83.4 | 47.5 |
| Cave | 24.3 | 31.6 | 48.4 | 55.5 | 45.4 | 47.8 | 43.4 | -- | 44.7 | 37.1 |
| Ruins 2 | 19.2 | 11.7 | 86.4 | 93.4 | 82.1 | 84.5 | 80.1 | 43.2 | -- | 77.9 |
| Bandit | 55.4 | 62.7 | 13.0 | 20.0 | 49.1 | 51.5 | 48.9 | 37.0 | 75.7 | -- |
| Deck head | 43.4 | 50.8 | 35.3 | 42.8 | 30.7 | 33.1 | 28.6 | 25.4 | 63.8 | 21.9 |

## Verification

**Commands:**
- `run_tests` EditMode, `Area1TerrainMathTests` and `ClockNightLightTests`, after the bake is saved -- expected: all pass

**Manual checks (if no CLI):**
- Play mode: set `Application.runInBackground = true` in one call, test in the next, and confirm `Time.frameCount` advances.
- Call `Physics.SyncTransforms()` before edit-mode rays, or run them in play mode.
- `git status` -- expected: no change to `ProjectSettings/ProjectSettings.asset` or the five unrelated paths beyond their starting state.

## Suggested Review Order

**Tool safety**

- Entry point: new sites are validated before the first root is replaced.
  [`Area1SitesBuilder.cs:171`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L171)

- Keeps the hand-built deck; `Walkway_` is no longer a tool part.
  [`Area1SitesBuilder.cs:55`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L55)

**Docks**

- An invisible surface the bake still collects joins the whole deck.
  [`Area1SitesBuilder.cs:440`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L440)

**Sightline and ruins**

- Arch square to the tents, so the opening reads over the palisade.
  [`Area1RemoteSites.cs:42`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L42)

- Layout anchor for the ruins, on the sight corridor.
  [`layout.json:707`](../../docs/World/Areas/area-001/layout.json#L707)

**Work sites**

- Dressing follows the `quarry` feature and keeps to the faces.
  [`Area1RemoteSites.cs:78`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L78)

- Top level gets no ledger, because posts end there.
  [`Area1RemoteSites.cs:117`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L117)

- Stays on the flat pad, clear of the line from the cleft.
  [`Area1RemoteSites.cs:157`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L157)

- Props moved off the slope the camp left.
  [`Area1RemoteSites.cs:234`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L234)

**Cave**

- Box chamber on the ground; walls set the interior, rocks stay outside.
  [`Area1RemoteSites.cs:273`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L273)

- Integer rows, because a float loop dropped the last one.
  [`Area1RemoteSites.cs:316`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L316)

**Peripherals**

- Hunting camp moved off the steep trail end.
  [`layout.json:698`](../../docs/World/Areas/area-001/layout.json#L698)

- Notes for future sessions, including the new palisade height.
  [`CLAUDE.md:60`](../../Assets/_Game/Scenes/CLAUDE.md#L60)
