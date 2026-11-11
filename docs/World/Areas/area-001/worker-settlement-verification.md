# Worker settlement wrap-up — 2026-11-11

The current saved StartingTown scene is the accepted layout after Valentin's manual blockout pass. No regeneration, geometry replacement, terrain operation or gameplay relocation was performed during this wrap-up.

## Current scene

- PlayersZone: 39 huts, 8 tarp bays, shared fire/repair/card props.
- WretchesCorner: 28 huts/partial shelters, 16 tarp bays.
- MafiaZone: 8 huts, 2 tarp bays.
- Canteen: timber cookhouse and dining canopy; Ditch retained as arranged.
- Player hut (-0.52,47.97), bed (-0.50,47.89); separate spawn (11.06,37.85).
- Cook (-6,20), cooking station (-7,22).

## Preservation

The whole `Area1_Blockout/TentCamp` root is hand-owned, controlled by `settlement-hand-owned.enabled` in layout.json and layout.seed.json. This includes all duplicated huts, tarp bays, child edits, courtyard props, ditch and canteen. Full Sites builds skip the root and do not apply hand-edits to it; scoped Worker Settlement builds refuse before mutation. Historical worker plot features remain construction history and do not describe the finalized layout. They must not be used to reconstruct it.

A live guard check returned false with the hand-owned explanation, retained the exact same Transform references and left StartingTown clean (`isDirty=false`). No full Sites run was performed because it would rebuild unrelated sites. A temporary backup of the accepted scene is at `Temp/WorkerSettlement/StartingTown.user-final.unity`; the saved scene itself is the durable artifact.

## Checks

- Unity compilation passed; console check after compile returned no errors.
- 97/97 EditMode tests passed across Area1WorkerSettlementTests, Area1HandEditsTests and Area1TerrainMathTests (job 5b85cfd9b68549318bb6ee64fc31f4ba).
- Existing baked NavMesh returns PathComplete from current spawn to square, Cook, inner gate, wilderness opening and player bed, sampling within 3 m.
- This is connectivity evidence on the current bake, not proof of controller clearance after all manual changes. No fresh bake, full play traversal, camera test or protected sightline certification was performed in wrap-up.
- Existing Core and terrain diffs belong to the user's current work and were preserved. Unity scene YAML has whitespace flagged by git diff --check; scene serialization was not reformatted.

## Remaining visual validation

Walk the latest arrangement in Play mode to assess tight passages and roof/camera occlusion, confirm the hunter fire and punishment-post views, and rebake terrain navigation when the layout is settled. The new ownership guard deliberately preserves user geometry even where it differs from the former spec's exact spacing/route targets.
