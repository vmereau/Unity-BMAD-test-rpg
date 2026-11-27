# Warehouse Cellar Concepts — reference captures, prompts and build brief

> Read when generating or judging a cellar artwork (B1–B6), or before building the cellar under the warehouse.
> Art rules these prompts follow: `Art/ART_DIRECTION.md`; camp rules: `CLAUDE.md` (this folder); the hall above,
> whose wood and rubble this room shares: `WAREHOUSE_CONCEPTS.md`. Spec: `spec-area-1-warehouse-cellar`.

The cellar is a new room under the warehouse's river landing and the north half of its hall
(`Area1_Blockout/Warehouse`, around (17, 0, 56)). Before this pack there was nothing there: the hall's floor lies on
solid ground and the bank under the landing is bare (the user's sketch: `Artworks/Starting area/Camp/Warehouse/refs/Warehouse basement entrance.PNG`).
This pack fixes its look before the build: six captures with temporary massing shapes, one image-to-image prompt per
capture, and the facts the build must keep.

**Built on 2026-11-27** (`spec-area-1-warehouse-cellar`): the cellar is part of the warehouse's script-built model
and generated prefab, under a hole in the terrain. Pipeline and contract: `Prefabs/World/Warehouse/CLAUDE.md` →
Cellar. The *Build brief* below describes the massing the captures were taken from; what changed is listed under
*As built*. Captures of the build: `Artworks/Starting area/Camp/Warehouse/Cellar/built/`.

---

## The place

- **Who built it.** The Company, when the dock was built: the bank was cut back under the landing and into the rock
  clump the hall stands on. It is older and rougher than the hall above.
- **What it is for.** A cool store for what comes off the boats: casks, salted fish, sacks, crates. Goods go in
  through the dock door and come up into the hall through a hatch.
- **Walls.** The back wall is the cut face of the rock itself. The sides and the front are dry-stone rubble of local
  grey stone, laid rough, with a timber lintel over the door. Never dressed masonry: cut, seamless stone is the
  ruins' signature (`ART_DIRECTION.md` → Environment).
- **Timber.** The hall's posts come down through the room as pillars on padstones: three under the landing, three
  under the hall's north wall. The ceiling is the underside of the hall's floor and of the landing's deck.
- **Dock door.** One plank door in the front wall, between the landing's east and middle posts, a stone step up
  from the mud. A lantern beside it.
- **Hatch and ladder.** A trapdoor in the hall's floor against its north wall, a long ladder under it. In this
  build the trapdoor stays shut; opening it and climbing belong to a later spec.
- **Off limits** to the player at all hours (owner: the warehouse guard), like the hall above.

## What the artworks should settle

Judge these on the results and write the answers in the verdict log: they decide what is modelled.

1. **Back wall**: the cut rock face, or dry-stone all round.
2. **The front under the landing**: how the rubble meets the posts and the deck (posts standing free in front of
   the wall, or built into it), and how the wall ends against the bank on each side.
3. **The door**: planks on strap hinges, single (as captured) or double; how heavy the lintel.
4. **Ceiling**: the bare joists and boards of the hall, or boarded under.
5. **Floor**: packed earth, flagstones or boards over mud.
6. **Lights**: one lantern by the door outside and one inside, or more; how dark the room may be.
7. **How full the room is**: how many casks, crates and sacks before it stops reading as a working store.

---

## How to generate

1. Give the tool the capture as the **input image** (`Artworks/Starting area/Camp/Warehouse/Cellar/refs/<name> ref.png`,
   1920 × 1080). Image-to-image, keeping the composition.
2. Build the prompt: copy the **template** below and paste the view's paragraph in place of the `{VIEW}` line.
3. Save the result beside the `refs` folder under the name given (`Artworks/Starting area/Camp/Warehouse/Cellar/`).
4. Generate **B1 first** and judge it before the others: it sets the look the rest must match. Then add the accepted
   B1 as a second reference image to every later view and append this sentence to the template's second line:
   `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`
5. The interiors (B4, B5) and the night view (B6) replace the template's light line with the one given under the view.
6. Fill the verdict log at the end.

The finished real objects in the captures are the hall, its landing, ramp, handrails and hoist, the palisade, the
crates and sacks under the hoist, the rocks, and in the interiors the dark boards of the ceiling. Everything smooth
and single-coloured is a placeholder:

| Placeholder | Stands for |
|-------------|------------|
| Mid-grey flat walls | Dry-stone rubble walls |
| Dark blue-grey flat wall (interiors) | The cut rock face at the back |
| Light orange-brown boxes | Carpentry: door, lintel, pillars, ceiling under the landing, ladder, hatch frame |
| Small grey slabs under the pillars, grey slab at the door | Padstones, the door step |
| Dull ochre floor | The cellar floor |
| Pale yellow cylinders | Casks |
| Dark brown boxes | Crates |
| Off-white flattened balls | Sacks |
| Small bright yellow box | A lantern |
| Dark brown flat frame and leaves at the palisade (B2, B3) | The dock gate, a plain plank gate of the camp |

---

## Template (same for all six views)

Keep every line **verbatim**; only `{VIEW}` changes.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Repaint the input image. Keep the camera, the composition and the position, size and proportions of every object exactly as they are. Keep the timber hall, its landing, ramp, handrails, posts and braces, the log palisade, the wooden crates and cloth sacks, the rocks, the muddy ground and the river as they are, and repaint only their surfaces. Every smooth single-coloured shape is a placeholder: replace each one with the real object described below, at the same size and place. Do not add, move or remove any building, wall, door or post, and add no object where the input shows none.
{VIEW}
Materials. The mid-grey flat walls are dry-stone walls of rough grey local rubble: uneven stones of many sizes packed without mortar, small stones wedged in the gaps, damp and dark at the foot, moss in the joints, never cut blocks. A dark blue-grey flat wall is the bare rock of the hill, cut back by hand: one rough face with pick marks and natural cracks, no joints. The light orange-brown shapes are carpentry of weathered dark oak, the same wood as the hall above: squared posts on flat padstones, a heavy lintel beam, a door of thick vertical planks on two blackened iron strap hinges with an iron ring handle, a ladder of two poles and pegged rungs. The dull ochre floor is packed earth, damp, with a few flat stones trodden into it. The pale yellow cylinders are old oak casks with dull iron hoops, standing on end. The dark brown boxes are rough plank crates. The off-white flattened shapes are sacks of coarse undyed linen. A small bright yellow box is a plain iron lantern with horn panes on an iron hook.
Overcast sky, cold diffuse daylight, damp air, wet mud. Muted earthy palette: stone grays, weathered wood browns, earth browns, mossy greens; no saturated colors, no glow.
No cut stone blocks, no brick, no mortar, no plaster, no arches, no vaults, no columns, no carved stone, no windows, no stairs, no fresh or new-looking timber, no fire, no smoke, no fog, no light rays, no cobwebs, no skulls, no chains, no animals, no new people, no text, no watermark, no logo, no signature, no frame. Wide 16:9 composition.
```

---

## Views

### B1 — Dock door (the look-setting view)

Input: `refs/B1 dock door ref.png`. Save as `B1 dock door.png`.

```
View from the muddy river shelf, looking straight at the space under the timber landing of the warehouse. Three tall squared posts with diagonal braces carry the landing; they stay exactly as they are. Behind the posts, the mid-grey flat wall fills the whole space from the mud up to the underside of the landing: it is the dry-stone front wall of a cellar. In it, between the left and the middle post, one plank door under a heavy timber lintel, shut, with a flat stone step in front and a lantern on a hook at its upper left, unlit. On the left the wall steps down and ends against the grassy bank; on the right it ends against the wet rock of the bank under the ramp. The ramp, the handrails, the hall above and the crates and sacks at the lower left stay as they are.
```

### B2 — River side

Input: `refs/B2 river side ref.png`. Save as `B2 river side.png`.

```
View from the river bank north-west of the warehouse. The timber hall, its landing on posts, the long cart ramp with handrails coming down to the right, the hoist, the log palisade and the pier stay exactly as they are. Under the landing, the mid-grey flat wall behind the posts is the dry-stone front wall of a cellar, with one small plank door under a timber lintel and an unlit lantern beside it. The dark brown flat frame left of the landing is a plain plank gate of the camp, standing open. The flat grey and red shapes in the far distance on the right are untouched background: paint them as plain weathered timber sheds.
```

Regenerated with B1 as the second image.

### B3 — Overview from the north-east

Input: `refs/B3 north-east overview ref.png`. Save as `B3 north-east overview.png`.

```
High view from the north-east, over the dock gate, down onto the river side of the warehouse. The hall with its shingled roof, the landing and ramp with handrails, the hoist at the hall's near corner, the log palisade and the big dark boulders stay exactly as they are. Under the landing the mid-grey flat walls are the dry-stone front and side wall of a cellar built into the bank: the side wall steps down toward the viewer and its top is rough rubble with grass growing over it. One plank door with a stone step and an unlit lantern. The dark brown flat frame, leaves and bench in the left foreground are the open plank dock gate and a rough plank bench.
```

Regenerated with B1 as the second image.

### B4 — Interior from the door

Input: `refs/B4 interior from the door ref.png`. Save as `B4 interior from the door.png`.

```
Inside the cellar, standing in the doorway and looking toward the back. The room is about six metres square and four metres high. The side walls are dry-stone rubble; the far wall, dark blue-grey in the input, is the cut rock face. The ceiling of dark boards is the underside of the hall's floor and stays as it is. One squared oak pillar stands on a padstone in the middle; parts of two more show at the left and right edges. At the upper right a long ladder leans up to a square trapdoor frame in the ceiling, which is shut. Against the far wall on the right, casks standing two high; in the far left corner, three crates; between them, a small heap of three sacks on the earth floor.
```

Light line for this view: `Dim interior: cold grey daylight falls in from the open door behind the viewer and fades toward the back; deep soft shadows, no other light. Muted earthy palette: stone grays, weathered wood browns, earth browns; no saturated colors, no glow.`

Generated with B1 as the second image.

### B5 — Interior toward the door

Input: `refs/B5 interior toward the door ref.png`. Save as `B5 interior toward the door.png`.

```
Inside the cellar, standing at the back and looking toward the dock door. The walls are dry-stone rubble. Five squared oak pillars on padstones stand in the room, the nearest dark in the foreground. In the far wall the plank door stands open inward under its timber lintel, showing wet mud and the grey river outside. An unlit lantern hangs on the pillar left of the door. The light orange-brown ceiling is the underside of the landing's deck: dark joists and boards. On the right, one crate against the wall; at the lower left, two sacks on the earth floor and a flat stone lying in the corner.
```

Light line for this view: `Dim interior: cold grey daylight falls in through the open door and across the earth floor; the corners stay dark; no other light. Muted earthy palette: stone grays, weathered wood browns, earth browns; no saturated colors, no glow.`

Generated with B1 as the second image.

### B6 — Dock door at night

Input: `refs/B6 dock door night ref.png`. Save as `B6 dock door night.png`.

```
Night view from the river shelf, east of the cellar door. Three tall squared posts with diagonal braces carry the timber landing; they, the ramp on the right, the hall above and the crates and sacks at the lower left stay exactly as they are. Behind the posts the flat wall is the dry-stone front wall of the cellar; at the left it steps down and ends against the grassy bank. Between the first and second post, one shut plank door under a heavy timber lintel, with a flat stone step. Left of the door a lit iron lantern hangs on a hook: it is the only light.
```

Light line for this view: `Night, overcast, no moon: the lantern throws a small warm light on the door, the nearest post and the mud in front, and everything else falls off into cold blue-black. Muted earthy palette, the lantern's fire-orange the only warm accent; no glow, no bloom.` For this view also remove `no fire,` from the template's last line.

Generated with B1 as the second image.

---

## Build brief (facts the build keeps)

Sizes of the massing in the captures, in world coordinates. The hall's floor is at y 4.65, the dock shelf at y −0.2.

| What | Value |
|------|-------|
| Room (inside faces) | x 13.8..19.9, z 50.1..59.0 (6.1 × 8.9 m; first build 6.1 × 6.4 m from z 52.6), floor y 0.1, ceiling y 4.33 (4.2 m clear) |
| Back wall (rubble; first build cut rock at z 52.6) | z 48.9..50.1, x 12.6..20.6 |
| West wall | x 12.6..13.8; top y 4.33 under the hall, 4.15 under the ramp's head |
| East wall | x 19.9..21.1; full height under the hall; north of z 56 a full-height strip x 19.9..20.3 and a shoulder x 20.3..21.1 with its top at y 3.5 (the hoist post's foot stands there, at (20.83, 3.5, 56.5)) |
| Front wall | z 59.0..59.35, behind the landing's front posts (z 59.6) |
| Door opening | x 17.8..19.1 (1.3 m), y 0.1..2.2 (2.1 m), lintel 0.25 m, step y −0.04 on the shelf side |
| Pillars | Landing's middle row at (14.25 / 17 / 19.75, z 57.9); new row under the hall's north wall at z 55.85 with a beam under the sill |
| Hatch | x 14.2..15.2, z 54.3..55.3 in the hall's floor; ladder foot at (14.7, 0.1, 54.15), leaning 14° |
| Lights | Inside on the middle pillar's east face at y 2.2; outside at (19.3, 2.1, 59.45) |
| Terrain hole | Cells x 526..532, z 565..571 of the 1024 hole map: world x 13.67..20.51, z 51.76..58.59 |

Proved on 2026-11-27 with the hole cut in memory (nothing saved):

- **Collision:** the terrain collider is gone inside the hole; rays from inside the room land on the room's floor and
  walls.
- **NavMesh:** a local bake puts NavMesh on the cellar floor, links it to the shelf through the open doorway
  (shelf → back wall: complete path through the door) and leaves the hall's floor and the landing as they were.
  No NavMesh appears on the removed ground.
- **Light:** with a sun and no fill, two thin sun stripes reach the floor through gaps in the timber above. No leak
  along the hole's edge.
- **Not proved yet:** the third-person camera in the room (Play, first build).

Constraints:

- **A strip of ground stays inside the room** between the hole's north edge (z 58.59) and the front wall: at
  x 14..15.5 it rises to y 0.2–0.5, above the floor (the grey wedge at the lower left of capture B5). The build
  needs a floor that covers it, a thicker wall foot there, or a small dip in the ground (a terrain change: ask).
  The hole cannot reach further north: its next row of cells ends at z 59.57, outside the wall.
- **The old landing posts and rubble** of the model end at the old ground and float in the room: the script must
  carry the middle posts down to the floor and drop the rubble course under the north wall over the room's width.
- **The fence line stays closed:** the collider on z 52 (`Colliders/Rubble/Skirt`) is behind the back wall and
  stays; `Skirt_River` opens only over x 13.8..19.9.
- **The guard's route and the hall are untouched:** floor, doorways, ramps, landing, `Ramp_Camp`, five waypoints.
- **The hoist goods** (x 21.3..23.2) clear the east wall's outer face (x 21.1): they need not move.
- The room is 0.5 m narrower than first planned (6.1 m instead of 6.6 m) so that the east wall stands under the
  landing's edge and no strip of sky opens between them.
- `RA_WarehouseCellar` goes inside the room's faces, y 0.1..4.3. `RA_Docks` covers the same volume at night.
- The warehouse root is owned by the Sites tool: the cellar goes into `build_warehouse.py` and the generated prefab.

### As built

- **Pillars:** the row under the hall's north wall is the hall's own two door posts (x 15.67 and 18.33), carried
  down to the cellar floor, not three new posts at x 14.25 / 17 / 19.75. The east one stands 3 m straight in from
  the dock door: the walk goes round it.
- **Door:** the leaf is the game's `Poor small door` (1.1 m wide), so the opening is 1.14 m (x 17.88..19.02), not
  1.3 m, and 2.14 m high. It hangs on the east jamb and opens into the room. Its planks and diagonal ledges are the
  kit's, not the strap hinges of B1.
- **Walls:** the east wall under the hall is 0.7 m thick, not 1.2 m (thicker, it buried the hall's gable posts);
  the hole's edge is 0.09 m inside it. West of the landing the walls stop at y 4.45 and lower, under the river
  ramp's deck.
- **Rubble** is coursed boxes of 0.26–0.66 m with broken corners on the kit's rock texture: rougher than cut
  blocks, more regular than the stones of B1. **Cut rock** is the same stone, darker, on a bulged sheet: no moss.
- **East shoulder:** three steps down the bank (tops at about y 4.1, 3.4 and 1.8), flat stones on top, no grass.
- **Ceiling:** a tight layer of boards under the hall's floor and the landing's deck, joists under the hall,
  trimmed round the hatch.
- **Floor:** the kit's mud texture with seven flat stones; three more as stepping stones outside the step.
- **Lights:** the outside lantern is a night light (on at 23:00, off by day); the inside one is always on, low.
  At noon the front is in the landing's shadow and reads dark.
- **Stores:** 10 casks, 3 crates and 3 sacks inside, 2 casks outside east of the door. The two crates under the
  hoist are now the light kit crate.
- **Hoist goods** did not move.

---

## Verdict log

Generated by the user on 2026-11-27 with their own tool, from the prompts above. The user's verdict on the batch:
the look and mood are right, with some inconsistencies between views.

| View | Date | Kept? | Notes (Carry / Ignore / Differs from) |
|------|------|-------|----------------------------------------|
| B1 dock door | 2026-11-27 | Yes | **Carry:** the front as a mix: coursed dry-stone rubble round the door and stepping down against the east bank, the bare cut rock face in the west bay with loose rubble at its foot; posts and braces standing free in front; single plank door in a timber frame under a heavy lintel, two strap hinges, ring handle; stone step and a few flat stepping stones in the mud; lantern on the wall left of the door. **Ignore:** the grass at the lower left. |
| B2 river side | 2026-11-27 | Yes | **Carry:** how the front reads from far: stone under the landing, the small door, casks standing by it. **Ignore:** the stone wall drawn under the whole ramp (the ramp stays on its trestles; the cellar ends at x 12.6); the casks on the landing. **Differs from B1:** rubble all along, no rock face. |
| B3 north-east overview | 2026-11-27 | Yes | **Carry:** the east wall stepping down toward the gate with a rough top and grass over it; the door with its lantern from above. **Ignore:** the wall drawn as far as the dock gate (it ends at x 21.1). |
| B4 interior from the door | 2026-11-27 | Yes | **Carry:** the room's look: cut rock at the back, rubble sides with moss low down, bare joists and dark boards overhead, pillar on a rough padstone, packed earth with a flat stone, casks two high, three crates, three sacks, a lantern on a pillar, the ladder. **Ignore:** the hatch open to the sky (it is shut, under the hall's floor). |
| B5 interior toward the door | 2026-11-27 | Partly | **Carry:** the door from inside (plank leaf opening inward, lintel), wet earth and flat stones at the threshold, the lantern on the pillar by the door, two casks beside the door. **Ignore:** the east wall drawn low and open to the sky: the cellar is closed on every side and overhead. |
| B6 dock door night | 2026-11-27 | Partly | **Carry:** the lantern's place (on the wall at the door's upper left, by the east post) and its reach; two casks left of the door. **Ignore:** the light: it does not read as night. Night is judged on the build at 23:00 in Play. |

**Decided by the user on 2026-11-27, from the images:**

- The look and mood are accepted.
- Walls may mix rubble and cut rock (as B1's front and B4's back).
- The cellar is never open to the sky (B5 is wrong there).
- The lantern's place in B6 is right; its light is not a night reference.
- The front is built as B1: rubble round the door and on the east side, the bare cut rock in the west bay with
  loose stones at its foot. That rock is modelled thick enough to cover the strip of ground left inside the wall.
- The lantern inside burns at all hours, dim (the room has no window); the one outside is a night light.
- Pipeline: the cellar goes into `build_warehouse.py` and the generated warehouse prefab (Sites places it); a
  one-shot command cuts the terrain hole and the terrain tool re-cuts it.

### Decided by the user at the second pause (2026-11-27)

- The rubble is liked: **every wall is rubble**, the cut rock is removed (back wall, the front's west bay inside and
  out). `M_Camp_Rock` is deleted. This replaces "walls may mix rubble and cut rock" above.
- **The room is larger**, grown south under the hall (the user's choice among four ways to grow): 6.1 × 8.9 m. The
  whole hall depth (9.8 m) was offered, but buried rocks reach z 49.8, so the back wall stands at z 50.1, with a
  0.5 × 2 m pier in the south-west corner over a rock that reaches into the room.
- Added with the larger room: a beam on two posts under the joists, a second always-on lantern on one of them,
  one more cask and crate and three sacks; the back-wall casks moved with the wall. Views `B7 interior back half`
  and `B8 interior from the back wall` in `built/`.
- Model 57,086 triangles: 2,086 over the 55,000 cap (the rubble walls are 16,766).
