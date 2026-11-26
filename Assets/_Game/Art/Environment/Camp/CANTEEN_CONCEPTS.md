# Canteen Concepts — reference captures, prompts and build brief

> Read when generating or judging a canteen artwork (K1–K7), or before building the cookhouse.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder); the warehouse,
> whose wood and roof this building shares: `WAREHOUSE_CONCEPTS.md`. Spec: `spec-area-1-canteen-cookhouse`.

The canteen is where the camp's workers are fed, west of the punishment square (`Area1_Blockout/TentCamp/Canteen`,
around (−15, 18)). Aldric (Cook) and Tob (KitchenHand) spend their day here. Before this pack it was a graybox:
an open-fronted box, a canvas bay and a few kit tables. This pack fixes its look before the build: seven captures
with temporary massing shapes, one image-to-image prompt per capture, and the facts the build must keep.

**Built on 2026-11-26** (`spec-area-1-canteen-cookhouse`): the graybox is replaced by a script-built model in a
generated prefab, placed with its dressing by `Tools/World/Place Area 1 Canteen`. Pipeline and contract:
`Prefabs/World/Cookhouse/CLAUDE.md`. The *Build brief* below describes the massing the captures were taken from;
what changed is listed under *As built*.

---

## The place

- **Who built it.** The Company, for its own convenience: fed workers work. It is better built than the huts
  (squared posts, tight boards, a real roof) and plainer than the warehouse. Same wood family, weathered, never new.
- **The cookhouse.** One building, 9 × 5.5 m, long side to the square. The south room is the kitchen: a rubble
  hearth with a clay-and-rubble chimney on the inner wall, a work table, shelves, Tob's pallet in a corner, and a
  serving hatch with a counter and a propped shutter toward the square. The north room is the store: shelves, casks,
  sacks, hanging meat. Its only ways in are an inner door from the kitchen and a wide delivery door at the back.
- **The store is off limits** to the player at all hours (owners: both cooks). The kitchen is not.
- **The cook porch.** An open lean-to on the kitchen's south end, over a tripod fire with a cauldron: the fire the
  player cooks on, in the open, with the kitchen door behind it.
- **The mess.** An open-sided shelter in the lee of the inner palisade, four trestle tables with benches, and two
  more tables in the open between it and the hatch.
- **The back.** An alley between the cookhouse and the backs of the huts, closed by a fence and a gate on the
  arrival lane: the cart, crates, barrels and sacks wait there by the delivery door. South of the porch, against
  the palisade: woodpile, chopping block, water butts, wash tub, fish rack.
- **At night** the hearth shows through the hatch, a lantern hangs beside it and the porch fire burns: the warm spot
  of the worker side.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they decide what is modelled.

1. **Walls**: horizontal boards between squared posts with the frame showing (as the warehouse), or something plainer.
2. **Roofs**: split shingles on the cookhouse (as the warehouse) or boards (as the huts); boards or patched canvas on
   the mess shelter and the porch.
