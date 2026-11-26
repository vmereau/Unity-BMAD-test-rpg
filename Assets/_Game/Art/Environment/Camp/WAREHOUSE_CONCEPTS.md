# Warehouse Concepts — reference captures, prompts and build brief

> Read when generating or judging a warehouse artwork (W1–W6), or before rebuilding the warehouse in Blender.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder).

The warehouse is the Company's storage hall over the starting corner of the camp: a graybox today
(`Area1_Blockout/Warehouse`, built by the Sites tool). This pack fixes its look before the rebuild: six captures of
the placeholder, one image-to-image prompt per capture, and the facts the rebuild must keep.

---

## Chosen direction

- **Timber on posts, open underneath.** A plank hall on heavy squared posts standing on padstones on the rock clump;
  daylight and bare rock show between the posts. No masonry storey: cut, seamless stone is the ruins' signature
  (`ART_DIRECTION.md` → Environment).
- **Rubble only on the fence line.** Under the middle of the hall, where the palisade would run, a rough dry-stone
  wall of local rubble fills the gap between rock and floor. It replaces the placeholder's `Skirt`.
- **Better kept than the huts, through construction.** Squared sawn posts and braces, tight boards, a full shingle
  roof, strap-hinged cart doors, tarred post feet. Same wood family as the huts: weathered, never new.
- **One line with the palisade.** The fence meets the hall in the middle of each gable end, under the ridge: the post
  there is a round log of palisade size and the fence butts into it. Every other post is squared.
- **Ramps are carpentry.** Plank decks on trestles with a low kerb, not floating slabs. Never stairs: carts use them.

