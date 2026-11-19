---
title: 'Hobb owns his hut: belongings box, restricted interior, night at home'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '24909de4e818f1c8c3f627f54d5b5d8a9eed7cb4'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Huts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hobb's hut (`worker-mate`) is dressed but means nothing in play: the player can walk in and nothing reacts, he has no belongings to steal, and at night he stands outside his door instead of being home.

**Approach:** Give every worker hut shell a disabled-by-default `RestrictedArea` volume over its interior, switched on with owners per scene instance. Turn it on for Hobb's hut, put an unlocked box he owns inside (8 gold, 2 bread, 1 apple; model `SM_TallSquareWoodBoxClosed`), and move his night routine block into the hut. Existing trespass (scold → fistfight) and theft (return or fight) flows do the rest. An **unseen** take from the box leaves Hobb suspicious: the next time the player talks to him he opens with a one-time line ("someone's been through my things… I'll be watching"), then his normal topics. Built as a reusable NPC "talk opener".

## Boundaries & Constraints

**Always:** Reuse `RestrictedArea`, `Ownership`, `Base_Container` as they are. Hut prefabs stay generated: the volume is added by `WorkerHutKitBuilder`, never by hand in the prefab. An unowned hut (the player's) produces no warnings and no reaction. Hobb's hut is restricted all day, `_warningsBeforeFight` 1. The box gets an Ownership (owner `NPC_TentMate`), a `SaveableObject` with a scene-instance `_saveId` (Validate Save IDs) and a `PersistentID` with its own killed fact, like `BrannsStash`. Gameplay objects stay outside `HomeYardDressing` (tool-rebuilt). The arrival escort and Hobb's briefing outside the door still work. Rebake the NavMesh after placing the box.

**Ask First:** Any code change to `RestrictedArea` or `NpcRoutine`, or to the theft/trespass brain logic beyond the one opener hook below. Moving or removing a `HomeYardDressing` prop to make room.

**Never:** A Sit activity or sitting animation (deferred). Dialogue beyond the suspicion opener; barks or quests. Other huts' owners. A lock on the box.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First trespass | Player walks into Hobb's hut, Hobb sees/hears him | Alert bark, chase, scold, grace to leave | Leaving ends the chase |
| Repeat trespass | Second entry within 24 in-game hours | Hobb brawls the player | KO resets warnings |
| Theft | Player opens the box while Hobb watches | Theft flow: return goods or fight | Unseen theft = no reaction |
| Unseen take | Player takes ≥1 item from the box, nobody catches him | Next Talk: Hobb's suspicion line first, then his topic list; never again | Saved (facts) |
| Caught take | Hobb catches the player for a theft of his goods | Opener marked played, never shows | — |
| Player's hut | Player inside own hut | Nothing happens, no log warning | — |
| Prefab rebuild | `Build Worker Hut Kit Prefabs` rerun | Hobb's override (active + owner) survives | If lost, builder keeps the existing volume's object |

</frozen-after-approval>

## Code Map

- `Editor/World/WorkerHutKitBuilder.cs` -- builds `WorkerHut_Home` / `_Long` from scratch via `SaveAsPrefabAsset`; add the volume here
- `Scripts/World/RestrictedArea.cs` -- shape = child BoxColliders; warns on no owners in `Awake` (inactive GO = never runs)
- `Scripts/World/Ownership.cs`, `Prefabs/World/Containers/Base_Container.prefab` -- owned container (Visual/`SM_chestA_a1` child)
- `StartingTown.unity` → `TentCamp/PlayersZone/worker-mate` (−2.18, 44.56, yaw 63.5); `HiddenStashQuest/BrannsStash` = reference owned box; `NpcRoutineSpots/Spot_TentMate_Night`; TentMate `NpcRoutine` blocks 6–21 Patrol, 21–6 Stand @ `Spot_TentMate_Night`
- `Scripts/AI/NPC/NPCPresence.cs:78` -- `Interact()` → `RaiseDialogueRequest`; `RequestForcedTopic(node, thenTopics)` already shows a topic then the topic list
- `Scripts/AI/EntityBrain.cs:1257` -- `TransitionToConfronting` (theft catch); `HandleTheftCommitted` sees `TheftCommittedData.owner`
- `Scripts/World/ContainerTakeFact.cs` -- sets a WorldFact once on a take from its inventory (`_items` allowlist)
- `Editor/World/HomeYardDresser.cs:171` -- Hobb's interior props (hut-local): bed x −0.89, crate table (0.75, −1.25), stool (0.45, −0.3), bucket, sacks (0.95, 0.55), kindling (−0.8, 1.05); doorway local x 0–1.05 at +Z

## Tasks & Acceptance

**Execution:**
- [x] `Editor/World/WorkerHutKitBuilder.cs` -- add an inactive child `RestrictedArea` (component `RestrictedArea`, no owners, one trigger `BoxCollider` on Ignore Raycast covering the room between wall faces, floor to eave) to both shells; rebuild both prefabs -- every future owned hut gets it with one override
- [x] Rebuild check -- set an override on a hut instance, rebuild, confirm it survives; if not, make the builder reuse the existing prefab's `RestrictedArea` object (LoadPrefabContents) -- owners must not be wiped by a rebuild
- [x] `StartingTown.unity` -- `worker-mate/RestrictedArea`: active, owner `NPC_TentMate`, all day, warnings before fight 1
- [x] `StartingTown.unity` → `PlayersZone/HobbsBox` -- `Base_Container` instance, chest visual off, `SM_TallSquareWoodBoxClosed` visual, collider fitted, at hut-local (−0.04, 0, −1.0) between bed head and crate table, model at 0.8 scale (0.38 m: the 0.44 m gap is narrower than the 0.48 m box); `Ownership` (TentMate), `InteractionHighlight`, `SaveableObject` (keyed by the `PersistentID` killed fact, validator clean), `PersistentID` with a new killed fact; stock `Item_Gold` 8, `Item_Bread` 2, `Item_Apple` 1
- [x] `StartingTown.unity` -- move `Spot_TentMate_Night` inside the hut, beside the stool facing the door, on the rebaked NavMesh
- [x] `Scripts/AI/NPC/NPCPresence.cs` (+ pure `TalkOpenerRules`, EditMode tests) -- optional talk openers `{WorldFact condition, StartDialogueNode topic, WorldFact played, bool cancelWhenWitnessed}`: on Talk, the first opener with condition true and played false opens its topic then the topic list, and sets played **only if the dialogue really opened** (cursor unlocked at once); skipped while this NPC's brain is chasing / scolding a thief; `MarkWitnessedOpenersPlayed()` sets played on every `cancelWhenWitnessed` opener whose condition is already true -- reusable "NPC brings it up next time"
- [x] `Scripts/AI/EntityBrain.cs` -- remember whether a theft this NPC adopted (witnessed) took its own goods; when that chase ends by any exit (scold, give-up, claimed by another catcher, refused-return brawl) call `NPCPresence.MarkWitnessedOpenersPlayed()` -- Hobb saw it = no "wasn't you" line later; expose `IsChasingThief`
- [x] `Data/NPCs/TentMate/` -- facts `WorldFact_Hobb_BelongingsRummaged`, `WorldFact_Hobb_RummageOpenerPlayed`; `Dialogues/Suspicion/Start_` + `Text_TentMate_Rummaged` (one line in Hobb's voice, no em-dashes); `HobbsBox` gets `ContainerTakeFact` (gold, bread, apple) → rummaged fact; TentMate scene `NPCPresence` opener wired
- [x] Docs -- `Prefabs/World/Huts/CLAUDE.md` (volume contract), `Scenes/CLAUDE.md` (box, owned hut, night spot), `Data/NPCs/TentMate/CLAUDE.md`, `deferred-work.md` (Sit activity)

**Acceptance Criteria:**
- Given night, when the clock passes 21:00 (or the player sleeps past it), then Hobb walks (or warps) to his spot inside the hut and stays there until 06:00.
- Given the player inside Hobb's hut in Hobb's sight, then he warns, chases and scolds; on the next entry he starts a fistfight.
- Given the player opens the box in Hobb's sight, then the theft flow runs; the box shows a red prompt/outline while Hobb lives.
- Given the player's own hut, then no RestrictedArea is active and no warning is logged.
- Given save and reload after looting, then the box stays emptied.
- Given an unseen take, when the player later talks to Hobb, then the suspicion line plays first, then his usual topics; talking again shows the topics directly.

## Spec Change Log

- 2026-11-19, review (all three reviewers, bad_spec): the opener was cancelled only when Hobb *caught* the player (`TransitionToConfronting`), so a theft he watched but lost the chase on, or one another witness claimed, still produced "I'd like to think it wasn't you"; and a catch after an empty-handed opening spent the line for good. Amended: cancel at the end of any chase over the NPC's own goods, and only openers whose condition is already true; skip openers while the NPC chases / scolds a thief; mark played only when the dialogue opened. Known-bad state avoided: Hobb accusing-yet-not-accusing a thief he chased. KEEP: the opener as a list on `NPCPresence` with saved condition / played facts, `RaiseDialogueRequest(topic, thenTopics: true)`, the `_pursuitOwnGoods` flag set at adoption from `TheftCommittedData.owner`, the box / area / routine / prefab work unchanged.
- 2026-11-19, review patches: area box inset to the visual wall faces (2.56 m); `_persistentID` guarded in the new owner check; the box is saved through its `PersistentID` killed fact (the save system prefers it over `_saveId`; validator clean), which departs from the frozen "scene-instance `_saveId`" wording and is raised to the human.
- 2026-11-19, re-review patches (no loopback): own-goods chases that end without `EndPursuit` (a hit or knockout mid-chase, a hostile taking over) are now spent by a check at the top of `EntityBrain.Update`; only item / container thefts count as "his things" (not doors / harvests); a dropped opener request falls back to the normal topic list. Rejected: a stronger "dialogue opened" signal than the cursor (editor-only edge); reload mid-chase keeps the opener (runtime chase state isn't saved).

## Design Notes

The volume ships **inactive**: an inactive `RestrictedArea` never runs `Awake`, so the unowned player's hut stays silent with no code change. An owned hut is two overrides (active + owners). `RestrictedArea` docs say "never inside a tool-built root" because Sites rebuilds delete scene children; here the builder itself creates the object, so that risk becomes the rebuild check above.

Found while implementing: the rebuild keeps a uniquely named child's fileID, so overrides survive with no builder change; duplicate-named siblings (walls, roof pieces) get new IDs each rebuild (big harmless prefab diff, no scene references them). A bare `SetActive` from a script is not recorded as an override. Editor play-mode checks: the opener opened Hobb's line and set its played fact; a skip to 23:00 put Hobb on the night spot inside. Trespass and theft reactions could not run headless (the unfocused editor doesn't tick) and are left to the manual checks.

A witnessed chase ends after the container UI closes (catch and give-up wait on a player who is standing at the box), so the rummaged fact is set by then; spending only openers whose condition is true means a witnessed empty-handed opening doesn't spend the line. Known edge: a chase that times out while the player is still in the container UI ends before the take → the line still plays later (accepted).

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after rebuild and in Play -- expected: no new errors or warnings

**Manual checks:**
- Play: enter Hobb's hut at day and at 23:00, twice; steal from the box in sight; sleep to 22:00 and check Hobb is inside.

## Suggested Review Order

**Owned hut volume (prefab-level)**

- Entry point: every shell gets an inactive, ownerless area; owned instances flip two overrides.
  [`WorkerHutKitBuilder.cs:240`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L240)

- Contract for owning a hut, and why rebuilds keep the overrides.
  [`Huts/CLAUDE.md:36`](../../Assets/_Game/Prefabs/World/Huts/CLAUDE.md#L36)

- Scene wiring: area on, box, stock, night spot (inspect `worker-mate`, `HobbsBox` in the editor).
  [`Scenes/CLAUDE.md:137`](../../Assets/_Game/Scenes/CLAUDE.md#L137)

**Talk opener (reusable NPC "brings it up next time")**

- Talk plays the first pending opener before the topic list.
  [`NPCPresence.cs:107`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L107)

- Played only if the dialogue opened; never mid-chase; dropped request falls back.
  [`NPCPresence.cs:114`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L114)

- Witnessed theft spends only openers whose condition is already true.
  [`NPCPresence.cs:148`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L148)

- The opener data and its two pure rules.
  [`NpcTalkOpener.cs:33`](../../Assets/_Game/Scripts/AI/NPC/NpcTalkOpener.cs#L33)

**Brain hook (Hobb saw it = no "wasn't you")**

- Own-goods flag set at adoption, item / container thefts only.
  [`EntityBrain.cs:1133`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1133)

- One spend helper used by every chase exit.
  [`EntityBrain.cs:1351`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1351)

- Catch-all for exits that bypass EndPursuit (hit, knockout, hostile takeover).
  [`EntityBrain.cs:479`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L479)

**Peripherals**

- Rule tests.
  [`TalkOpenerRulesTests.cs:1`](../../Assets/Tests/EditMode/TalkOpenerRulesTests.cs#L1)

- AI doc for the opener mechanism.
  [`AI/CLAUDE.md:247`](../../Assets/_Game/Scripts/AI/CLAUDE.md#L247)

- Deferred: sitting NPCs, opener scoping, owned-hut validator.
  [`deferred-work.md:1091`](deferred-work.md#L1091)
