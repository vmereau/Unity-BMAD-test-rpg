# Home Yard Concepts — reference captures, prompts and build brief

> Read when generating or judging a home-yard artwork (Y1–Y7), or before dressing the yard or the owned huts.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder), in particular
> *Density*, *Scale* and *Weight*; hut shell contract: `Prefabs/World/Huts/CLAUDE.md`. Spec:
> `spec-area-1-home-yard-huts-and-yard`. Earlier passes of the same yard: H1–H7 (`Artworks/Starting area/Camp/`).

The home yard is the ring of board huts round one fire in `TentCamp/PlayersZone`, around (5.5, 39), where the player
and Hobb live (`STARTING_TOWN.md` → Home yard art pass). Two passes gave it its huts, tarps and the player's and
Hobb's interiors. Every other hut is still empty. Four workers of the yard have no home: Pim's and Gerrit's pallets
stand in the open in Wretches' Corner, Tam stands at the fire day and night, and Bren stands at the Wretches' fire
all night. The yard's props (repair trunk, card game, chopping block, firewood) were dropped wherever there was room,
two of them in the escort's lane corridor. This pack fixes the look of the four new homes and of the rearranged yard
before the build: seven captures with temporary massing shapes, one image-to-image prompt per capture, and the facts
the build must keep.

---

## The place

- **Who built it.** Nobody planned it. The oldest corner of the worker half grew round one hearth, hut by hut, from
  whatever boards the Company let go. Every hut is built at the poorest quality in the camp (the warehouse is the best).
  What people use every day ended up against the hut walls and under the eaves, never in the walkway. The trodden
  mud between the doors gets boards where it stays wet.
- **The hearth.** The tripod fire with a kettle, the plank bench to the north and Tam's log seat to the east.
  The escort passes on both sides of it, so nothing stands south of it.
- **Tam's hut** (`worker-hut-03`, door facing the fire). A long-time labourer, beaten more than once, who keeps the
  fire. A pallet bed, a crate table with a candle and a bowl, two sacks, rags drying on a peg rail, a walking stick
  by the bed. Poor and tidy.
- **Pim's and Gerrit's hut** (`worker-hut`, the east hut with full eaves). Two field-gang hands who share: old Gerrit
  and young Pim. Two pallet beds along the side walls, one crate table between their heads under the rear opening,
  Gerrit's threshing flail hung on his wall, Pim's sack hung on a peg above his bed, their box by the door, and Pim's
  hoe leaning outside by the doorway.
- **Bren's hut** (`worker-hut-02 (5)`, door facing the Wretches' Corner fire). A quarryman who tells stories at that
  fire. A pallet bed with a second blanket rolled at its foot, kept for Odo, who vanished; a rough stone block he
  shapes in the evenings, with a chisel on it; a tally stick on the wall (what he owes the Company); two sacks.
- **The wood corner** (east pocket). A stack of split firewood and a chopping block on a patch of chips.
- **The work corner.** The repair trunk against the front wall of Pim's and Gerrit's hut, beside the doorway, and a
  sawhorse with a plank on it.
- **The evening corner** (west pocket, between two hut fronts). The card game: a low crate with three stools round
  it and a lantern on a post.
- **The water corner** (west pocket, by the doors pocket). A water barrel, a bucket and a wash basin on a plank bench.
- **At night** the fire, the lantern over the card game and the candles of the occupied huts, seen through their
  doorways. Everything else is dark.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they decide what is built and what is modelled.

1. **Wood and work corners**: free-standing (as massed) or stacked against a hut wall under the eaves; does the
   sawhorse read as work in progress?
2. **How much the yard holds** before it stops reading as poor: are four corners plus the hearth too many?
3. **Each owner's hut**: does each one read as *his* from the doorway (rags, flail and hoe, stone and tally stick)?
   Which belongings the kit lacks and must be modelled (flail, tally stick, chisel, walking stick).