Older concept of the same building, seen from outside the dock gate: `Artworks/Starting area/Camp/Docks/Dock checkpoint.png`.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Warehouse/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Warehouse/`).
4. Generate **W1 first** and judge it before the others: it sets the look the rest must match. If the tool takes a
   second reference image, add the accepted W1 to every later view and append this sentence to the template's second
   line: `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`
5. Fill the verdict log at the end.

In every capture the flat brown boxes are placeholders: the hall, the fence walls, the two sloping ramps, the landing,
the hoist, the dock gate. The huts and rocks are final; terrain and water keep their shapes but not their surfaces.

---

## Template (same for all six views)

Keep every line **verbatim**; only `{VIEW}` changes.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every building exactly as they are. Keep the shapes of the terrain, the rocks and the river, and repaint their surfaces; ignore the hard cast shadows of the input. The flat brown blocks are placeholders: replace each one with the real structure described below. Any other flat brown or grey block not described below is a plain weathered timber fence, gate or shed of the same camp: keep its size and position and paint it in the same wood. Do not add, move or remove buildings.
{VIEW}
The large building is the trading company's warehouse: a long timber hall raised on a clump of grey rock, its floor about four metres above the camp. Heavy squared posts of pitch-stained dark oak stand on flat padstones on the rock, tied together with diagonal braces and cross beams. The space under the floor stays open between the posts, with daylight and bare rock showing; only deep under the middle of the hall, on the line of the palisade, a rough dry-stone wall of grey rubble fills the gap between rock and floor. The walls are tight horizontal weathered planks between the posts. The gabled roof keeps the shape and pitch of the input and is covered in split wooden shingles, grey and weathered, with a few paler shingles where it was mended. Each long wall has one wide cart doorway, set to one side of the middle exactly where the dark opening is in the input, with a heavy plank double door on blackened iron strap hinges, standing open, and an empty iron lantern bracket beside it. Sun-bleached grey boards, rain streaks under the eaves, mud splashed on the post feet, tar on the lower posts.
The tall flat walls are the camp palisade: upright debarked logs about six metres tall with sharpened tops, pegged and lashed to rails, dark and weathered. Where the palisade meets the hall it butts into the middle of the hall's end wall, under the ridge, against a round log post of the same size. The sloping flat boards are cart ramps: plank decks three and a half metres wide on timber trestles, a low kerb beam along each edge, cross battens for grip and two pale wheel tracks worn down the middle. Where a pole with a red cloth shows in the input, it is a plain faded dark-red flag.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: weathered wood browns and greys, stone grays, mossy greens, earth browns; no saturated colors, no glow.
No masonry walls, no stone foundation storey, no cut stone blocks, no brick, no plaster, no half-timbered infill, no towers, no windows, no stairs, no steps, no tall handrails, no fresh or new-looking timber, no fire, no lit lanterns, no smoke, no fog, no light rays, no carts, no animals, no emblems on the flag, no new people, small distant figures stand in natural working poses, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### W1 — Camp side (the look-setting view)

Input: `refs/W1 camp side ref.png`. Save as `W1 camp side.png`.

```
Raised view from the south-east of the camp; the workers' huts fill the lower left and stay exactly as they are. The warehouse's long south wall faces the viewer with its cart doorway; a wide cart ramp climbs from the yard in the foreground straight up to that doorway, over the grey rock clump. Right of the hall the camp palisade runs to a gate of pointed stakes, with two barrels and a bench at its foot. Left of the hall there is a gap: only the flag pole and a low broken fence rail, with the river visible through it. Keep the gap open. The tall brown walls further left are the palisade on the far side of the camp.
```

### W2 — River side

Input: `refs/W2 river side ref.png`. Save as `W2 river side.png`.

```
View from the river bank north-west of the warehouse. The hall's long north wall faces the viewer with its cart doorway; outside it a plank landing stands on tall posts over the falling bank, and from the landing a cart ramp runs down to the right, along the wall and past the hall's west end, to the muddy bank. At the hall's left corner a timber hoist arm stands out over the bank; left of it, the two posts and beam are a plank gate standing open, with a small guard shelter of rough planks beside it at the water. At the far left edge a timber pier stands in the river and stays as it is. The camp's roofs show in the distance on the right and stay as they are.
```

### W3 — Dock gate and hoist

Input: `refs/W3 east end hoist ref.png`. Save as `W3 east end hoist.png`.

```
View from the river bank path north-east of the warehouse, outside the dock gate. The hall's river-side wall with its cart doorway is seen at an angle; the east gable is mostly hidden behind the palisade. On the near corner of the hall a timber hoist stands out from the wall: a braced post, a horizontal arm, a wooden pulley block and a hemp rope hanging down the bank. In front of the doorway the plank landing on posts; right of it the river ramp runs down and away. The brown walls in the foreground are the palisade and the open plank leaves of the dock gate, coming down to the water between large dark boulders, which stay as they are; the low brown frame in the left foreground is a lean-to guard shelter, a sloping plank roof on short posts. The small pier at the far right edge stays as it is.
```

### W4 — West end and palisade junction

Input: `refs/W4 west end palisade ref.png`. Save as `W4 west end palisade.png`.

```
View from high ground inside the camp, west of the warehouse, at the height of its floor. The hall's west gable end faces the viewer: vertical plank cladding in the gable and a small shuttered loading hatch under the ridge. The tall brown slab against the left half of this end wall is the last short length of the camp palisade, butting into the round log post in the middle of the end wall. Behind it, the long brown slope running down to the left is the river-side cart ramp. The thin low rail in front is a broken stretch of fence: one rail on short posts. Under the hall the grey rock clump shows, with the post feet on padstones and open space between them. On the right the camp ramp runs down from the cart doorway; the hut roofs in the foreground stay as they are, and the small house and the waterfall in the right distance are background.
```

### W5 — Overview

Input: `refs/W5 overview ref.png`. Save as `W5 overview.png`.

```
High view from the south-east over the corner of the camp. The warehouse stands in the middle on its rock clump, its shingled roof seen from above, ridge running left to right. The long brown slope coming toward the viewer is the camp cart ramp, from the cart doorway down to the yard between the huts. Right of the hall the palisade runs to a gate of pointed stakes at the edge of the bank, and a second line of palisade runs behind it down to the river. Left of the hall there is a gap with only the flag pole and a low broken rail; keep it open. The tall brown wall curving away at the upper left is the palisade on the far side of the camp. Behind the hall the river, with the hoist arm standing out over the bank at the hall's far right corner. The huts, the barrels, the bench and the trees stay as they are.
```

### W6 — Camp ramp and rock base (construction detail)

Input: `refs/W6 camp ramp and rock ref.png`. Save as `W6 camp ramp and rocks.png`.

```
Low view from the rocks beside the camp ramp, looking up at the warehouse's south wall. The cart ramp crosses the picture from lower left to the cart doorway at upper right: its underside shows the trestles, two long stringer beams, cross joists and the plank deck above, with the kerb beam along its edge. Behind it, the grey rock clump with the hall's post feet on padstones and the diagonal braces, open between the posts. The big lichen-covered boulders in the right foreground stay as they are. On the left, above the ramp, the palisade and hut roofs; under the ramp, a hut wall and a straw bed on the ground, which stay.
```

---

## Build brief (facts the rebuild keeps)

Measured on the placeholder (`Editor/World/Area1CampInterior.BuildWarehouse`, scene `Area1_Blockout/Warehouse`).

| What | Value |
|------|-------|
| Floor | 12 × 8 m (x × z), top at y 4.65, centre (15, 52); the highest ground in the footprint |
| Walls | 4 m high, 0.4 m thick; ridge east–west, roof pitch 30°, ridge about y 11.2 |
| Roof overhang | 0.5 m past the gable ends, 0.26 m at the eaves (placeholder; a deeper eave must clear the 2.8 m door head and the east yard's ramp tarp) |
| Cart doorways | One per long wall, 2.4 m wide, 2.8 m high, centred 2 m east of the middle (x 17) |
| Camp ramp | 3.5 m wide, 13.2 m plan run (deck 13.85 m), 4.2 m drop, 17.6°, from the south doorway down to (17, 34.8) |
| Landing | 6 × 3.8 m outside the north doorway, on posts 3.8–4.2 m long |
| River ramp | 3.5 m wide, 10 m plan run (deck 10.4 m), 16.2°, from the landing west along the wall down to (4, 58) |
| Hoist | North-east corner, arm 2.6 m out over the bank, rope 5 m |
| Posts | Camp side 0.95–3.3 m from rock to floor: the underside is tall, not a crawl space |
| Palisade | Meets the hall on its ridge line (z 52), in the middle of each gable end: the west panel ends at x 9.3 (top y 7.3), the east one starts at x 20.7 (top y 9.7); `dock-fence-upper` leaves at x 24.5 |
| Fence line under the floor | `Skirt`: one wall 12 × 2.1 × 0.4 m on z 52, from the rock up to the floor |
| Flag pole | `CampCentre/Flag_Warehouse` at the south-west corner (8.7, 47.7), top about y 11.6 in the scene (the code aims at 13) |
| Dock gate | `DockGate` posts at x 24.5 (z 57.5 and 60.5) with two leaves, and its `GuardPost` lean-to at (27.7, 57.1): outside the rebuild, but in views W2 and W3 |

Gameplay and tool constraints:

- **The floor, both doorways and both ramps are a route** (camp ↔ docks) on the NavMesh, 3.5 m wide for carts. Keep
  their positions, widths and slopes; trestles and kerbs stay inside the 3.5 m width. Doors stay open and at least
  2.1 m high.
- `RestrictedAreas/RA_Warehouse` covers the interior (owner `NPC_WarehouseGuard`). The guard spawns inside the south
  doorway (17, 4.65, 49.5) and patrols five waypoints (`NpcPatrolRoutes/WarehouseGuard`): camp ramp (17, 3.74, 45),
  camp door, west end (10.5, 52), river landing (17, 58), east end (19.5, 52.5). All five must stay on walkable
  surface, so the interior floor is kept even though the interior is not dressed.
- **The fence line must stay closed under the floor**: Camp Edge clips the palisade at the footprint and the
  `Skirt` closes the gap. The rubble wall takes over that job, visually and as a collider.
- **The gap west of the hall stays open**: `BrokenStretch` (x −1..7, a rail on short posts) keeps the hunter's fire in
  view from the worker huts. No palisade there.
- `HomeYardDresser` ties the east yard's ramp tarp to the camp ramp: keep an **active** object at exactly
  `Area1_Blockout/Warehouse/Ramp_Camp`, with a `BoxCollider`, scaled (3.5, 0.2, 13.85) so that its local ±0.5 are the
  deck's faces (`RampEdge`). Switch its renderer off rather than deactivating it, or change the dresser. Hut-05's
  corner is 0.4 m from that ramp and a floor bed lies under its deck (about 2.9 m up): keep trestles out of that pocket.
- The warehouse root is **owned by the Sites tool**: a run replaces it whole (and would already lose the two
  hand-duplicated posts `Post_Camp_0 (1)` and `Post_Camp_1 (1)`). The rebuild either goes into `Area1CampInterior`
  (as a prefab the tool places) or the root leaves the tool, keeping its name and parent
  (`Editor/World/CLAUDE.md` → Ownership).
- Scope the user chose for the rebuild (2026-11-26): the hall, both ramps, the landing, the hoist, the palisade
  junction and the rock clump dressing. Not the interior. Crates and sacks under the hoist are new dressing (the
  code builds `Crate_0..2`, the saved scene has none).
- Rocks near the building, to check and rearrange in the rebuild: hand-placed scene roots `SM_Rock_06 (20–22)`,
  `SM_Rock_07 (3–4)`, `SM_Rock_08 (5–6)`, `SM_Rock_09 (3–4)`.

---

## Verdict log

Batch 1 was generated on 2026-11-26 from the **first draft** of these prompts (before the review that produced the
template above), so it shows three things the template now forbids: palisade painted into the gap west of the hall,
a centred door in W1, and lit lanterns. The six images agree with each other and are usable as they are; regenerate
a view only if it is needed as a clean reference. "Kept?" is the user's call.

| View | Date | Kept? | Notes (what to carry into the build, what to ignore) |
|------|------|-------|-------------------------------------------------------|
| W1 camp side | 2026-11-26 | | **Carry:** horizontal planks between squared posts with the frame showing, grey shingles with pale mended patches, double plank door on strap hinges, palisade of sharpened logs with two lashing bands, plank ramp with wheel-worn boards. **Ignore:** the door is centred and shut (it is 2 m east of the middle and open); palisade fills the gap left of the hall (`BrokenStretch` stays a low rail); the handrails stand inside the deck (keep 3.5 m clear). |
| W2 river side | 2026-11-26 | | **Carry:** landing and ramp on trestle posts, rubble packed under the floor between the posts, hoist post at the east corner, lantern by the door. **Ignore:** the ramp is drawn about 2 m wide (it is 3.5 m); rain; the grey blocks on the hill are untouched placeholders. |
| W3 east end hoist | 2026-11-26 | | **Carry:** the best view of the structure: landing on squared posts with diagonal braces and padstones, a dry-stone bank of rubble behind them, hoist arm with pulley block over crates and sacks, vertical boards in the gable, a faded banner on the palisade. **Ignore:** the palisade joins at the hall's corner (it meets the middle of the gable end). |
| W4 west end palisade | 2026-11-26 | | **Carry:** west gable with vertical boards and a shuttered hatch under the ridge, the short palisade length butting into the end wall, padstones and rubble on the rock, both ramps as trestle decks. **Ignore:** tall handrails on both ramps if the kerb is chosen. |
| W5 overview | 2026-11-26 | | **Carry:** the whole massing: roof, braced posts over rubble, ramp to the yard, fence to the stake gate. **Ignore:** palisade drawn across the gap west of the hall; the hall reads taller than its 4 m walls. |
| W6 camp ramp and rocks | 2026-11-26 | | **Carry:** ramp construction from below (posts, stringers, deck boards, kerb beam at the edge, no handrail), a dry-stone wall under the floor behind the ramp, door with strap hinges. **Ignore:** nothing structural. |

**Open points the images raise for the build** (decide before the rebuild spec):

1. **Rubble under the floor.** The chosen direction keeps the underside open with rubble only on the fence line; W2,
   W3, W5 and W6 pack rubble between the perimeter posts, 1–2 m high, and it reads well.
2. **Ramp edges.** W1, W2, W4 and W5 show post-and-rail handrails; W6 shows only a kerb beam. Either must leave the
   deck 3.5 m clear.
3. **Lanterns.** Painted lit in daylight; in game they would be night lights (`ClockNightLight`).
