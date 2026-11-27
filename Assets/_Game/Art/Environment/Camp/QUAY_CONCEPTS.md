# Quay Concepts — reference captures, prompts and build brief

> Read when generating or judging a quay artwork (D1–D6), or before building the quay, the tally room, the cellar
> side room or the dock gate. Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this
> folder); the hall and the cellar, whose wood and rubble this site shares: `WAREHOUSE_CONCEPTS.md`,
> `WAREHOUSE_CELLAR_CONCEPTS.md`; the palisade the gate belongs to: `PALISADE_CONCEPTS.md`.
> Spec: `spec-area-1-warehouse-quay`.

The quay is the river side of the warehouse (`StartingTown`, around (16, 0, 62)). Before this pack the shelf in front
of the cellar door was bare mud falling into the water, the bank between the hall's east gable and the dock fence was
empty grass with two rocks, and `Area1_Blockout/DockGate` with its guard lean-to was a graybox frame (the user's
sketch: `Artworks/Starting area/Camp/Warehouse/refs/Warehouse dock + dock entrance.PNG`). This pack fixes the look
before the build: six captures with temporary massing shapes, one image-to-image prompt per capture, and the facts
the build must keep.

**Built on 2026-11-27** (`spec-area-1-warehouse-quay`): the quay, the barge, the tally room, the retaining wall and
the cellar's side room are part of the warehouse's script-built model and generated prefab (pipeline and contract:
`Prefabs/World/Warehouse/CLAUDE.md` → Quay); the gate, the guard lean-to and the fire basket are the generated
`Prefabs/World/DockGate/DockGate.prefab` (that folder's `CLAUDE.md`). The *Build brief* below describes the massing
the captures were taken from; what changed is listed under *As built*. Captures of the build:
`Artworks/Starting area/Camp/Quay/built/`.

---

## The place

- **Who built it.** The Company, after the hall: once boats came faster than the mud shelf could take them, a plank
  quay was driven out over the water on tarred piles. It is built like the warehouse (squared timber, tight planks),
  but it stands in the wet and is knocked about by cargo every day.
- **What it is for.** Everything the camp eats or ships crosses these boards and is counted: casks, crates and sacks
  come off a river barge, wait on the quay, and go into the cellar by its door or up into the hall by the hoist.
- **Quay.** A plank deck 17.7 × 4.5 m along the shelf, level with the mud so carts roll onto it, a kerb beam on the
  river edge, four mooring posts, one ladder down to the water. Goods stand along the river edge; the side toward
  the hall stays a clear cart lane from the river ramp's foot to the dock gate.
- **Barge.** One flat cargo barge, about 7 × 2.2 m, tied alongside and half unloaded, a gangplank up to the deck.
- **Tally room.** A lean-to in the corner where the hall's east gable, the north palisade and the dock spur meet:
  the palisade logs are two of its walls, the gable the third, one plank wall with a shuttered hatch looks down on
  the quay, a single-pitch shingle roof sits under the palisade tops. The clerk counts from here. It is entered from
  the hall, through a door in the gable: a desk, a stool, a shelf of ledgers, a strongbox, a candle. A rock of the
  bank comes up through the floor in one corner.
- **Retaining wall.** Under the room, the bank is held by a stepped dry-stone wall of local rubble, continuing the
  cellar's front wall east to the dock fence. Never dressed masonry.
- **Cellar side room.** Behind that wall, under the tally room, a side room of the cellar (about 3 × 4 m), reached
  through the cellar's east wall. It has no view of its own: it is built in the cellar's as-built look.
- **Dock gate.** The opening in the dock spur becomes a palisade gateway: two heavy flat-topped log posts, a log
  lintel, a short row of pointed logs carried over it from the tall side to the low side, two leaves of lashed logs
  standing open. Outside it, a guard's plank lean-to with a bench, a spear rack and a brazier.
- **Off limits.** The tally room and the side room are the warehouse guard's at all hours; the quay is the
  Company's at night (21–06), like the shore today.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they decide what is modelled.

1. **Quay deck**: tight and level, or patched and uneven, with replaced boards.
2. **Goods**: how much may stand on the quay before it stops reading as a poor camp.
3. **Tally room**: the hall's own carpentry, or visibly a later, cheaper add-on; palisade logs left bare inside or boarded.
4. **Gate**: how heavy the posts and lintel, and what goes above the lintel (pointed logs, a plain beam, nothing).
5. **Night**: how many lights and where (two lantern posts, the cellar door's lantern, the guard's brazier).
6. **Retaining wall**: how tall each step, how rough, grass on its top or bare stone.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Quay/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Quay/`).
4. Generate **D3 first** and judge it before the others: it is the only view that shows the quay, the tally room,
   the retaining wall and the gate together, so it sets the look the rest must match. Then add the accepted D3 as a
   second reference image to every later view and append this sentence to the template's second line:
   `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`
5. The interior (D6) and the night view (D5) replace the template's light line with the one given under the view.
6. Fill the verdict log at the end.

The finished real objects in the captures are the hall with its landing, ramp, handrails and hoist, the cellar's
stone front with its door, lantern and casks, the log palisade, the crates and sacks under the hoist, the rocks,
the old pier and the footbridge. Everything smooth and single-coloured is a placeholder:

| Placeholder | Stands for |
|-------------|------------|
| Light orange-brown slabs, beams and posts | Carpentry: the quay's deck, kerb and bearers, ladder, lantern posts, net rack frame, gangplank, the tally room's floor, door, shutter, desk, stool and shelves, the lean-to's posts, bench and spears |
| Dark grey-brown smooth cylinders and flat leaves | Round log work: quay piles, mooring posts, the gate's posts, lintel, the logs above it and the two gate leaves |
| Mid-brown flat walls | Plank walls: the tally room's wall, the lean-to's back and side |
| Grey-brown sloping slabs | Shingle roof of the tally room, plank roof of the lean-to |
| Mid-grey stepped blocks | The dry-stone retaining wall |
| Dark brown boxes | Crates |
| Pale yellow cylinders | Casks and barrels |
| Off-white flattened balls | Sacks |
| Dull green flat sheet | A tarp over the sacks |
| Tan flat discs and sheet | Coiled hemp rope; a fishing net on its rack |
| Blue-grey long box | The cargo barge |
| Black box, black short cylinder | The strongbox; the brazier's iron basket |
| Pale parchment blocks | Ledgers |
| Small bright yellow boxes | Lights: lanterns, the candle, the brazier's fire |

---

## Template (same for all six views)

Keep every line **verbatim**; only `{VIEW}` changes.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every object exactly as they are. Keep the timber hall, its landing, ramp, handrails, posts and hoist, the dry-stone wall with its plank door under the landing, the log palisade, the rocks, the muddy ground, the river and the distant hills and trees as they are, and repaint only their surfaces; ignore the hard cast shadows of the input. Every smooth single-coloured shape is a placeholder: replace each one with the real object described below, at the same size and place. Do not add, move or remove any building, wall, gate or post, and add no object where the input shows none.
{VIEW}
Materials. The light orange-brown shapes are carpentry of weathered dark oak, the same wood as the hall: thick sawn planks laid tight with worn, damp, grey surfaces and a few paler replaced boards, squared beams and posts, pegged joints. The dark grey-brown smooth cylinders and flat leaves are round log work of the same kind as the palisade: whole debarked trunks with long drying cracks, black with tar where they stand in water, lashed with dark tarred hemp rope; a flat leaf is a gate leaf of upright logs lashed to two cross poles. The mid-brown flat walls are walls of rough weathered planks. The grey-brown sloping slabs are roofs of split wooden shingles, grey and weathered. The mid-grey stepped blocks are dry-stone walls of rough grey local rubble: uneven stones of many sizes packed without mortar, damp and dark at the foot, moss in the joints, never cut blocks. The dark brown boxes are rough plank crates. The pale yellow cylinders are old oak casks with dull iron hoops. The off-white flattened shapes are sacks of coarse undyed linen. A dull green flat sheet is a stained, patched canvas tarp. The tan discs are coils of thick hemp rope and a tan sheet is a mended fishing net hung to dry. The blue-grey long box is a flat-bottomed river barge of tarred dark planks, low in the water. A black box is a small iron-bound chest; a black short cylinder is an iron fire basket on three legs. Pale parchment blocks are worn leather-bound ledgers. A small bright yellow box is a plain iron lantern with horn panes, unlit.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: weathered wood browns and greys, stone grays, earth browns, mossy greens; no saturated colors, no glow.
No cut stone blocks, no brick, no mortar, no plaster, no arches, no towers, no battlements, no cranes, no sails, no masts, no cobblestones, no stairs, no fresh or new-looking timber, no fire, no smoke, no fog, no light rays, no skulls, no chains, no banners, no carts, no animals, no new people, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### D3 — From the water (the look-setting view)

Input: `refs/D3 from the water ref.png`. Save as `D3 from the water.png`.

```
Raised view from over the river, looking straight at the river side of the warehouse. The timber hall with its shingled roof, the landing on posts, the long ramp with handrails coming down to the right, the dry-stone wall with a plank door under the landing and the two casks beside it stay exactly as they are. In front of them, the long light orange-brown slab is a plank quay on tarred log piles, level with the muddy shelf, with a kerb beam along its river edge, four tall mooring posts with rope coils and a ladder down to the water. On the quay, from left to right: a stack of crates, a lantern post, two groups of casks, sacks under a tarp, a second stack of crates, a second lantern post, one cask lying on its side and a net drying on a rack. At the quay, in the water, the cargo barge lies alongside with casks, crates and a sack in it and a gangplank up to the deck. Left of the hall, against its end wall and in the corner of the palisade, the mid-brown wall with a small shuttered hatch and the grey-brown sloping roof are a small lean-to room; under it the mid-grey stepped blocks are a dry-stone retaining wall holding the bank. At the far left the palisade comes down into the water; the dark opening in it at the mud's level is a gateway.
```

### D1 — Along the quay

Input: `refs/D1 along the quay ref.png`. Save as `D1 along the quay.png`.

```
Eye-level view from the muddy shelf at the west end of the quay, looking east along it. On the right, the posts of the warehouse's landing with the dry-stone wall behind them and the bank under the ramp stay exactly as they are. The light orange-brown slab filling the lower left is the plank deck of the quay, its near side level with the mud. Along its far, river edge: in the left foreground one cask lying on its side and a lantern post; then a stack of crates, sacks under a tarp, a group of standing casks and a second lantern post with more crates behind it. At the end of the quay the palisade crosses the picture and runs into the river; the opening in it on the right is a gateway of heavy logs. The river, the far bank, the trees and the small footbridge in the distance stay as they are.
```

### D2 — Dock gate from outside

Input: `refs/D2 dock gate from outside ref.png`. Save as `D2 dock gate from outside.png`.

```
Eye-level view from the muddy bank path outside the camp, looking west at the dock gate. The log palisade fills the picture, very tall on the left where it stands on the bank and lower on the right where it runs down to the river; it and the big dark boulders on the left stay exactly as they are. In the middle is the gateway: two heavy round log posts with flat sawn tops, a round log lintel across them above head height, and above the lintel a short row of upright logs with pointed tops, stepping down from the tall side to the low side. The two flat dark leaves standing open toward the viewer are gate leaves of upright logs lashed to cross poles. Through the gateway: wet mud, a dry-stone wall and a few crates. In the left foreground, in front of the boulders, a low guard shelter: a plank back and side wall, two posts, a sloping plank roof, a plank bench under it and three spears leaning in the corner. At the lower left an iron fire basket, unlit. The hoist arm above the palisade stays as it is.
```

### D4 — Overview

Input: `refs/D4 overview ref.png`. Save as `D4 overview.png`.

```
High view from the north-east over the river corner of the camp. The warehouse with its shingled roof, its landing and ramp, the palisade, the boulders, the camp's huts behind and the old dark pier in the water on the right stay exactly as they are. Along the shore in front of the warehouse, the light orange-brown slab is the plank quay on log piles, with crates, casks, sacks under a tarp, two lantern posts and a net rack on it, and the cargo barge tied alongside with a gangplank. Against the warehouse's near end wall, in the corner of the palisade, a small lean-to room with a sloping shingle roof and a shuttered hatch; under it a stepped dry-stone retaining wall. In the palisade below it, the gateway of heavy logs with its two leaves standing open toward the viewer, and outside it, against the boulders, the guard's plank lean-to with a bench and an iron fire basket on the mud.
```

### D5 — Along the quay at night

Input: `refs/D5 along the quay night ref.png`. Save as `D5 along the quay night.png`.

```
Night view from the muddy shelf at the west end of the quay, looking east along it. On the right, the posts of the warehouse's landing with the dry-stone wall behind them and the bank under the ramp stay exactly as they are. The slab filling the lower left is the plank deck of the quay. Along its river edge: one cask lying on its side, a lantern post with a lit lantern, a stack of crates, sacks under a tarp, a group of standing casks and a second lit lantern post with crates behind it. At the end of the quay the palisade runs into the river, with the gateway of heavy logs on the right. A third small lantern burns on the stone wall under the landing, far on the right.
```

Light line for this view: `Night, overcast, no moon: each lantern throws a small warm pool on the boards and the goods around it, the deck between them stays dim, and the river, the far bank and the sky fall off into cold blue-black. Muted earthy palette, the lanterns' fire-orange the only warm accent; no glow, no bloom.` For this view also replace `unlit` with `lit` at the end of the materials paragraph and remove `no fire,` from the template's last line.

### D6 — Tally room interior

Input: `refs/D6 tally room interior ref.png`. Save as `D6 tally room interior.png`.

```
Inside a small lean-to room, about three metres square, seen from one upper corner. Two of its walls are the bare inside of the camp palisade: upright round logs standing tight side by side, with rope lashings; they stay exactly as they are. Where they meet, in the far corner, a grey rock of the bank comes up through the floor and stays as it is. The floor is wide worn planks. Against the left log wall a plain plank desk with an open ledger and an unlit candle stub on it, a three-legged stool in front of it, and above it two plank shelves pegged to the logs with five ledgers standing on the lower one. Against the right log wall a small iron-bound chest on the floor. At the left edge, part of a plank wall; at the right edge, the edge of an open plank door.
```

Light line for this view: `Dim interior: cold grey daylight falls in from a small open hatch behind the viewer on the left and fades toward the corner; deep soft shadows, no other light. Muted earthy palette: weathered wood browns and greys, stone grays, parchment; no saturated colors, no glow.`

---

## Build brief (facts the build keeps)

Sizes of the massing in the captures, in world coordinates. The hall's floor is at y 4.65, the dock shelf at
y −0.17, the water at y −2.

| What | Value |
|------|-------|
| Quay deck | x 6.5..24.2, z 61.0..65.5 (17.7 × 4.5 m), top y 0.0, 0.14 m thick; sleeper beam on z 60.85..61.1 sunk to y −0.3 |
| Piles | Two rows (z 63.3 and 65.3) every 2.4 m from x 7; the riverbed under the outer row is y −1.3 (west) to −2.9 (east) |
| Kerb | 0.18 m, on z 65.32..65.5, open over x 15.3..16.3 for the ladder |
| Mooring posts | x 9.4, 13.6, 18.2, 22.6 at z 65.75, tops y 0.9 |
| Lantern posts | (11.3, 65.17) and (20.6, 65.17), 2.6 m tall, lantern at y 2.3 |
| Goods | East stack x 21.4..23.5; casks x 16.6..19.9; sacks and tarp x 13.9..15.4; west stack x 11.8..13.3; net rack x 7.2..9.1 (1.8 m tall); all north of z 63.6 |
| Cart lane | The shelf z 59.8..61 and the deck's south 2 m (z 61..63): nothing stands there |
| Barge | x 13.6..21, z 66.0..68.2, gunwale y −1.15, hold floor y −1.75; gangplank from (17.0, 0, 65.4) to (17.6, −1.13, 66.4) |
| Tally room | Floor x 21.0..24.25, z 52.4..56.0 at y 4.65; plank north wall on z 55.85..56.0, 2.5 m high; hatch x 22.1..22.9, y 5.65..6.4; roof from y 8.5 at the palisade (z 52.3) to y 7.2 at z 56.4; door from the hall z 53.5..54.5, 2.1 m high |
| Retaining wall | x 20.7..24.2: upper step z 56.0..56.6 to y 4.5, lower step z 56.6..57.2 to y 2.6 |
| Gate | Posts 0.5 m across at (24.5, 57.45) and (24.5, 60.55), tops y 5.4 and 4.7; lintel axis y 3.2 (3.2 m clear over the lane); nine logs above it, tops y 5.6 falling to 4.9; leaves 1.5 × 3.0 m, open to the east |
| Guard lean-to | x 26.5..29.6, z 56.1..57.8, back 1.9 m, front 2.3 m, open to the north; brazier at (30.2, 58.4) |

Constraints:

- **The walk from the river ramp's foot (4, 58) to the gate** runs along z 60–60.6 today. The quay adds to it; the
  lane row of the table stays empty. `DockGuard/WP_3_RampFoot` (8, −0.12, 62) lands on the deck: the deck needs a
  walk box and NavMesh, with no step over 0.3 m from the shelf (the sleeper is flush).
- **Nothing taller than 2 m west of x 9**: the hunter's fire is seen across the shelf from the broken stretch
  (`STARTING_TOWN.md` → Camp edge).
- **The quay ends at the dock spur** (x 24.2; `dock-fence-lower` runs to z 66.3, past the deck's edge at 65.5), so
  the deck is no way round the gate. It does not touch the placeholder pier (`Docks`, x −5..6).
- **`RA_Docks`** ends at z 64.6 and x 23.3 today: it grows to z 66.1 and x 24.2 to cover the deck.
- **The palisade is not changed.** `outer-fence-3/Panel_061` and `Panel_062` are the room's south wall and
  `dock-fence-upper/Panel_147` and `Panel_148` its east wall; their collider boxes (0.6 m) stand about 0.15 m
  proud of the logs' faces. The warehouse zone, the Camp Edge clipping and the junction log stay as they are.
- **The rock in the room's corner** is `SM_Rock_08 (4)` (hand-placed scene root): it rises to y 5.7 at x ≥ 23.5,
  z 52.5..53.5, a metre above the floor. Boards are cut round it; moving it is an Ask First.
- **The door from the hall** stands in the gable's north half, between the junction log (z 52) and the corner post
  (z 56). The guard's `WP_4_East` (19.5, 52.5) is 1.5 m from it. `RA_Warehouse` (x ≤ 20.7 today) grows over the room.
- **The retaining wall's face is at z 57.2** because `HoistGoods` stand at x 21..23.3, z 57.2..59.3. The bank's toe
  (y 2.0 at z 57.0, y 0.2 at z 57.5) shows 0.3 m in front of the wall's foot: the build covers it with fallen
  stones or moves the wall's foot out and the goods with it (they are placed by Sites). The hoist post's foot stands
  on the cellar's east shoulder at (20.83, 3.5, 56.5), which the wall joins.
- **Cellar side room:** under the tally room, east of the cellar's east wall (x 19.9..20.6 under the hall). The
  bank is solid (y 3.7..4.4) and two rocks are buried in it: `SM_Rock_08 (4)` (bounds x 21.8..27.2, y 3.1..5.7,
  z 50.2..55.9) and `SM_Rock_06 (17)` (x 23.8..28, y −3..5.2, z 52.2..55.9). Ray-test the room's volume against
  them before fixing its walls: its east wall likely stands near x 23.3, and its ceiling may need to stop under
  y 3.1. The hole map's cells are 0.977 m: walls at least 1.2 m thick wherever they hide the hole's edge, as in the
  cellar. `RA_WarehouseCellar` grows over the room.
- **The gate keeps its place and width** (x 24.5, z 57.5..60.5, 3 m): `dock-fence-upper` ends at z 57.8 with its top
  at y 9.8 and `dock-fence-lower` starts at z 60.2 with its top at y 4.9. The south post stands on the bank
  (y 1.2), the north one on the lane (y −0.17). `DockGate` is a Sites root built by `Area1CampInterior.BuildGate`,
  which the other gates share: only the dock gate changes.
- **The open south leaf** lies along z 57.4 from x 24.7 to 26.2, in front of the lean-to's west end (x 26.5): the
  lean-to starts east of the leaf's swing.
- **Owners:** the quay, the tally room, the wall and the side room belong with the warehouse (Sites root
  `Warehouse`, generated prefab); the gate and lean-to with `DockGate` (Sites). Goods on the quay and the barge
  are placed by Sites in the warehouse root's frame, like `HoistGoods`.
- **Budget:** 40,000 new triangles (the warehouse is 124,502 in the scene today, the gate 132).

### As built

- **Tally room:** 3.3 × 5.4 m, from the palisade to the retaining wall's face (z 52.2..57.6), not to the hall's
  north wall line: the user asked at the second pause for it to cover the stone ledge in front of the hatch. Its
  plank wall stands on the stone; north of the hall's corner it has a short west wall of vertical boards, 10 cm
  from the hoist's post. It has no wall of its own on the east (the dock spur's logs) or the south (the palisade).
  Roof 15°, from y 8.6 at the palisade to y 7.15 over the plank wall.
- **Its door** is at z 54.3..55.44, not 53.5..54.5: a gable post stands at z 54. It is the game's `Poor small door`
  (unlocked, opens into the room), like the cellar's.
- **Retaining wall:** one face at z 57.7 (not two steps, not 57.2): past the bank's toe, so the hoist goods stayed.
  No cap stones: the room's floor and sill lie on it.
- **Cellar side room:** 1.95 × 3.45 m and 2.85 m high (inside x 21.25..23.2, z 52.95..56.4), not about 3 × 4 m. The
  terrain's hole cells (0.98 m: cells x 533..535, z 566..569), the dock spur and a rock's underside (1.5 m under the
  tally floor) bound it. Reached by a passage 1.05 m wide and 2.1 m high through the cellar's east wall
  (z 54.3..55.35). No rock was moved.
- **Net rack:** on the deck's west end, as in the captures. The shore west of the quay, where D3 and D4 painted it
  and the user agreed to move it, is the dock guard's walk between `WP_0_Shore` and `WP_3_RampFoot`.
- **Goods:** the crates and the sacks stand where the user moved them by hand at the second pause (the script's
  table holds those places). The tarp lies where the sacks first stood, beside them.
- **Guard lean-to:** 2.3 m long (x 26.3..28.6, z 56.1..57.6), not 3.1 m: east of x 28.6 the bank rises, and rocks
  stand behind. The fire basket stands across the lane, at (27.4, 60.85): in front of the lean-to it stood in the
  gateway's line.
- **Gate:** posts 0.5 m, tops y 5.4 and 4.7; nine pointed logs on the lintel, tied by a pole on each face; leaves
  1.42 × 3.0 m of seven upright logs on two cross poles and a diagonal. Its logs are the palisade's wood
  (`M_Camp_PalisadeLog`).
- **Night:** two lantern posts and the cellar door's lantern on the quay side, the fire basket outside the gate.
  The tally room has no light of its own; the river door's lantern (unshadowed) reaches into it through the wall.
  The side room has a lantern that is always on, like the cellar's.
- **Restricted areas** grew by child boxes, not by larger boxes: `RA_Warehouse/TallyRoom`,
  `RA_WarehouseCellar/SideRoom` and `/Passage`, `RA_Docks/Quay` (which also covers the barge).
- **Triangles:** warehouse model 74,132 (17,046 of them new), gate 3,520; the 25 kit props on the quay and in the
  barge are 34,276 at LOD 0 (they have LODs). The 40,000 cap is passed by the props (see the spec's change log).

---

## Verdict log

D3 and D1 were generated by the user on 2026-11-27 with their own tool. D2, D4, D5 and D6 were generated the same
day through Meshy (`meshy_image_to_image`, `nano-banana-pro`, 16:9, 9 credits each, 36 in all, one pass): the
capture first and the accepted D3 second (for D5 the painted D1, as "the same view by day"), with the template's
materials paragraph cut down to what each view shows. "Kept?" is the user's call.

| View | Date | Kept? | Notes (Carry / Ignore / Differs from) |
|------|------|-------|----------------------------------------|
| D3 from the water | 2026-11-27 | Yes | **Carry:** the whole front: a tight plank deck on thick tarred piles with rope-wound mooring posts, kerb, ladder and gangplank; crates, casks and sacks under a draped tarp along the river edge; lantern posts with a bracket and a hanging lantern; the barge; the tally room as a small plank lean-to with a shingle roof and a shuttered hatch; the bank under it held by one tall face of coursed rubble, continuous with the cellar's front. **Ignore:** the rubble drawn under the whole river ramp (it stays on trestles); the net rack moved off the deck onto the shore. **Differs from the brief:** the retaining wall is one full-height face, not two steps. |
| D1 along the quay | 2026-11-27 | Yes | **Carry:** the deck's surface: long worn boards, a few laid across or patched, wet, level with the mud at its near end; the lantern post (squared post, braced arm, iron lantern); crate and cask types; the gateway seen as a plain opening with a lintel in the palisade. **Ignore:** nothing structural. |
| D2 dock gate from outside | 2026-11-27 | Yes | **Carry:** the gateway: two heavy posts, a round lintel, a tight row of logs above it with blunt tops, leaves of upright logs on two cross poles with a diagonal brace and rope lashings; the lean-to (horizontal plank back and side, two posts, plank roof, bench, spears in the corner); the fire basket on three legs; the retaining wall seen through the gate. **Ignore:** the logs above the lintel drawn level (they step down to the river side); the tall wall on the left painted as flat boards. |
| D4 overview | 2026-11-27 | Yes | **Carry:** the massing from above: the lean-to's shingle roof tucked into the palisade corner, the stone under it, the gate and lean-to below, the barge at the quay. **Ignore:** the quay drawn short and deep (it is 17.7 x 4.5 m); the second small roof beside the lean-to. **Differs from D3:** a large patched tarp on poles over the goods, not asked for (a decision for the user). |
| D5 along the quay night | 2026-11-27 | Yes | **Carry:** three small warm lights (two lantern posts, the cellar door's lantern), each reaching about 3 m, the deck between them dim, the river black. **Ignore:** boards painted over the mud lane on the right (the lane is the shelf). Night is judged on the build at 23:00 in Play. |
| D6 tally room interior | 2026-11-27 | Yes | **Carry:** bare palisade logs as two walls with a rope lashing band, wide worn floor boards cut round the rock, a plain pegged desk with an open ledger and a candle stub, a round three-legged stool, two pegged shelves with ledgers, a small iron-bound chest, a ledged plank door. **Ignore:** no ceiling is shown (the roof's rafters and shingles close it). |

**Decided by the user on 2026-11-27, from the images** ("keep all views, your answers stand"):

1. **Deck:** tight and level as D3, with D1's wear: a few paler replaced boards, one or two laid across.
2. **Goods:** the amount of D3: two crate stacks, about six casks, sacks under a draped tarp, one cask on its side.
3. **Tally room:** a cheaper add-on: plank north wall, palisade logs bare inside, shingle lean-to roof.
4. **Gate:** as D2, the logs above the lintel stepping down to the river side.
5. **Night:** two lantern posts and the cellar door's lantern on the quay side, the guard's brazier outside the gate.
6. **Retaining wall:** one full-height face of coursed rubble as D3, not two steps; the hoist goods move north or
   stones cover the bank's toe.
- D4's tarp canopy on poles is not built. The net rack stands on the shore west of the quay, not on the deck.
- Pipeline: the quay, the tally room, the retaining wall, the cellar side room and the barge go into
  `build_warehouse.py` and the generated warehouse prefab (Sites places it; `Place Warehouse Cellar` cuts the second
  hole and rebuilds the restricted boxes); the gate and the guard lean-to are a new model with its own parts file,
  placed by Sites under `DockGate` with the graybox as fallback; quay goods are placed by Sites like `HoistGoods`.

### Decided by the user at the second pause (2026-11-27)

- The user moved the quay's crates and sacks by hand; those places are now the script's.
- **The tally room is extended north** to cover the stone ledge in front of its hatch: its plank wall stands flush
  on the retaining wall's face.
