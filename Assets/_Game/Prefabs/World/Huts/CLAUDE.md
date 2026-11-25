# CLAUDE.md — Assets/_Game/Prefabs/World/Huts

> Art-pass worker hut shells for the Area 1 camp: a board-built Blender model with colliders, weight poles and a
> restricted area added by the builder. Model and wood rules live in `Art/Environment/Camp/CLAUDE.md` (→ Hut shell);
> light values are the constants at the top of `Editor/World/HomeYardDresser`.

---

## Prefabs

| Prefab | Size (wall centre lines) | Used by |
|--------|--------------------------|---------|
| `WorkerHut_Home` | 3 × 3 m, eave 2.5 m, eaves 0.3 m | The player's hut `worker-player`, Hobb's `worker-mate` and `worker-hut` in `StartingTown` |
| `WorkerHut_Home_B`, `WorkerHut_Home_C` | Same, other seeds; `_C` has the doorway mirrored | Plots with room for the full eaves (none in the camp today) |
| `WorkerHut_Tight`, `_Tight_B`, `_Tight_C`, `_Tight_D` | Same walls, eaves 0.09 m; `_B` and `_D` have the doorway mirrored | The 40 swapped huts of `PlayersZone` and `MafiaZone` |
| `WorkerHut_Long` | 3 × 4 m, eave 2.5 m | Not placed yet; for plots with a free metre |

The list is `WorkerHutKitBuilder.Variants`, in step with `VARIANTS` in `build_worker_hut.py`: a new shell goes in
both (and in `WorkerHutRolloutRules`' pools if the rollout should place it; the rollout refuses to run when a name
there is not in `Variants`), plus an `.fbx.meta` copied from `WorkerHut_Home.fbx.meta` with a new GUID (it carries the material remap).
A seed changes the roof gaps, the board ends and the grain shifts. The tight roof reaches 1.69 m from the middle,
no further than the graybox roof did (1.70 m); the normal roof reaches 1.9 m.

All are **generated**: `Tools/World/Build Worker Hut Kit Prefabs` (`Editor/World/WorkerHutKitBuilder`; the name is
from the kit days) rebuilds them from scratch, so edit the builder or the model script, not the prefab. Scene
instances follow the prefab. The builder takes the model (`Art/Environment/Camp/models/worker hut/WorkerHut_*.fbx`)
apart into the prefab's own `Walls` and `Roof/RoofBoards`, so after re-exporting the model, run the builder again.
A build with a missing model, a model without `Walls` and `Roof`, or a material other than the two shell
materials stops before saving and leaves the prefab as it was.

## Shell Contract

- Local **+Z is the front doorway**, −Z the rear opening; the origin is the floor centre. There is no floor: the
  hut stands on the terrain, so check the ground under a new plot (a bank inside the footprint shows as a mound).
- The doorway is **off-centre**: local x 0 to 1.05, 2.14 m high, with a 0.1 m sill, visual only (no collider). The
  camp's doors are kept above 2.1 m for the player's capsule. A mirrored shell has it at x −1.05 to 0 and nothing
  else changes (the rear opening and shutter stay where they are): read `Variant.DoorMinX` / `DoorMaxX`
  (`WorkerHutKitBuilder.TryGetVariant(instance, out variant)`), not the `DOOR_*` constants, for any hut but the home two.
- The rear opening is local x −0.45 to 0.4, 1.05 to 1.95 m up, with a plank shutter open against the wall outside.
- Wall build-up, from the middle outward: boards from 1.28 to 1.315 m, posts and plates to 1.455 m. The wall
  **colliders** (`Colliders`, one box per wall span and per roof slope) run from 1.35 to 1.55 m, so a prop hung on
  the inside of a wall goes at about 1.25 to 1.28 m and something leaning on the outside meets the boards at 1.32 m.
  The player's capsule stops 7 cm short of touching the boards from inside, as with the kit shell.
- The side walls' top plates stick out 8 cm past both gables at eave height (local (±1.385, 2.43, ±1.58) on the home
  hut): a place to tie a line.
- `Roof` carries a `NavMeshModifier` (Not Walkable), like the graybox huts. Scene bakes collect render meshes, so
  rebake after placing or moving a hut.
- Each roof slope holds two `WeightPole`s (Camp `roof pole` model, scaled to the roof length) with a `PoleLashing` at
  each end round the barge boards; no stones. Visual only (no colliders). They sit on the model's roof boards
  (`BOARD_TOP`): move it if the roof's height changes in the model script.
- Every wood renderer uses `M_Camp_HutShell`, `M_Camp_HutShellDark` or `M_Camp_PoleWood`, all on one texture set
  (`Art/Environment/Camp/CLAUDE.md` → One wood, by construction). The model's materials come from its importer remap.
