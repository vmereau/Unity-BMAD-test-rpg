# Gang River House Concepts — reference captures, prompts and build brief

> Read when generating or judging a gang house artwork (R1–R8), or before building the house, its deck and pier,
> the palisade gate, the basement or the smuggling tunnel.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder); the palisade whose
> logs the new wall shares: `PALISADE_CONCEPTS.md`; the cellar the tunnel reaches: `WAREHOUSE_CELLAR_CONCEPTS.md`.
> Spec: `spec-area-1-gang-river-house`.

The gang house stands on the lake shore east of the palisade's end (`Area1_Blockout/GangHouse`, centre
(43.5, 53.5), turned 205°), with `GangDock` behind it. Before this pack it was a graybox: a box house on posts, a
back deck, a plank dock and a boat. The wilderness opening beside it (the terrace between the palisade's end and
a rock clump, on the way to `CampBridge`) was closed only by five loose barricades. The user's sketch:
`Artworks/Starting area/Camp/Gang River House/Refs/gang river house aerial idea.PNG`. This pack fixes the look
before the build: eight captures with temporary massing shapes, one image-to-image prompt per capture, and the
facts the build must keep.

**Built on 2026-11-27** (`spec-area-1-gang-river-house`), in two stages: the house, deck, pier, walkway, gate and
palisade, then the basement and the tunnel. Pipeline and contract: `Prefabs/World/GangHouse/CLAUDE.md`. The
*Build brief* below describes the massing the captures were taken from; what changed is listed under *As built*.

---

## The place

- **Who built it.** The Company put up a fishing lodge on the shore when the camp was young. The gang took it
  over: the top men sleep under a real roof, and Brann keeps his stash there. Better built than the huts (squared
  posts, tight boards, a shingled roof), plainer than the warehouse, and patched by people who do not care how it
  looks: boards nailed over gaps, a few shingles replaced by planks.
- **The house.** One room on a raised floor, on a rubble base that shows 1.2 m on the camp side and a full storey
  on the lake side. Door and plank ramp on the camp side. Inside: a rubble hearth and chimney at the west end, a
  table, Brann's corner with his chest, two bunks, a shut hatch in the floor.
- **The lake side.** The base is a storey high there. A plank deck on piles runs along it at its foot, 1.5 m above
  the water, under a narrow balcony of the main floor carried on four posts. A pier runs from the deck into the
  lake, with a boat. Net poles, barrels and crates stand on the deck.
- **The hidden door.** A small plank door in the rubble base, on the deck, behind fishing nets hung to dry. The
  only way to the deck on foot is a plank walkway on piles down the house's east end.
- **The basement.** The gang's store: a low rubble room under the whole house, casks, crates and sacks that never
  passed the tally, a lantern that always burns, a ladder to the hatch.
- **The tunnel.** The gang's own night work: a narrow drift dug from the basement to the side room of the
  Company's cellar under the warehouse, about 27 m, shored with poles and planks. The black market's goods come
  through it.
- **The wall and the gate.** The palisade is carried on from its old end across the terrace, down the slope
  between the bridge and the house, and into the lake. A cart gateway like the dock's, two leaves of lashed logs
  standing open, lets people through to the bridge, beside the post where the racket guard takes his toll.
- **At night** a lantern burns by the camp door and one on the deck, and the hearth shows through the balcony
  door.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they decide what is modelled.

1. **Walls and roof**: horizontal boards between squared posts as the cookhouse, how patched; split shingles with
   board repairs, or boards.
2. **The rubble base**: how rough, how it meets the boards, how much moss and damp at the waterline.
3. **The lake face**: the balcony (how deep, railed or not), the deck's piles, how much stands on the deck.
4. **The hidden door**: how well the nets hide it; nets alone, or nets and stacked crates.
5. **Pier and boat**: plank width, mooring posts, how worn.
6. **The gate in the palisade**: two leaves of lashed logs like the dock gate's, the posts and the lintel, how
   the leaves stand open.
