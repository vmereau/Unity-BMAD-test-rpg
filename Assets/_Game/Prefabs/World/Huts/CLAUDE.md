# CLAUDE.md — Assets/_Game/Prefabs/World/Huts

> Art-pass worker hut shells for the Area 1 camp, assembled from Hivemind kit pieces.
> Kit rules (pieces, scale) live in `Art/Environment/CLAUDE.md` → *Camp Kit*; light values are the constants at the
> top of `Editor/World/HomeYardDresser`.

---

## Prefabs

| Prefab | Size (wall centre lines) | Used by |
|--------|--------------------------|---------|
| `WorkerHut_Home` | 3 × 3 m, eave 2.5 m | The player's hut `worker-player` and Hobb's `worker-mate` in `StartingTown` |
| `WorkerHut_Long` | 3 × 4 m, eave 2.5 m | Not placed yet; for plots with room behind them |

Both are **generated**: `Tools/World/Build Worker Hut Kit Prefabs` (`Editor/World/WorkerHutKitBuilder`) rebuilds
them from scratch, so edit the builder, not the prefab. Scene instances follow the prefab. A build with a missing
kit piece stops before saving and leaves the prefab as it was.

## Shell Contract

- Local **+Z is the front doorway**, −Z the rear opening; the origin is the floor centre. There is no floor: the
  hut stands on the terrain, so check the ground under a new plot (a bank inside the footprint shows as a mound).
- The doorway is **off-centre**: local x 0 to 1.05, 2.14 m high. The door module's frame has a 0.1 m sill, visual
  only (no collider). The module is stretched 1.1× in height: its own opening is 1.95 m, and the camp's doors are kept
  above 2.1 m for the player's capsule.
- The rear opening is local x −0.45 to 0.4, 1.05 to 1.95 m up.
- Walls are kit wall modules (`SM_wall3mB_a1` / `SM_wall4mB_a1` sides, `SM_wallDoor2m_a1` + `SM_barnWallB_1m_a1`
  front, `SM_wallWindow3m_a1` rear). The modules start at their pivot and run along local +X.
- The kit pieces' own colliders are **disabled**; `Colliders` holds one box per wall span and per roof slope.
- `Roof` carries a `NavMeshModifier` (Not Walkable), like the graybox huts. Scene bakes collect render meshes, so
  rebake after placing or moving a hut.
- Inside room: about 2.56 × 2.56 m between wall faces (Home). A `Bed_WorkerPallet` along the −X wall leaves a walking
  strip of about 1.7 m; keep solid props out of it.

## Dressing

`Tools/World/Dress Home Yard` (`Editor/World/HomeYardDresser`) builds `PlayersZone/HomeYardDressing` in
`StartingTown`: hut interiors, door flaps, the fire, the repair trunk, the card game and the night lights.
Rerunning it asks first, then rebuilds that group and discards hand edits inside it (no undo). It switches the
graybox `Campfire`, `RepairTrunk` and `CardGame` groups off instead of deleting them. Its prop positions are written
for `WorkerHut_Home`, and it refuses other huts. Props not marked solid are kept out of the NavMesh bake.
