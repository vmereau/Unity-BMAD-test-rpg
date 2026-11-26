# Palisade Concepts — reference captures, prompts and build brief

> Read when generating or judging a palisade artwork (P1–P6), or before building the palisade log kit.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder).
> Spec: `spec-area-1-palisade-rebuild`.

The palisade is the timber wall round the Company camp: 13 lines, about 420 m, 165 panels
(`Area1_Blockout/CampEdge`). This pack fixed its look before the build: six captures of the graybox placeholder, one
image-to-image prompt per capture, and the facts the build had to keep.

**Built on 2026-11-26** (`spec-area-1-palisade-rebuild`): every panel draws a wall of logs assembled by the Camp Edge
tool from a Blender kit. How it is built: `Editor/World/CLAUDE.md` → Palisade log walls. What differs from the
artworks is listed under *As built*.

---

## Starting direction

Already painted this way in `Artworks/Starting area/Camp/Warehouse/W4 west end palisade`, `Morning gate` and `Arrival`:

- **One row of upright round logs**, debarked, set tight side by side, each sharpened to a blunt axe-cut point.
  Tops at slightly uneven heights; on a slope the tops step with the ground.
- **Held by rails and lashings, on the camp side only.** Two horizontal split-log rails, pegged, with tarred hemp
  lashings every few logs. The outside face is bare logs.
- **Same wood family as the huts and the warehouse**: weathered grey-brown, never new; darker and damp at the foot.
- **Rougher than the warehouse, better than the huts.** It is Company work: straight and tight, but built fast from
  whole trunks, so no two logs match.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they become the kit's sizes.