7. **Where the palisade meets the water**: logs standing in the lake, how far, how they end.
8. **Basement**: rubble all round, the ceiling's joists, how full.
9. **Tunnel**: pole props and plank lagging against earth; how tight, how wet, one lantern or none in sight.
10. **Night**: two lanterns and the hearth, or fewer.

---

## How to generate

1. Give the tool the capture as the **input image**
   (`Artworks/Starting area/Camp/Gang River House/Refs/<name> ref.png`, 1920 × 1080). Image-to-image, keeping the
   composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result in `Artworks/Starting area/Camp/Gang River House/` under the name given.
4. Generate **R1 first** and judge it before the others: it sets the look the rest must match. Then add the
   accepted R1 as a second reference image to every later view and append this sentence to the template's second
   line:
   `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`
5. The interiors (R5, R6) and the night view (R8) replace the template's light line with the one given under the
   view.
6. Fill the verdict log at the end.

The finished real objects in the captures are the log palisade, the flag on its pole, the rocks, the plank bridge
with its rails, the warehouse and the camp's huts, the trees, the ground and the water. The dark brown boat, the
knee-high dark rail on the shore and the grey block shapes on the far hill are older graybox: the template names
them. Everything else smooth and single-coloured is a placeholder:

| Placeholder | Stands for |
|-------------|------------|
| Flat mid-brown walls | The house's plank walls |
| Flat grey-brown slabs | Roofs: the house, the lean-to over the balcony |
| Mid-grey flat walls and boxes | The rubble base of the house (the basement's walls), the hearth's chimney |
| Dark red-brown tall slabs and posts | The new stretch of log palisade, the two gate posts |
| Light orange-brown boxes and poles | Carpentry: ramp, deck, piles, balcony, posts, rails, walkway, pier, doors, the gate's lintel and its two open leaves, net poles, basement posts and beam, rack, ladder, tunnel props |
| Dull green flat sheet | Fishing nets hung to dry |
| Pale yellow cylinders | Barrels and casks |
| Dark brown boxes | Crates |
| Off-white flattened balls | Sacks |
| Small bright yellow box | A lantern |
| Dull ochre floor (R5, R6) | Packed earth floor |
| Dark earth-brown flat walls and ceiling (R6) | The tunnel's earth, behind its planks |
| Black rectangle in a frame (R5) | The tunnel's mouth |

The ground under the deck and the house is lowered in the captures only (see the build brief).

---

## Template (same for all eight views)

Keep every line **verbatim**; only `{VIEW}` changes (and the light line for R5, R6 and R8).

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every object exactly as they are. Keep the tall walls of upright sharpened logs, the flag pole, the rocks, the plank bridge with its rails, the timber buildings in the distance, the trees, the ground and the water as they are, and repaint only their surfaces; ignore the hard cast shadows of the input. Every smooth single-coloured shape is a placeholder: replace each one with the real object described below, at the same size and place. Do not add, move or remove any building, wall, door or post, and add no object where the input shows none.
{VIEW}
Materials. The flat mid-brown walls are the plank walls of an old lodge: squared posts of pitch-stained dark oak with the frame showing, weathered horizontal planks between them, vertical boards in the gables, a few rough boards nailed over gaps. The flat grey-brown slabs are roofs of split wooden shingles, grey and weathered, mended here and there with plain planks. The mid-grey flat walls are a base of dry-stone rubble: rough grey local stones of many sizes packed without mortar, damp and dark at the foot, moss in the joints, never cut blocks; a grey box on the roof is a chimney of the same rubble bound with clay, soot-black at the top. The dark red-brown tall slabs are a palisade of upright sharpened logs, bark-stripped and weathered grey-brown, pressed together and lashed to pole rails with rope, the same as the finished log walls nearby. The light orange-brown boxes and poles are plain carpentry in weathered dark wood: thick split planks for decks, ramps and walkways, tarred piles standing in the water, squared posts, pole rails, plank doors on blackened iron strap hinges, gate leaves of upright logs lashed to cross poles, a squared lintel beam. A dull green flat sheet is old fishing net hung on poles to dry, tarred cord, torn and knotted. The pale yellow cylinders are old oak barrels with dull iron hoops. The dark brown boxes are rough plank crates. The off-white flattened shapes are sacks of coarse undyed linen. A small bright yellow box is a plain iron lantern with horn panes, unlit. The dark brown boat is a worn flat-bottomed plank boat; a low dark rail on the shore is a rough pole rail; grey block shapes far away are bare weathered stone.
Overcast sky, cold diffuse daylight, damp air, wet mud and shingle at the water's edge. Muted earthy palette: weathered wood browns and greys, stone grays, mossy greens, earth browns; no saturated colors, no glow.
No cut stone blocks, no brick, no mortar, no plaster, no half-timbered infill, no glass windows, no upper storey added, no towers, no signboard, no banners except the plain faded dark-red flag, no fresh or new-looking timber, no fire, no lit lanterns, no smoke, no fog, no light rays, no skulls, no chains, no animals, no new people, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### R1 — Camp side (the look-setting view)

Input: `Refs/R1 camp side ref.png`. Save as `R1 camp side.png`.

```
View from the open ground of the camp, looking north at the house's front. A long low plank house under a shingled roof stands on a grey rubble base about waist high; a rubble chimney rises at the left end of the roof. In the middle of the front wall, an open doorway with a plank ramp coming down to the grass, and an unlit lantern on the wall left of the door. At the right end of the house a narrow plank walkway with a pole rail goes down toward the water behind the house. On the left, behind the rocks, the dark red-brown slabs are the log palisade, with a gateway beside the flag pole: two posts, a lintel beam and two leaves of lashed logs standing open. The rough pole rail in the right foreground, the rocks, the trees and the lake stay as they are.
```

### R2 — Lake side

Input: `Refs/R2 lake side ref.png`. Save as `R2 lake side.png`.

```
View from the end of the pier, looking back at the house from the lake. The plank pier leads to a plank deck on tarred piles along the foot of the house. The house rises above it in two parts: below, the grey rubble base, a storey high; above, the plank wall with one plank door, behind a narrow balcony with a pole rail carried on four tall posts, under a lean-to roof of shingles. On the deck, two barrels at the left, two crates at the right, an unlit lantern on a post. Right of the middle, fishing nets hang from a pole frame against the rubble base, covering it from the deck to head height. At the left end of the deck a plank walkway with a rail climbs round the corner of the house. On the right the log palisade comes down the bank and stands in the water beside the deck. The boat in the lower left stays as it is.
```

### R3 — Palisade gate

Input: `Refs/R3 palisade gate ref.png`. Save as `R3 palisade gate.png`.

```
View at eye level on the trodden earth of the terrace, looking north. On the left the finished log palisade, a barrel and a plank bench stay exactly as they are. Right of it, a gateway wide enough for a cart: two heavy log posts, a squared lintel beam across their tops, and two leaves of upright logs lashed to cross poles, standing open toward the viewer, one against each post; through it, the path goes down to the plank bridge and up the far bank. Right of the gateway the dark red-brown slab carries the palisade on: the same upright sharpened logs, as tall as the finished wall, lashed to pole rails. The flag pole in front of the wall and the rocks in the right foreground stay as they are. At the right, behind the rocks, the shingled roof and plank wall of the lodge with its rubble chimney.
```

### R4 — From the bridge

Input: `Refs/R4 from the bridge ref.png`. Save as `R4 from the bridge.png`.

```
View from the plank bridge, looking back toward the camp; the bridge and its rails in the foreground stay exactly as they are. The dark red-brown slabs are one long log palisade: it comes down the bank from the upper right, where it joins the finished log wall, and runs to the left into the lake, its logs shorter as the ground falls, the last ones standing in the water. Draw its top as one uneven line of sharpened points following the slope, not as steps. At the upper right, on top of the earth bank, the palisade ends at a cart gateway: two log posts and a lintel beam, with a leaf of lashed logs standing open, and the camp's huts seen through it. At the far left, beyond the wall, the end of a plank pier.
```

### R5 — Basement

Input: `Refs/R5 basement ref.png`. Save as `R5 basement.png`.

Light line for this view: `Dim interior with no window: one small lantern on a post gives a weak warm light that dies within a few metres; the corners are black. Muted earthy palette: stone grays, weathered wood browns, earth browns; no saturated colors, no glow.` For this view also remove `no fire, no lit lanterns,` from the template's last line and read the lantern as lit.

```
Inside a low store room under the house, seen from one end. The walls are dry-stone rubble on every side. The ceiling is the underside of the floor above: dark joists and boards, low enough to touch, carried by a beam on two squared posts; a lantern hangs on the nearer post. The floor is packed earth. In the far wall, a black opening in a rough pole frame, the height of a man: the mouth of a tunnel. Right of it, a plank rack leaning half across it, as if to hide it. On the left, sacks piled on the floor and crates stacked two high against the wall. In the right foreground, three casks standing on end. At the right edge, part of a plank door frame in the wall.
```

### R6 — Tunnel

Input: `Refs/R6 tunnel ref.png`. Save as `R6 tunnel.png`.

Light line for this view: `Underground, no daylight: one small lantern hung on a prop gives a weak warm light on the nearest timbers and the floor; beyond it the passage goes black. Muted earthy palette: earth browns, weathered wood browns; no saturated colors, no glow.` For this view also remove `no fire, no lit lanterns,` from the template's last line and read the lantern as lit.

```
Inside a narrow tunnel dug through earth, looking along it. It is barely wider than a man's shoulders and just high enough to stand. Every two steps a frame of two rough pole props and a cap pole holds the roof; between the frames, split planks are wedged against the earth of the walls and the roof, with dark wet earth and roots showing in the gaps. The floor is trodden mud with a line of boards laid end to end. A lantern hangs on a prop at the left, a few frames in. Further on, a crate and a sack stand against the right wall. At the far end the tunnel turns left into the dark.
```

### R7 — Overview

Input: `Refs/R7 overview ref.png`. Save as `R7 overview.png`.

```
High view from over the lake, looking south onto the shore of the camp. In the middle, the lodge seen from above: a shingled roof with a rubble chimney at its right end, a lean-to roof over the balcony on the lake side, the plank deck on piles below it with barrels, crates and hanging nets, and the pier running out into the lake with the boat beside it. A narrow plank walkway goes round the left end of the house. Right of the house the dark red-brown slabs are the log palisade: it leaves the finished log wall at the upper right, passes an open cart gateway beside the flag pole, runs down the bank close to the house and ends standing in the water. Right of it, the earth bank and the plank bridge stay as they are. The warehouse, the huts, the rocks and the dark graybox blocks at the top of the picture stay as they are.
```

### R8 — Night, from the lake

Input: `Refs/R8 night from the lake ref.png`. Save as `R8 night from the lake.png`.

Light line for this view: `Night, clear and cold, dim blue moonlight, deep shadows. Two small warm lights only: an iron lantern on a post on the deck, and firelight from the hearth showing through the open door on the balcony. Their orange light falls a few metres on the planks, the rubble and the water under the deck, and no further. Muted palette: night blues, dark wood browns, stone grays; no other light, no torches, no glow around objects.` For this view also remove `no fire, no lit lanterns,` from the template's last line.

```
Night view from the lake, low over the water, looking at the back of the house. The plank deck on piles runs along the foot of the grey rubble base; above it the plank wall and the balcony on four posts under its lean-to roof, with the balcony door open on the firelight inside. A lit lantern hangs on a post on the deck above two barrels. Right of it, nets hang on a pole frame against the rubble, dark. The pier comes toward the viewer on the right, with the boat in the foreground. At the right the palisade stands black against the sky. On the left, the dark shore and the huts of the camp.
```

---

## Build brief (facts the build keeps)

Measured on the site and on the massing used for the captures. World coordinates (x east, z north) unless marked
*local*: the house's frame, origin at (43.5, 53.5), turned 205°, so local +z is the camp side, −z the lake, +x the
west end (chimney), −x the east end (walkway). Heights are world y. The water is at y −2.

| What | Value |
|------|-------|
| House | 9 × 6 m (local x ±4.5, z ±3), main floor y 2.15, walls 3 m, ridge y 6.9, chimney top y 7.6 (limit y 10 for both) |
| Camp door and ramp | Door local x −2 (1.6 m wide in the graybox; 1 m or more in the build), ramp 1.6 × 4.3 m down to ground y 0.6 |
| Rubble base / basement | Outer faces on the footprint, walls 0.6 m; room local x ±3.9, z ±2.4 (7.8 × 4.8 m), floor y −0.5, clear to the joists about 2.35 m |
| Hidden door | Lake wall, local x 1.45..2.55, sill y −0.5, 2.1 m high (`Poor small door`, unlocked) |
| Tunnel mouth | West wall, about local z 0.25..1.55 in the captures; **free of rock anywhere on that wall** (only the south-west outer corner touches `SM_Rock_07 (7)`) |
| Deck | Local x −5.8..4.8, z −3..−6.5, top y −0.5, on piles |
| Balcony | Local z −3..−4.5 at the main floor, four posts at z −4.4 from the deck to a lean-to roof; one door at local x −2 |
| Nets | Pole frame local x 0.7..3.3 at z −3.75, sheet 2.5 × 1.9 m in front of the hidden door |
| Walkway | 1.2 m wide along the east end (local x −4.6..−5.8), from ground y 0.55 at local z +3.5 down to the deck at z −3.6 (8.4°), rail on the outer side |
| Pier | 2.4 × 8.4 m, centre (46.9, 63.6), turned 11°, top y −0.5; the boat beside it |
| Palisade line | Gate posts at x 30.3..30.6 and 33.1..33.4 on z 52, then (33.4, 52.1) → (37.3, 53.0) → (39.9, 58.5) → (41.6, 62.2) → (42.3, 64.0); tops from about y 9 on the terrace to 3 in the water |
| Palisade gate | x 30.6..33.1 on z 52 (2.5 m clear, as the dock gate), lintel 3.2 m above the terrace (y 3.2), two 1.25 m leaves open toward the camp; `SM_Rock_07 (7)` starts at x 33.2, right behind the east post |
| Tunnel (captured stretch) | 1.5 m wide, 2.1 m clear, floor y 0, along z 51 from the side room (x 23.2) to x 33, then north-east; frames every 1.8 m |

Constraints:

- **The deck needs lower ground.** The shore under the deck is y −0.75 at its east end but up to +0.6 at its
  west end; a deck at y −0.5 sits in the bank there. The captures lower the ground under the deck to y −0.9 and
  under the house to the basement's floor (in memory only: nothing was saved). The build needs a cut of up to
  1.5 m under the deck's west half (**Ask First**: terrain heights), then `Capture Area 1 Terrain Sculpt`; under
  the house it uses a terrain hole, as the warehouse cellar does.
- **Tunnel route.** `SM_Rock_06 (17)`, `(18)` and `(19)` are buried from x 24 to 32, z 52 to 57, from y −4 to 5:
  no way straight east from the side room. Free of rock and under 3.2 m of ground: z 49..52 from x 22 to 34, then
  z 54..56 east to the house. So the tunnel leaves by the side room's **south** wall, runs east under the terrace
  near z 51, turns north-east at x 33..34 round the rock clump (x 33..40, z 46..55), and reaches the basement's
  west wall near z 55.
- **Thin cover at the house.** East of x 37 the ground falls from y 2.4 to 1.5: over the tunnel's last 2–3 m the
  cover is 0.2 m or less, and the new palisade's logs (buried 0.7 m) cross it there. The build lowers the tunnel
  to the basement's floor before x 37, and may need a low mound behind the palisade (**Ask First**) or a gap in
  the logs' depth there.
- **The rock clump carries the wall.** From (33.4, 52.1) to (37.3, 53) the line crosses `SM_Rock_07 (7)` and
  `SM_Rock_06 (25)`: Camp Edge panels follow the terrain, not rocks, so the logs there start inside the rock.
- **The house's west gable is 0.9 m from the palisade** from (37.3, 53) to (39.9, 58.5): nobody passes there.
  The only ways to the deck are the east walkway, the lake, and the hidden door.
- **Removed by the build:** five hand-placed scene roots, `SM_Barricade_Var5`, `(1)`, `(3)`, `(4)` and
  `SM_Barricade_Var4` (x 29..37, z 51..56). **Moved:** `TollGates/TollGate_Wilderness` (36, 5, 54) into the
  gateway. **Kept where they are:** the flag `SM_Flag_UV_SM_Flag_UV` (34.6, 50.5), 1.2 m east of the gate's east post;
  `NpcRoutineSpots/Spot_RacketGuard_WildernessGate` (33, 49), 3 m south of the gateway; the bench and barrels by the
  palisade's end.
- **`HiddenStashQuest`** is hand-owned: Brann and `Brann_DaySpot` (44.6, 2.18, 52.8) and `BrannsStash`
  (45.6, 2.45, 53.0) stand on the main floor, which keeps its height. Furniture leaves both clear.
- **`CampEdge/ShoreRail`** ends 1.5 m south-east of the walkway's head (its last post near (47.5, 48.5)): the
  walkway starts beyond it.
- **Paths that must stay complete** after the rebake: gang fire (33, 19.5) → Brann's spot; the racket guard's
  post → through the gate → the bridge's south end (35, 58.5); camp → the walkway → the deck.
- **Hand edits:** `hand-edits.json` pins `GangHouse`, `BackDeck`, nine posts, four skirts and the ramp. Once the
  root is a prefab instance each Sites run warns that they match nothing: the user unticks them.
- Doorways at least 2.1 m high and 1 m wide, steps under 0.3 m
  (`STARTING_TOWN.md` → Camp centre).
- The graybox house and dock are 624 triangles. Budget asked for everything: 90,000.

---

## Verdict log

"Kept?" is the user's call.

| View | Date | Kept? | Notes (Carry / Ignore / Differs from) |
|------|------|-------|----------------------------------------|
| R1 camp side | 2026-11-27 (Meshy) | | **Carry:** the whole look: weathered horizontal planks between dark squared posts, vertical boards in the gable, a mossy dry-stone base about 1.2 m high, split shingles with a few planks nailed over them, a plank ramp with cross battens, an iron lantern on a bracket left of the door, the pole rail at the walkway's head. **Ignore:** the brick chimney and its smoke (rubble, cold); the house drawn shorter than its 9 m. |
| R2 lake side | 2026-11-27 (Meshy) | | **Carry:** the two-part lake face: rubble base a storey high, plank wall above behind a narrow balcony on four posts with one pole rail and a balcony door on strap hinges; deck planks laid across, piles with weed at the waterline, pier with stub mooring posts; nets on a pole frame with stakes, hanging to the deck and hiding the wall behind; barrels and crates against the base; the lantern on a balcony post; the palisade's last logs standing in the water, shorter toward the lake. **Ignore:** the door-like dark panel left of the nets (the hidden door is behind them). |
| R3 palisade gate | 2026-11-27 (Meshy) | | **Carry:** the gateway: two squared posts as tall as a door and a half, a heavy lintel beam through them, leaves of upright planks or split logs with a diagonal brace on long strap hinges, open toward the camp; the new wall as tall as the old one, with the same pole rails; a wet trodden path through the gate. **Ignore:** the left leaf drawn half shut; the chimney's shape. **Differs from R1:** rubble chimney (right), brick in R1. |
| R4 from the bridge | 2026-11-27 (Meshy) | | **Carry:** the wall coming down the bank as one uneven line of points, logs dark and wet at the foot, shorter as the ground falls, the last four or five in the water; the gate's lintel seen from outside; the pier's end beyond. **Ignore:** the wall drawn in two rows near the top of the bank. |
| R5 basement | 2026-11-27 (Meshy) | | **Carry:** rubble walls on every side, heavy dark joists and boards overhead on a beam and two squared posts, packed earth with a few flat stones, a lantern on an iron hook on the nearer post, sacks, crates two high, three casks, the tunnel's mouth as a plain pole frame with a slatted plank panel leaning beside it, a plank door frame at the right. **Ignore:** nothing. |
| R6 tunnel | 2026-11-27 (Meshy) | | **Carry:** frames of two round props and a cap pole every 1.8 m, wide split planks on edge wedged behind them with dark wet earth and roots in the gaps, planks overhead between the caps, trodden mud with two lines of boards, one lantern on a prop, a crate against the wall. **Ignore:** nothing. |
| R7 overview | 2026-11-27 (Meshy) | | **Carry:** the layout as planned: lodge, deck with nets and barrels, pier and boat, wall close to the house's west end and into the water, gateway by the flag. **Ignore:** nets drawn along the whole deck (one frame, in front of the door); the walkway is hidden by the roof. |
| R8 night from the lake | 2026-11-27 (Meshy) | | **Carry:** two warm lights (lantern on the balcony post over the deck, hearth through the balcony door), their reach of a few metres on the rubble and the planks, the palisade black against the sky. **Ignore:** the flame drawn in the doorway itself (the hearth is inside, at the west end). |

Generated through Meshy at the user's request (plan and cost of 72 credits given first, no regeneration) with
`meshy_image_to_image`, `nano-banana-pro`, 16:9, 9 credits each: R1 from its capture alone, the others with the
capture first and the R1 result second, the second line of the template reading "Repaint the first input image …
add no object where the first image shows none" plus the reference sentence, each view's materials paragraph cut
down to what that view shows, and "not brick" added to the chimney after R1 drew one in brick.

**Decided by the user on 2026-11-27, from the images** ("all is good", on the build proposed from them):

1. **Walls and roof**: horizontal planks between squared posts, vertical boards in the gables, split shingles with
   plank patches (the cookhouse's family, more patched).
2. **Rubble base**: as R1 and R2, mossy and damp at the foot; the chimney is rubble, never brick.
3. **Lake face**: as R2: a balcony 1.5 m deep with one pole rail, four posts down to the deck, one balcony door.
4. **Hidden door**: behind one net frame, nets down to the deck, two crates beside it.
5. **Pier**: cross planks, stub mooring posts, the existing boat.
6. **Gate**: a cart gateway as R3 (asked by the user at the first pause, instead of a narrow door): 2.5 m clear,
   a lintel through two posts, two braced leaves standing fully open toward the camp. No lean-to, no fire basket.
7. **Palisade at the water**: as R4 and R2: the last logs stand in the lake, shorter toward the end.
8. **Basement**: as R5, with a slatted plank panel beside the tunnel's mouth.
9. **Tunnel**: as R6.
10. **Night**: the camp door's lantern, the deck's lantern, and the hearth through the balcony door.

- **Ground:** the bank under the deck's west half is cut down by up to 1.5 m (agreed), then the sculpt is captured.
- **Pipeline:** the house, deck, balcony, pier, walkway, basement and tunnel are one Blender script and one
  generated prefab that Sites places as the `GangHouse` root, fitted to a ground capture (the warehouse's
  pipeline, the warehouse script's helpers as a library). The palisade is a new Camp Edge line in `layout.json`.
  The gate is the dock gate's gateway without its lean-to, as its own generated prefab and Sites root. One place
  command removes the barricades, moves the toll gate and, in stage B, cuts the terrain hole.

### As built — stage A (2026-11-27)

Pipeline and contract: `Prefabs/World/GangHouse/CLAUDE.md`. Captures of the build:
`Artworks/Starting area/Camp/Gang River House/built/` (R1–R4, R7 by day; R8, R9 and R10 at 23:00 in Play).

- **No ground capture:** the base, the piles and the ramp's feet are modelled deeper than the ground.
- **Balcony door** at local x 0.55..1.65, west of the middle, not at x −2: Brann's chest lies between the camp
  door and that spot. It matches R2, which drew the door on the right.
- **Balcony roof:** rough boards on pole rafters under one bark pole, not shingles.
- **Pier** square to the deck (local x 0..2.4, out to z −15), not 14° off as the graybox dock.
- **Walkway** from local z 0.6 (where the ground meets it) to the deck, 10.8°; south of its head the path is the
  ground along the east gable.
- **Rubble base:** coursed stones on the kit's rock texture, like the cellar's: more regular and less mossy than
  R1 and R2.
- **Nets:** two layers of knotted cord on a pole frame with two stakes; the base shows through them.
- **Gate:** the dock gate's gateway (round log posts, a log lintel with pointed logs standing on it, leaves of
  lashed logs), not R3's squared posts and plank leaves; its west post stands beside the old palisade's closing
  post.
- **Palisade:** eight panels of the palisade kit, tops from y 8.6 on the rocks to 3.9 in the lake; the top line
  steps from panel to panel, as everywhere on the ring.
- **Goods:** two casks, three crates and two sacks on the deck; a cask, a crate and a sack in the room.
- **Balcony door, hatch:** see stage B.

### Decided by the user at the second pause (2026-11-27)

- Stage A accepted as built ("Go" for stage B).

### As built — stage B (2026-11-27)

Captures: `built/R5 basement`, `R14 basement from the steps`, `R6 tunnel`, `R11 tunnel under the fence`,
`R12 hidden door behind the nets`, `R13 side room opening` (edit mode: the underground lanterns always burn).

- **Basement** 6 × 3.9 m (local x −3..3, z −2.4..1.5), not the 7.8 × 4.8 m of the brief: the camp wall and both
  end walls are 1.5 m thick, to hide the ragged edge of the terrain hole (cut in whole cells).
- **Tunnel** about 23 m, not 27, and deeper than planned: world y 0.1 at the side room, −1.7 from the wilderness
  gateway to the house (the brief said never below −0.7). The gate's posts and the gang fence's buried logs cross
  its line. Route: the side room's south wall, east along z 51.6, north under the gateway itself, east along
  z 54.2 into the house's west wall.
- **Stair well:** five plank treads at the room's west end join the tunnel's floor to the room's.
- **The gang fence's panels are buried 0.4 m**, not 1.5 m, so the tunnel passes under them.
- **Hidden door:** the game's `Poor small door` (planks and ledges, not strap hinges), behind the net frame, which
  stands 0.95 m off the wall so the player can slip behind it.
- **Tunnel mouth:** a plain pole frame with a slatted panel leaning beside it, as R5.
- **Lining:** as R6, with 0.2 m of packed earth behind the planks and over the roof.
- **Lights:** one lantern in the basement and three in the tunnel, always on, low, with shadows.
- **Side room:** its south wall has an opening 1.2 × 2 m with timber jambs and lintel; its two crates moved to the
  east wall.
- **Triangles:** about 94,000 for the whole site, over the 90,000 planned (the kit props and lanterns are 36,000).

### Decided by the user at the third pause (2026-11-27)

- Stage B accepted ("really good"), with two changes, both made:
  - **no hatch and no ladder**: the pier and the hidden door are the way into the basement; the room upstairs and
    the basement are not joined;
  - **the tunnel's seams closed** with packed earth: stretches overlap where they meet in line, the earth layer is
    0.2 m thick, and walls, floor and roof run on to the slanting face of the house's wall.
