---
title: 'Sealed Passage: Odo''s ring, goat-track drop, found-him-first'
type: 'feature'
created: '2026-11-18'
status: 'done'
baseline_commit: 'c20674a7'
context:
  - '{project-root}/docs/World/Quests/SealedPassage.md'
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/QuarryWorker/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Odo's tally token is a dead quest item: it can't be worn or sold, and handing it to Bren just deletes it. The goat track lets the player walk back out, so the "door opens from inside" beat is optional. A player who finds Odo before meeting Bren still has to promise to go and look for him.

**Approach:** Turn the token into Odo's ring (low-value copper heirloom he always wore, so the body was never robbed): a `Ring1` `ArmorSO`, +1 defense, sellable, equippable. Giving it to Bren moves it into his inventory, even when equipped. The goat track ends in a one-way drop into the ring, so the only exit is the cleft door's latch. Bren's SouthFace gains a "I found him already" row (only if the ring was taken first) that starts the quest and goes straight into the report, ring row included.

## Boundaries & Constraints

**Always:** Keep the item asset GUID and `itemId` (bundle contents, report rows, saves). Keep the fact asset names (`TokenTaken`, `ReportToken`), only their texts change. Bren's voice per DIALOGUE.md (full sentences, no ellipses, ≤300 chars, no em-dashes). Pure rules in static helpers with EditMode tests. Capture the sculpt after grading; rebake the NavMesh on `Terrain_StartingArea`.

**Ask First:** Moving the track line by more than ~10 m; a Terrain / Sites rerun; changing other `requiredItem` choices.

