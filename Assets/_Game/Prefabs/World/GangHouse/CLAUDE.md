# CLAUDE.md — Assets/_Game/Prefabs/World/GangHouse

> The Area 1 gang house (`StartingTown` → `Area1_Blockout/GangHouse`): the lodge on its rubble base, the camp ramp,
> the balcony, the deck, the walkway, the pier, the net frame, the basement behind a hidden door and the smuggling
> tunnel to the warehouse cellar, as one script-built Blender model wrapped in a generated prefab, plus a command
> that readies its site. The wilderness gate beside it is a second generated prefab
> (`Prefabs/World/DockGate/WildernessGate.prefab`). Look and build brief:
> `Art/Environment/Camp/GANG_HOUSE_CONCEPTS.md`; wood rules: `Art/Environment/Camp/CLAUDE.md`.
> Spec: `spec-area-1-gang-river-house`.

---

## Pipeline

`GangHouse.prefab` and `WildernessGate.prefab` are **generated**: never edit them or their scene instances by hand.

| Step | What | Writes |
|------|------|--------|
| 1 | `Art/Environment/Camp/models/gang house/Source~/build_gang_house.py` in Blender (`blender -b -P`, or in a session `runpy.run_path(path, run_name="__main__")`) | `GangHouse.fbx`, `GangHouse.json` (parts file), `Source~/GangHouse.blend` |
| 2 | `Tools/World/Build Gang House Prefab` (`Editor/World/GangHouseBuilder`) | `GangHouse.prefab` |
| 3 | `models/wilderness gate/Source~/build_wilderness_gate.py`, then `Tools/World/Build Wilderness Gate Prefab` (`DockGateBuilder`) | `WildernessGate.fbx`, `.json`, `WildernessGate.prefab` |
| 4 | `Tools/World/Place Area 1 Gang House`, with `StartingTown` open (once; again if the deck's, the basement's or the tunnel's plan changes) | Live terrain: the bank under the deck lowered, the hole over the basement; the old opening's barricades removed; `TollGates/TollGate_Wilderness` moved into the gateway |
| 5 | `Tools/World/Capture Area 1 Terrain Sculpt` if step 4 lowered anything, then `Build Area 1 Camp Edge` (the `gang-fence` line) and `Build Area 1 Sites` (both prefabs, the goods, the boat), rebake, save | The scene roots, `terrain-sculpt.bin`, the palisade's meshes, the NavMesh |

- The house's script loads the warehouse's (`models/warehouse/Source~/build_warehouse.py`) as a library: mesh
  helpers, materials, export. The gate's script loads the **dock gate's** and builds its `gate` and `leaf` parts
  with another seed: a change to the dock gate's sizes changes both gates.
- **Sizes are written once**, in the scripts. The builder reads colliders, walk surfaces, Not Walkable volumes,
  lanterns, lights and the hidden door from `GangHouse.json` (`version` 2); Sites reads the house's pose
  (`centre`, `yaw`, `floorWorldY`), the kit goods (`goods`) and the boat's place (`anchors.boat`); the place
  command reads the ground rectangles (`anchors.groundCut`, `groundCutInside`), the hole (`basement.hole`) and the
  tunnel's line (`tunnel.nodes`). **Written twice:** the house's pose (`CENTRE`, `YAW` in the script and the
  layout's `gang-house`: Sites builds the graybox house when they differ by more than 5 cm or 0.5°); the gate's
  (`CENTRE`, `YAW` and the layout's `wilderness-gate`, which is what places it); the palisade's line (the layout's
  `gang-fence`, laid 0.9 m off the house's west gable by hand); the tunnel's first point (`TUNNEL_PATH[0]` here and
  `SIDE_TUNNEL` in the warehouse's script: a test and the place command compare them); Brann's and the chest's
  world positions (the tests).
- **No ground capture.** The base, the piles and the ramp's feet are modelled deeper than the ground. The heights
  the script assumes (`CAMP_GROUND`, `WALKWAY_HEAD_Y`, `WATER`) were measured on the site: after a sculpt there,
  check the ramp's foot and the walkway's head.
- Materials: the builder copies the warehouse importer's remap (`Wh_*` → `M_Camp_*`) onto `GangHouse.fbx`, and the
  dock gate's onto `WildernessGate.fbx` (round logs on `M_Camp_PalisadeLog`), on every build.
- A missing model, a model object the parts file names, a malformed parts file, a non-`M_Camp_` material or a
  missing door prefab stops a build before saving. Without the house's prefab Sites logs an error and builds the
  graybox house and dock; without the gate's it builds no gate.

## Frame and contract

Local frame of the house: x along the house toward its **west** end (the chimney), y up, z toward the **camp**
(−z is the lake); origin on the main floor's top at the house's centre (world (43.5, 2.15, 53.5), turned 205°).

- **Root children:** `Model` (`Frame`, `Walls`, `Floor`, `Roof`, `Doors`, `Hearth`, `Base`, `Iron`, `Furniture`,
  `Shelters`, `Decks`, `Piles`, `Nets`, `Basement`, `BasementWalls`, `Tunnel`, `TunnelTimber`), the walk boxes
  `Floor`, `Balcony`, `Deck`, `Walkway`, `Ramp`, `Pier`, `Basement`, `Basement_Lake`, `BasementSteps`,
  `BasementLanding`, `Tunnel_0`…`Tunnel_5`, `Colliders` (`Walls`, `Roof`, `Doors`, `Base`, `Furniture`,
  `Structure`, `Rails`, `Tunnel`), `NavMesh` (the net frame, `TunnelStrip`), `HiddenDoor`, `DoorLantern`,
  `DeckLantern`, `HearthFire`, `BasementLantern`, `TunnelLantern_West` / `_Middle` / `_East`. Sites adds `Goods`.
- **Room** 9 × 6 m, plates 2.6 m above the floor, ridge at world y 6.8, chimney top 7.45 (limit 10: the
  tents-to-ruins line). Camp door local x −2.55..−1.45 in the +z wall, balcony door x 0.55..1.65 in the −z wall,
  both 2.1 m high with fixed leaves standing open inward. **No hatch**: nothing joins the room and the basement.
- **Kept clear for `HiddenStashQuest`** (hand-owned, not moved): Brann's spot at local (−1.3, 0, 0.2) and his
  chest at (−2.1, 0.3, −0.4), in the room's east half. The table and benches are west of the middle, the bunks
  along the east gable.
- **Deck** local x −5.8..4.8, z −6.5..−3.05 at world y −0.5; **pier** x 0..2.4 out to z −15, same height, with
  mooring stubs on its east side; **balcony** z −4.5..−3 at the main floor, railed, reached only through the
  balcony door; **walkway** along the east end from the ground at z 0.6 down to the deck (10.8°), a pole rail on
  its outer side. The ground south of the walkway's head is the path: nothing is built on it.
- **Net frame** local x 0.9..3.3 at z −4.0, 0.95 m in front of the hidden door: solid, Not Walkable. The player
  slips behind it from either end.
- **NavMesh:** roofs, furniture, hearth, door leaves, base, piles and rails, nets, basement walls and the tunnel's
  timber carry a Not Walkable `NavMeshModifier`; `Frame`, `Walls`, `Iron`, `Floor`, `Decks`, `Basement` and
  `Tunnel` carry none. Goods and the hidden door are left out of the bake.
- **Night lights** (`ClockNightLight`, dark by day): a kit lantern on a bracket left of the camp door, one on the
  balcony's second post over the deck, and `HearthFire`, a shadow-casting point light. Values: the constants at
  the top of `GangHouseBuilder`.
- **Wilderness gate:** the dock gate's gateway (2.5 m clear, 3.2 m under the lintel, two leaves standing open) at
  the layout's `wilderness-gate` (31.95, 52), turned 90° so its leaves stand on the camp side. No lean-to, no fire.
- Tests: `Tests/EditMode/GangHousePrefabTests` (doorways, walk boxes, deck above the water, Brann and his chest
  clear, heights, night and always-on lights, bake modifiers, the hidden door, the tunnel clear from end to end
  and meeting the warehouse's opening, the hole's cells, malformed parts refused, the gateway's opening).

## Basement

A rubble room under the house's lake side and middle: inside faces local x −3..3, z −2.4..1.5 (6 × 3.9 m), floor
at world y −0.5 (the deck's), 2.35 m to the joists.

- **The terrain has a hole over it.** `basement.hole` is a rectangle of the prefab's frame, which stands turned:
  every hole cell whose four corners lie inside it is cut (`GangHouseBuilder.HoleCells`; 35 cells today). The
  place command cuts it in the scene; the terrain tool cuts it again on every run (`CutBasementHole`, after the
  warehouse cellar's). A hole is not a height: the sculpt capture does not hold it.
- **The camp wall and both end walls are 1.5 m thick** (the lake wall 0.65 m). A hole is cut in whole cells, so up
  to 1.3 m of ground is left inside the rectangle's edge: the thick walls hide it. Along the lake wall the ground
  is lowered instead (`anchors.groundCutInside`, inside the footprint only, no bank round it). Before growing the
  room, run `GangHousePrefabTests.BasementHole_LiesInsideTheFootprint_AndOverTheRoom`.
- **Hidden door:** `HiddenDoor`, a nested `Prefabs/World/Doors/Poor small door` in the lake wall at local
  x 1.45..2.59, on the deck, behind the nets (hinge on the east jamb, opens into the room, unlocked, stretched
  1.06 in height, save id `area1-gang-house-hidden-door`). Locking it is a later spec.
- **Stair well** at the west end (local x 0.45..3, z −0.27..1.5): a landing at the tunnel's level and five plank
  treads up to the room's floor, under one smooth ramp collider (`BasementSteps`, 28°).
- A beam on two posts under the joists, a slatted panel leaning beside the tunnel's mouth, boards closing the
  joists from below over the whole footprint (the floor's boards have no underside).
- **`BasementLantern`** on the east post always burns, with shadows (no clock: there is no daylight down there).
- **Stores** (`Goods`): three casks against the lake wall, three crates and three sacks along the east wall.
- No restricted area yet (later spec).

## Tunnel

A drift shored with pole frames, split planks and packed earth, about 23 m: from the outer face of the south wall
of the warehouse cellar's side room, east under the terrace along world z 51.6, north under the wilderness
gateway itself (x 32.4), east again along z 54.2 into the house's west wall. Its line and depths are `TUNNEL_PATH`
(world coordinates) in the script; 1.3 m between the lagging, 2.1 m under the caps.

- **Depth:** its floor is at world y 0.1 at the side room, falls to −1.55 by x 30 and runs at **−1.7** from the
  gateway to the house (the water is at −2; no swimming starts there). It has to: the gate's west post is buried
  to y 1.04 at x 30.4, and the gang fence's logs to y 0.65 where the tunnel passes under them (x 37.5..38.5).
- **The gang fence is shallow for that reason:** `Area1BlockoutBuilder.LineBury` gives `gang-fence` panels 0.4 m
  under their lowest ground instead of 1.5 m. Lowering the ground under that line (a wider ground cut did, once)
  drops its panels into the tunnel: after any terrain change near local x 4.5..6, z −1..3, run Camp Edge and
  `GangHousePrefabTests`, and sweep the tunnel in the scene.
- **Route chosen against the rocks:** `SM_Rock_06 (17)`, `(18)`, `(19)` are buried from x 24 to 32, z 52 to 57,
  and the clump at x 34..39, z 46..53 goes deeper than the tunnel. Before moving the line, ray-test it against
  every collider from y −2.3 up.
- **Where it meets the warehouse:** the warehouse's model leaves an opening in the side room's south wall
  (`cellar.tunnel` in `Warehouse.json`: local x 6.7..7.9, 2 m high). The place command refuses when the tunnel's
  first point is more than 5 cm from that opening's middle with the warehouse where it stands.
- **Where it meets the house:** on the slant (25° off square). Its walls end on the west face; floor and roof run
  0.4 m further. The west wall's own colliders leave the way through (`Base_West_South` / `_North` / `_Header` /
  `_Foot`); the tunnel's walls (`Colliders/Tunnel`) line it.
- **No seam:** stretches that meet in line overlap by 8 cm, corners are closed by 0.2 m of packed earth behind
  the planks, and the earth layer runs behind every wall and over the roof.
- **Agents never take it:** `NavMesh/TunnelStrip`, a Not Walkable slab across its second stretch. The hidden door
  is out of the bake too, so nobody paths into the basement.
- Three lanterns always burn in it (`TunnelLantern_*`), with shadows.

## What the place command does

`GangHouseBuilder.Place` edits hand-owned objects by design (it uses `GameObject.Find`); it never saves or bakes.

- **Ground:** lowers the bank inside `anchors.groundCut` (under the deck) to world y −0.9, under a bank that rises
  0.6 m over 1.2 m, and the ground inside `anchors.groundCutInside` (the basement's lake side) with no bank. It
  never fills; a second run changes nothing.
- **Hole:** opens the basement's cells.
- **Removes** every scene root named `SM_Barricade_*` standing in the old opening (world x 27..39, z 49..58).
- **Moves** `TollGates/TollGate_Wilderness` so that its zone box is centred on the gateway.
- **Refuses** (nothing changed) without the terrain, the toll gate, both parts files, or when the tunnel does not
  meet the warehouse's opening.

## Known limits

- `hand-edits.json` pins the old graybox (`GangHouse/BackDeck`, `Post_*`, `Skirt_*`, `Ramp`): each Sites run warns
  that they match nothing. It also pins `Barricades/NotWalkable_0` and `_1` to where two of the removed barricades
  stood: one of them now lies across the west half of the gateway, and the two barricades left at (70, 18) have no
  volume of their own. Untick all of them in the Hand Edits window, rerun Sites and rebake.
- The palisade's panels follow the terrain: between the gate and the house's corner the `gang-fence` logs start
  inside two boulders (`SM_Rock_07 (7)`, `SM_Rock_06 (25)`).
- The palisade ends in the lake about 1 m deep: the player can swim round it.
- The camp and balcony doors are fixed open leaves; locks, keys, loot and restricted areas are later specs.
- The balcony's lean-to roof is rough boards on pole rafters (the artwork drew shingles).
- The nets are two layers of cord: the hidden door shows through them from close by.
- 19 kit props (`Goods`) are 24,280 triangles at LOD 0 and the six kit lanterns about 12,000: with them the whole
  site is about 94,000 triangles, over the 90,000 planned.
- A camera render from inside the tunnel crashed the editor once in Play (cause unknown); edit-mode captures of
  the same views work.
