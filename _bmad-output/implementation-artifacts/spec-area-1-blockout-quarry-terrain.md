---
title: 'Area 1 blockout: quarry cliffs and sealed Ruins 2'
type: 'feature'
created: '2026-11-05'
status: 'done'
baseline_commit: 'b0c7271'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The quarry and Ruins 2 are open pads: the player walks straight from one to the other, and from the ridge into either. The quest-002 passage has nothing to seal.

**Approach:** `Area1TerrainBuilder` gets cliffs on the quarry's south and east sides, a closed cliff ring around Ruins 2, and a narrow open-air cleft between them at about (255, −125). A new re-runnable tool adds a planks-and-rubble blocker that a `WorldFact` removes. It also clears trees for the next spec's cave and hunting camp. Then re-run Camp Edge, check the waterfall, rebake.

## Boundaries & Constraints

**Always:** Terrain changes go through the tool or `layout.json`, and the result stays deterministic. Ground outside the new cliffs and the two clearings stays identical. Site objects sit under `Area1_Blockout/Quarry`, and a re-run replaces only that root. Commit `.meta` files with their assets.

**Ask First:** Moving the quarry or Ruins 2 pads. Moving the cleft more than 5 m. Any cliff change that touches the quarry road or its end.

**Never:** Other sites, the fort, the hollow, enemy moves, quarry or Ruins 2 dressing (next spec). Editing hand-built objects, tents, center stage, inner fence, palisade or Company house objects. Touching the unrelated working-tree changes. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sealed | Fact false | Blocker active; no NavMesh path or walk from outside to Ruins 2 | N/A |
| Opened | Fact set true | Blocker gone at once; path complete, player walks through | N/A |
| Load | Saved fact true | Blocker off after the region loads | N/A |
| No Core | `WorldStateManager.Instance` null | Blocker stays, one warning | Logged |
| Broken layout | Missing `quarry-cleft` or `hunting-camp` | Logs the id | Abort before writing |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json` -- new `quarry-cleft`, `hunting-camp`
- `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- pure cliff and cleft height helpers
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- `Land`, `TreeAllowed`, `TryLoadLayout`
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `Box`, ground and layout helpers to share
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `GetFact`, `SetWorldEvent`
- `Assets/_Game/Data/Events/OnWorldFactChanged.asset` -- `GameEventSO_Fact` raised on every fact change
- `Assets/_Game/Scenes/StartingTown.unity`, `Art/Terrain/StartingTownTerrain.asset`, `Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset` -- rebuilt and rebaked

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- add `quarry-cleft` (road, width 4) `[[252,-116],[260,-134]]` and point `hunting-camp` (32, 214) -- layout stays the source of truth
- [x] `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- add `CliffSurface` and `CutCleft` -- pure and testable
- [x] `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- cover rise, crest, outer fall and cleft width
- [x] `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- build the massif and cleft in `Land`; keep trees 20 m off `cave` and 22 m off `hunting-camp`
- [x] `Assets/_Game/Scripts/World/FactRemovedObject.cs` -- hide a target GameObject while a `WorldFact` is true -- read on `Start`, then listen to `GameEventSO_Fact`
- [x] `Assets/_Game/Data/Facts/WorldFact_QuarryPassageOpened.asset` -- key `Quarry_passage_opened`; nothing sets it until quest-002
- [x] `Assets/_Game/Art/Materials/Blockout_Stone.mat` -- URP Lit, grey stone -- shared with the next spec
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- menu `Tools/World/Build Area 1 Sites`: builds `Area1_Blockout/Quarry/CleftBlocker`
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run the terrain, Camp Edge and sites tools; rebake
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `Scripts/World/CLAUDE.md`, `Data/Facts/CLAUDE.md`, `area-001-company-encampment.md`, `deferred-work.md` -- record the tool, cliffs, fact and blocker

**Acceptance Criteria:**
- Given the terrain before and after, when height samples are compared, then only the massif footprint and the two clearings differ, and the camp, roads, river and pads are unchanged.
- Given NavMesh paths with the blocker in play, when the target is the Ruins 2 centre, then none completes from the camp, quarry, ridge or mountain foot.
- Given the player capsule, when it runs at the ring from outside on all four sides and at the blocker, including jumps, then it never reaches the pad.
- Given the fact set in play mode, when a path and the capsule go from the quarry to Ruins 2, then both pass through the cleft, and the cleft is at least 3 m wide and open to the sky.
- Given NavMesh paths from the camp centre, then the quarry entrance still takes 25–40 s at 6 m/s, within 1 s of today.
- Given the Camp Edge re-run, then its panel count and position hash are unchanged.
- Given play mode, when the camera faces the waterfall, then it still reads as a waterfall.
- Given eye-level screenshots in the quarry, at the blocker and inside Ruins 2, with the camera above ground, then they read like `Quarry` for cliff scale and a hidden crack, and nothing magenta shows.

## Design Notes

**Massif, one shape:** every sample gets `w`, its distance to the nearest floor edge: the Ruins 2 pad, or the quarry's south and east edges `[[216,-118.5],[256,-118.5],[256,-93.5]]`. That line stops one crest short of the pad's far corners, because the crest rounds past each end and would otherwise wrap around them. The surface rises at 1.6 (58°) from each pad, stays at y 56 up to `w` 20, then falls at 1.6, and `h = max(land, surface)`. Pad rises are combined with `min`, so the ring never buries the quarry's corner.

**Why the outer face matters:** with a gentle back slope, a player could climb the ridge, walk the crest and drop into Ruins 2. A closed outer face above 45° makes the crest unreachable; quarry cliffs get it too, because they join the ring. Land south-east of the pad reaches about y 48 at the ring's edge, which leaves about 8 m of outer face. Don't lower the top without checking that.

**Jumps:** `PlayerController` allows jumps on steep ground and has no slope slide, so a player holding jump can climb any face. The cliffs stop walking and agents, and an invisible `RuinsRing` on `Ignore Raycast` stops jumps: a collider loop 10 m out from the pad, from one end of the blocker around to the other. The blocker's `Seal` adds a `Cap` collider up to the same height, so the air above the cleft is closed until the seal goes.

**Cleft:** after the massif, `h = min(h, floor + 2 × max(dist − 2, 0))`, with the floor rising from 25 to 30 between the pad edges, measured by distance to each pad, so neither pad is lowered. Walls are 63°, about 11 m tall at the saddle.

**Blocker:** 6 m into the cleft at about (254.4, −121.5), where the walls have risen, so it reads as a sealed crack. A tall box collider spans the flared walls, with planks and rubble as visuals. `NavMeshModifier` ignores its meshes at bake, so the cleft is baked open, and a carving `NavMeshObstacle` closes it at runtime. Removal opens the path with no rebake. A bake cuts an enabled carving obstacle into the NavMesh, even under the modifier, so the obstacle stays disabled in the scene and `FactRemovedObject` enables it in `Awake`. Test sealing in play mode.

## Verification

**Commands:**
- `run_tests` with EditMode filter `Area1TerrainMathTests` -- expected: all pass

**Manual checks (if no CLI):**
- Run the terrain tool twice and compare height hashes -- expected: identical.
- Set `Application.runInBackground = true` in one call, test in the next, and confirm `Time.frameCount` advances.

## Suggested Review Order

**Cliffs and cleft**

- Entry point: one block for the quarry and ring, then the cleft cut.
  [`Area1TerrainBuilder.cs:328`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L328)

- Floor measured between pad edges, so neither pad is lowered.
  [`Area1TerrainBuilder.cs:339`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L339)

- Cliff line stops short of the pad's far corners.
  [`Area1TerrainBuilder.cs:730`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L730)

- The height shapes, pure and tested.
  [`Area1TerrainMath.cs:75`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L75)

**Sealing Ruins 2**

- Jumps climb any cliff, so an invisible loop closes the ruins.
  [`Area1SitesBuilder.cs:147`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L147)

- Cap closes the air above the seal until it goes.
  [`Area1SitesBuilder.cs:126`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L126)

- Obstacle stays off in the scene: bakes cut enabled ones in.
  [`Area1SitesBuilder.cs:123`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L123)

**Opening it**

- Enables the carve at runtime, before anything can path.
  [`FactRemovedObject.cs:25`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L25)

- Reads the fact on start, which covers loads.
  [`FactRemovedObject.cs:41`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L41)

- Then follows fact changes live.
  [`FactRemovedObject.cs:51`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L51)

**Tool safety**

- Sites tool entry: every check runs before the old root goes.
  [`Area1SitesBuilder.cs:45`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L45)

- Terrain tool refuses a cleft it can't build.
  [`Area1TerrainBuilder.cs:683`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L683)

- Shared layout reader, split from the Camp Edge checks.
  [`Area1BlockoutBuilder.cs:285`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L285)

**Peripherals**

- Trees stay out of the cleft; cave and hunting camp clearings follow.
  [`Area1TerrainBuilder.cs:649`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L649)

- New layout features.
  [`layout.json:585`](../../docs/World/Areas/area-001/layout.json#L585)

- Math tests.
  [`Area1TerrainMathTests.cs:31`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L31)

- Tool facts for future sessions.
  [`CLAUDE.md:56`](../../Assets/_Game/Scenes/CLAUDE.md#L56)
