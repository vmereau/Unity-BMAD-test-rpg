# StartingTown — Area 1 site layout facts

> Read before placing, moving or wiring anything in `StartingTown.unity` (the Company Encampment). Per-site layout
> facts, hand-owned roots and gameplay constraints. Zone table: `Scenes/CLAUDE.md`. How the tools that build
> `Area1_Blockout` run, what each one owns, rebuild order and rebake: `Editor/World/CLAUDE.md`.

NPC day/night spots and routines of the StartingTown cast: `Scripts/AI/NPC/CLAUDE.md` → StartingTown routine cast.

---

## Hand-owned scene roots

### WorkSiteManagers

Hand-owned root outside the builders (Warden's errand): the four work-site managers (`NPC_base Variant` instances) and
their all-day Stand spots:

- `QuarryOverseer` by the quarry sledge (219, −99)
- `TimberBoss` by the wood camp's east shelter (−214, 2)
- `LeadHunter` by the hunting camp fire (31, 222.5)
- `FarmReeve` outside the farm manager's house east door (−17.5, −62)

Recheck them if a site layout moves.

### SealedPassage (quest-002)

Hand-owned root outside the builders:

- `DaceHut` — graybox shed at (210.5, −95) with Dace's owned key pickup on the table.
- `NewCut` — Company scaffold, tools and survey stakes at the cleft's ring end.
- `SurveyNumbers` — red marks on the Ruins 2 altar.
- `OdoRemains` — graybox seated body facing the altar + `OdoBundle`: a `Base_Container` with its chest visual off,
  `ContainerTakeFact` → `TokenTaken`, Odo's ring + bread + mushroom.
- `RingEntryTrigger` — `WorldFactTrigger` → `RingEntered`: `Shape` 17 m past the door, `Shape_GoatTrack` at the goat
  track's foot, `Shape_GoatTrackDrop` over the landing pit.
- `GoatTrack` — three cairns, and three `Monster_DarknessSpider Variant` on the shelf east of the ring, each with its own
  generated KilledFact.

Bren (`WorkSiteManagers/QuarryWorker`) stands at `QuarryWorker_Spot` on the quarry floor 06:00–21:00 and at
`NpcRoutineSpots/Spot_QuarryWorker_Night` (Wretches' Corner fire) at night.

### HiddenStashQuest

Hand-owned root outside the builders: Brann, two routine spots, and `BrannsStash` inside the gang house.

- Brann watches the chest 06:00–18:00, then stands at the Mafia Zone gang fire 18:00–06:00.
- Chest stock is five bread/two apples/25 gold coins, owned and persistent, with no lock. Food and gold are in its
  quest-take allowlist.
- Recheck these placements if the gang house moves; do not run a builder to author this quest.

### Restricted areas

`RestrictedAreas` (scene root, hand-owned) holds `RestrictedArea` volumes: `RA_Warehouse` (warehouse interior, owner
`NPC_WarehouseGuard`, always) and `RA_Docks` (layout `docks` rect clipped north of the warehouse, owner
`Faction_Company`, 21–06). Guards' waypoints live under `NpcPatrolRoutes/<NPC>`; routine spots under `NpcRoutineSpots`.
Keep all three out of `Area1_Blockout` (tool-rebuilt).

---

## Camp edge

Built by Camp Edge (`Editor/World/CLAUDE.md` → Camp Edge for the procedure).

- The ring has four open gaps, plus the warehouse doors as a guarded way through: arrival (−38.4, 36.3, open towers, no
  blocker: `PassBlocker` is the story lock), wood (−51, 5.6), quarry (55.8, −5.8) and `RampGate` (−6.9, −51). The hill
  closes it on the south (see **Fort hill**).
- West of (−31.5, −43.3) `outer-fence` runs along the hilltop, then turns east along the hill's north foot into the wood
  opening, because anywhere at the bank's foot a running jump from the hill clears the panels. Inside, that leaves a
  bank about 7 m high in front of the wall, and an empty shelf on the hill behind it. Test this line with a running start
  from 5 m out.
- East of the north palisade's end at (30, 51.9) it is open round to the inner fence's east end: the wilderness trail and
  the shore both leave that way (see **Lakeside**).
- The docks are outside the ring, inside the Company compound (below).
- `BrokenStretch` (x −1..7 on the north fence) keeps the hunter's fire in view from the worker huts: keep the line from
  (10, 40) to (−24, 109) clear. Its blocker uses a collider on `Ignore Raycast`, which the player still hits and sight
  rays and the camera skip (so it needs a Not Walkable volume: `Editor/World/CLAUDE.md` → NavMesh bake).
- `outer-fence-4` has five panels; on the bank at its east end the panel tops reach y 8.6.

---

## Camp sites

`Fort`, `RampGate`, `Docks`, `BridgeRails`, `Barricades` and `HuntersFire` are built by Sites; the hand-built
`CampBridge`, `CompanyCliff`, `FightPit` and the root `SM_Barricade_*` are not (ownership: `Editor/World/CLAUDE.md`).

- **Docks:** the two hand-built `SM_WoodPlatformFloorKit_01a` deck pieces live in `Docks` and stay; rerun Sites if they
  move. The deck's colliders sit 0.45 m under the planks with holes at the T junction, and its planks have gaps that
  split a render-mesh bake, so `DeckFloor_*` are the walkable surface for the player and the bake: a box with its
  `MeshRenderer` on `ShadowsOnly`. The deck model's own collider rises to y 1.2 at the edges, so a straight walker can
  snag near the T junction at (7.5, 69.3); agents and the player pass.
- **Barricades:** `SM_Barricade_*` colliders match their mesh bounds, but the meshes are sparse, so the bake leaves
  NavMesh inside them; `Barricades/NotWalkable_*` close that.
- **HuntersFire** is driven by `ClockNightLight`. A point light only reaches the ground around it, so the 80–90 m read
  comes from the flame and halo quads, which centre near y 3 because the rim clears the (11, 35) sight line by only 0.4 m.
- `Fort` is under **Fort hill**, `FightPit` under **Lakeside**.

---

## Camp interior

Sites builds these from `Editor/World/Area1CampInterior`, and Camp Edge builds the fence lines (`inner-fence-*`,
`dock-fence-*`, `compound-fence-*`). Rerun both if `warehouse` moves: Camp Edge clips the panels at its footprint, so
its walls are the fence there, and a skirt closes the line under the floor.

- The inner palisade runs ring to ring, so the gate at (0, 9) is the only way between the two halves inside the ring;
  everything else goes out through an opening.
- The warehouse is a real route: camp ramp, hollow floor at the highest ground in its footprint, river door, ramp to the
  deck. It is the doc's Storage Hut. The NavMesh still links the low tip of the warehouse's camp ramp, so a walker
  heading east from the player's hut goes round its foot.
- The dock spur at x 24.5 clears the hand-placed barricades and runs into the river; `DockGate` stands open, and
  restricting it is later gameplay.
- `BrokenStretch` is unchanged, so the fire still shows from the huts.
- Each area keeps part of its block free for later NPCs.

### Camp centre

Sites builds `CampCentre` (stage, punishment post, flags round the square, on the inner gate and on the warehouse) from
`Editor/World/Area1TentCamp` and the `camp-centre` point, even while the settlement is hand-owned.

- Three 6 m sight lanes run from the post to `arrival-gate`, `wilderness-trail`'s first point and `inner-gate`.
- `Area1TentCamp.TryRead` still checks the layout's tent / fire points (`players-tent`, `tents-*`, `fire-*`,
  `black-market`, `zone-ditch`) before every Sites run: it stops a run if any stands in a lane, on the square, on
  `lakeside`, on the fight pit's bank, in the ditch, too close to the inner fence or to another point, or on the
  warehouse, the work-gang tents or the latrine, so move points in the layout and rerun.
- Steps stay under the player's 0.3 m `stepOffset` (stage risers 0.24 m), and doors are at least 2.1 m high, because the
  player's capsule reaches 1.8 m.
- The Warden stands at (−3, 12) by the gate by day; event-001 snaps him to the stage (see **Arrival sequence**).

---

## Company compound

The north bank between `Tower_R` and the dock spur.

- `compound-fence-upper` and `-lower` (Camp Edge) close it on the west, and the lower line runs to z 84, because the
  river at z 81 is 1.2 m deep and swimming starts at 1.3 m. The only ways in are `CompoundGate` (5 m, open), the
  warehouse, `DockGate` or swimming.
- **Road paint:** the terrain tool paints `path-*` in mud under the carved roads, then `delivery-road` in Road and
  `company-road` in Cobble, so the Company Road stays clean over everything, including through the inner gate. These
  lines are paint only and never reach `CarveRoads` (moving one: `Editor/World/CLAUDE.md` → Run order). The Company Road
  is also hand-painted Road through the centre (`Editor/World/CLAUDE.md` → Hand-painted camp ground).
- No trees grow inside the `compound` rectangle.
- The Company Road is 5 m wide to the rock ramp at (17.3, −21), and the ramp keeps its 2–4 m between hand-placed rocks.
- The warehouse ramps are 3.5 m wide for carts: 17.6° on the camp side, 16.2° on the river side. The river ramp's north
  edge stands about 0.8 m over the falling bank near its foot, so the player joins it from the foot end; the bake still
  links that edge, so a straight walker from the deck snags there.
- `StagingYard` holds the cart shed, stacks, smokehouse, net racks and gutting table, west of the tents-to-fire strip and
  north of the delivery road.
- `GuardPost` (`guard-post`, which has a `yaw`, like `gang-house`) is the enterable house in the corner by the gate, with
  `Lockup` and `TallyShed` as closed shapes under it. The ground falls about 3 m across the house, so its floor sits at
  the highest point, a skirt closes the underside, and the 1.6 m ramp lengthens up to 7 m of plan until it is no steeper
  than 30°, with a warning if it can't (4.25 m of plan, 4.9 m along the slope, today). `walkway-guard-post` starts at a
  fixed point by the ramp foot, so move it if the ramp moves.
- The old `GuardPost` lean-tos under `CompoundGate` and `DockGate` are separate objects with the same name.

---

## Fort hill

The plateau at y 10 is the fort, about 50 × 37 m.

- The perimeter runs about 1.5 m inside the rim, because a panel's bottom follows the ground and a line on the rim stands
  on the slope.
- Stone (`fort-wall-*`, Sites) faces the camp and the ramp: from the junction with `outer-fence-5` at (39, −37.7) along
  the north rim, through the ruin pillars and the tower, down the west rim to the cart gate. It must not reach down into
  the chamber around (−3, −44).
- Timber (`hill-palisade`, Camp Edge) closes the back, from the cart gate round the south and east rims to that junction.
- `outer-fence-stub` runs from `RampGate` to the west wall, so the only ways onto the plateau are the ruin pillars at
  (23.3, −34.6) and `Fort/CartGate` at (9.2, −66.2).
- `ramp-gate` and `fort-cart-gate` have a `yaw`, and `BuildGate` turns everything with it. Keep a gate square to its
  road, because the leaves swing back along local +X and stand in the lane at an angle. `RampGate` has no lean-to: there
  are rocks on the camp side and the road on the other.
- The ramp's lower half is outside the palisade on purpose, with `RampRail` on its outer edge, so the cart gate is the
  real defence.
- The hill's rim notch keeps the stone line at z ≈ −37, with the hoist's rim frame at (33, −33) outside the wall.
- The old inner fort walls are gone, so `House` and the big building stand in an open bailey until the interior is
  designed.
- `path-fort-ramp` is both paint and a sculpt: the hill is hand-sculpted, so a generated ramp would add to those edits
  instead of replacing them. Grade it with `Tools/World/Grade Area 1 Fort Ramp` (`Editor/World/CLAUDE.md` → Grading
  tools).
- No trees grow in the fort rectangle (`zone-3`) or within 6.5 m of the ramp line.

---

## Lakeside

The camp's east side is open.

- `outer-fence-4` joins the inner fence's east end at a diagonal, and north of that the lake side has no palisade, only
  the hand-placed `SM_Rock_*`.
- `ShoreRail` is knee-high and runs from the bank to the house only, because south of (56, 36) the bank reaches the water
  and a rail would stand at the shoreline. It's cut into pieces of up to 3 m with every third one missing; its bars and
  posts are too thin for a bake, so each piece has a Not Walkable volume.
- **Fight pit:** the bank comes from the terrain tool (`fight-pit` feature; capture rule in `Editor/World/CLAUDE.md` →
  Valley terrain), but the `FightPit` root (rocks and gap posts) is hand-owned: Sites doesn't build it, so edit it
  freely. The bench, stone and log there are hand-placed. The crest is highest towards `fort-tower` and the gap faces
  north. The tower looks down into the bowl, so from a top eye at y 26.6 the bank alone hides only the near half of the
  floor; the detailed pit will replace this placeholder.
- **GangHouse** shares `BuildHouseShell` with `GuardPost`: the ground falls under it, so its floor is 1.6 m over the camp
  side and it has a ramp. `BackDeck` sits just above the highest ground under it, is reached from the sides, and sets
  the height of `GangDock`. It stands on the tents-to-ruins line, with the ridge at y 6.9 and the chimney at y 7.6; keep
  both under y 10.
- `lakeside` is open ground with no trees and no tents (`Area1TentCamp.TryRead` keeps points off it); nothing is built
  there yet.

---

## Stoneworks

The yard inside the quarry opening keeps the root name `WorkshopYard`; Sites builds it from
`Editor/World/Area1Stoneworks` and the `stone-lodge`, `masons-row`, `tally-post`, `fort-hoist`, `ox-pen` and
`rubble-tip` features of `layout.json`. Reference: `Art/Artworks/Starting area/Camp/Camp carry entrance.png`.

- It stands on the Company side of the inner gate, which is why the huts are for skilled masons and there are no worker
  tents.
- Hut ridges stay under y 3.6: the palisade top is at about y 5.5 and `jumpForce` 5 gives 1.27 m, so a roof is no way
  over.
- The tool store, weighing beam and sledge are laid out from `masons-row`, and the trough and tally board from
  `tally-post`.
- `fort-hoist` lists four points in order: foot crane, its rim frame, outcrop crane, its rim frame. The second crane
  stands on the outcrop, as in the art. The hoists are scenery, but keep the ground round the foot crane clear, as a
  later route may use it.
- `path-sledge` is paint only, so moving it needs a terrain run.
- The pen's rails are too thin for a bake, so `NotWalkable_Pen` closes it.

---

## Timberworks

The yard inside the wood opening keeps the root name `TimberYard`; Sites builds it from
`Editor/World/Area1Timberworks` and the `timber-yard`, `forge`, `company-store`, `seasoning-shed`, `carpenters-shop`
and `charcoal-clamp` features of `layout.json`. Reference: `Art/Artworks/Starting area/Timber yard.png`.

- Wood comes in at the tally, is sawn, seasons under the long open shed and goes to the carpenters, while offcuts feed
  the clamp and its charcoal feeds the forge.
- `path-timberworks` is paint only, so moving it needs a terrain run. It leaves the wood road at x −30 and joins the fort
  road at z −10.5.
- The forge shell is tool-owned, but Edric, `SmithingStation_Anvil`, `SM_Chimney_Segment`, the root `SM_WoodBench_01a`
  and `Spot_Blacksmith_Night` are hand-owned scene objects, so moving `forge` leaves them behind. Edric's day spot is his
  spawn position, because his day routine block has no spot. Keep the 1.4 m door at the room's west wall clear of props.
- The store is built on the shared house shell, and the Storekeeper still stands outside it by his table.

---

## Garrison quarter

The ground under the fort ramp, south of the Timberworks; Sites builds the root `Garrison` from
`Editor/World/Area1Garrison` and the `garrison-barracks`, `staff-house`, `short-measure`, `grain-store`, `drill-yard`,
`workers-graves`, `garrison-well` and `garrison-bell` features of `layout.json`.

- `reserve-kennels` and `reserve-barber` are empty plots that no tool reads.
- `path-garrison` is the one way in, from the fort road through the yard, which doubles as the square; it and
  `path-drill-yard` are paint only, so moving them needs a terrain run.
- `Camp/CompanyHouse` is the fort, so the new house is `staff-house` in the tools and "Company staff house" in the docs.
- The armoury (the barracks' stone east end) and the grain store are closed rooms with solid doors, so the bake leaves
  them unreachable islands; later theft quests open them.
- `BuildHouseShell` takes an optional door position and minimum ramp, because the default 4 m ramp would cross the spine
  on flat ground. The fixed doors and props set each plot's minimum size, which Sites checks.
- The ground east of the staff house stays open to the fort road, so the spine is the only made way in, not a closed one.
- Keep props at the barracks under 1.2 m: its eave is out of jump reach, and from the ridge a jump could clear the
  palisade 4 m away.
- The graves are on the low bank at the toe of the moved fence, with mounds along the contour. That bank is gullied from
  the hand flatten, so some mounds overhang. Their numbers are TextMeshPro objects, because a legacy `TextMesh` with the
  built-in font material draws through the ground.
- The queue rail is too thin for a bake and has no Not Walkable volume, as it only marks a line.

---

## Farm

The Company farm on the plain behind the fort hill. Sites builds the root `Farm` from `Editor/World/Area1Farm` and these
`layout.json` features: `farm-manager-house`, `tithe-barn`, `threshing-floor`, `ox-mill`, `ox-byre`, `ox-paddock`,
`hay-yard`, `dung-heap`, `farm-well`, `farm-tally` and the five `field-*` (each with a `crop`: wheat, barley, oats, hay,
garden).

- The field gang leaves the camp by the fields gate. The ramp top has twin gates, both built by Sites under `RampGate`:
  the fort gate (`ramp-gate`, with leaves) leads up the cart ramp, and the fields gate (`fields-gate`, child
  `RampGate/FieldsGate`, posts and lintel only) opens onto the plain, where `path-farm` (paint only) starts.
- `ramp-rail` runs unbroken from beside the fort gate's south leaf to the cart gate.
- `CampEdge/outer-fence/Panel_001` and `Panel_002` are saved as removed hand edits to let the track through.
- Moving either gate means rechecking the walk to the farm on the NavMesh.
- The manager's house stands outside the palisade, at least 9 m from it.
- The barn is a closed room with shut cart doors, so the bake leaves it an unreachable island; a later theft quest opens
  it.
- Crop rows and haycocks have no colliders and a `NavMeshModifier` that ignores them, so the gang and agents walk
  through.
- The wattle fences (0.9 m) have colliders and a 3 m gap on the field's `gate` side (n, s, e or w), or else on the side
  facing the well, and the paddock has a Not Walkable volume.
- The terrain tool keeps the `farm` rectangle tree-free and paints each `field-*` except hay in Mud. It stops on a field
  outside `farm` (trees would grow in it) and on a `field-*` the Sites tool's `FarmFields` doesn't list.
- The Stoneworks pen keeps its own oxen.
- Keep the farm west of x 44, at least 50 m from the cave spiders.

---

## Remote sites

Sites builds them from `Editor/World/Area1RemoteSites` (a run replaces these roots whole: `Editor/World/CLAUDE.md` →
Ownership).

- `Ruins` centres on the `ruins-arch` feature (127, 146). The arch sits 2 m north of that and turns square to the tents,
  so its opening reads from there and (127, 143) stays walkable. Measured from the tents' eye (15, 2.17, 28), the
  palisade hides y 40–44 at x 118–138 and the lip is y 35, so an 18 m tower shows its top 9–13 m. Foliage partly screens
  the lower piece at x 140.
- `Cave` is a box chamber on the ground, with its mouth towards the camp and rock meshes outside it; the rocks stay clear
  of the interior at any yaw, and terrain holes aren't used.
- `Ruins2Dressing` stays within 10 m of the centre, where the pad is flat.
- `RuinsRing`'s north wall runs along z ≈ −118.6, so test the ring from outside it.
- The `Bandit` root's `waypoint 1`–`5` children are the child `bandit`'s route: one all-day Patrol block in its
  scene-added `NpcRoutine._blocks` (`waypoints`); the nearest is about 13 m from the wood road, and it detects at 8 m.
- Walk times between all sites are in `spec-area-1-blockout-remote-sites.md`, with the changes from the inner palisade
  in `spec-area-1-blockout-camp-interior.md`.

---

## Quarry and Ruins 2

`Area1TerrainBuilder.Cliffs` raises one rock mass along the quarry's south and east edges and around Ruins 2, up to
y 56. Its outer face is steep all the way round, which stops walking and agents. `PlayerController` has no slope slide
and allows jumps on steep ground, so a player holding jump can still climb any cliff: `Quarry/RuinsRing` (invisible
colliders on `Ignore Raycast`) and the seal's `Cap` are what stop that (`Editor/World/CLAUDE.md` → Climbability).

**Ways in:**

- `quarry-cleft` (`layout.json`), 4 m wide and open to the sky. List its points from the quarry to Ruins 2: the blocker
  and the ring's ends depend on that order.
- The goat track (`path-goat-track`, quest-002): from the forest edge at (326, −66) up the north flank onto the natural
  shelf east of the ring (x 310–340, y ≈ 47), over the rim in a notch at y 50 onto a ledge at (298, −148) y 47, then a
  drop due west across the ring gap into a pit at y 31 (`drops: [7]` in the layout), and on through a cut to the pad at
  (277, −158). Graded by `Tools/World/Grade Area 1 Goat Track` (`Editor/World/CLAUDE.md` → Grading tools).

**The drop is not one-way:** the player can climb back out of the pit or leave through the cleft door's `InsideLatch`.
The hand-placed `SealedPassage/GoatTrack/DropBlockers` that made it one-way were removed by design; if a one-way drop is
wanted again, restore vertical boxes (lip across the ring gap, walls on the pit's sides) and prove it with a
`CharacterController` jump-climb run (git history: `GoatTrackDropCheck` / `Tools/World/Check Goat Track Drop`).

- The terrain tool keeps trees 3 m off the track, and Sites' `BuildRuinsRing` splits whichever ring wall it crosses
  (today `Wall_1_A` / `Wall_1_B`, a 7.8 m gap along the east wall, z −144.3 to −152, 5 m square to the track; Sites logs
  it; cut ends stop flush, only corners overlap), so moving the line moves the gap.
- The NavMesh doesn't link the shelf to the pad across the drop (edit-mode paths go round through the cleft, closed
  at runtime), so the shelf spiders can't follow the player down.

**`CleftBlocker`** (Sites-owned, in `Quarry`; its height comes from the ground, so rerun Sites after a terrain run):

- `FactRemovedObject` hides its `Seal` child while `WorldFact_QuarryPassageOpened` is true.
- The `Seal` is a wall (three box colliders round a 1.3 m doorway) holding `CompanyDoor` (a `Simple door` instance:
  locked, beginner lockpicking, key `Item_Key_QuarrySouthDoor`, owned by Dace, swings into the cleft, fixed save id
  `area1-quarry-south-face-door` so a rebuild keeps its saved state) and `InsideLatch` (`DoorLatch`); its `Cap` starts
  3.2 m over the ground so the doorway stays open.
- The `NavMeshObstacle` still spans the whole width: NPCs never path through the cleft. The bake ignores the blocker
  (`NavMeshModifier`) and a carving `NavMeshObstacle` closes the cleft at runtime, so removing it needs no rebake. That
  obstacle stays disabled in the scene, because a bake cuts an enabled carving obstacle into the NavMesh even under an
  ignore modifier; `FactRemovedObject` enables it in `Awake`. Edit-mode paths therefore go through the cleft: test
  sealing in play mode.

---

## Worker settlement (hand-owned)

`Area1_Blockout/TentCamp` is hand-owned, including its Canteen and Ditch children (flag and tool behaviour:
`Editor/World/CLAUDE.md` → Settlement hand-owned flag).

- The saved scene contains 75 huts and 26 tarp bays. Historical worker plots do not reproduce the current scene.
- 43 of the huts are board-built shells (**Hut rollout**, below); the Wretches' 28 hovels, four huts and the tarp bays
  outside the home yard are still graybox.
- `CampCentre` and `Walkways` remain tool-owned.
- The player's hut, bed and `DevPlayerSpot` moved with the home yard art pass (below), and `PlayerSpawnPoint` is outside
  the arrival gate; do not assume they share an anchor. `Bed`, `PlayerSpawnPoint`, TentMate's spots and BeatenWorker
  are hand-placed and don't follow the layout's `players-tent` point. `Bed` is in the player's hut (0.38, 48.15),
  TentMate's routine is round his hut `worker-mate` next door, BeatenWorker stands at the campfire (7.4, 40.4).
- Cook and cooking station remain outside TentCamp as hand-owned gameplay objects.
- Hut doors stay at least 2.1 m high and steps under 0.3 m (see **Camp centre**).

### Mafia buildings and arena overlooks

Five hand-owned children live under `TentCamp/MafiaZone`: `MafiaQuarters`, `ArenaSpectatorPlatforms`, `BossHouse`,
`BossGarden` and `BossOverlook`. They were authored at (31.5,16.5), (43.1,21.8 / 24.65), (45.5,12.5), (51.25,13.2) and
(51.5,18) in world (x,z), and have been moved by hand since, so measure the live scene. Authoring / rebuild commands:
`Editor/World/CLAUDE.md` → One-shot authoring commands.

**`MafiaQuarters`** is the gang's barracks/warehouse at (24.9,13.2): a 6.05 × 7.45 m timber hall, eaves 3.4 m, ridge at
y≈6.3, turned 90° so its gable door faces east.

- It fills the whole plot between `worker-hut-11`, huts 15/27, `worker-hut-25` and `inner-fence-east`: its walls stand
  0.1 m from the palisade and 0.3–0.5 m from the neighbouring hut walls (roof edges come within a few centimetres), too
  narrow for the player's 0.6 m capsule.
- Hut 11 stands askew, so `GapBoards` closes the one wider wedge at its north corner; move the boards if hut 11 moves.
- The plot is not a way between the Player's Zone and the mafia fire.
- Its one door (2 m wide, world z 14.4..16.4) opens into the lane between huts 25 and 27; keep that lane and hut 25's
  north-facing door clear.
- `Furnishings` (three bunks along the rear wall, mess table, crates at the palisade end, barrels beside the door) is
  Not Walkable and stays under 1.2 m at the palisade end.
- The interior has no light source, so it is black in captures: add a temporary `HideFlags.DontSave` point light to
  inspect it.

**Boss house, garden, overlooks:** the boss house opens west onto a stair approach and east into the enclosed raised
garden, which connects to the shaded arena balcony. Their common floor is y=4.65 to clear hand-placed rim rocks. A
taller south garden screen blocks an easy jump towards the nearby palisade. Public viewing decks have separate stairs
from the square. These buildings are distinct from the lakeside `Area1_Blockout/GangHouse`, whose stash quest remains
in place.

### Home yard art pass (`spec-area-1-home-yard-beauty-corner`)

The first spot of Area 1 at final quality, and the visual reference for the rest (concepts
`Art/Artworks/Starting area/Camp/H1`–`H4`).

- `PlayersZone/worker-player` and `worker-mate` are instances of `Prefabs/World/Huts/WorkerHut_Home` (3 × 3 m; contract
  in that folder's `CLAUDE.md`).
- The player's hut stands 0.5 m forward of the graybox one, at (−0.30, 47.52), because the ground rises steeply right
  behind it: a 4 m hut does not fit there without terrain work.
- Its doorway is off-centre, at about (−0.11, 45.94); Hobb's is at about (−0.59, 44.76). The two doors open onto the same
  1.5 m pocket, so keep it clear.
- `Bed` (root, now `Bed_WorkerPallet`) lies along the hut's east wall, head to the back.
- `PlayersZone/HomeYardDressing` holds every prop and both night lights and is rebuilt by `Tools/World/Dress Home Yard`
  (`Editor/World/CLAUDE.md` → One-shot authoring commands).
- The graybox `Campfire`, `RepairTrunk` and `CardGame` groups are inactive, not deleted.
- The escort's `HutLanes` pass 1.6 m either side of the fire at (5.55, 38.95): the card game's logs already narrow the
  west lane, so add nothing solid south of the fire or on the lanes, and check lane-to-lane paths after a rebake.
- `ArrivalSequence/DevPlayerSpot` is 1.4 m outside the player's door at (0.51, 44.69), `TentMateSpot` beside the door at
  (1.41, 45.30), and `Spot_TentMate_Night` inside Hobb's hut (below).
- `FireLight` and `CandleLight` are point lights driven by `ClockNightLight`, so they are off by day and in edit mode.
- The other huts of the zone were swapped by the hut rollout (below).

**Hut shell (`spec-area-1-worker-hut-board-shell`):** both huts are now the board-built Blender shell on one wood
(same footprint, doorway and rear opening; the walls end 1.455 m from the middle and their colliders at 1.55 m, 10 cm
inside the old ones). The dressing followed: wall-hung rope
and peg rail at 1.25 m, the spade against Hobb's front boards, the drying line tied to the player hut's top plate end.

**Pass 2 (`spec-area-1-home-yard-pass-2`, concepts `H5`–`H7`):** one wood tone on both huts and weight poles instead
of roof stones (hut builder); the rest is built by `Dress Home Yard`:

- `TarpBayEast` at (4.3, 47.11), open side south toward the fire (2.16 m clear under the front flap): tarp from two
  2.4 m poles at its front to stakes at its back, two sleepable `Bed_FloorPallet`s, bowl, cup, a `ClothBundle` and
  boards in front. Poles and stakes are solid thin capsules with Not Walkable volumes; with the guy stakes the bay
  spans about 2.9 × 3.2 m. It replaces the graybox
  `worker-tarp-north (3)` (inactive) and stands 1.26 m west of it: the north-west wall of the hut then called hut-07 (now the shell `worker-hut`), from (5.62, 45.63)
  to (7.45, 47.88), cut through the old spot. Its east side clears that wall by about 0.5 m.
- `LeanTo`, in Hobb's hut frame: a tarp from Hobb's south-east eave to hut-02's north wall over one `Bed_FloorPallet`,
  in the wedge where the graybox `worker-tarp-north (1)` (inactive) overlapped the kit hut. No posts: the escort's
  `HutLane_L_6` (0.11, 43.10) is at the wedge's mouth, about 0.3 m past the bed's foot (the bed has no solid collider).
  The tarp's outer edge is tied under the eave of hut-02's shell (`EaveTie`, two ropes round the roof's edge), at the
  two spots along the wedge where it met the graybox wall; it follows hut-02 on the next dresser run.
- A drying line with two shirts from the end of the player's hut's top plate (its front corner on the bay's side) to the bay's west pole (shirts hang above
  1.8 m), a wooden spade against Hobb's front wall left of his door, a plank bench north of the fire between the two
  lanes and a log seat east of the east lane, and boards over the mud east of the doors' pocket.
- After the rebake the escort's lane paths are as before (only the pre-existing L3→4 detour round hut-03 and the
  missing NavMesh at R_1), and the beds, both doors and Hobb's night spot are reachable.

### Hut rollout and east yard (`spec-area-1-worker-hut-rollout`)

`Tools/World/Replace Graybox Worker Huts` swapped 40 graybox huts for shells in place (35 in `PlayersZone`, 5 in
`MafiaZone`): same names, positions and yaw, no hut moved. Rules and variants: `Prefabs/World/Huts/CLAUDE.md` → Rollout.

- Every swapped hut has the **tight roof** (`WorkerHut_Tight` to `_Tight_D`): neighbours stand 2.9–3.8 m apart, and
  the few huts with no hut in reach (06, 09, 11, 25, 26) meet a rock, `MafiaQuarters` or a graybox tarp bay, so no
  plot clears the normal 1.9 m roof. 33 pairs of shell roofs still cross, as the graybox roofs did, about half of
  them into the neighbour's wall line (closest: `worker-hut-14` / `14 (3)` at 2.94 m, `03` / `03 (1)` at 3.08 m);
  closing those needs huts moved, which nothing has done yet.
- `MafiaZone/worker-hut-30` (the market hut) keeps its `TradingCounter` box, now a child of the shell, standing on
  the ground inside (the plank floor it stood on is gone). The counter fills the west half behind the front wall, so
  this hut has the default doorway (`WorkerHut_Tight`), not a mirrored one.
- The shells' wall colliders end 1.55 m from the middle, 4 cm further out than the graybox walls: `worker-hut-03`,
  `02 (2)` and `03 (1)` stand that much closer to the escort's lanes (0.34–0.55 m, already under 0.7 m before).
- Left graybox: `PlayersZone/worker-hut-02 (9)` (the ground drops 0.34 m under its walls) and
  `MafiaZone/worker-hut-28`, `28 (1)`, `28 (2)` (narrower boxes, walls on ±1.29 m, two of them on a bank).
- The Mafia huts lost their graybox plank floor: the shell has none.
- After the rebake every shell's room is reachable through its doorway (43 of 43), and the escort's lane paths are
  as before.

East side of the home yard, built by `Dress Home Yard` in `HomeYardDressing/EastYard` (world space, no poles):

- **Eaves tarp** between the facing side walls of `worker-hut` and `worker-hut-06` (a wedge 2.6 m wide at its mouth,
  1.25 m at the back), one sleepable `Bed_FloorPallet` along hut-06's wall and a `ClothBundle`. Replaces the graybox
  `worker-tarp-north` (inactive).
- **Ramp shelter**, replacing `worker-tarp-north (4)` and `(5)` (inactive), in two sheets that meet at hut-06's
  south-east corner. The *porch* runs from the ends of hut-06's top plates (its front gable) to hut-05's north eave,
  over the lane between the two huts: hut-06's doorway opens under it, 2.4 m clear, with boards over the mud. The
  *ramp bay* runs from hut-06's east eave up to the west edge of `Warehouse/Ramp_Camp` (ropes round the deck), over
  the pocket between the hut, the ramp and the rocks: one floor bed under the tarp, one under the ramp's deck (about
  2.9 m up there), a bowl, a cup and a bundle.
- The pocket is a dead end (rocks to the north, hut-05's corner 0.4 m from the ramp), reached through the porch lane.
- Moving hut-05, hut-06, `worker-hut` or the ramp moves the tarps on the next dresser run; a hut swapped back to
  graybox makes the dresser skip its tarp.

### Hobb's hut and bed

**Hobb owns his hut (`spec-hobb-hut-ownership`):**

- `worker-mate/RestrictedArea` is switched on (owner `NPC_TentMate`, all day, one warning before a fistfight).
- `PlayersZone/HobbsBox` (hand-owned, outside `HomeYardDressing`) is a `Base_Container` with its chest visual off and
  `SM_TallSquareWoodBoxClosed` as `BoxModel`, between Hobb's bed head and his crate table. It is unlocked, owned by Hobb
  (`Ownership`), keyed for saves by `PersistentID` → `KilledFact_HobbsBox`, holds 8 gold, 2 bread and 1 apple, and its
  `ContainerTakeFact` sets `WorldFact_Hobb_BelongingsRummaged` (Hobb's talk opener).
- `Spot_TentMate_Night` is inside the hut beside the stool, facing the door (unused since Hobb sleeps, below).

**Hobb sleeps (`spec-npc-routine-sleep`):**

- `PlayersZone/HobbsBed` (hand-owned, outside `HomeYardDressing`) is a `Bed_WorkerPallet` instance
  (the dresser doesn't place it), owned by Hobb (`Ownership`), name tag "Hobb's bed".
- Children `NpcApproach` (−2.32, 0.72, 44.28, on the NavMesh, facing the bed) and `NpcSleepAnchor` (local 0.2, −0.48,
  0.09, yaw −90 — head toward the bed's +X end, by `HobbsBox`).
- His 21–06 routine block is Sleep in it.
- Moving the bed or the box means rechecking the approach on the NavMesh. Moving the box or rerunning the dresser with
  new prop positions means rechecking the NavMesh path to that spot.

---

## Arrival sequence

event-001, `spec-area-1-arrival-escort-beating` + `spec-area-1-arrival-hut-briefing`. The scene root `ArrivalSequence`
(`Game.World.ArrivalSequence`, data `Data/World/ArrivalSequence_Event001`) is hand-owned.

- `PlayerSpawnPoint` stands outside the arrival gate at (−40.8, 37.0), facing the camp; `ArrivalSequence/DevPlayerSpot`
  (dev skip target) is outside the player hut's south door.
- `Lanes/Lane_L_*` / `Lane_R_*` (same count; the last pair flanks `WatchSpot` at (−9.5, 25), west of the stage) follow
  the arrival sight lane.
- The stage only has NavMesh on its east half (x ≥ −0.5, z 22.5–26.5): `WardenStageSpot` (1.2, 23.6) and `TamStageSpot`
  (−0.1, 24.8) sit there; recheck them if the stage or its bake changes.
- `CompanyGuard_ArrivalLeft` / `Right` (`NPC_base Variant`, own `NPC_CompanyGuard_Arrival*` entities and killed facts)
  stand all day at `NpcRoutineSpots/Spot_ArrivalGuard_Left` / `Right`, each side of the gate.
- BeatenWorker has an all-day routine to `Spot_BeatenWorker_Campfire` and `_availableAfter` = `WorldFact_Arrival_Done`.
- Part 2: `HutLanes/HutLane_L_*` / `HutLane_R_*` (7 each; index 0 = the stage lane ends) run east across the square,
  north past the campfire, to the yard shared by the player hut's south door and Hobb's hut `worker-mate` east door — the
  yard is only reachable from the east (the west way goes through Hobb's hut). The last pair flanks `TentMateSpot`
  (1.41, 45.30, Hobb's briefing spot) and `HutWatchSpot` (1.09, 44.70).
- Set `_devStart` on the `ArrivalSequence` component to SkipAll (start by the hut) or SkipToHutEscort (start at part 2),
  or use the context menus **Dev: Skip Arrival** / **Dev: Skip to Hut Escort** in Play.