1. **Log thickness**: thick trunks (about 0.3 m, 10 logs per panel) or slimmer poles (about 0.2 m, 15 per panel).
2. **Tops**: long sharp points, short blunt points, or a mix with a few flat-sawn tops.
3. **Top line**: nearly level, or clearly ragged (up to 0.4 m between neighbours).
4. **Rails and lashings**: how visible from inside; rope, iron, or pegs only.
5. **Tone**: how much darker than the hut boards; how much moss and tar at the foot.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Palisade/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Palisade/`).
4. Generate **P1 first** and judge it before the others: it sets the look the rest must match. If the tool takes a
   second reference image, add the accepted P1 (or `Warehouse/W4 west end palisade`) to every later view and append
   this sentence to the template's second line:
   `The second image is a material and construction reference only: copy its logs, rails and wood, never its camera or layout.`
5. Fill the verdict log at the end.

In every capture the flat dark-brown walls are the palisade placeholders. The board huts, the warehouse, the rocks,
the trees and the log gate leaves are final; terrain keeps its shape but not its surface. Figures standing with
their arms out are placeholders for people.

---

## Template (same for all six views)

Keep every line **verbatim**; only `{VIEW}` changes.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every structure exactly as they are. Keep the shapes of the terrain, the rocks and the trees, and repaint their surfaces; ignore the hard cast shadows of the input. The flat dark-brown walls are placeholders: replace each one with the palisade described below, keeping its line, its length and its height. Any other flat brown, pale or grey block not described below is a plain weathered timber shed, shelter or fence of the same camp: keep its size and position and paint it in the same wood. Do not add, move or remove buildings.
{VIEW}
The tall flat walls are the palisade of a trading company's labour camp: a single row of upright round logs, stripped of their bark, standing tight side by side with no gaps between them, about five metres tall, three times the height of a man. Every log is a whole trunk as thick as a man's thigh, slightly tapered, with knots and long drying cracks, its top hacked with an axe into a short blunt point; the tops stand at slightly uneven heights, and where the ground rises or falls the top of the wall steps with it, log by log. The logs are sunk straight into the earth, with trampled mud and a few tufts of grass at their foot. On the side facing the inside of the camp, two rough horizontal rails of split log run along the wall, one at chest height and one near the top, pegged to the logs and tied with dark tarred hemp rope every few logs. The side facing outward is bare logs only. Weathered grey-brown wood, sun-bleached near the tops, darker and damp near the ground, stained black with pitch at the foot, with patches of moss low on the shaded side.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: weathered wood browns and greys, stone grays, mossy greens, earth browns; no saturated colors, no glow.
No masonry, no stone wall under the logs, no brick, no plaster, no planks in the wall, no gaps between logs, no walkway or platform on the wall, no battlements, no roof on the wall, no new towers, no iron spikes, no skulls, no banners on the wall, no fresh or new-looking timber, no fire, no smoke, no fog, no light rays, no carts, no animals, no new people, figures stand in natural working poses with their arms down, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### P1 — North wall from the huts (the look-setting view)

Input: `refs/P1 north wall from the huts ref.png`. Save as `P1 north wall from the huts.png`.

```
View from the workers' yard inside the camp, looking north. The palisade runs across the whole picture along the top of a mossy rock bank, well above the yard, and its inside face with the two rails is toward the viewer; its top steps up toward the right. The board hut on the left and the straw bed in the foreground stay exactly as they are. The low brown frame with a pale roof in the middle is a small lean-to shelter of rough planks; the long low brown block right of it is a plank bench against a board windbreak. The pale and brown blocks at the right edge are the corner of another board hut like the one on the left.
```

### P2 — Arrival gate from outside

Input: `refs/P2 arrival gate from outside ref.png`. Save as `P2 arrival gate from outside.png`.

```
View from the muddy road outside the camp, looking in through the gateway. The palisade stands on a grassy bank on both sides of the road, its bare outside face toward the viewer, the top stepping with the ground. The road passes through an open gap between the two wall ends; there are no gate leaves. The two taller square blocks standing at the wall ends, one each side of the gap, are plain gate towers of heavier upright logs lashed together, a head taller than the wall, with nothing on top. Through the gap the camp shows: board huts, a muddy yard and faded dark-red flags on poles, which stay where they are.
```

### P3 — Inner gate

Input: `refs/P3 inner gate ref.png`. Save as `P3 inner gate.png`.

```
View inside the camp from the workers' side, looking south down the muddy main street to the inner gate. The palisade crosses the whole picture from left to right, its bare outside face toward the viewer, with one gateway in the middle; the two gate leaves of lashed logs standing open in it stay exactly as they are. The red rectangles on poles are plain faded dark-red flags with no emblem. The low enclosure of dark upright logs on the left with a cart in front, the plank platform on posts in the right foreground and the trestle tables on the right stay as they are. Beyond the wall, the grey blocks on the hill are a plain grey stone fort with one square tower, and the house on the hill stays as it is.
```

### P4 — South-west wall from outside

Input: `refs/P4 south-west wall from outside ref.png`. Save as `P4 south-west wall from outside.png`.

```
View from the edge of the wood outside the camp, on level grass. The palisade crosses the whole picture behind the birch trunks, its bare outside face toward the viewer, the top rising in two steps toward the middle. The birch trees in the foreground stay exactly as they are. The small grey block showing above the wall on the right is the top of a distant square stone tower.
```

### P5 — Hill palisade from below

Input: `refs/P5 hill palisade from below ref.png`. Save as `P5 hill palisade from below.png`.

```
Low view from the foot of a steep rock outcrop, looking up. The palisade runs along the very rim of the mossy cliff and curves away to the left, its bare outside face toward the viewer, the logs standing right at the edge of the rock with their feet wedged among stones. The cliff face, its moss and the branches in the upper right corner stay as they are. A short length of the same wall shows low at the right edge.
```

### P6 — Construction detail at the warehouse

Input: `refs/P6 construction detail ref.png`. Save as `P6 construction detail.png`.

```
Close view from inside the camp, looking up at the place where the palisade meets the warehouse. The plank warehouse on the left stays exactly as it is, with its posts, braces and lantern bracket. The palisade fills the right half of the picture, its inside face toward the viewer, close enough to show the grain of the logs, the axe-cut points, the two split-log rails, the wooden pegs and the tarred rope lashings. The wall butts into the middle of the warehouse's end wall, against a round log post of the same size. The narrow taller block standing in front of the wall is one heavier log post where a second wall leaves at a right angle. The two barrels in the foreground stay as they are.
```

---

## Build brief (facts the build keeps)

Measured on the placeholder (`Editor/World/Area1BlockoutBuilder.BuildPalisade`, scene `Area1_Blockout/CampEdge`).

| What | Value |
|------|-------|
| Lines | 13, from `layout.json`: `outer-fence` (80 m, 30 panels), `outer-fence-stub` (5 m, 3), `outer-fence-2` (28 m, 11), `outer-fence-3` (51 m, 19), `outer-fence-4` (13 m, 5), `outer-fence-5` (34 m, 12), `hill-palisade` (74 m, 32), `inner-fence-west` (47 m, 16), `inner-fence-east` (50 m, 17), `dock-fence-upper` and `-lower` (6 m, 2 each), `compound-fence-upper` (17 m, 7), `compound-fence-lower` (25 m, 9) |
| Panels | 165 boxes named `Panel_NNN` (numbered across lines), up to 3 m long plus 0.3 m overlap at each end, 0.6 m thick |
| Height | Top = the higher of (mean ground + 5 m ± 0.3 m jitter) and (highest ground under the panel + 3 m); bottom 1.5 m under the lowest ground. Boxes are 6.2–14.0 m tall |
| Gameplay | Hut ridges stay under y 3.6 and the player jumps 1.27 m: no panel top may come down |
| Hand edits | `hand-edits.json` pins 31 panels (position and size, several raised by hand) and removes `outer-fence/Panel_001` and `_002` (the fields gate) |
| Warehouse | `outer-fence-3/Panel_060` ends at x 9.31 and `Panel_061` starts at x 20.69, inside the gable walls; the round junction logs stand at the warehouse's local x ±5.86, z 0 |
| Openings | Arrival gate (two graybox towers, `CampEdge/ArrivalGate`), inner gate, dock gate, compound gate, ramp and fields gates, fort cart gate: all stay as they are; the wall ends on a full log at each |
| Stays graybox | The arrival towers, every gate. (`CampEdge/BrokenStretch`, the 8 m collapsed stretch west of the warehouse, was left graybox by this pack and dressed later: `BROKEN_STRETCH_CONCEPTS.md`) |
| Tool | Camp Edge owns the root and rebuilds it on every run; the wall follows the ground, so it is rebuilt after any terrain run |

---

## Verdict log

| View | Generated | Verdict | Notes (what to keep, what the game overrides) |
|------|-----------|---------|-----------------------------------------------|
| P1 north wall from the huts | 2026-11-26 | Accepted | Slim poles and thin rails: reads lighter than P6. The stepping top line along the bank is right |
| P2 arrival gate from outside | 2026-11-26 | Accepted | Rails painted on the outside face: the game keeps them on the camp side. Tower logs lashed in a cluster: gate spec |
| P3 inner gate | 2026-11-26 | Accepted | |
| P4 south-west wall from outside | 2026-11-26 | Accepted | Very regular logs and a level top; pale slits between logs are not kept (no gaps) |
| P5 hill palisade from below | 2026-11-26 | Accepted | |
| P6 construction detail | 2026-11-26 | **Best: the build's reference** | Thick trunks, short points, pole rails, rope lashings, a heavier flat-topped post |

**Decisions taken from the artworks** (user, 2026-11-26: "P6 palisade looks best"):

- **Log thickness:** thick trunks, about 0.3 m (0.26–0.33), roughly ten per 3 m panel.
- **Tops:** short blunt axe-cut points, about 0.3 m high, a few facets each; no flat-sawn tops on ordinary logs.
- **Top line:** mildly ragged, up to about 0.25 m between neighbours; it climbs with the ground.
- **Rails and lashings:** two round poles (about 0.11 m), camp side only, one at chest height and one under the
  tops; tarred rope lashings where a rail crosses a post. No iron.
- **Posts:** a heavier log (about 0.4 m) with a flat sawn top, slightly taller than its neighbours, where rails join:
  one per panel joint.
- **Tone:** the warehouse's wood, grey-brown with knots; dark and damp over the lowest half metre.

---

## As built

| Decision | Built |
|----------|-------|
| Log thickness | Six log heads, 0.26–0.32 m across, pressed at least 10% into each other: ten or eleven per panel |
| Tops | Points 0.27–0.36 m high, eight facets cut in pairs; the lowest cut of every log is 2 cm above the panel's box top, the apex 0.3–0.6 m above it |
| Top line | Up to 0.2 m between neighbours; a lower panel climbs to its taller neighbour over its last 1.5 m |
| Rails | Two 0.13 m poles on the camp side: 1.25 m above the camp-side ground, and 0.75 m under the lower of the two tops at each joint. A wall under about 3 m gets one |
| Posts | 0.4 m, flat sawn top a little out of level, 0.3–0.4 m above the top line, one per panel joint |
| Lashings | Two crossed rope loops round each post and rail; the rope shows on the outside face too, where it passes round the post |
| Tone | `M_Camp_PalisadeLog` (the huts' wood at 1.2), `M_Camp_PalisadeFoot` (0.8, slightly green) over the lowest 0.3 m |

**Differs from the artworks:**

- The wood is the huts' plank texture wrapped round a trunk: long streaks, no knots, and a visible repeat on a long
  flat stretch seen from far (P4). A dedicated trunk texture would fix both.
- No moss and no pitch at the foot: only a slightly darker band with a ragged upper edge.
- The posts read less than P6's heavy post, and the rails are straight poles, 0.13 m against the 0.11 m decided.
- The darker foot is 0.3 m high, not the half metre decided: a taller band read as paint.
- Rails are on the camp side only (P2 painted them outside).
- The arrival towers and every gate are still graybox (own specs).