4. **Night**: fire, one lantern, candles through three doorways: enough, or should the lantern go?
5. **Mud and boards**: boards only at the owned doors, or a board path across the wettest stretch?
6. **The card game's place**: in front of the two west huts (as massed) or under a tarp.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Home yard/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Home yard/`).
4. Generate **Y1 first** and judge it before the others: it sets the look the rest must match. If the tool takes a
   second reference image, add the accepted Y1 to every later view, change the second line's opening to
   `Repaint the first input image.`, and append these two sentences to that line:
   `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`
   `Add no object where the first image shows none.`
5. **Y4, Y5 and Y6 are interiors** and **Y3 is the night view**: each replaces the template's fifth line, as given
   under the view (Y3 also edits the last line).
6. Fill the verdict log at the end.

In every capture these are real and finished: the board huts with their pole-weighted board roofs, the log
palisade, the warehouse and its ramp, the tarps and their ropes, the tripod over the ring of fire stones, the plank
bench and the log seat at the fire, the wooden pallet beds with their blankets, the loose boards on the mud, the
flags. Every smooth single-coloured shape is a temporary massing placeholder, by colour:

| Placeholder | Stands for |
|-------------|-----------|
| Light orange-brown boxes, poles, slabs and short cylinders | Carpentry: sawhorse and its plank, wash bench, lantern post, the three stools of the card game, a peg rail, the flail, the hoe, the walking stick, the tally stick, the chisel's handle; flat slabs in front of doors are boards on the mud |
| Dark brown boxes | Plank crates: crate tables (with a candle), the lower lidded boxes by the beds (the owners' boxes), the long low box against a hut front (the repair trunk), the card crate. The tall long block in the east pocket is a stack of split firewood |
| Dark brown short cylinder; small dark brown discs | A chopping block; wooden bowls on the tables |
| Pale yellow cylinders | The tall one is a water barrel, the low wide one on the bench a wash basin, the small one a bucket |
| Off-white flattened balls | Full sacks (in Y5 one hangs on the wall from a peg) |
| Flat pale straw-yellow slabs | Loose straw on the hut floors; outside, wood chips round the chopping block |
| Thin pale grey-beige slabs on a wall | Rags hung to dry (Tam's hut) |
| Grey box | A rough block of quarry stone being shaped (Bren's hut) |
| Dull green-grey roll | A rolled wool blanket (Bren's hut) |
| Small yellow box | A tallow candle (on the crate tables); the lantern on its post |
| Black ball | An iron kettle hanging from the tripod |

---

## Template (same for all seven views)

Keep every line **verbatim**; only `{VIEW}` changes (and the fifth line for Y3–Y6, the last line for Y3).

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every structure and object exactly as they are. Keep the shapes of the terrain and the trees, and repaint their surfaces; ignore the hard cast shadows of the input. The board huts with their board roofs weighted with lashed poles, the tall walls of upright sharpened logs, the large timber warehouse and its ramp, the canvas tarps and their ropes, the iron tripod over the ring of fire stones, the plank bench and the log seat, the wooden pallet beds with their blankets, the loose boards on the mud and the flags are finished: keep them exactly as they are. Every smooth single-coloured box, slab, pole, cylinder and ball is a placeholder: replace each one with the object described below, keeping its position, its size and its direction. Do not add, move or remove buildings.
{VIEW}
The place is the oldest yard of a trading company's labour camp, where poor workers live in small board huts round one fire. The light orange-brown shapes are plain camp carpentry in weathered, split wood: a sawhorse, a plank bench, round stools cut from logs, a lantern post, a peg rail, and tools with worn wooden hafts. The dark brown boxes are small plank crates and boxes, old and patched, with rope handles; the tall long block is a stack of split firewood under a few loose boards. The dark brown short cylinder is a chopping block with an axe scar, the small discs wooden bowls. The pale yellow cylinders are an old oak water barrel with rusted iron hoops, a shallow wooden wash basin and a wooden bucket. The off-white flattened balls are sacks of rough undyed linen, stained and patched. The flat straw-yellow slabs are trodden straw on a dirt floor, or wood chips on the mud. The small yellow box is a stub of tallow candle in a clay dish, or a small blackened iron lantern. The black ball is a soot-blackened iron kettle. Bare trodden earth with puddles, worn paths between the doors.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: weathered wood browns and greys, stone grays, mossy greens, earth browns; no saturated colors, no glow.
No masonry walls, no cut stone buildings, no brick, no plaster, no glass windows, no furniture beyond what is described, no rugs, no tablecloths, no decorations, no banners except the plain faded dark-red flags, no fresh or new-looking timber, no fire, no lit lanterns, no lit candles, no smoke, no fog, no light rays, no animals, no people, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### Y1 — The yard by day (the look-setting view)

Input: `refs/Y1 home yard day ref.png`. Save as `Y1 home yard day.png`.

```
View at eye level from the south entrance of the yard, looking north. On the left, close to the viewer, the side wall of a board hut stays as it is. In the middle of the yard, the iron tripod over the ring of fire stones with the iron kettle hanging from it, cold, and the plank bench behind it. Beyond the fire on the left, against the front of a hut beside its doorway, the water corner: the water barrel, and the wash basin on a low plank bench. At the back in the middle, the canvas tarp bay and the huts. Right of the middle, against the front of the far hut beside its doorway, the long low repair trunk with a sawhorse in front of it, a plank resting on the sawhorse. On the right in the foreground, the wood corner: a stack of split firewood, waist high, and a chopping block on a scatter of wood chips, with the log seat lying left of them.
```

### Y2 — Overview

Input: `refs/Y2 overview ref.png`. Save as `Y2 overview.png`.

```
High view from the south-east over the yard. Board huts with board roofs stand in a ring round a small open yard of trodden mud. In the middle of the yard, the tripod fire with its kettle, the plank bench north of it and the log seat east of it. Left of the fire, in front of two hut fronts, the card game: a low crate with three round stools and a lantern on a post beside it; further up, the water barrel and the wash basin on its bench. Right of the fire, the stack of firewood and the chopping block on wood chips. At the top right, the canvas tarps between the huts, and against the front of the hut at the top right of the yard, the repair trunk and the sawhorse. The graybox sheds at the top left, the warehouse at the right edge and the flags at the left stay as they are.
```

### Y3 — The yard at night

Input: `refs/Y3 home yard night ref.png`. Save as `Y3 home yard night.png`.

Replace the template's **fifth line** (`Overcast sky…`) with:

```
Night, clear and cold, dim blue moonlight, deep shadows. Three small warm lights only: the low fire under the kettle in the middle of the yard, the iron lantern on its post over the card game, and a candle seen through the doorway of the hut at the right. Their orange light falls a few metres on the mud, the hut fronts and the seats, and no further. Muted palette: night blues, dark wood browns, stone grays; no other light, no torches, no glow around objects.
```

and delete `no fire, no lit lanterns, no lit candles, ` from the last line. View paragraph:

```
View at night from the doors at the north end of the yard, looking south across it. In the foreground, loose boards on the mud. In the middle, the tripod fire burning low with the kettle over it and the plank bench in front. Beyond it on the left, the stack of firewood and the chopping block, and nearer on the left, the sawhorse and the repair trunk in shadow. On the right, the card game in front of the hut fronts: the low crate with three round stools and the lit lantern on its post above them. In the right foreground, the water barrel, the wash basin on its bench and a bucket, dark. At the right, the doorway of the hut with the boards in front of it shows a candle inside. Dark huts and the black line of the palisade behind; pale moonlit hills.
```

### Y4 — Tam's hut

Input: `refs/Y4 Tam hut interior ref.png`. Save as `Y4 Tam hut interior.png`.

Replace the template's **fifth line** with:

```
Interior, dim: cold grey daylight falls in through the doorway behind the viewer and the small square opening in the back wall, the corners stay in shadow. Unlit candle. Muted earthy palette: dark wood browns, sackcloth greys, earth browns; no saturated colors, no glow.
```

View paragraph:

```
Interior of a small board hut, seen from just inside the doorway. Rough board walls, a dirt floor with trodden straw. Along the left wall, the wooden pallet bed with its blanket stays as it is. Against the back wall on the right, under the opening, a small crate used as a table, with a candle stub in a clay dish and a wooden bowl. Between the bed and the crate, a plain walking stick of rough wood leans against the back wall. On the right, close to the viewer, two worn rags hang to dry from pegs on a peg rail, and two half-empty linen sacks lie on the floor against the right wall. Poor, bare and tidy.
```

### Y5 — Pim's and Gerrit's hut

Input: `refs/Y5 Pim and Gerrit hut interior ref.png`. Save as `Y5 Pim and Gerrit hut interior.png`.

Replace the template's **fifth line** with the one of Y4. View paragraph:

```
Interior of a small board hut shared by two farm labourers, seen from just inside the doorway. Rough board walls, a dirt floor with trodden straw. Two wooden pallet beds with their blankets stand along the left and the right walls and stay as they are. Between their heads, under the small square opening in the back wall, a small crate used as a table, with a candle stub in a clay dish and a wooden bowl. High on the right wall, a threshing flail hangs from two pegs: a long wooden staff with a short swingle tied to its end by a leather thong. A plank box with a rope handle stands in the lower right corner beside the doorway, and a linen sack lies next to it.
```

### Y6 — Bren's hut

Input: `refs/Y6 Bren hut interior ref.png`. Save as `Y6 Bren hut interior.png`.

Replace the template's **fifth line** with the one of Y4. View paragraph:

```
Interior of a small board hut of a quarryman, seen from just inside the doorway. Rough board walls, a dirt floor with trodden straw, the small square opening in the back wall closed by its plank shutter. Along the left wall, the wooden pallet bed with its blanket stays as it is; across its foot lies a second wool blanket, rolled and tied with cord, unused. Against the back wall on the right, a small crate used as a table, with a candle stub in a clay dish and a wooden bowl. In front of it on the floor, a rough block of grey quarry stone, half shaped, with an iron chisel and a small mallet lying on it and stone dust on the straw. High on the right wall, a tally stick of pale wood with rows of notches hangs from a peg. Two linen sacks lie against the right wall near the viewer.
```

### Y7 — The west pocket

Input: `refs/Y7 west pocket ref.png`. Save as `Y7 west pocket.png`.

```
View at eye level from the middle of the yard, looking north-west at the space between two hut fronts. On the left, close to the viewer, the corner post and boards of a hut stay as they are. In the middle, the card game: a low crate used as a table, three round stools cut from logs round it, and a small iron lantern on a rough post beside it, unlit, a few cards and a wooden mug on the crate. Behind it on the right, against the front of a hut beside its doorway, the water corner: an oak water barrel with a lid and a dipper, and a shallow wooden wash basin on a low plank bench. On the right, the trodden mud with loose boards, a doorway with a cloth flap, and the edge of the canvas tarp bay; the plank bench at the right edge stays as it is.
```

---

## Build brief (facts the build keeps)

Measured on the site and on the massing used for the captures (world coordinates, x east, z north; hut-local
coordinates in each hut's own frame: +Z is the doorway, origin at the floor centre, room ±1.28 m between boards).

| What | Value |
|------|-------|
| Hearth | Stays at (5.55, 38.95) with its ring, bench (4.95, 40.4) and log seat (7.8, 39.2); a kettle hangs from the tripod |
| Tam's hut | `worker-hut-03`, Tight_D (doorway mirrored: local x −1.05..0), centre (4.8, 34.7), yaw 354, door (4.1, 36.3) |
| Pim's and Gerrit's hut | `worker-hut`, Home (doorway local x 0..1.05, full eaves), centre (7.68, 46.05), yaw 219, door (6.2, 45.1) |
| Bren's hut | `worker-hut-02 (5)`, Tight_D (mirrored), centre (−4.7, 33.4), yaw 272, door (−6.4, 32.9), 4 m from the Wretches' fire (−8.5, 36.5) |
| Beds (hut-local, yaw 90, head to the back) | Tam and Bren (0.89, −0.26); Gerrit (−0.89, −0.26), Pim (0.89, −0.36) |
| Crate table, candle | Tam and Bren (−0.75, −1.0); Pim and Gerrit (0, −1.0) between the bed heads |
| Owned box | Tam and Bren (0.75, 1.0); Pim and Gerrit (−0.55, 1.02) |
| Wall things | Tam: peg rail and rags on the −X wall (y 1.4–1.75); Gerrit: flail on the −X wall (y 1.55); Pim: sack on a peg on the +X wall (y 1.25); Bren: tally stick on the −X wall (y 1.5) |
| Wood corner | Firewood stack (9.95, 38.8), 0.6 × 1.7 m, 1 m high; chopping block (8.9, 39.95) on chips |
| Work corner | Repair trunk against `worker-hut`'s front wall, hut-local (−0.65, 1.85); sawhorse hut-local (−0.8, 2.45), parallel to the wall |
| Evening corner | Card crate (1.7, 37.9); stools (1.0, 37.75), (1.85, 37.2), (2.4, 38.05); lantern post (0.75, 38.55), lantern at 1.5 m |
| Water corner | Barrel (0.45, 41.05); wash bench (1.2, 40.75) with basin; bucket (0.95, 41.4) |
| Door boards | In front of each owned doorway (hut-local (±0.5, 2.0)); Pim's hoe leans on the front wall outside, hut-local (1.3, 1.72) |

Constraints:

- **Lanes.** Nothing solid within 0.7 m of a `HutLanes` segment. Measured on the massing: sawhorse 0.72 m, wash bench
  0.96 m, bucket 0.82 m, repair trunk 1.13 m, card-game stools 1.18 m. The sawhorse had to come 0.4 m in toward the
  wall to pass. Tam's hut already stands 0.34–0.55 m from the lane (its walls, since the rollout), so props *inside*
  it measure 0.33–0.56 m behind the wall: the dresser's lane check must skip props inside a hut room.
- **The work corner moved.** The plan said "under `worker-hut`'s west eave". That eave faces the tarp bay 0.5 m
  away, so the trunk stands against the hut's south-west front, left of the doorway, instead.
- **Doors pocket** (the player's and Hobb's doors) stays clear; the old doorstep bucket and sacks there move to the
  water corner.
- **Removed or moved by the build:** the dresser's `CardGame`, `RepairTrunk`, `Fire/Firewood`, `Fire/ChoppingBlock`,
  `Fire/Axe`, `Doorstep/Bucket` and `Doorstep/Sacks` groups are rebuilt at the new places. `TitheBarnShortfall/FieldHandBed`
  and `BarnWorkerBed` move into `worker-hut` (same parent).
- **Crate tables.** `SM_Crate_01` (about 356k triangles at LOD0) and `SM_Crate_02` (about 292k) are replaced
  everywhere in the dressing by a crate under 3,000 triangles.
- **Restricted areas.** Each owned shell's `RestrictedArea` switched on with its owner(s): `worker-hut-03` Tam,
  `worker-hut` Pim and Gerrit (today on with a null owner), `worker-hut-02 (5)` Bren; all day, one warning.
- Tam is passive (`DetectionRange 0`): as an owner he sees trespass at his witness profile's theft sight range.
- The shells have no floor: props stand on the terrain (flat within ±0.1 m in all three rooms).

---

## Verdict log

"Kept?" is the user's call.

| View | Date | Kept? | Notes (Carry / Ignore / Differs from) |
|------|------|-------|---------------------------------------|
| Y1 home yard day | 2026-11-26 (Meshy) | | **Carry:** the look: weathered grey-brown boards, the firewood stacked in two cross-laid bays on rails under a loose plank lid, a chopping block on a fan of chips, a second low block beside it, the sawhorse of split timber, overcast light on wet mud. **Ignore:** the crates, sacks, barrel and lantern piled against Tam's side wall and the pallet bed laid in the open (invented from the template's list); the lantern post beside the fire; the sawhorse moved to the middle. **Differs:** the work corner was painted as a standing tool rack (a frame with hanging tools) beside the far hut's door |
| Y2 overview | 2026-11-26 (Meshy) | | **Carry:** only the ring layout and the chopping block with an axe in it on scattered chips. **Ignore:** most placeholders left unpainted (yellow and orange shapes); a barrel, basin and bucket painted among the Wretches' graybox sheds; a second card game (crate, stools, lantern) invented at the flags. Weakest view: not a layout reference |
| Y3 home yard night | 2026-11-26 (Meshy) | | **Carry:** the night: low fire under the kettle lighting the bench and a few metres of mud, a lantern hung from a bracket on a post, a candle in a doorway, everything else in moonlit blue. The water corner (oak barrel, tub on a plank bench, a bucket), three log stools round a crate, the firewood stack under its plank lid with a chopping block and a log, the sawhorse, the trunk in shadow, loose boards in front of the doors. **Differs:** the lantern and its post stand at Tam's doorway, not over the card game |
| Y4 Tam hut interior | 2026-11-26 (Meshy) | | **Carry:** two worn rags hung from a peg rail of round pegs, a plank crate table with a candle stub in a clay dish and a wooden bowl, two half-empty patched sacks, trodden straw spread on the dirt. **Ignore:** the walking stick left as a smooth orange pole (model a rough stick) |
| Y5 Pim and Gerrit hut interior | 2026-11-26 (Meshy) | | **Carry:** the crate table between the bed heads with candle and bowl; the flail: a long pole on two pegs with a short swingle tied by a thong; a tied sack on the box by the door. **Ignore:** the straw placeholders left unpainted (Y4 and Y6 show the straw). **Differs:** the flail's staff runs along the wall as a rail, the swingle hanging from it |
| Y6 Bren hut interior | 2026-11-26 (Meshy) | | **Carry:** all of it: the second blanket rolled and tied with cord across the bed's foot, a rough grey block with a chisel and a wooden mallet on it and chips on the straw, the crate table with candle and bowl, patched sacks, the rear opening closed by its plank shutter. **Differs:** the tally is a notched board hung from a peg rail, not a stick |
| Y7 west pocket | 2026-11-26 (Meshy) | | **Carry:** the card game: a plank crate with cards and a wooden mug, three log stools; the lantern hung from an iron bracket on a rough post; the oak water barrel with a dipper hooked on it, a wash tub on a plank bench; loose boards and straw on the mud. **Ignore:** nothing invented beyond small straw patches |

Generated through Meshy at the user's request with `meshy_image_to_image`, `nano-banana-pro`, 16:9, 9 credits each
(63 credits, no regeneration): Y1 from its capture alone, the others with the capture first and the Y1 result second,
the template's second line reading "Repaint the first input image … Add no object where the first image shows none"
plus the reference sentence. Y1, the only view sent without that sentence, painted every object of the template's
materials paragraph somewhere in the frame, as K1 did for the canteen.

**Decided by the user on 2026-11-26, from the images** (on the build proposed from them):

1. **Wood and work corners** stand free: firewood cross-stacked in two bays on rails under a loose plank lid with a
   chopping block on chips (Y1, Y3); the work corner is the trunk and the sawhorse with a plank, **no tool rack**.
2. **Density**: four corners plus the hearth, none of Y1's extra crates, sacks or barrel.
3. **Belongings**: as Y4–Y7. Kit where it has them (crate tables, hanging lantern and bracket, kettle, log stools,
   walking stick, mallet); Blender pieces for the flail, Bren's tally (a **notched board** on a peg, as Y6), the rolled
   and tied blanket, the stone block with its chisel, and the rags.
4. **Night**: the fire, **one lantern on a bracket post over the card game** (Y7, not at Tam's door as Y3), a candle
   in each owned hut.
5. **Boards** at the owned doorways only, plus the existing mud boards.
6. **Card game** in front of the two west huts (Y7).
7. **Work corner** against `worker-hut`'s south-west front, left of its doorway (the plan's west eave faces the tarp bay).
