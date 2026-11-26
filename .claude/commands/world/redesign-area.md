---
description: Redesign a graybox area of the world to final quality — site survey and program, concept pack (massing captures, image-to-image prompts, build brief), the user's artworks and verdict, then the build from kit props and script-built Blender models
argument-hint: <area, e.g. "canteen" or "the dock guard post"> [phase: survey | concepts | build] (empty = ask)
---

Your task is to take one **area** of a scene (a building, a corner, a stretch of wall) from graybox to final look,
through the workflow of the Area 1 art pass: `spec-area-1-home-yard-beauty-corner`, `-home-yard-pass-2`,
`-warehouse-concept-pack` / `-warehouse-rebuild`, `-palisade-rebuild`, `-broken-stretch`, `-canteen-cookhouse`.
Each of them **decided what the place is and holds, fixed its look with concept art painted over captures of the
real site, then built it from kit props and script-built Blender models**, with the user judging at two pauses.

Work through the steps in order and **stop at every HALT**. The work is one quick spec (`gds-quick-dev`) in two
phases; this command supplies what goes in it. When `$ARGUMENTS` names a phase, check that the earlier phases are
done (spec `status`, the concepts doc's verdict log) and resume there.

**Never** call `generate_image` / OpenRouter. Meshy (`meshy_image_to_image`, `meshy_image_to_3d`) only after the
plan and the cost are given and the user says yes. Never edit kit materials, `hand-edits.json`, or anything inside a
tool-owned root by hand.

## Step 0 — Load the rules

Read in full (they are the source of truth; never restate them from memory):
- `Assets/_Game/Art/ART_DIRECTION.md`, `Assets/_Game/Art/Environment/Camp/CLAUDE.md` (wood, kit props, scale,
  weight, kit lights, custom Blender pieces, cloth, density), `.claude/rules/meshy-mcp.md` (*Image to image*)
- `Assets/_Game/Editor/World/CLAUDE.md` (*Run order*, *Ownership*, *NavMesh bake*, *One-shot authoring commands*,
  *Play tests*, the *Checklist*) and `Assets/_Game/Scenes/CLAUDE.md`
- the scene's site file (`Assets/_Game/Scenes/STARTING_TOWN.md` for Area 1): the section for the area and its
  neighbours
- the closest precedent, as a model to copy, chosen by the pipeline it will likely need (Step 5):
  `CANTEEN_CONCEPTS.md` + `Prefabs/World/Cookhouse/CLAUDE.md` (a building with rooms and gameplay),
  `WAREHOUSE_CONCEPTS.md` + `Prefabs/World/Warehouse/CLAUDE.md` (a building fitted to rough ground),
  `BROKEN_STRETCH_CONCEPTS.md` (a dressed site owned by a tool), `PALISADE_CONCEPTS.md` (a long kit assembled by a tool)

## Step 1 — Pick the area and survey the site

If `$ARGUMENTS` names no area, ask with `AskUserQuestion` (candidates: graybox sites from the site file and
`deferred-work.md`). Then measure the site through Unity MCP (`find_gameobjects`, `execute_code`), never from
memory, and write down:

- **What is there:** the graybox roots and their owner (which `Tools/World` builder owns the root, or hand-owned;
  `Editor/World/CLAUDE.md` → Ownership), footprints in world coordinates, the neighbours that touch it (huts, fence
  lines, flags, roads, rocks).
- **Who uses it:** NPCs whose routine spots, beds, stand spots or shops are inside (`NpcRoutineSpots`, `Data/NPCs/*`),
  interactables (stations, beds, containers), restricted areas, quest facts that name a place there
  (`quest_report`).
- **What must stay clear:** sight lanes, escort / patrol lanes and waypoints, walks NPCs use (ask a NavMesh path
  between the ends), doorways and steps rules (2.1 m × 1 m doorways, steps under 0.3 m, unless the site file says
  otherwise).
- **The ground:** heights over the plot (sample a grid; plots that look flat often rise 0.3–1 m at one end), what a
  level cut would cost, and whether a terrain change is outside a builder's reach.
- **Budget:** triangle count of what stands there today.

## Step 2 — The program: what this place is and holds (HALT)

Propose, in the conversation, short and concrete:
- **The place's story** (who built it, for what, how worn), in the voice of the concepts docs: it decides the build
  quality between the huts (poorest) and the warehouse (best built).
- **The program:** each structure with a footprint and rough sizes, the rooms, the openings, the dressing groups
  (work corner, yard, interior), the night lights, and what is gameplay (restricted part, station, bed, NPC spots
  that move with the build).
- **What the artworks must settle:** the open look questions (walls, roofs, a key detail, how much dressing, night
  lights, a neighbour to move or not), numbered.
- **The views:** five to seven captures. Always one look-setting view (generated first), one overview from above,
  one night view; interiors when there are rooms.
- **Conflicts found in the survey** (a lane through the plot, a flag in front of a wall, ground that needs a cut) and
  the plan change each suggests.

Ask the open choices with `AskUserQuestion`. **HALT** until the user agrees the plan.

## Step 3 — Write the spec

Run `gds-quick-dev` with the agreed plan. Slug `spec-<area-prefix>-<place>` (e.g. `spec-area-1-canteen-cookhouse`).
Title: `'<Area> <place>: concept pack, then <what is built>'`. Context: the files of Step 0. Its frozen block takes
the shape the precedents converged on:

- **Approach:** "Two phases. First a concept pack: captures of the site with temporary massing shapes,
  image-to-image prompts and a build brief, then a pause while the user generates and judges the artworks. Then the
  build, from existing kit assets and Blender (MCP) models."
- **Always:** the agreed plan with world coordinates; the gameplay rules (restricted parts and their owners, stations
  reachable without trespass, moved spots and beds still working); build quality and materials (`M_Camp_*` on kit
  textures, rules of `Camp/CLAUDE.md`, kit materials never edited, kit props size-checked); night lights with
  `ClockNightLight`; doorway / step sizes; on the NavMesh; lanes kept clear; ownership (which tool owns the root, run
  order unchanged; one script as the only source of sizes); **two pauses** (after the concept pack, after the first
  build is captured by day and at 23:00 in Play); rebake the NavMesh (`StartBakingSurfaces`, check for a
  `... 1.asset`), save the scene.
- **Ask First:** any image or Meshy generation (cost and plan first); moving a neighbour, flag, lane or waypoint;
  terrain changes outside the plot; a triangle budget (precedents: 40,000–60,000 for a building or site); anything
  restricted beyond what was agreed.
- **Never:** what belongs to later specs (loot, locks, new NPC routines, dialogue, quests); other areas;
  `generate_image` / OpenRouter; hand-placed art inside a tool-owned root; editing `hand-edits.json`.
- **I/O matrix:** first run, rerun (same poses, no duplicates, no new assets), model or material missing (nothing
  changes / graybox fallback, an error naming what is missing), the gameplay rows (trespass, station, routine).
- **Tasks:** phase 1 (captures, concepts doc, **pause**), phase 2 (Blender script, materials, builder / place
  command, tests, scene run + rebake + captures, **pause**, play test, docs).

## Step 4 — Phase 1: the concept pack (HALT)

**Captures** (nothing saved in the scene):
1. Hide the graybox pieces the build replaces. Under one temporary root, add **massing shapes** for the plan:
   primitives at the planned sizes, each family in one flat unlit-looking colour (walls mid-brown, roofs grey-brown,
   stone grey, carpentry light orange-brown, barrels pale yellow cylinders, sacks off-white flattened balls, crates
   dark brown, food dark red, lights a small yellow box). Keep the table of colours: it goes in the doc.
2. Level the ground in memory if the build will cut it (restore the heights afterwards).
3. Add a temporary sun (edit mode has no runtime ambient), place a camera per view and render 1920 × 1080 to
   `Assets/_Game/Art/Artworks/<area folder>/<Place>/refs/<ID> <view name> ref.png` (IDs: one letter for the place
   and a number, e.g. `K1`, `W3`). Look at each image (`Read`) and retake any that is unclear.
4. Reload the scene without saving, restore the terrain heights, and check that `git status` shows no scene change.

**The concepts doc** `Assets/_Game/Art/Environment/<folder>/<PLACE>_CONCEPTS.md`, with the sections of
`CANTEEN_CONCEPTS.md`, in this order:
- header (read-when line, the rules it follows, the spec), what the place was before;
- *The place* (story and program) and *What the artworks should settle* (Step 2's questions);
- *How to generate*: input image, template + view paragraph, save name beside `refs/`, **look-setting view first**,
  then the accepted look-setting artwork as a second reference image for the others with the sentence
  `The second image is a material and construction reference only: copy its wood, roof and details, never its camera or layout.`;
  which views replace the light line (interiors, night); the **placeholder colour table**;
- *Template* (one code block, kept verbatim, `{VIEW}` the only slot): style line (Gothic 1 and 2, realistic and
  grounded); the repaint rules (keep camera, composition, positions, sizes; name the finished real objects to keep;
  every smooth single-coloured shape is a placeholder; add, move or remove no building); `{VIEW}`; the materials
  paragraph mapping each placeholder colour to its real object in `ART_DIRECTION.md` vocabulary; the light line
  (overcast, cold diffuse daylight); the negative line (banned materials, no new people, no text, …, `Wide 16:9
  composition.`);
- *Views*: per view, input file, save name, a paragraph that describes **only what that view shows** (meshy-mcp.md:
  every object a prompt names gets painted), and light-line replacements for interiors and night;
- *Build brief (facts the build keeps)*: a size table in world coordinates, then the constraints (lanes, ground cut,
  things that move with the build, things removed, owner rules);
- *Verdict log*: a table `View | Date | Kept? | Notes (Carry / Ignore / Differs from)`, empty.

Add a pointer to the doc in the folder `CLAUDE.md` (e.g. `Camp/CLAUDE.md` header) and tick the phase-1 tasks.

**HALT:** give the user the doc and the refs folder. They generate the artworks with their own tool. If they ask for
Meshy instead: `meshy_check_balance`, then present `meshy_image_to_image`, `nano-banana-pro`, 16:9, **9 credits per
view**, the total and no regeneration unless agreed; generate the look-setting view first from its capture alone, the
others with the capture first and the accepted look-setting result second, the template's second line reading
"Repaint the first input image … add no object where the first image shows none". Save next to `refs/` under the
doc's names.

## Step 5 — Verdicts and the build plan (HALT)

Look at every artwork (`Read`), fill the verdict log (Carry / Ignore / Differs from), and propose an answer to each
numbered question from the images. Then choose the **build pipeline**, one of the four the precedents used:

| Pipeline | When | Precedent |
|----------|------|-----------|
| **Dresser** — kit props + small custom Blender pieces, placed by one editor tool into one group | Yards, interiors, small structures the kit or a small model can cover | `HomeYardDresser`, home yard passes |
| **Generated prefab + place command** — one Blender script → FBX + parts file (`.json`) → builder → prefab; a `Tools/World/Place …` command removes the graybox, levels the ground, places prefab and dressing, moves gameplay objects | A hand-owned building with rooms or gameplay | `CookhouseBuilder`, canteen |
| **Generated prefab as a tool's root** — as above, but the owning builder (Sites, Camp Edge) instantiates the prefab under the same root name; fitted to a ground capture (`Capture … Ground`) | A building inside a tool-owned root, on rough ground | `WarehouseBuilder`, warehouse |
| **Parts-file layout or kit, assembled by the owning tool** — the script models only shaped pieces, each on its own pivot, and writes where each goes; the tool fits them to the ground on every run | Long or ground-following pieces, sites rebuilt by a tool | palisade, `BrokenStretchBuilder` |

Present the decisions, the pipeline and anything the build will deviate from in the frozen text. Record the user's
answer under the verdict log (*Decided by the user on <date>*). **HALT** until they agree.

## Step 6 — Phase 2: the build

Follow the precedent's own `CLAUDE.md` pipeline. Rules all of them shared:
- **One script is the only source of sizes.** `Art/Environment/<folder>/models/<piece>/Source~/build_<piece>.py`
  (run headless `blender -b -P`, or in a session `runpy.run_path(path, run_name="__main__")`) writes the FBX, the
  parts file (colliders, walk surfaces, Not Walkable volumes, light points, anchors for spots / stations / beds /
  restricted boxes, `centre`, triangle counts) and the `.blend` (result only; parts file before the `.blend`).
  Builders and tests read the parts file; any size written twice is listed in the prefab's `CLAUDE.md`.
  Reuse the warehouse script's helpers as a library (one wood) rather than copying them.
- Wood, UVs, materials, export axes, kit props, kit lights, cloth: `Camp/CLAUDE.md`. Count triangles of kit props
  before placing them in numbers.
- **Builders** validate everything (model, groups, materials, parts fields, scene anchors by rooted path) **before**
  changing anything; a bad input stops before saving or builds the graybox fallback, with an error naming what is
  missing. Reruns give the same poses with no duplicates and no new assets. They refuse in Play and Prefab Mode.
- **NavMesh:** Not Walkable modifiers on roofs, furniture, hearths and thin props, none on the frame (it cuts the
  floor at every post); dressing props out of the bake. Walk boxes separate from the visual decks.
- Ground cut by the place command → `Capture Area 1 Terrain Sculpt` (or the scene's capture) before any terrain run.
- **Tests** (`Assets/Tests/EditMode/<Piece>PrefabTests.cs` or `<Piece>Tests.cs`): doorway sizes read from the parts
  file, walk surfaces, clear lanes / spots (collider reach, not pivots), restricted box inside its room, lights carry
  `ClockNightLight`, malformed parts files refused without a throw.
- In the scene: run the commands in the documented order, rebake, `read_console` (no errors, no kit-light warnings),
  then captures from **every view by day** and the night view(s) **at 23:00 in Play** (lights can only be judged in
  Play), saved to `Artworks/<…>/<Place>/built/<ID> <view name> built.png`. Report the triangle total.

**HALT:** show the built captures beside the artworks, list every Ask First item the build ran into (budget, ground
cut, a moved neighbour) and every deviation. Apply the user's hand edits back into the script if they moved things
(as the broken stretch's shore group). Then the play test (`Editor/World/CLAUDE.md` → Play tests): walk every route,
push at walls and rails, gameplay rows of the matrix (trespass, station, NPC routine at its hours).

## Step 7 — Review and docs

- Review through `gds-quick-dev`'s review step (blind hunter, edge-case hunter, acceptance auditor); patch, and log
  loopbacks you did not take for the user in the spec change log.
- Docs: *As built* in the concepts doc (every difference from the artworks and the brief); a new
  `Prefabs/World/<Piece>/CLAUDE.md` (pipeline table, frame and contract, what a rerun discards, known limits) when
  there is a prefab; `Editor/World/CLAUDE.md` (the command, ownership, run order); the site file; folder
  `CLAUDE.md` files of moved NPCs or systems; the root `CLAUDE.md` folder index; `deferred-work.md` for what was left.
- Spec change log: phase 1, artworks (who generated, credits), phase 2 deviations, user decisions at the pauses,
  review; files not named in a task line. Set `status: done`.
- Offer `/perso:commit` (one commit for the pack if it was committed separately, one for the build).
