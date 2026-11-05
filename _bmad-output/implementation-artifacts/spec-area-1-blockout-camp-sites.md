---
title: 'Area 1 blockout: camp-side sites'
type: 'feature'
created: '2026-11-05'
status: 'done'
baseline_commit: 'a26fd36'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json', '{project-root}/Assets/_Game/Scenes/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp's nearby sites are missing or broken: the fort has no wall or tower, the dock deck isn't reachable, a barricade snags paths, the hollow isn't hidden, and the hunter's fire doesn't exist.

**Approach:** Extend `Area1SitesBuilder` with one graybox root per site under `Area1_Blockout`, read positions from `layout.json`, add a clock-driven night light, then rerun Camp Edge and rebake. The remote sites, enemies, final walk-time table, map and planner go to a second spec.

## Boundaries & Constraints

**Always:** A tool re-run replaces only the roots it owns. Sites are simple placeholder shapes, and the hollow's fold uses rock meshes. Hours come from `DayNightConfig`. The wade lanes at x 31–32.4 and x 37.5–39 stay open. Runtime `NavMeshObstacle`s stay disabled in the scene. Sightlines are confirmed by a screenshot with a marker. Commit `.meta` files with assets. `ProjectSettings.asset` ends unchanged.

**Ask First:** Moving, replacing or reparenting `CampBridge`, `CampDock`, `CompanyCliff`, `SM_Floor_A_01 (8)`, `SM_bigRockA_a1 (18)`, the barricades or any Company house gameplay object. Any terrain change. If the wall can't reach the outcrop's south tip, keep `outcrop-edge` and tell Valentin.

