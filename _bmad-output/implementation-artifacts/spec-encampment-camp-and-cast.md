---
title: 'Encampment: camp layout and cast'
type: 'feature'
created: '2026-11-04'
status: 'done'
baseline_commit: '799cdda'
context:
  - '{project-root}/_bmad-output/brainstorming-area-1-company-encampment-2026-04-30.md'
  - '{project-root}/Assets/_Game/Data/NPCs/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `StartingTown` is a test village, but the game opens in a Company labor camp. Layout, NPCs and text all contradict the narrative, and test content is mixed into the scene.

**Approach:** Convert the scene in place into a graybox of the camp: re-zone it with existing props and terrain, repurpose the six NPCs, add the Beaten Worker, and remove test content. Scripted story beats follow in later specs.

## Boundaries & Constraints

**Always:** Keep the scene name `StartingTown` (`SceneLoader`, `GameConstants`, save data use it). Rename assets with `AssetDatabase` so GUIDs survive. Preserve shop stock, teaching and routines. Tents are one prefab (`Tent_Graybox`) so art can be swapped later. Region scenes stay without a directional light. "Warden" is the canonical title (narrative-design's "Camp Manager").

**Ask First:** Any terrain sculpting. Any new C# gameplay code. Any change to `Core.unity`.

**Never:** Arrival sequence, warden tour, work sites, racket-guard encounter or stash quest (logged in `deferred-work.md`). River, docks, fight club hollow. Old-save migration.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scenes/StartingTown.unity` -- layout, NPC instances, test content
- `Assets/_Game/Data/NPCs/` -- identities, dialogue, memories
- `Assets/_Game/Data/Enemies/StartingTown/Generic/` -- `KilledFact_*` per NPC
- `Assets/_Game/Data/Quests/` -- quest wording; `Spiders` tracks 2 `KilledFact`s
- `Terrain_StartingArea` -- `NavMeshSurface`; basin y≈0 (x ±40, z −25..40), reachable rock outcrop y≈10 (x 0..30, z −30..−45)
- `Assets/Hivemind/` -- `SK_Roof_Tarp_AsStaticMesh`, `SM_FlagPole_01`, `SM_VikingFlag_01a`, planks

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Prefabs/World/Tent_Graybox.prefab` -- build from tarp, poles and planks, with a collider a player can't walk through -- single swap point
- [x] `StartingTown.unity` -- apply the Zone Plan; group new props under `Camp/<Zone>` roots -- gives the camp its geography
- [x] `StartingTown.unity` -- delete test content: 3 stance bandits, `CookingTestFood`, both `TestItem_Health_Potion`, `Item_Lockpick`, `SwordBase_World`, `Axe_World`, `Flamberge_World` -- clears the scene
- [x] `Merchant` scene instance -- add the Flamberge to stock; leave existing stock alone -- two-handed combat stays testable
- [x] `Data/NPCs/Bandit/` -- restore `_baseHealth` to 50, delete `StanceTest/` -- ends the stance test
- [x] `Data/NPCs/` + `Generic/KilledFact_*` -- rename per Cast table (folder, `NPC_*`, `KilledFact_*`, chain file prefixes, `entityName`, scene GO) -- no town names remain
- [x] `Data/NPCs/{Warden,RacketGuard,TentMate,Storekeeper}/CLAUDE.md` -- create identities; update `Innkeeper`→`Cook` and `BlackSmith` identities -- required before dialogue edits
- [x] `Data/NPCs/*/Dialogues`, `Teachings`, `Memories` -- rewrite Rewrite Rules lines -- text matches the camp
- [x] `Data/NPCs/BeatenWorker/` -- use `NPC:create`; greeting + "Who should I watch out for?" topics; place in the Player's Zone -- the one new NPC
- [x] `Data/Quests/` -- reword both quests; spider text says two, matching the tracked facts -- fixes the "five" bug
- [x] `NpcRoutineSpots` + `NpcRoutine` -- retarget to new positions -- schedules follow the move
- [x] `Terrain_StartingArea` -- rebake NavMesh -- moved structures block correctly
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `Data/Facts/CLAUDE.md`, `Data/Quests/CLAUDE.md` -- record zones and new names; grep old names -- keeps docs true

**Acceptance Criteria:**
- Given a new game, when the player walks the camp, then each zone in the Zone Plan is present and reachable, and the Company house is visibly above the worker area.
- Given day and night, when `NpcRoutine` runs, then every NPC reaches its spots with no path errors, and nobody needs a door.
- Given the civilization gate on the south trail, when the player approaches it, then it blocks passage; the north-east trail stays open.
- Given dialogue with each NPC, when all topics are read, then no line mentions a town, village, inn, adventurer or elder.
- Given the shops and teachers, when used, then stock, lockpick, smithing, strength and Power Strike teaching still work.
- Given the spider quest, when both tracked spiders die and the player reports back, then it completes and pays out with text saying two.
- Given Find Herbalist, when accepted from the Warden, then it starts and tracks as before.

## Spec Change Log

- 2026-11-04 (implementation): zone plan fitted to the real terrain. Exits moved onto the existing painted trails (gate from west to south trail, wilderness path to the north-east trail); inner fence moved to z≈9 to clear `Village house (1)`. Added a Company hall, because `House` is only 6×6 m. Tam placed in the Player's Zone per `docs/World/NPCs/npc-002`. Outcrop shack left in place (deferred). `Weapon_TestSword` ×50 added properly: at baseline it sat in slot 7 of a 7-slot array and never appeared.

- 2026-11-04 (review, run inline without independent reviewer agents): removing `CookingTestFood` left Apple, Bread and BrownMushroom with no source, so both sandwich recipes were uncookable; the Storekeeper now stocks 10 of each. `NPC_Bandit._baseHealth` is a float, so the first edit through `intValue` silently did nothing; fixed with `floatValue`.

## Design Notes

**Zone Plan** (approximate world x, z; fit to the ground and adjust by eye)

| Zone | Where | Contents |
|---|---|---|
| Company house | Outcrop, (8..23, −39) | `PF_WoodenHouse03` as the hall; `House` moved, rotated 180° so the door faces the camp, with its crate; flags on the north edge |
| Company yard | South of the inner fence | Forge, anvil, `Chest`, `Locked Chest`, archery targets, `Village house (1)` as the store |
| Inner fence | E–W at z≈9, gate gap at x≈0 | New fence runs from BanditVillage; the old pen fences are removed; open gate gap |
| Center stage | (0, 14) | Raised planks, punishment post, flags; visible from the gate |
| Canteen | `Village house` (−17, 25) | Cook, `CookingStation_Fireplace` moved outside, benches |
| Player's Zone | (8..22, 20..34) | 3 tents, `Bed` beside the player's tent, `PlayerSpawnPoint`, Tent Mate, Beaten Worker by the fire |
| Wretches' Corner | (−8..6, 30..40) | 4 tents, worn and close together |
| Mafia Zone | (24..36, 12..24) | 3 tents, better kept, with crates and a fire |
| Civilization gate | South trail, (−15, −50) | Closed gate and guard shelter; blocks passage |
| Wilderness path | North-east trail, (28, 50) | Open exit, marked by a gap in fencing |
| Wilderness | Beyond the rim | Move `Bandit` group and all 3 spiders |

The basin rim is the camp's edge, so a full outer fence isn't needed. The Warden's night spot is outside the Company house door, since NPCs can't open doors.

**Cast**

| Old | New `entityName` | Folder | Change |
|---|---|---|---|
| Elder Aldric | Warden Voss | `Warden` | New surname: the cook is also Aldric. Brutal, public authority |
| Guard | Guard Harl | `RacketGuard` | Corrupt, keeps the spider quest |
| Villager | Old Hobb | `TentMate` | Older worker, camp logistics |
| Innkeeper | Aldric Sorn | `Cook` | Secret fence; lockpicks now "under the pots" |
| Merchant | Storekeeper | `Storekeeper` | Company employee |
| Blacksmith | Edric (unchanged) | `BlackSmith` | Now a Company smith; dialogue unchanged, frogs stay |
| (new) | Tam | `BeatenWorker` | Dry, sardonic: "You get used to it. Well — most people do." |

**Rewrite Rules:** Workers are addressed as "you" or "new meat", never "adventurer" or "traveller". Alderath is now "Alderath Camp, Company land". Mira becomes the Company's missing herbalist, and the Warden orders the search rather than asking. Harl dumps the spiders on the player, and his reward line loses "the town owes you".

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all pass
- MCP `quest_report` -- expected: no new errors; spider V13 mismatch gone
- `grep -riE '\b(town|village|elder|tavern|adventurer)\b' Assets/_Game/Data/NPCs Assets/_Game/Data/Quests --include=*.asset` -- expected: no player-facing matches

**Manual checks:**
- Top-down and ground screenshots of each zone; no pink materials.
- Play from a new game: walk all zones, sleep to night, check the console for errors.

## Suggested Review Order

**Camp layout**

- Start here: the zone map and what lives where.
  [`Scenes/CLAUDE.md:30`](../../Assets/_Game/Scenes/CLAUDE.md#L30)

- All new props sit under `Camp/<Zone>` in this scene.
  [`StartingTown.unity:1`](../../Assets/_Game/Scenes/StartingTown.unity#L1)

- One prefab per tent, so real art is a single swap.
  [`Tent_Graybox.prefab:1`](../../Assets/_Game/Prefabs/World/Tent_Graybox.prefab#L1)

**Cast**

- Tone source for the Warden, whose order replaces the Elder's request.
  [`Warden/CLAUDE.md:1`](../../Assets/_Game/Data/NPCs/Warden/CLAUDE.md#L1)

- The one new NPC, following the existing npc-002 design.
  [`BeatenWorker/CLAUDE.md:1`](../../Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md#L1)

- Routine notes reflect the moved night spots.
  [`Scripts/AI/CLAUDE.md:55`](../../Assets/_Game/Scripts/AI/CLAUDE.md#L55)

**Quests**

- Spider text now says two, matching the tracked facts.
  [`Quest_SpiderClear.asset:16`](../../Assets/_Game/Data/Quests/Spiders/Quest_SpiderClear.asset#L16)

**Follow-ups**

- Deferred items, including the river and the camp boundary.
  [`deferred-work.md:1`](deferred-work.md#L1)
