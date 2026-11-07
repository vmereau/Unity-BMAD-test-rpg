---
title: 'Area 1 hand-edit capture'
type: 'feature'
created: '2026-11-07'
status: 'in-review'
baseline_commit: 'f60ba59'
context: ['{project-root}/Assets/_Game/Scenes/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Build Area 1 Sites` and `Build Area 1 Camp Edge` destroy and rebuild their roots with no warning, so hand edits inside them are lost. `layout.json` can't describe child positions or deletions, and many of those values are hardcoded in the builders.

**Approach:** A review window compares the live scene with a fresh build in a temporary clone of `Area1_Blockout`, lets the user tick the real edits, and saves them to `docs/World/Areas/area-001/hand-edits.json`. Both builders reapply that file at the end of every run.

## Boundaries & Constraints

**Always:** Scope is the roots each tool rebuilds, plus only `DeckFloor_*`, `Boat`, `MooringPost` and `MooringRope` in `Docks`. Paths are relative to `Area1_Blockout`. A scan never changes live objects, and the clone is destroyed even on failure. Saving replaces the whole file with the ticked rows, so an unticked row stops being applied. Every path that doesn't resolve on reapply logs a warning, never an error. A rerun with no file behaves exactly as today.

**Ask First:** Any change to what a builder generates, beyond splitting out the root it builds into.

**Never:** Terrain holes, paint, trees or heights. Hand-owned objects (`CampBridge`, `CompanyCliff`, `FightPit`, deck pieces, root-level props). Recreating objects added by hand inside a tool root: they are listed as "will be lost" and not saved. Changes to `layout.json`, `layout.seed.json` or the planner. No automatic run on import, save or play.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Moved child | `GuardPost/Lockup` differs from the clone | Row ticked by default, saved with local pos, rot and scale, reapplied after a run | N/A |
| Height-only change | Only local y differs | Row shown, tagged "height only", unticked by default | Stale ground and a hand edit look the same, so the user decides |
| Deleted | In the clone, not live | `removed` row, ticked by default; the object is destroyed after a run | N/A |
| Added by hand | Live, not in the clone | "Will be lost" row, no tick box | Not saved |
| Same-named siblings | Two children share a name | Path uses `Name[1]` for the second | Ordinal resolved per parent |
| Override target gone | Path no longer built | Run finishes | `GameLog.Warn` per path, then a total |
| Broken file | Invalid JSON or wrong shape | Builder stops before destroying anything | `GameLog.Error`, "nothing changed" |
| Builder can't run | Missing feature or material | Scan shows the reason | Clone destroyed, no file written |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- `Build()` finds the root with `GameObject.Find`, validates, then calls `NewRoot` per site; `DockParts` names what it owns in `Docks`
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- Camp Edge `Build()`, replaces `CampEdge`
- `Assets/_Game/Editor/World/Area1TerrainSculpt.cs` -- precedent: a data file beside the layout, pure and testable
- `docs/World/Areas/area-001/planner.html:411` -- export writes only version, savedAt, features, decisions, notes, so it would drop any extra section
- `Assets/_Game/Scenes/CLAUDE.md` -- documents tool ownership

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Editor/World/Area1HandEdits.cs` -- new: `Snapshot` of a tool root (paths with ordinals, local transforms), pure `Diff(baseline, live)` and `Merge(rows, saved)`, `TryLoad`, `Save`, `Apply(root, owns)` -- all logic testable without a scene
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- split `Build()` into the menu item and `Run(GameObject root, bool applyEdits, out string problem)`; collect the owned root names in `NewRoot`; expose them and a dock-part filter -- lets the clone be built and scoped
- [x] `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- same split -- same reason
- [x] `Assets/_Game/Editor/World/Area1HandEditsWindow.cs` -- new `Tools/World/Area 1 Hand Edits` window: Scan, rows grouped by root with a tick-all per root, Save; pre-tick rows already in the file -- review step
- [x] `Assets/Tests/EditMode/Area1HandEditsTests.cs` -- matrix cases for diff, ordinals, epsilon, JSON round trip, malformed file -- pure logic
- [x] `Assets/_Game/Scenes/CLAUDE.md` -- describe the tool, pinning and the stale-ground rule -- future agents
- [ ] `docs/World/Areas/area-001/hand-edits.json` -- capture today's edits through the window -- first real use

**Acceptance Criteria:**
- Given today's committed scene and ticked edits, when Sites then Camp Edge run, then every `GuardPost`, gate lean-to, `Warehouse`, walkway and dock entry in the file matches the live scene, and a rescan lists no new rows.
- Given a scan, when it ends or fails, then no clone remains and live transforms are unchanged.
- Given an entry in the file, when the terrain is re-sculpted and tools rerun, then that object keeps its stored transform (pinned), and neighbours follow the ground.

Review pass 1: three independent reviewers, patched in place with no revert and the frozen block untouched. No scope or clone-isolation violations. Added: saved edits with no scene difference stay listed and ticked, and Save is enabled after any successful scan, so nothing vanishes unseen and an undone edit can be cleared. Save refuses when the scene changed since the scan. Scan reports builder exceptions. Height-only now includes scale y, because panel heights follow the ground. Rotation uses a real angle, because `Quaternion.Angle` reads 0 below about 0.16 degrees. Moved entries store `built`, and a run warns when that changes, because `Panel_{n}` is a running count across fence lines. Ordinals are limited to 9 digits. Sites applies only paths under its own roots. A prefab-instance child is switched off, because Unity refuses to destroy it. The Camp Edge hash is taken before the edits. KEEP: baseline by clone, `BuiltRoots` filled by `NewRoot`, reapply last, `hand-edits.json` beside the layout. Accepted limits, documented in `Scenes/CLAUDE.md`: a scan can mark the scene modified, only transforms and existence are compared, a removed entry has no retarget check, renames read as removed plus added, and edits don't reach geometry built from them.

## Design Notes

**Separate file, not `layout.json`:** this departs from the agreed location. The planner exports a fixed field list and loads from its own store, so saving from it would silently delete a section in `layout.json`. A sibling file has the same format, follows `terrain-sculpt.bin`, and avoids any planner change.

**Baseline by clone:** `Instantiate` the root, run the tool on the clone with `applyEdits: false`, compare, destroy. No generated values are duplicated, and tool seeds make runs repeatable. Barricades are scanned from the scene and the clone holds the deck, so validation passes unchanged.

**Stale ground:** the camp was re-sculpted without a rerun, so ground-following objects differ from the clone although nobody moved them. Pinning those would freeze wrong heights, hence height-only rows start unticked.

**Epsilon:** 1 mm position, 0.01° rotation, 0.001 scale. Values are written to 4 decimals.

```json
{ "moved": [{ "path": "GuardPost/Lockup", "pos": [-4.38, -0.1029, 6], "rot": [0, 0.3434, 0, 0.9392], "scale": [1, 1, 1], "built": [-6.77, -0.1029, -1.32] }],
  "removed": ["Warehouse/Crate_0"] }
```

## Verification

**Commands:**
- Unity Test Runner, EditMode, filter `Area1HandEditsTests` -- expected: all pass. Needs UnityMCP reconnected, or a manual run.

**Manual checks (if no CLI):**
- Scan, tick, Save, run Sites and Camp Edge, rescan: edits persist and only rows already in the file appear, pre-ticked. Then rebake the NavMesh.

## Suggested Review Order

**Baseline by clone**

- Entry point: clone the root, build into it, snapshot, always destroy it.
  [`Area1HandEditsWindow.cs:117`](../../Assets/_Game/Editor/World/Area1HandEditsWindow.cs#L117)

- The menu item now only finds the root, so Run can target a clone.
  [`Area1SitesBuilder.cs:181`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L181)

- Ownership comes from what NewRoot built, so it can't drift from the tool.
  [`Area1SitesBuilder.cs:398`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L398)

**Comparing and saving**

- Height-only includes scale y, because panel heights follow the ground.
  [`Area1HandEdits.cs:328`](../../Assets/_Game/Editor/World/Area1HandEdits.cs#L328)

- Saved edits with no scene difference stay listed, so Save can't drop them unseen.
  [`Area1HandEdits.cs:116`](../../Assets/_Game/Editor/World/Area1HandEdits.cs#L116)

- Save refuses when the scene changed after the scan.
  [`Area1HandEditsWindow.cs:142`](../../Assets/_Game/Editor/World/Area1HandEditsWindow.cs#L142)

**Reapplying**

- Resolves everything first, warns on a moved build position, then changes the scene.
  [`Area1HandEdits.cs:132`](../../Assets/_Game/Editor/World/Area1HandEdits.cs#L132)

- Sites only touches paths under its own roots.
  [`Area1SitesBuilder.cs:413`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L413)

- A broken file stops the run before the first destroy.
  [`Area1SitesBuilder.cs:329`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L329)

**Peripherals**

- Pure logic: diff, ticks, ordinals, epsilon, JSON and malformed files.
  [`Area1HandEditsTests.cs:12`](../../Assets/Tests/EditMode/Area1HandEditsTests.cs#L12)

- Pinning, stale-ground rule and accepted limits for future agents.
  [`CLAUDE.md:60`](../../Assets/_Game/Scenes/CLAUDE.md#L60)