3. **The hatch**: how wide, how the shutter is propped, how much of the kitchen shows through it.
4. **Hearth and chimney**: how big the rubble chimney reads above the roof, how much soot.
5. **The mess**: open on all sides or boarded on the palisade side; how many tables before it stops reading as poor.
6. **The back alley**: how tall and closed the fence is, and whether the gate is worth having.
7. **The porch fire**: the tripod and cauldron as captured, or a low rubble fire bench.
8. **Night**: three warm lights (hatch, lantern, porch fire) or fewer.
9. **The flag** (`CampCentre/Flag_0`) stands 1 m in front of the wall just south of the hatch. Keep it, or move it
   to the corner of the porch.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Canteen/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Canteen/`).
4. Generate **K1 first** and judge it before the others: it sets the look the rest must match. If the tool takes a
   second reference image, add the accepted K1 (or `Warehouse/W1 camp side`) to every later view and append this
   sentence to the template's second line:
   `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`
5. **K4 and K5 are interiors** and **K6 is the night view**: each replaces the template's fifth line, as given under
   the view.
6. Fill the verdict log at the end.

In every capture the log palisade, the board huts, the stage with its post, the flag, the cart, the tripod with its
cauldron, the straw pallet and the trough are real. Every smooth single-coloured shape is a temporary massing
placeholder, by colour:

| Placeholder | Stands for |
|-------------|-----------|
| Flat mid-brown walls | The cookhouse's plank walls (and the wind wall of the porch, the leaves of the delivery door) |
| Flat grey-brown slabs | Roofs: cookhouse, porch lean-to, mess shelter |
| Grey boxes | Rubble hearth, its hood and the chimney |
| Light orange-brown boxes and poles | Carpentry: posts, tables, benches, counter, shutter props, shelves, fence, gate, tally board, fish rack |
| Pale yellow cylinders | Barrels, casks, water butts; the low wide one is a wash tub |
| Off-white flattened balls | Sacks |
| Dark brown boxes | Crates; the long one against the palisade is a woodpile, the short cylinder a chopping block |
| Dark red shapes | Hanging meat (in the store), split fish (on the rack) |
| Small yellow box, black ball | Lantern beside the hatch; cauldron on the hearth |

The ground under the buildings is levelled in the captures only (see the build brief).

---

## Template (same for all seven views)

Keep every line **verbatim**; only `{VIEW}` changes (and the fifth line for K4, K5 and K6).

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every structure exactly as they are. Keep the shapes of the terrain and the trees, and repaint their surfaces; ignore the hard cast shadows of the input. The tall walls of upright sharpened logs, the board huts, the timber stage, the flag pole, the wooden cart and the iron cauldron on its tripod are finished: keep them exactly as they are. Every smooth single-coloured box, slab, pole, cylinder and ball is a placeholder: replace each one with the object described below, keeping its position, its size and its direction. Any other flat brown or grey block not described below is a plain weathered timber shed or fence of the same camp. Do not add, move or remove buildings.
{VIEW}
The place is the canteen of a trading company's labour camp. The flat mid-brown walls are the cookhouse, a long low single-storey timber building: squared posts of pitch-stained dark oak with the frame showing, tight horizontal weathered planks between them, vertical boards in the gables, a low sill on a single course of rough grey rubble. The flat grey-brown slabs are roofs: on the cookhouse a gabled roof of split wooden shingles, grey and weathered, with a few paler shingles where it was mended; on the lean-to porch and on the open shelter, rough overlapping boards on pole rafters, sagging a little, weighted with lashed bark poles. The grey boxes are a hearth and chimney of rough local rubble bound with clay, blackened with soot at the top and above the fire. The opening in the long wall is a serving hatch: a worn plank counter, and above it a plank shutter hinged at the top on blackened iron strap hinges, propped open on two poles. The light orange-brown boxes and poles are plain camp carpentry in weathered wood: posts, trestle tables of thick split planks with long benches, shelves, a rough pole fence and gate, a rack. The pale yellow cylinders are old oak barrels and casks with rusted iron hoops; the low wide one is a wooden wash tub. The off-white flattened balls are full sacks of rough undyed linen, stained and patched. The dark brown boxes are plain plank crates; the long one against the log wall is a stack of split firewood, the short cylinder beside it a chopping block with an axe scar. The dark red shapes are food hung to keep: salted joints of pork on iron hooks indoors, split smoked fish on the outdoor rack. The small yellow box is an iron lantern on a bracket. Bare trodden earth with worn paths, puddles, wood chips, a few wooden bowls and spoons left on the tables.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: weathered wood browns and greys, stone grays, mossy greens, earth browns; no saturated colors, no glow.
No masonry walls, no cut stone blocks, no brick, no plaster, no half-timbered infill, no glass windows, no upper storey, no towers, no signboard, no painted sign, no tablecloths, no banners except the plain faded dark-red flag, no fresh or new-looking timber, no fire, no lit lanterns, no smoke, no fog, no light rays, no animals, no new people, figures stand in natural working poses with their arms down, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### K1 — From the square (the look-setting view)

Input: `refs/K1 from the square ref.png`. Save as `K1 from the square.png`.

```
View from the timber stage on the camp square, looking south-west; the stage's corner post in the lower left foreground stays as it is. The cookhouse's long east wall faces the viewer under its shingled roof, with the serving hatch left of the middle, its shutter propped open, the lantern on its bracket right of the hatch and a plank tally board with chalk marks further right. Through the hatch, a work table and the dark kitchen. The rubble chimney stands above the far slope of the roof. The plain dark-red flag on its pole stands in front of the wall, left of the hatch. Left of the cookhouse, the roof of the lean-to porch, and in front of it the open-sided mess shelter with trestle tables and benches under its board roof; two more tables stand in the open in the foreground. Behind everything, the tall log palisade, trees and hills. At the far right, board huts across the lane.
```

### K2 — Mess and cook porch

Input: `refs/K2 mess and cook porch ref.png`. Save as `K2 mess and cook porch.png`.

```
View at eye level from the open tables, looking west. On the left, the mess shelter: posts, a low board roof, trestle tables and benches, with the log palisade close behind it. In the middle distance, under the lean-to porch roof on four posts, the iron cauldron hangs on its tripod over a ring of stones: keep it, cold and unlit. Behind the porch, a plank wind wall; left of the porch, a stack of split firewood against the palisade and a chopping block. On the right, the south end and the long east wall of the cookhouse, with the flag pole in front and the serving hatch at the right edge, the grey rubble hearth showing through it. Two trestle tables in the foreground.
```

### K3 — Back alley and delivery door

Input: `refs/K3 service yard ref.png`. Save as `K3 back alley.png`.

```
View from the lane north of the cookhouse, looking south into the alley behind it. On the left, the cookhouse's blank north gable end with vertical boards in the gable, and the rubble chimney above the roof. Between the cookhouse and the board huts on the right, the alley is closed by a rough pole fence about chest high with its gate standing open: the tall light-brown post, the flat panel and the open leaf are that fence and gate. In the alley the wooden cart stands as it is, tail toward the viewer; beside it, against the cookhouse wall, a crate, and further in a wide plank delivery door standing open, with barrels, crates and sacks beyond. At the end of the alley, a fish rack with split fish against the log palisade. The dark open-fronted hut on the right stays as it is.
```

### K4 — Kitchen, toward the hatch

Input: `refs/K4 kitchen interior ref.png`. Save as `K4 kitchen interior.png`.

Replace the template's **fifth line** (`Overcast sky…`) with:

```
Interior, dim: cold grey daylight falls in through the hatch and the open doorway, the corners stay in shadow. Unlit hearth. Muted earthy palette: dark wood browns, soot blacks, stone grays; no saturated colors, no glow.
```

View paragraph:

```
Interior of the cookhouse kitchen, seen from its south-west corner. Plank walls on squared posts, an open roof of pole rafters and the underside of the shingles, a floor of wide worn planks. On the left wall, the grey boxes are the big rubble hearth: a raised fire bench, a soot-black hood and a chimney breast rising through the roof, with an iron cauldron and a pot hook, ash on the stones. Left of the hearth, a water barrel and plank shelves with wooden bowls, clay jars and iron pans. In the middle, a heavy work table scarred by knives, with a loaf, onions and a cleaver. In the far wall, a plain plank door, closed, with an iron latch: it leads to the store. On the right wall, the serving hatch with its counter, open to the daylight and the camp outside, which stays as it is. In the lower right corner, the foot of a straw pallet with a blanket, which stays. Bunches of dried herbs and onions hang from a rafter.
```

### K5 — Store

Input: `refs/K5 store interior ref.png`. Save as `K5 store interior.png`.

Replace the template's **fifth line** (`Overcast sky…`) with:

```
Interior, dim: cold grey daylight falls in through the open delivery door only, the rest stays in shadow. Muted earthy palette: dark wood browns, sackcloth greys, stone grays; no saturated colors, no glow.
```

View paragraph:

```
Interior of the cookhouse store room, narrow and crowded, seen from the inner door. Plank walls on squared posts, pole rafters and the underside of the shingles above, a plank floor. On the right, the large light-brown block is a wall of rough plank shelves from floor to head height, loaded with clay jars, round cheeses, loaves, small kegs and folded sacks. On the left, three casks stand against the wall. From a beam overhead hang three salted joints of pork on iron hooks. On the floor, full linen sacks piled against the shelves and a plank crate. In the far wall, the wide delivery door stands open onto the alley: the cart outside and the huts beyond stay as they are. Everything is counted and in order: a tally of chalk strokes on one post.
```

### K6 — Night, from the square

Input: `refs/K6 night from the square ref.png`. Save as `K6 night from the square.png`.

Replace the template's **fifth line** (`Overcast sky…`) with:

```
Night, clear and cold, dim blue moonlight, deep shadows. Three small warm lights only: the hearth fire seen through the serving hatch, the iron lantern on its bracket beside the hatch, and the low fire under the cauldron in the porch. Their orange light falls a few metres on the wall, the counter and the mud, and no further. Muted palette: night blues, dark wood browns, stone grays; no other light, no torches, no glow around objects.
```

and delete `no fire, no lit lanterns, ` from the last line. View paragraph:

```
View at night from the open tables on the square side, looking west at the cookhouse. The long east wall faces the viewer: the serving hatch in the middle with its shutter propped open and the hearth fire glowing inside, the lit lantern right of it, the tally board further right. The flag pole stands in front of the hatch as a dark line. On the left, the lean-to porch with the cauldron on its tripod over a small fire, and nearer, the posts and low roof of the mess shelter in shadow. Two trestle tables stand dark in the foreground. Behind, the black line of the palisade and pale birch trees in the moonlight; at the far right, dark huts.
```

### K7 — Overview

Input: `refs/K7 overview ref.png`. Save as `K7 overview.png`.

```
High view from the north-east over the west side of the camp square. In the middle, the cookhouse seen from above: a long shingled roof, ridge running away from the viewer, with the rubble chimney on its far slope; on its near long wall the serving hatch with its propped shutter, the lantern and the tally board, and the plain dark-red flag on its pole in front. At its far end the lower board roof of the porch. Left of it, the board roof of the mess shelter against the palisade, with trestle tables under it and two more in the open. At the right end of the cookhouse, the fence and open gate of the back alley, with the cart behind it between the cookhouse and the huts. The log palisade runs across the picture behind the canteen; the larger buildings and brown blocks beyond it belong to the company's compound and stay as plain timber buildings. The timber stage in the lower left foreground, the huts on the right and the flags stay as they are.
```

---

## Build brief (facts the build keeps)

Measured on the site and on the massing used for the captures (world coordinates, x east, z north).

| What | Value |
|------|-------|
| Cookhouse | 9 × 5.5 m, x −18.5..−13, z 15.5..24.5; walls 2.6 m, ridge north–south at about y 4.3 (roof pitch 30°), eaves 0.35 m |
| Kitchen | South room, z 15.5..21 (5.5 × 5.5 m); door 1.1 × 2.1 m in the south gable (x −15..−13.9), into the porch |
| Hatch | East wall, z 18.5..20.6, sill 0.95 m above the floor, head 2.0 m; counter 0.75 m deep through the wall |
| Hearth | On the partition, kitchen side, x −17.5..−15.7; chimney 0.8 × 0.7 m through the west slope to about y 5.1 |
| Store | North room, z 21..24.5 (5.5 × 3.5 m); inner door 1.0 × 2.1 m in the partition (x −14.6..−13.6); delivery door 1.6 × 2.2 m in the west wall (z 22..23.6) |
| Porch | Lean-to on the south gable, z 12.5..15.5, four posts, wind wall on the west side; the cooking station (tripod fire, 1.4 × 2.7 m) stands at (−16.6, 14.1) |
| Mess shelter | x −11.5..−5.5, z 10..14.5, six posts, single-pitch roof falling north (3.0 → 2.35 m); four 2.2 m tables; two more in the open near (−9.6, 16.9) and (−6.9, 16.3) |
| Back alley | x −22..−18.5 between the cookhouse and huts `worker-hut-02 (7)` / `(9)` (their fronts at x −22); fence and gate on z 25 |
| Work corner | Against the palisade south of the porch: woodpile x −18.6..−15.6 at z 9.8, chopping block, two water butts and a wash tub west of the porch, fish rack near (−20.6, 11.2) |

Constraints:

- **The arrival sight lane decides the north end.** The 6 m lane from the punishment post to `arrival-gate`
  (−38.4, 36.3) passes north of the plot: its south edge is at z 26.0 over x −13 and z 27.4 over x −18.5. Nothing
  taller than the fence may stand north of that, so the cookhouse ends at z 24.5 and **the service yard is the
  alley west of the store, not a yard north of it** (a change from the first sketch of the plan).
- **The ground is not flat.** It is y 0 north of z 17 and rises to +0.3..0.5 m south of z 16 (west of x −12) and
  west of x −21. The captures level it to y 0 over x −21.5..−12, z 12.4..25 and x −12.5..−5, z 9.9..15.2 (in
  memory only: nothing was saved). The build needs the same cut, about 0.5 m at most, then
  `Capture Area 1 Terrain Sculpt`. The woodpile stays on the unlevelled strip against the palisade.
- **`CampCentre/Flag_0`** at (−12, 17.6) is tool-owned and stands 1 m off the east wall, 0.9 m south of the hatch.
  The hatch was placed to clear it. Moving the flag is an Ask First.
- **Restricted volume**: the store room only (inside faces of its four walls), owners `NPC_Cook` and
  `NPC_KitchenHand`, all hours, under `RestrictedAreas`.
- **Things that move with the build**: `CookingStation_Fireplace` (−9.4, 21.2) → the porch; `KitchenHandBed` → the
  kitchen's south-west corner; `Spot_Cook_Night` (−14.5, 23.3) is inside the future store and moves to the kitchen;
  Tob's stand spot (−10.6, 22.3) → behind the counter; Aldric's (−6, 20) → in front of the hatch;
  `Decor/SM_cartB_a1` → the alley; `SM_Trough` (−20, 16) stays or goes to the work corner.
- **Removed by the build**: `TentCamp/Canteen/worker-canteen`, `worker-dining`, and the kit sets under `Camp/Canteen`.
- `walkway-canteen` (layout road (−2, 20) → (−9, 23), tool-owned, no visible boards today) ends short of the hatch.
- Doorways at least 2.1 m high and 1 m wide, steps under 0.3 m (`STARTING_TOWN.md` → Camp centre).

---

## Verdict log

"Kept?" is the user's call.

| View | Date | Kept? | Notes (what to carry into the build, what to ignore) |
|------|------|-------|-------------------------------------------------------|
| K1 from the square | 2026-11-26 (Meshy) | | **Carry:** the whole look: low plank building on a rubble sill, grey shingles with mended patches, hatch with a top-hung shutter on two props, lantern on a bracket, chalk tally board, board-roofed mess with trestle tables. **Ignore:** the crate, sacks, firewood, chopping block and fish rack against the front wall (invented: they belong to the back); writing on the board. Boards are vertical here |
| K2 mess and cook porch | 2026-11-26 (Meshy) | | **Carry:** mess roof of boards on pole rafters with bark poles on top, posts set in the mud, porch as a lean-to with a board wind wall, tripod fire on a ring of stones, firewood stacked against the palisade, rubble sill and hearth seen through the hatch. **Ignore:** smoke from the chimney |
| K3 back alley | 2026-11-26 (Meshy) | | **Carry:** wattle-and-pole fence with a sagging gate, cart ruts and standing water, delivery door of diagonal-braced planks, barrels and sacks along the wall, fish rack at the end, patched gable. **Ignore:** the chimney's crenellated top. **Differs from K1:** horizontal boards between posts on the gable (point 1 below) |
| K4 kitchen interior | 2026-11-26 (Meshy) | | **Carry:** rubble fire bench under a clay-plastered hood, cauldron on a pot hook, heavy knife-scarred table, wall shelves with bowls, jars and pans, onions and herbs on the rafters, plank door to the store, open rafters and board underside. **Ignore:** the hearth sits in the corner (it is in the middle of the partition wall) |
| K5 store interior | 2026-11-26 (Meshy) | | **Carry:** shelves from floor to head height with jars, cheeses, loaves, kegs and folded sacks, standing casks, hams on hooks from a tie beam, sack pile, crate, chalk tally on a post, flour on the floor. **Ignore:** the opening at the left edge (the inner door is behind the camera) |
| K6 night from the square | 2026-11-26 (Meshy) | | **Carry:** three warm lights (hearth through the hatch, lantern, porch fire), light dying within a few metres, reflections in the puddles. **Ignore:** writing on the board |
| K7 overview | 2026-11-26 (Meshy) | | **Carry:** the layout as planned: cookhouse, porch, mess against the palisade, two open tables, cart in the alley. **Ignore:** a door in the north gable (that end is blank), props against the front wall (copied from K1), no chimney on the roof |

Generated through Meshy at the user's request (their own image tool had hit its limit; plan and cost of 63 credits
given first, no regeneration) with `meshy_image_to_image`, `nano-banana-pro`, 16:9, 9 credits each: K1 from its
capture alone, the others with the capture first and the K1 result second, the second line of the template reading
"Repaint the first input image … add no object where the first image shows none" plus the reference sentence, and
each view's description cut down to the objects that view shows. Without that cut, K1 painted every object the
template lists (sacks, crate, firewood, fish rack) against the nearest wall.

**Decided by the user on 2026-11-26, from the images** ("all seems good", on the build proposed from them):

1. **Walls**: horizontal boards between squared posts with the frame showing, vertical boards in the gables (as the
   warehouse and K3; K1, K2 and K6 drew vertical boards).
2. **Roofs**: split shingles on the cookhouse, rough boards on pole rafters under bark poles on the porch and the mess.
3. **Hearth**: the K4 one (rubble fire bench, cheeks, a hood, pot hook), in the middle of the partition wall.
4. **Back alley**: a chest-high fence of stakes on two rails, with a two-leaf gate standing open (the west leaf
   into the alley, the east one toward the lane).
5. **Porch fire**: the tripod and cauldron (the existing cooking station).
6. **Night**: three warm lights (hearth through the hatch, lantern beside it, porch fire).
7. **Flag**: left where it is.

### As built

- Floor 0.15 m above the levelled ground, on a single course of rubble; a plank doorstep outside the kitchen door
  and the delivery door.
- Hatch z 18.6..20.8 (2.2 m), sill 0.95 m above the floor (1.10 m above the ground outside).
- The partition's frame faces the kitchen at z 20.8..21.0; the store is 5.02 x 3.22 m inside its boards.
- Doors are fixed leaves standing open (kitchen door, inner door, both delivery leaves): the restricted area closes
  the store, not a lock. A real door, lock and key are a later spec.
- The mess's two table rows stand 2.5 m apart (z 11.0 and 13.5), not 2.1 m: at 2.1 m the benches left 0.48 m, too
  narrow for a walker.
- The fish rack stands at the south end of the alley, on levelled ground (world (-20.3, 13.3)), not on the bank by
  the palisade. The wood corner (three firewood stacks) is on the unlevelled strip against the palisade; the chopping block
  stands at the porch's open east end.
- The Cook's night spot is in the kitchen between the hearth and the store's door, not in a corner: Tob's pallet has
  the south-west corner.
- The tally board is plain boards (no chalk marks); no window in the kitchen's west wall; no shelves on the store's
  east wall (the inner door's leaf stands there).
- `SM_Trough` and `CampCentre/Flag_0` were not moved. `walkway-canteen` is unchanged.