**Never:** Fall damage, ropes or climbing. A ring visual on the hand. Changing the Warden-notice hand-ins.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Give ring, in bag | Ring in inventory, "Here. His ring." | Ring leaves player, added to Bren's inventory; `ReportToken` | Bren has no inventory → consumed + warn (existing) |
| Give ring, worn | Ring equipped, not in bag | Row visible; ring unequipped, then given | N/A |
| Ring sold | Not held, not worn | Row hidden; "nothing" row completes | N/A |
| Other requiredItem | Warden notices (`giveToNpc` false) | Unchanged: consumed | N/A |
| Found first | `TokenTaken`, quest not started, SouthFace choice | "I've been behind that door already. I found him." shown; sets `PassageAccepted`, goes to `Choice_QuarryWorker_Report` | Row hidden when `TokenTaken` false |
| Track drop | Player walks the track over the rim | Drops onto the pad; `RingEntered` set | N/A |
| Climb back | Player on the pad below the drop | No ≤45° path back up; exit by the latch | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` -- `ChoiceOption` (`requiredItem`, `hideWhenMissing`)
- `Assets/_Game/Scripts/World/DialogueSystem.cs:302-460` -- `SelectChoice`, `HasChoiceItem`, `TakeChoiceItem`, `GiveItemToNpc`
- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` -- `IsEquipped`, `Equipped`, `Unequip(slot)`
- `Assets/_Game/Data/Items/Quest/Item_Odo_TallyToken.asset` -- `ItemSO`, quest item; `ArmorSO` script guid from `Armor_TestHelmet.asset`
- `Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/{Report,SouthFace,After}/`, `Memories/`
- `Assets/_Game/Data/Quests/SealedPassage/Quest_SealedPassage.asset` -- step 1 and `ReportToken` entries
- `docs/World/Areas/area-001/layout.json:2125` -- `path-goat-track` points / heights
- `Assets/_Game/Editor/World/Area1FortRamp.cs` -- grader, `GOAT_MAX_ANGLE` 35° per-segment check
- `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- pure terrain helpers
- StartingTown `SealedPassage/RingEntryTrigger/Shape_GoatTrack`, `SealedPassage/OdoRemains` (bundle)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` -- `ChoiceOption.giveToNpc` (bool, tooltip) -- opt-in hand-over to the NPC
- [x] `Scripts/World/DialogueSystem.cs` -- `[SerializeField] EquipmentSystem _playerEquipment`; `HasChoiceItem` also counts an equipped item; taking an equipped item unequips it first; `giveToNpc` routes through `GiveItemToNpc` instead of `TakeChoiceItem`; pure decision in a static helper -- worn ring + Bren's inventory
- [x] Scene (Core / UICanvas, wherever `DialogueSystem` lives) -- wire `_playerEquipment`
- [x] `Tests/EditMode/` -- helper rows: in bag, worn, neither, `giveToNpc` off
- [x] Item asset -- swap to `ArmorSO`, rename `Armor_Odo_Ring`: "Odo's Ring", `slot` Ring1, `defenseBonus` 1, `isQuestItem` 0, buy 6 / sell 2, description per Design Notes; world prefab `Prefabs/Items/Armor_Odo_Ring.prefab` (pickup setup copied from `Item_Key_QuarrySouthDoor.prefab`) using the Blender ring; sync `ItemCatalog`
- [x] Blender MCP -- simple worn copper band (torus, slight wear) → `Art/Items/OdoRing/` FBX + URP copper material; Blender render → icon `Art/UI/Items/` (user added, 2026-11-18)
- [x] Report -- both ring rows `giveToNpc` true; row "Here. His ring."; rewrite `Text_QuarryWorker_Report_Token`, `_Token_2`, After_Token per Design Notes
- [x] SouthFace -- gate-only `Mem_QuarryWorker_FoundFirst` (unlock `TokenTaken`, synced to `NPCEntity.memories`); new row on `Choice_QuarryWorker_SouthFace` (requiredMemory, `dialogueFact` `PassageAccepted`) → new text → `Choice_QuarryWorker_Report`
- [x] Quest -- step 1 entry "I found Odo, and took the ring from his hand.", `ReportToken` entry "I gave Bren Odo's ring. At least he knows now."
- [x] `layout.json` + `Area1FortRamp.cs` + `Area1TerrainMath.cs` -- new inner profile: notch → ledge ≥ 8 m above the pad → drop point ≤ 2 m further at pad level → pad; `"drops": [segment index]` exempts that segment from the 35° check; regrade, capture, rebake; move `Shape_GoatTrack` under the drop if needed
- [x] ~~`Area1TerrainMath.CanReach` flood fill~~ (removed, see Spec Change Log) → `Editor/World/GoatTrackDropCheck.cs` -- `Tools/World/Check Goat Track Drop` drives a temporary `CharacterController` (player CC size / slope / step, `PlayerConfig.jumpForce`, gravity) from a grid of pit starts in 12 directions, then a turn towards the ledge, jumping whenever grounded; fails if any run gets past the drop's top; also walks off the ledge and expects the pit floor; pure verdict helper + tests (incl. a box-vs-steep-collider climb test)
- [x] StartingTown `SealedPassage/GoatTrack/DropBlockers` -- invisible `Ignore Raycast` boxes: `Lip` (fills the gap below y 44: walk-off top, sheer pit face), `PitWall_N` / `PitWall_S` (y 28–62 along the pit's sides) -- a terrain face of any steepness is jump-climbable
- [x] Docs -- quest spec (item, report, SouthFace, flow, Part D), `Scenes/CLAUDE.md` (drop), `Scripts/World/CLAUDE.md` (`giveToNpc`, worn items), `Data/NPCs/QuarryWorker/CLAUDE.md`

**Acceptance Criteria:**
- Given the regraded track and the drop blockers, when `Check Goat Track Drop` runs, then walking off the ledge lands in the pit and no simulated jump-climb from the pit gets back past the ledge.
- Given the ring equipped, when the player opens the inventory, then defense shows +1 and the ring can be sold to a trader for 2 gold.
- Given the edit, when running `quest_report SealedPassage` and `quest_report audit`, then no new issues; EditMode tests green.

## Spec Change Log

- **2026-11-18, review loop 1 (bad_spec, drop verification).** Trigger: blind review said terrain faces are
  jump-climbable whatever the heightmap step; confirmed by driving a `CharacterController` up the 16 m pit face (max y 71:
  it counts as grounded on terrain contacts). The flood-fill `CanReach` proof (1.6 m per cell) gave a false pass.
  Amended: the drop check task (CC simulation instead of the flood fill) and a new blockers task; AC 1 reworded.
  Known-bad state avoided: a "one-way" drop the player can climb straight back out of. KEEP: all ring / dialogue /
  quest / docs work, `giveToNpc` + worn hand-over (with the review patch: item before gold, re-equip on failure), the
  layout profile with `drops`, the grader's drop exemption and `CutDropPit`, the triggers, the capture and rebake.

## Design Notes

Ring description: "A thin copper band, worn smooth and nearly through at the back. Someone has scratched ODO inside it. Worth next to nothing, so nobody ever bothered to take it off him."

Report token texts (≤300): "That's his ring. His grandfather's, he said, and he never once took it off, swore it kept the rock off his head. Didn't keep much off in the end, did it?" then "I'll wear it for him, somebody should. Here, take this, friend. It's not much, but I'd rather it went to you than to Dace's ledger." After_Token: "I wear his ring on my little finger now, it's all it'll fit. Some days I cut a few blocks over and tell myself they're his, and Dace's ledger doesn't know the difference."

Found-first row: "I've been behind that door already. I found him." → Bren: "You've been in? Before I'd even asked? Then you know more than I do, friend, and I've waited long enough." → report choice (truth / lie, then latch / ring / nothing).

## Verification

**Commands:**
- MCP `run_tests` EditMode -- all pass
- MCP `quest_report SealedPassage`, `quest_report audit` -- no new issues

**Manual checks:**
- Grader log + `Check Goat Track Drop` output; scene screenshot of the ledge and the drop.
- Play mode: walk the track, drop, try to climb back, leave via the latch; give the worn ring to Bren and check his inventory.

## Suggested Review Order

**Goat-track drop (the riskiest part)**

- Entry point: a CharacterController simulation proves the drop one-way; heightmap models gave false passes.
  [`GoatTrackDropCheck.cs:38`](../../Assets/_Game/Editor/World/GoatTrackDropCheck.cs#L38)

- Escape rule: behind the top, or standing over the ledge within the gap's width.
  [`GoatTrackDropCheck.cs:191`](../../Assets/_Game/Editor/World/GoatTrackDropCheck.cs#L191)

- Two-leg jump-climb driver, shared with the physics tests.
  [`GoatTrackDropCheck.cs:132`](../../Assets/_Game/Editor/World/GoatTrackDropCheck.cs#L132)

- The blockers that actually make it one-way (Lip, PitWall_N / _S), hand-placed scene boxes.
  [`StartingTown.unity:265973`](../../Assets/_Game/Scenes/StartingTown.unity#L265973)

- Layout: ledge, drop point 7 listed in `drops`.
  [`layout.json:2187`](../../docs/World/Areas/area-001/layout.json#L2187)

- Grader cuts a landing pit behind each drop after the corridor pass.
  [`Area1FortRamp.cs:115`](../../Assets/_Game/Editor/World/Area1FortRamp.cs#L115)

- Drop segments are left out of the steepness checks.
  [`Area1FortRamp.cs:247`](../../Assets/_Game/Editor/World/Area1FortRamp.cs#L247)

- Pure pit rectangle: only ever lowers the ground.
  [`Area1TerrainMath.cs:26`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L26)

**Giving the ring (worn or not) to Bren**

- Hand-over before gold; worn item unequipped, re-worn if the hand-over fails.
  [`DialogueSystem.cs:458`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L458)

- Choice order: item check, gold check, hand-over, then pay.
  [`DialogueSystem.cs:305`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L305)

- A worn copy counts as held, so the hidden ring row still shows.
  [`DialogueSystem.cs:450`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L450)

- Opt-in flag: Warden notices stay consumed.
  [`ChoiceDialogueNode.cs:26`](../../Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs#L26)

- Bag wins over the worn copy.
  [`ChoiceItemRules.cs:12`](../../Assets/_Game/Scripts/World/ChoiceItemRules.cs#L12)

**Quest content**

- Ring rows give to Bren.
  [`Choice_QuarryWorker_Report_Truth.asset:35`](../../Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/Report/Choice_QuarryWorker_Report_Truth.asset#L35)

- Found-him-first row, appended so seen-state indices don't move.
  [`Choice_QuarryWorker_SouthFace.asset:59`](../../Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/SouthFace/Choice_QuarryWorker_SouthFace.asset#L59)

- Token converted in place to an ArmorSO ring (same GUID / itemId).
  [`Armor_Odo_Ring.asset:31`](../../Assets/_Game/Data/Items/Quest/Armor_Odo_Ring.asset#L31)

**Tests**

- Vertical box stops a jump-climb, a 70° slope doesn't: the design's premise.
  [`GoatTrackDropCheckTests.cs:58`](../../Assets/Tests/EditMode/GoatTrackDropCheckTests.cs#L58)

- Worn ring handed to the NPC.
  [`ChoiceItemRulesTests.cs:35`](../../Assets/Tests/EditMode/ChoiceItemRulesTests.cs#L35)

- Pit rectangle rows.
  [`Area1TerrainMathTests.cs:394`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L394)

