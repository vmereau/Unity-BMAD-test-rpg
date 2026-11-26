# Broken Stretch Concepts — reference captures, prompts and build brief

> Read when generating or judging a broken-stretch artwork (B1–B6), or before building the collapsed stretch.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder); the standing wall:
> `PALISADE_CONCEPTS.md`. Spec: `spec-area-1-broken-stretch`.

The broken stretch is the 8 m gap in the north palisade behind the player's hut (`Area1_Blockout/CampEdge/BrokenStretch`,
x −1..7 on z 52.4), between the mossy rock bank and the warehouse. Before this pack it was a graybox rail on two posts
with an invisible wall. This pack fixed its look before the build: six captures with temporary massing shapes, one
image-to-image prompt per capture, and the facts the build had to keep.

**Built on 2026-11-26** (`spec-area-1-broken-stretch`, first pass): how it is built, `Editor/World/CLAUDE.md` → Broken
stretch; what differs from the artworks, *As built* at the end.

---

## The story

- **Why it fell.** The wall stood on a bank of rock and packed stone above the river. After a storm the wet ground
  slipped: the stone footing gave way, and about eight metres of wall went with it. Some logs fell into the camp over
  the rocks, most fell outward onto the shelf above the shore.
- **What is left.** A row of snapped stumps on the old line, the last logs of each standing end leaning out of line
  with a broken rail hanging, fallen logs, and footing stones spilled down both sides.
- **The repair.** The Company is rebuilding it, slowly. Pole scaffolding and props hold the two ends, a pair of
  crossed poles with a rope and block stands ready to raise new logs, fresh pale logs are stacked on the shelf and
  one lies on trestles half pointed, and squared quarry stone for a new footing came down on a sledge.
- **Meanwhile.** A rope hung with rags closes the gap in name only. A stool and a cold fire basket mark where a
  guard will sit at night (the guard is a later spec).
- **What the player gets.** A way to scramble out of the camp behind their own hut, and at night the hunter's fire
  across the river, seen through the gap.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they decide what is modelled.