**Never:** Remote sites or enemy moves (next spec). Moving tents, center stage, inner fence or palisade. Filling the terrain holes, or touching the chamber and door near (−3, −44). A wall around the `House`, or a plinth. Slope-slide or mountain-wall work. Touching `Core.unity`, the TMP font, Shimmer2 meta, `.lighting` files or the meshy test folder. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Night | Hour inside night | Light, flame and glow on | N/A |
| Day | Hour inside day | All off | N/A |
| Twilight | Hour in a twilight ramp | Level follows `1 − Daylight` | N/A |
| Time skip | `SkipTo` crosses sunset or sunrise | Correct on the next frame | N/A |
| No clock | `GameClock.Active` null | Off, logs once, picks up a clock that appears later | Logged |
| Bad config | Config or light missing | Off and disabled | `GameLog.Error` |
| Broken layout | Missing or malformed site feature | Logs the id | Abort before writing |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json` -- site source of truth; `hollow`, `outcrop-edge`, new fort features
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- tool to extend; keeps `Quarry` as is
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `Box`, `Ground`, `Yaw`, `TryReadLayout`; already warns when `outcrop-edge` is absent
- `Assets/_Game/Scripts/Core/Time/DayNightMath.cs`, `GameClock.cs` -- `Daylight`, `HourOfDay`, `GameClock.Active`
- `Assets/_Game/Data/Config/DayNightConfig.asset` -- sunrise, sunset, twilight
- `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs` -- polling and no-clock pattern to follow
- `Assets/_Game/Scenes/StartingTown.unity`, `Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset` -- rebuilt and rebaked

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- move `hollow` to (32, 30), 10 × 8; add `fort-wall-*` lines and `fort-tower` (1, −31); remove `outcrop-edge` once the wall reaches the tip -- layout stays the source of truth
- [x] `Assets/_Game/Scripts/World/ClockNightLight.cs` -- polls `GameClock.Active`, sets light, flame and glow from a pure `NightLevel` -- region scenes can't reference Core
- [x] `Assets/Tests/EditMode/ClockNightLightTests.cs` -- cover day, night, both ramps and wrapped hours
- [x] `Assets/_Game/Art/Materials/Blockout_FireGlow.mat` -- URP additive glow -- reads at distance
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- build `Fort`, `Docks`, `BridgeRails`, `Barricades`, `Hollow` and `HuntersFire`; validate everything before replacing a root
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run Sites, rerun Camp Edge, bake, save; then run tests
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `Scripts/World/CLAUDE.md`, `area-001-company-encampment.md`, `deferred-work.md` -- record the roots, light, fold and closed items

**Acceptance Criteria:**
- Given the fort, when viewed at eye level, then the wall meets the House, the ruin pieces and the tip, the tower stands near (1, −31), and the pillars are the only opening.
- Given `OutcropEdge` removed, when the player runs and jumps off the south tip, then the wall stops them, and Camp Edge's palisade panels are otherwise unchanged.
- Given a marker on the hollow floor, when viewed from the wall, tower top and yard, then a screenshot shows it hidden, and the bench, stone, fir and log are unmoved.
- Given night, when viewed from (11, 35) and the docks, then a screenshot with a marker shows the fire's glow.
- Given NavMesh paths and the player capsule, then all four openings, the full dock deck, the fort through its gate and both wade lanes are reachable, and the bridge still crosses with rails.
- Given the snag at (28.5, 55.4), when paths pass the barricades, then none runs into a collider.
- Given the rebake, then Ruins 2 stays sealed in play mode, the four site times from the camp centre stay within 1 s of 35.8, 30.7, 34.6 and 32.6 s, and the waterfall is unaffected.
- Given eye-level screenshots of the fort, docks, hollow and fire with the camera above ground, then they read like the artworks and nothing is magenta.

## Spec Change Log

- Review pass 1: Design Notes corrected to what was built and tested: tower 4 m wide (4.5 m would bury `SM_smallStoneE_a1`), west wall at x −0.5 (inset, so its panels stop above the chamber), and fold on the `hollow` south edge at about z 26. Patches only; no code was reverted. KEEP: `DeckFloor_*` colliders, added after a walker fell through the deck's own collider gap at the T junction.

## Design Notes

**Fort wall:** three lines, each starting and ending inside something solid. North: tower to about x 15.4, butted against `SM_Ruin_01`, so the ruin's pillars are the gate. West and south: tower along the rim near x −0.5, around the tip near z −50.5, to the `House` west wall at about (19.2, −45). East: `House` north-east corner along the rim to `SM_Ruin_04`, because the Company cliff rocks stop at outcrop height. Keep the ramp route at (13, −28) to (20, −33) clear. Stone blocks with timber hoardings and sheds on top, as in `Company fort`.

**Tower:** about 4 m wide, to clear `SM_smallStoneE_a1`, and about 15 m tall.

**Hollow fold:** rock meshes from about x 22 to x 40 along the hollow's south edge (z 26), about 6 m tall: a tower-top eye at y 25 needs that to hide a standing figure at the floor's far edge.

**Fire:** the ground rim clears the (11, 35) line by only 0.4 m, so the glow centres at about y 3. A real light only lights nearby ground at 85 m, so the distance read comes from an emissive flame and an additive glow about 6 m across.

**Dock:** the deck's NavMesh is in pieces, and only the first 2–3 m joins the bank; paths then leave the side into the shallows. Find the cause in the bake first, then bridge it from the `Docks` root, without editing `CampDock`. Boat and mooring sit on the deck's river side.

**Barricades:** colliders match the mesh bounds, but the meshes are sparse, so the bake leaves NavMesh inside them. Not Walkable volumes under `Barricades` close that and the 0.6 m gap behind them.

## Verification

**Commands:**
- `run_tests` with EditMode filter `ClockNightLightTests` and `Area1TerrainMathTests`, after the bake is saved -- expected: all pass

**Manual checks (if no CLI):**
- Play mode: set `Application.runInBackground = true` in one call, test in the next, confirm `Time.frameCount` advances, and check the light through a `SkipTo` and with no clock.
- `git diff --stat ProjectSettings/ProjectSettings.asset` -- expected: empty.

## Suggested Review Order

**Tool safety**

- Entry point: everything is checked before the first root is replaced.
  [`Area1SitesBuilder.cs:153`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L153)

- Hand-built objects are validated too, and never rebuilt.
  [`Area1SitesBuilder.cs:176`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L176)

- Number checks shared with Camp Edge, so bad layouts stop early.
  [`Area1BlockoutBuilder.cs:286`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L286)

**Fort**

- Wall lines end inside solid objects; panels stop above the chamber.
  [`Area1SitesBuilder.cs:298`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L298)

- The wall shapes, and the removed `outcrop-edge`.
  [`layout.json:542`](../../docs/World/Areas/area-001/layout.json#L542)

**Docks and barricades**

- Boards for the bake, invisible floors for the player.
  [`Area1SitesBuilder.cs:384`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L384)

- Collider-only floor covers the deck's collider holes.
  [`Area1SitesBuilder.cs:398`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L398)

- Not Walkable volumes fix the snag without moving barricades.
  [`Area1SitesBuilder.cs:510`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L510)

- Rails stay inside the deck, clear of the wade lanes.
  [`Area1SitesBuilder.cs:473`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L473)

**Hollow**

- The fold follows the `hollow` feature, so the layout stays the source.
  [`Area1SitesBuilder.cs:533`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L533)

**Hunter's fire**

- Polls the clock, so skips and a missing clock need no events.
  [`ClockNightLight.cs:45`](../../Assets/_Game/Scripts/World/ClockNightLight.cs#L45)

- Keeps alpha, so the glow fades in step with the light.
  [`ClockNightLight.cs:80`](../../Assets/_Game/Scripts/World/ClockNightLight.cs#L80)

- Flame and halo carry the 80–90 m read; the light only reaches the ground.
  [`Area1SitesBuilder.cs:564`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L564)

**Peripherals**

- Night level tests, including the midnight wrap.
  [`ClockNightLightTests.cs:13`](../../Assets/Tests/EditMode/ClockNightLightTests.cs#L13)

- Notes for future sessions.
  [`CLAUDE.md:57`](../../Assets/_Game/Scenes/CLAUDE.md#L57)