- Inside room: about 2.56 × 2.56 m between wall faces (Home). A `Bed_WorkerPallet` along the −X wall leaves a walking
  strip of about 1.7 m; keep solid props out of it.

## Restricted Area (owned huts)

Every shell has a child `RestrictedArea`: a `Game.World.RestrictedArea` with one trigger `BoxCollider` on Ignore
Raycast covering the room between the boards' inner faces, floor to eave (Home 2.56 × 2.5 × 2.56 m). The doorway is outside it.
It ships **inactive with no owners**, so unowned huts (the player's) never run it and log nothing. To make a hut
owned, on the scene instance: switch `RestrictedArea` on and list the owner(s) (`_ownerEntities` / `_ownerFactions`),
plus hours and warnings if needed. Hobb's `worker-mate` is the first (owner `NPC_TentMate`, all day, one warning).

A rebuild keeps those overrides: `SaveAsPrefabAsset` over the existing prefab keeps a child's fileID when its name
and path are unique and unchanged. Renaming or moving `RestrictedArea` in the builder drops
every instance's owners. Siblings that share a name (`Side`, `Front`, `Rear`, `Slope`, the poles and lashings) get
new fileIDs on every rebuild (a large but harmless prefab diff), so never put a scene override on one of them.
Set the active flag through `SerializedObject` (`m_IsActive`) or the Inspector: a bare
`SetActive` from a script is not recorded as an override and a rebuild reverts it.

## Rollout (graybox → shell)

`Tools/World/Replace Graybox Worker Huts` (`Editor/World/WorkerHutRollout`, rules in `WorkerHutRolloutRules`, tested
in `Tests/EditMode/WorkerHutRolloutRulesTests`) swaps every graybox 3 × 3 m hut of `PlayersZone` and `MafiaZone` in
place: same name, sibling order, x, z and yaw, seated on the ground under its centre; the graybox is deleted (one
undo step). Huts that are already shells are left alone, so it can be rerun after a plot is fixed.

- **Tight roof** when the normal roof would cross a neighbour's roof or another building. Every plot of the camp is
  that packed, so all 40 swapped huts are tight (the five with no hut in reach meet a rock, `MafiaQuarters` or a
  graybox tarp bay instead). 33 pairs of shell roofs still cross, as the graybox roofs did, and 21 more pairs cross
  between a shell and a hut that is still graybox; in about half of the shell pairs the roof reaches the
  neighbour's wall line. The run lists every pair (`ROOFS CROSS`) and never moves a hut.
- **Mirrored doorway** when something solid stands within about a metre of the default doorway half, or the ground
  there is more than 0.3 m above the floor line; both halves blocked leaves the hut graybox. Characters, loose
  items and `HomeYardDressing` (rebuilt round the huts) don't count.
- **Left graybox** when the ground rises more than 0.15 m above the floor line inside the room or drops more than
  0.3 m below it under the walls (the shell has no floor and its posts reach 0.3 m down), and when the hut is not
  the plain 3 × 3 m box (walls on ±1.44 m, door on +Z, unscaled, upright, no component on its root).
- **Kept:** a child that is not a graybox part (the market hut's `TradingCounter` in `MafiaZone/worker-hut-30`) is
  moved into the shell at its old world place and listed as `KEPT`. A doorway half with such a child in the first
  metre inside it counts as blocked, so the counter's hut gets the doorway on the other half. Today: `worker-hut-02 (9)` (0.34 m drop) and the three narrower
  `worker-hut-28*` of the Mafia zone (walls on ±1.29 m).
- **Long hut** only where its extra metre, plus a 0.6 m walking gap, is free of roofs, colliders and escort lanes.
- Among the shells a plot may take, the one the fewest neighbours within 4.5 m already show wins; the order they are
  tried in comes from the hut's name (FNV hash), so the same camp gives the same result.
- Graybox tarps the dresser replaces (`WorkerHutRollout.ReplacedTarps`) don't count as blocking a doorway.

## Dressing

`Tools/World/Dress Home Yard` (`Editor/World/HomeYardDresser`) builds `PlayersZone/HomeYardDressing` in
`StartingTown`: hut interiors, door flaps, the fire, the repair trunk, the card game and the night lights.
Rerunning it asks first, then rebuilds that group and discards hand edits inside it (no undo). It switches the
graybox `Campfire`, `RepairTrunk` and `CardGame` groups off instead of deleting them. Its prop positions are written
for `WorkerHut_Home`, and it refuses other huts for the player's and Hobb's. The east yard tarps and the lean-to
hang from whatever shell stands there (`EaveTie`: a point under the eave, 2.30 m up on a normal roof, 2.41 m on a
tight one), so run the rollout first; an east yard tarp whose hut is still graybox is skipped with an error and its graybox tarp
stays on (the lean-to instead keeps its old tie line, with a warning). Props not marked solid are kept out of the NavMesh bake.