1. **Which way it fell**: into the camp over the rocks, outward toward the river, or both (the captures show both).
2. **How open the middle is**: stumps right across, or torn out clean for a few metres.
3. **Repair works**: how much scaffolding, and whether the tall crossed hoist poles are worth their height.
4. **The closure**: a rope with rags, a row of low sharpened stakes, or both.
5. **New wood**: how pale against the old wall before it reads as a different game.
6. **The ground**: how large the scar of bare earth is, and whether water stands in it.
7. **Where the work yard is**: on the outer shelf (as captured), or also inside the camp at the foot of the rocks.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Broken stretch/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Broken stretch/`).
4. Generate **B5 first** and judge it before the others: it is the closest view and sets the look the rest must match.
   If the tool takes a second reference image, add the accepted B5 (or `Palisade/P6 construction detail`) to every
   later view and append this sentence to the template's second line:
   `The second image is a material and construction reference only: copy its logs, ropes and wood, never its camera or layout.`
5. **B2 is the night view**: use the night variant of two template lines, given under B2.
6. Fill the verdict log at the end.

In every capture the log walls, the board huts, the warehouse with its ramp, the dock, the rocks and the trees are
final. Every smooth single-coloured shape is a temporary massing placeholder, by colour:

| Placeholder | Stands for |
|-------------|-----------|
| Dark brown cylinders | Old palisade logs: short standing ones are stumps, tall ones at the ends lean, long ones on the ground have fallen |
| Thin light-brown poles | Repair works: scaffold with platform and ladder (west end), slanted props (east end), crossed hoist poles, trestles |
| Pale yellow cylinders | New logs: a stack on bearers, one on the trestles |
| Grey boxes | Stones: scattered = spilled footing stones, neat pile = quarry blocks; the flat brown slab is the sledge |
| Thin line with red rectangles | Rope with rags between two stakes |
| Short black cylinder, small brown box | Cold fire basket and stool |

---

## Template (same for all six views)

Keep every line **verbatim**; only `{VIEW}` changes (and two lines for B2).

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every structure exactly as they are. Keep the shapes of the terrain, the rocks and the trees, and repaint their surfaces; ignore the hard cast shadows of the input. The tall walls of upright sharpened logs, the board huts, the plank warehouse and its plank ramp are finished: keep them exactly as they are. Every smooth single-coloured cylinder, pole and box is a placeholder: replace each one with the object described below, keeping its position, its size and its direction. Any other flat brown, pale or grey block not described below is a plain weathered timber shed, shelter or fence of the same camp. Do not add, move or remove buildings.
{VIEW}
The scene is a gap about eight metres wide in the log palisade of a trading company's labour camp, where the wall has collapsed and is being repaired. The ground under the wall slipped toward the river after a storm: the bank shows a scar of bare wet earth and clay with a shallow washout channel holding brown water, and the rough stones that carried the wall have spilled down both sides in a loose fan, half sunk in mud. The dark brown cylinders are old palisade logs, the same weathered grey-brown debarked trunks as the standing wall, as thick as a man's thigh: the short ones standing in a row are stumps, snapped off jagged and splintered between knee and waist height; the tall ones at the two ends of the gap are the last logs of the wall, leaning out of line, with a broken rail pole and frayed tarred rope hanging from them; the long ones lying on the ground are fallen logs, cracked and muddy, some still tied in pairs by their rope lashing. The thin light-brown poles are repair works made of rough thin poles lashed together with hemp rope: a scaffold of four uprights with one plank platform and a ladder, slanted props shoring up the leaning logs, two low crossed trestles, and one tall pair of crossed poles tied at the top, with a rope and a wooden pulley block hanging from it. The pale yellow cylinders are freshly cut logs for the repair, stripped of bark, clean pale wood clearly lighter than anything else in the picture: a stack lying on two bearers, and one across the trestles with its end half cut to a point and wood chips on the ground under it. The grey boxes are stones: the scattered ones are rough broken footing stones, the neat pile is roughly squared quarry blocks beside a low wooden sledge. The thin line with red rectangles is a sagging hemp rope hung with faded dark-red rags, tied between two stakes across the gap as a makeshift barrier. The short black cylinder is a cold iron fire basket, unlit, with a three-legged stool beside it.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: weathered wood browns and greys, stone grays, mossy greens, earth browns; the fresh logs are the only pale accent; no saturated colors, no glow.
No masonry wall, no brick, no planks in the wall, no new towers, no gate, no door in the gap, no metal scaffolding, no iron chains, no machines, no wheels, no carts, no banners, no skulls, no spikes, no fire, no smoke, no fog, no light rays, no animals, no new people, figures stand in natural working poses with their arms down, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### B5 — At the breach (the look-setting view)

Input: `refs/B5 at the breach ref.png`. Save as `B5 at the breach.png`.

```
Close view from the mossy rock bank inside the camp, standing at the foot of the gap and looking out toward the river. Two fallen logs run from the gap toward the viewer across the moss in the foreground. The row of stumps and the rope with rags cross the middle of the picture. On the left, three logs lean steeply against each other beside the standing wall, with the scaffold and its ladder behind them; on the right, one log leans against the corner of the plank warehouse, which stays as it is. The tall crossed poles with the hanging block stand right of the middle. Behind the stumps, the pale log on trestles, the stack of pale logs and scattered stones lie on a shelf of bare earth; beyond it, birch trees and low hills on the far side of the river, and the top of the warehouse's plank ramp on the right.
```

### B1 — From the hut yard

Input: `refs/B1 from the hut yard ref.png`. Save as `B1 from the hut yard.png`.

```
View from the workers' yard inside the camp, looking north between the board huts. The gap shows in the middle distance above a dark canvas tarp shelter: leaning logs and the top of the scaffold on its left side, the tall crossed poles on its right, birch trees and a hill beyond the river behind them. Left of the tarp, under two rough linen shirts on a drying line, one fallen log and a few spilled stones reach the yard at the foot of a mossy rock bank. The huts, the tarp shelter with its two bedrolls, the boards on the mud, the sacks and the bucket stay exactly as they are.
```

### B2 — Night, the fire across the river

Input: `refs/B2 night fire across the river ref.png`. Save as `B2 night fire across the river.png`.

Replace the template's **fifth line** (`Overcast sky…`) with:

```
Night, clear and cold, dim blue moonlight, deep shadows. The only warm light is one small campfire far away on the opposite bank of the river, seen through the gap: a tiny orange point with a faint reflection on the water. Muted palette: night blues, dark wood browns, stone grays; no other light, no torches, no lanterns, no glow around objects.
```

and delete `no fire, ` from the last line. View paragraph:

```
View at night from the mossy rocks inside the camp, at the foot of the gap, looking out across the river. The small orange dot low in the middle of the picture is the distant campfire on the far bank; keep it small, exactly where it is, and unobstructed. Fallen logs and spilled stones lie across the moss in the foreground. The stumps and the rope with rags cross the middle. On the left, a mossy rock and the leaning logs with the scaffold and ladder behind them; on the right, the tall crossed poles with the hanging block. Beyond the gap, the pale logs catch a little moonlight, then the dark river and the birch trees on the far bank.
```

### B3 — From the shore

Input: `refs/B3 from the shore ref.png`. Save as `B3 from the shore.png`.

```
View from the river shore outside the camp, looking south up a bank of bare trodden earth into the gap. The plank warehouse and its plank ramp with handrails on the left stay exactly as they are, and so does the standing palisade on its mossy rocks on the right. In the gap between them: the tall crossed poles with the hanging block, slanted props against the wall end beside the warehouse, the stack of pale logs and the pale log on trestles, fallen logs lying toward the viewer, and in front the pile of quarry blocks with the sledge. The scaffold with its ladder stands on the right against the end of the wall, with leaning logs beside it. Through the gap, the roofs of the camp's huts and a wooded hill show in the distance.
```

### B4 — Along the wall

Input: `refs/B4 along the wall ref.png`. Save as `B4 along the wall.png`.

```
View from the bank outside the camp, looking east along the line of the wall toward the plank warehouse, which stays as it is. On the right, a steep grassy bank and, close to the viewer, the poles of the scaffold with its plank platform overhead. In the middle, on a shelf of bare earth: the pile of quarry blocks and the sledge, the pale log on trestles, the stack of pale logs, fallen logs and scattered stones, the ladder and the leaning logs, and behind them the tall crossed poles. On the left, the warehouse's wide plank ramp with handrails runs down toward the river beside a short fence of sharpened stakes; both stay as they are.
```

### B6 — From above

Input: `refs/B6 from above ref.png`. Save as `B6 from above.png`.

```
High view from above the camp, looking down and north onto the gap: a layout view, so keep every object exactly where it lies. The standing palisade runs in from the left on a mossy rock bank and continues on the right into the grey shingle roof of the warehouse. In the gap between them: the row of stumps with the rope and rags across it, two fallen logs and spilled stones running down over the moss toward the hut roofs at the bottom, and on the far side the fallen logs, the scaffold at the left end, the tall crossed poles, the stack of pale logs and the pile of quarry blocks on a shelf of bare earth. Above that, the shore, the dark river and a timber dock; on the right, the warehouse's plank ramp. The red rectangle on a pole at the right is a plain faded dark-red flag with no emblem. The plank roofs of the huts at the bottom stay as they are.
```

---

## Build brief (facts the build keeps)

Measured in the scene on 2026-11-26 (`StartingTown`, `layout.json` point `fence-break` at (3, 52.4)).

| What | Value |
|------|-------|
| Gap | `outer-fence-3/Panel_059` ends at x −0.7, `Panel_060` starts at x 6.7: 7.4 m of open line |
| Ground | Camp yard y 0.5. The wall line is on a bank at y 1.7–1.8; the bank is a flat shelf out to z 57.5, then falls to the shore (y −0.2 at z 60–65) and the water (from z 68, top at y −2). Dock deck at (1, 69) |
| Camp side | A bank of hand-placed mossy rocks fills x −3..10, z 48–54 up to y 2.2–3.0 (`SM_Rock_04_1 (3)`, `(4)`, `SM_Rock_01_1`, `SM_Rock_09 (5)`, scene roots, with colliders): the player already walks up them to the rail. Moving one is an Ask First |
| Room | The yard below is full: player's hut at (−0.3, 47.6), tarp bay at (4.3, 47), hut `worker-hut` at (7.7, 46.1). The only free ground for a work yard is the outer shelf, x −3..9, z 53–58 |
| Outside | The shelf and shore are inside `RA_Docks` (restricted 21–06). The warehouse's plank ramp to the dock starts at x 9 |
| Today's stretch | A rail at 1.1 m on two 1.3 m posts, a 6 m × 8 m collider on Ignore Raycast, a Not Walkable volume 2 m deep |
| Crossing | No invisible collider after the build; the player walks and jumps over (jump height 1.27 m). A Not Walkable strip closes the gap to agents; the work yard stays walkable, because the walk from the warehouse ramp to the dock crosses it |
| Old wood | The palisade kit's logs (0.26–0.32 m, `Palisade.json`), `M_Camp_PalisadeLog` |
| Tool | Camp Edge owns `CampEdge/BrokenStretch` and rebuilds it on every run, fitted to the ground |

### Where the fire shows from (measured, eye height 1.6 m)

The hunter's fire is at (−24, 3, 109.2), 60–75 m away. A render test from every metre of the yard (2026-11-26, before
the build) gave:

- **Fully visible** only from the rock bank and the foot of the gap: x 1..8, z 47–51.
- **A sliver** from one lane about 1 m wide running back through the yard: (6, 38), (5–6, 40), (7–9, 33–35).
- **Hidden** everywhere else: by the tarp bay's canvas (no collider, so a physics ray passes through it), by hut
  `worker-hut`, by the rock `SM_Rock_01_1`, and by birch trees on the near bank.
- A physics ray alone is misleading here: cloth and trees have no colliders. Test the view with a render.
- A sight ray from the yard passes only 0.3–0.5 m above the bank at the wall line, so anything on the shelf hides
  the fire from there. From the rock bank the eye is about 1.6 m above the rocks, and rays pass 1.5–2 m above the
  shelf: only tall things matter (in capture B2 the log on trestles half-hides the fire because that capture's eye
  was low).
- **After the build** (render test, 2026-11-26): the fire shows in full from the rock bank over x 1.5..7, z 50–52,
  and the clear rays cross the wall line from x −0.6 to x 5.8, a window 6.3 m wide. The scaffold and the hoist stand
  at the west edge of that fan; the quarry stone, the sledge and the dragged logs are under it.

---

## Verdict log

| View | Generated | Verdict | Notes (what to keep, what the game overrides) |
|------|-----------|---------|-----------------------------------------------|
| B5 at the breach | 2026-11-26 (user) | Accepted | Used as the second reference image for B2, B3, B4 and B6 |
| B1 from the hut yard | 2026-11-26 (user) | Accepted | |
| B2 night, fire across the river | 2026-11-26 (Meshy) | Accepted | Fire kept as a small point under the log on trestles (the build moves that log out of the line). Adds a water-filled washout channel running into the camp |
| B3 from the shore | 2026-11-26 (Meshy) | Accepted | Invents a row of stumps, the fire basket and the stool in the foreground (they stand at the gap); the rope with rags hangs high between the wall ends |
| B4 along the wall | 2026-11-26 (Meshy) | Accepted, with the notes below | Closest to the capture. Fire basket and stool moved beside the scaffold; a second fallen pair of lashed logs in the foreground. **Override:** the stack and the quarry blocks crowd the head of the warehouse ramp, and the new logs are thinner and shorter than palisade logs |
| B6 from above | 2026-11-26 (Meshy, second try) | Accepted | First try rejected by the user (river and dock became a stream between rocks, the tarp bay a log on trestles). The second names the river, dock, shore, ramp and tarp as staying, and keeps the layout. The stack still lies across the head of the ramp, as in the capture: the build moves it |

Generated through Meshy (`meshy_image_to_image`, `nano-banana-pro`, 16:9, 9 credits each): the capture as the first
reference file, the accepted B5 as the second, and the template with "the first input image" in its second line.

**Decisions taken from the artworks:**

- **The way to the warehouse stays clear** (user, 2026-11-26, on B4): nothing of the work yard stands on the walk from
  the shelf to the warehouse's plank ramp or on the ramp's head. The captures' log stack (x 3.6..8.6, z 55.6–56.6) and
  quarry pile are too close to it: the build keeps a lane at least 2.5 m wide to the ramp and puts the stack and the
  stone west of it.
- **New logs are palisade logs** (user, 2026-11-26, on B4): the worked logs are the same trunks as the wall, same
  thickness (0.26–0.32 m), the same length a wall log needs and the same axe-cut point, only pale and clean. Not thin
  poles or short billets.
- **A worksite people can move through** (user, 2026-11-26: "views are fine, but be careful about having props in
  the path; it should make sense from a worksite standpoint, where workers can navigate the area"): props stand
  beside the walks, never on them.
- **The fire is promised from the rock bank at the breach**, not from the yard (user, 2026-11-26): the yard keeps
  its sliver, and the tarp bay stays where it is.
- The other look questions were left as the captures show them: it fell both ways, the middle is torn out, the
  scaffold and the hoist stay, the closure is a rope with rags, the work yard is outside. A finishing pass may follow.

---

## As built (first pass)

| What | Built |
|------|-------|
| Old wood | Eight splintered stumps (0.3–0.85 m), five leaning end logs standing 0.9 m in the ground, four broken lengths (two fallen into the camp over the rocks, two dragged aside on the shelf); the palisade's log sizes and `M_Camp_PalisadeLog` |
| Middle | Torn out for 3.4 m between the stumps: the 2.5 m walk through the gap |
| Repair works | Pole scaffold at the west end (deck at 2.1 m, ladder on its east side), a hoist of two crossed poles with rope and block over the old line, west of the walk (its legs lean, and would narrow a walk under it), three props, a hanging rail |
| Stone | Eleven spilled footing stones (eight down the camp side); seven quarry blocks and a sledge with one more on the shelf |
| Closure | A rope with seven rags between two stakes, 0.45 m inside the old line; the stakes stop a walker, the rope does not |
| Watch | A stool and a cold fire basket at the east end |
| Walks | Three sets of boards on the mud from the gap to the ramp's foot; the walk is 2.5 m wide and clear of every collider |
| New logs | Five 6.2 m palisade logs stacked on two bearers, one on two trestles with chips, on the west shore (world x −3..−10, z 62–66), where the user moved them by hand to free the docks' other entrance; `M_Camp_NewLog` |
| Ground | The shelf sunk by up to 0.3 m outside the old line and painted Mud |
| Size | 60 parts, 37 meshes, 3,868 triangles, 60 colliders |

**Differs from the artworks:**

- The new logs are on the shore, not on the shelf beside the hoist: the warehouse ramp's foot lands on the shelf, and
  a 6 m log anywhere on it crosses a walk.
- No washout channel with standing water, no wood chips or tools on the shelf, no fallen logs still tied in pairs,
  no fallen log lying toward the water, and the log on the trestles is a finished pointed log, not a half-cut one.
- Eight of the eleven spilled stones lie on the camp side; the artworks spill them both ways.
- The scar is a shallow hollow and a change of ground paint; the camp side of the gap is the same mossy rocks as
  before (they are hand-placed and were not moved).
- Stones are blocks with knocked corners on the warehouse's rubble texture; the quarry blocks read dark in shade.
- The player walks through the middle without having to jump: it is an open door at night until the guard exists.
