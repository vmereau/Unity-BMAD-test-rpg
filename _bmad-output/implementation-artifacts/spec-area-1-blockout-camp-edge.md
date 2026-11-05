---
title: 'Area 1 blockout: camp edge'
type: 'feature'
created: '2026-11-04'
status: 'done'
baseline_commit: '164b7c1'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp has no outer wall. The Civilization gate and the old WildernessPath fences still mark the old layout, and nothing stops the player walking out behind the outcrop.

**Approach:** A re-runnable editor tool builds a closed graybox palisade from `layout.json`, with an opening at every road crossing, the open north-west arrival gate, and one broken stretch that keeps the hunter's fire in view from the tents. The old gate and fences go. Rebake the NavMesh. Sites and enemies are the next spec.

## Boundaries & Constraints

**Always:** Build under `Area1_Blockout/CampEdge`, and a re-run replaces only that root. Fence shapes come from `layout.json`. The ring stays closed to walking and to NavMesh except at the four road openings. Keep the existing terrain unless the ford bank fails the player test. Commit `.meta` files with their assets.

**Ask First:** Moving any tent, center stage, inner fence or Company house object. Any change to the openings' positions. Any fence height other than 5 m.

**Never:** Sites, fort, hollow, enemy moves or quarry terrain (next spec). A blocker on the arrival gate: the pass blocker stays the story lock. Touching the unrelated working-tree changes. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First run | No `CampEdge` root | Root built, scene dirty | N/A |
| Re-run | `CampEdge` exists | Replaced, same panel count and positions | N/A |
| Broken layout | Missing fence or gate feature | Logs which id | Abort before changing the scene |
| Steep ground | Panel on a 58° rim | Bottom buried below the lowest ground under it, no gap | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- add polyline-to-panels helper
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- `KeptRects` comments, and the ford's south ramp (see Design Notes)
- `Assets/_Game/Scenes/StartingTown.unity` -- `Camp/CivilizationGate`, `Camp/WildernessPath`, `Area1_Blockout`, `Terrain_StartingArea` NavMeshSurface (collects all render meshes)
- `Assets/_Game/Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset` -- rebaked
- `docs/World/Areas/area-001/` -- `layout.json`, `layout.seed.json`, `area-001-company-encampment.md`
- `_bmad-output/implementation-artifacts/deferred-work.md` -- sites spec with its approved decisions

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- add `outer-fence-6` `[[39,-37.7],[38,-50],[20,-57],[-2.7,-52.2]]`, point `fence-break` at (3, 52.4) and `outcrop-edge` -- closes the ring and records the stretch; `layout.seed.json` keeps the original low-fence layout, so it stays as it is
- [x] `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- add a helper that cuts a polyline into panels and skips a break interval -- pure and testable
- [x] `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- cover panel count, corners, and the skipped interval
- [x] `Assets/_Game/Art/Materials/Blockout_Timber.mat` -- URP Lit, dark timber -- shared with the next spec
- [x] `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- menu `Tools/World/Build Area 1 Camp Edge`: palisade, arrival gate, broken stretch
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run the tool; delete `Camp/WildernessPath` and `Camp/CivilizationGate`, keeping `SM_woodenCoverA_a1` in place under `Camp`; rebake
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `area-001-company-encampment.md`, `deferred-work.md` -- update the zone table, tool notes and design doc; narrow the deferred entry to sites

**Acceptance Criteria:**
- Given NavMesh paths from the camp centre, when each opening's outside point is the target, then the path completes and passes through that opening.
- Given the player capsule driven with `CharacterController.Move` in play mode, when it walks out and back through each of the four openings, then it crosses each one, including the ford-side bank.
- Given NavMesh and the capsule, when each tries the broken stretch, the fence-6 trough or any palisade panel, then neither crosses.
- Given rays from the tents and spawn point to the fire point, then they pass through the broken stretch and hit nothing.
- Given rays from the tents to 12 m above the ruins' south lip, then they clear the palisade.
- Given eye-level screenshots at the arrival gate and from the tents, then they read like `Arrival` and `Master layout 2` for wall height and gate placement.

## Design Notes

**Panels:** boxes about 3 m long, slightly overlapped, top 5 m above the mean ground under each, ±0.3 m jitter from a fixed seed so the line reads as stakes. Art can replace the whole root later.

**Openings:** already gaps in the layout: arrival (−38.4, 36.3), wood (−51, 5.6), quarry (55.8, −5.8), wilderness (32.7, 51.8). Only the arrival gate gets structure: two 7 m timber towers on the gap's ends, no leaves or blocker.

**Broken stretch:** x −1..7 on the north fence, where ground is 1.8 m. Tents-to-fire rays cross there only 0.3–0.9 m above ground. So: end posts, one rail at 1.1 m that the rays pass under, a tall invisible `BoxCollider` on `Ignore Raycast` so sight tests and the camera skip it, and a `NavMeshModifierVolume` set to Not Walkable, because bakes collect render meshes and would miss the collider. Verify that the Player layer still collides with `Ignore Raycast`.

**Ring closure:** `outer-fence-6` runs south of the trough, so the back alley around the outcrop stays inside the camp. It passes just under the outcrop's south tip, so a player stepping off that 10 m edge would drop over it: `outcrop-edge` is an invisible blocker along the tip until the fort wall replaces it.

**Ford bank:** the player test failed: going back up to the camp, the bank is over the 45° limit, while NavMesh still crossed. The kept-ground blend and road grading both steepened the 30° ramp. Both now leave the camp-side ramp alone, with the tight blend only on the crossing, so its flanks keep their ledge-free blend. This needed a terrain re-run.

**Fire view, as tested:** rays hit nothing from the docks and four tent-area points, but tent tarps have no colliders, so rays miss them. Screenshots with a marker show the fire through the break from (11, 35), the north edge of the player's tents, and from the docks. Deeper in the tents, tarps and the rim hide it.

## Verification

**Commands:**
- `run_tests` with EditMode filter `Area1TerrainMathTests` -- expected: all pass

**Manual checks (if no CLI):**
- Run the tool twice and compare panel count and a position hash -- expected: identical.
- Set `Application.runInBackground = true` in play tests and confirm `Time.frameCount` advances -- expected: results are from a ticking game.

## Suggested Review Order

**Palisade tool**

- Entry point: validates first, then replaces only `CampEdge`.
  [`Area1BlockoutBuilder.cs:59`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L59)

- Panels follow the ground and stay buried on steep rims.
  [`Area1BlockoutBuilder.cs:99`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L99)

- Layout checks that keep a bad file from touching the scene.
  [`Area1BlockoutBuilder.cs:285`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L285)

**Closed to walking, open to sight**

- Rail above the fire rays, blocker for the player, volume for agents.
  [`Area1BlockoutBuilder.cs:156`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L156)

- Stopgap at the outcrop tip until the fort wall exists.
  [`Area1BlockoutBuilder.cs:198`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L198)

- Open arrival gate: towers only, the pass stays the story lock.
  [`Area1BlockoutBuilder.cs:128`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L128)

**Ford bank**

- Tight blend on the crossing only, so the flanks keep no ledges.
  [`Area1TerrainBuilder.cs:365`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L365)

- Camp side only: the north bank's road grading stays.
  [`Area1TerrainBuilder.cs:383`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L383)

- Road grading skips the ramp.
  [`Area1TerrainBuilder.cs:445`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L445)

**Peripherals**

- Panel cutter and its tests.
  [`Area1TerrainMath.cs:75`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L75)
  [`Area1TerrainMathTests.cs:94`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L94)

- Ring closure in the layout.
  [`layout.json:280`](../../docs/World/Areas/area-001/layout.json#L280)

- Navigation reference for the volume.
  [`ItemIconGenerator.asmdef:7`](../../Assets/_Game/Editor/ItemIconGenerator.asmdef#L7)

- Tool facts for future sessions.
  [`CLAUDE.md:53`](../../Assets/_Game/Scenes/CLAUDE.md#L53)
