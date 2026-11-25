# CLAUDE.md — Assets/_Game/Prefabs/World/Huts

> Art-pass worker hut shells for the Area 1 camp: a board-built Blender model with colliders, weight poles and a
> restricted area added by the builder. Model and wood rules live in `Art/Environment/Camp/CLAUDE.md` (→ Hut shell);
> light values are the constants at the top of `Editor/World/HomeYardDresser`.

---

## Prefabs

| Prefab | Size (wall centre lines) | Used by |
|--------|--------------------------|---------|
| `WorkerHut_Home` | 3 × 3 m, eave 2.5 m | The player's hut `worker-player` and Hobb's `worker-mate` in `StartingTown` |
| `WorkerHut_Long` | 3 × 4 m, eave 2.5 m | Not placed yet; for plots with room behind them |

Both are **generated**: `Tools/World/Build Worker Hut Kit Prefabs` (`Editor/World/WorkerHutKitBuilder`; the name is
from the kit days) rebuilds them from scratch, so edit the builder or the model script, not the prefab. Scene
instances follow the prefab. The builder takes the model (`Art/Environment/Camp/models/worker hut/WorkerHut_*.fbx`)
apart into the prefab's own `Walls` and `Roof/RoofBoards`, so after re-exporting the model, run the builder again.
A build with a missing model, a model without `Walls` and `Roof`, or a material other than the two shell
materials stops before saving and leaves the prefab as it was.

## Shell Contract

- Local **+Z is the front doorway**, −Z the rear opening; the origin is the floor centre. There is no floor: the
  hut stands on the terrain, so check the ground under a new plot (a bank inside the footprint shows as a mound).
- The doorway is **off-centre**: local x 0 to 1.05, 2.14 m high, with a 0.1 m sill, visual only (no collider). The
  camp's doors are kept above 2.1 m for the player's capsule.
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

## Dressing

`Tools/World/Dress Home Yard` (`Editor/World/HomeYardDresser`) builds `PlayersZone/HomeYardDressing` in
`StartingTown`: hut interiors, door flaps, the fire, the repair trunk, the card game and the night lights.
Rerunning it asks first, then rebuilds that group and discards hand edits inside it (no undo). It switches the
graybox `Campfire`, `RepairTrunk` and `CardGame` groups off instead of deleting them. Its prop positions are written
for `WorkerHut_Home`, and it refuses other huts. Props not marked solid are kept out of the NavMesh bake.
