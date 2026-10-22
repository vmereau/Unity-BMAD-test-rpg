---
title: '/NPC:shop-dialogue — author NPC shop topics, stock and visibility conditions'
type: 'feature'
created: '2026-10-22'
status: 'done'
baseline_commit: 'd977cf72a8e54652d6fc9e9649830b47bab51637'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Data/NPCs/CLAUDE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/MEMORIES.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Giving an NPC a shop means hand-wiring a `StartDialogueNode` → `ShopDialogueNode` chain, a memory with visibility facts, and editing the scene NPC's `InventorySystem._startingItems` override. No command covers it, and `MEMORIES.md` documents shop effects that don't exist.

**Approach:** A prompt-driven command `/NPC:shop-dialogue` with modes: create shop topic, add/set item stock, remove item, set shop visibility conditions, list. A small deterministic editor helper edits the scene stock. First use: the Innkeeper sells 10 lockpicks once the player learns Beginner Lockpicking. His Beginner teaching choice also gets its own confirm line telling the player he can sell picks.

## Boundaries & Constraints

**Always:**
- Shop layout: `<NPC>/Dialogues/Shop/` holds `Start_<NPC>_Shop` (repeatable, `dialogueFact` null) → `Shop_<NPC>` (`ShopDialogueNode`, NPC line in-character per `<NPC>/CLAUDE.md`); `<NPC>/Memories/Mem_<NPC>_Shop` with `effects.startdialog`.
- Visibility = topic-level only: memory `unlockConditions` (ALL) / `invalidationConditions` (ANY) using existing `Fact` assets; new `SkillFact`s go in `Data/Player/` per `Data/Skills/CLAUDE.md`.
- Stock = `InventorySystem._startingItems` on the scene instance whose `PersistentID.Entity` is the NPC; edits recorded as prefab-instance overrides, scene marked dirty and saved.
- Innkeeper: topic label "I need lockpicks", unlocked by `SkillFact_BeginnerLockpicking_Learned`, no invalidation; stock `Item_Lockpick` ×10. Beginner choice `confirmNextNode` → new `Text_Innkeeper_Lockpicking_ConfirmBeginner` (mentions he sells picks) looping back to `TeachChoice_Innkeeper_Lockpicking`; Expert keeps the shared confirm.

**Ask First:**
- The NPC has 0 or ≥2 scene instances in the open scenes, or `Mem_<NPC>_Shop` already exists in create mode.
- The user requests per-item conditions or price modifiers (not supported by the system).

**Never:**
- Runtime code changes (trade UI, restocking, item-level gating, price modifiers).
- Editing `NPCEntity.memories` by hand (auto-synced).
- Raw YAML edits of the scene.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Add new item | stock lacks item, count 10 | entry appended `{item, 10}` | N/A |
| Set existing | item present ×3, count 10 | that entry becomes 10, no duplicate | N/A |
| Remove | item present, count 0 | entry removed | N/A |
| Remove missing | item absent, count 0 | stock unchanged | result flags not found |
| Bad count | count < 0 or null item | stock unchanged | error returned |
| No instance | NPC not in open scenes | nothing changed | error lists open scenes |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` -- `StartingItem {item, count}`, `_startingItems` (shop stock source)
- `Assets/_Game/Scripts/World/DialogueSystem.cs:118` -- `ShopDialogueNode` opens `NPCTradeUI` with NPC inventory
- `Assets/_Game/Scripts/World/PersistentID.cs` -- `Entity` identifies scene NPC instances
- `Assets/_Game/Data/NPCs/Merchant/` -- reference shop chain + memory
- `Assets/_Game/Data/NPCs/Innkeeper/Teachings/Lockpicking/` -- teach chain to edit
- `Assets/_Game/Scenes/StartingTown.unity` -- Innkeeper instance
- `.claude/commands/NPC/teach-dialogue.md` -- command style reference
- `Assets/_Game/Scripts/Editor/NPCMemoriesAutoSync.cs` -- `Sync All NPC Memories` fallback (fixed during implementation: trailing-slash `GetSubFolders` returned nothing)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Editor/NPCs/NpcShopEditor.cs` -- static `NpcShopEditor` (namespace `Game.Editor`): pure `ApplyStockChange(List<StartingItem>, ItemSO, int count)` returning a result (added/updated/removed/notFound/error); `FindInstances(NPCEntity)` over loaded scenes; `SetStock(NPCEntity, ItemSO, int)` and `DescribeStock(NPCEntity)` via `SerializedObject`, record overrides, save scene -- deterministic core called through `execute_code`.
- [x] `Assets/Tests/EditMode/NpcShopEditorTests.cs` -- cover the pure matrix rows.
- [x] `.claude/commands/NPC/shop-dialogue.md` -- command: read NPC docs + identity; resolve NPC; detect mode(s) from prompt; gather missing values; plan + confirm; create assets via MCP; call `NpcShopEditor`; check console + `NPCEntity.memories` sync; report.
- [x] Innkeeper run -- run the command for the Innkeeper shop + stock, and add the Beginner confirm node.
- [x] Docs -- fix `MEMORIES.md` effects table (only `startdialog` exists; shop visibility via conditions); add shop section to `DIALOGUE.md`; add command to root `CLAUDE.md` table and `Data/NPCs/CLAUDE.md`; Innkeeper `CLAUDE.md` asset table.

**Acceptance Criteria:**
- Given Beginner Lockpicking is not learned, when talking to the Innkeeper, then "I need lockpicks" is not listed.
- Given the player learns Beginner Lockpicking from him, then the confirm line mentions he sells picks and returns to the choice menu; Expert still uses the old confirm line.
- Given Beginner Lockpicking is learned, when choosing "I need lockpicks", then the trade window opens showing 10 Lockpicks at their buy value.

## Design Notes

Shops expose the whole NPC inventory, so conditions can only gate the topic. The helper mirrors `ItemAssetBuilder`: Claude orchestrates and the C# does the edits, so the scene override edit can be reviewed and is repeatable.

## Verification

**Commands:**
- `run_tests` EditMode `NpcShopEditorTests` -- expected: all pass
- `read_console` errors after compile and after the Innkeeper run -- expected: none

**Manual checks:**
- `quest_report` / Quest Explorer validator shows no new issues for Innkeeper memories.

## Suggested Review Order

**Stock editing (editor helper)**

- Scene write path: Play-mode / dirty-scene guards, SerializedObject override, scene save.
  [`NpcShopEditor.cs:93`](../../Assets/_Game/Editor/NPCs/NpcShopEditor.cs#L93)

- Pure stock rules: add / set / remove / unchanged, count cap, duplicate collapse.
  [`NpcShopEditor.cs:42`](../../Assets/_Game/Editor/NPCs/NpcShopEditor.cs#L42)

- Instance lookup by `PersistentID.Entity` across loaded scenes.
  [`NpcShopEditor.cs:74`](../../Assets/_Game/Editor/NPCs/NpcShopEditor.cs#L74)

**Orchestration (Claude command)**

- System limits the command must surface (topic-level conditions only, scene stock).
  [`shop-dialogue.md:14`](../../.claude/commands/NPC/shop-dialogue.md#L14)

- Mode detection, then plan with clean-scene pre-check.
  [`shop-dialogue.md:36`](../../.claude/commands/NPC/shop-dialogue.md#L36)

- execute_code snippet and result handling.
  [`shop-dialogue.md:110`](../../.claude/commands/NPC/shop-dialogue.md#L110)

**Innkeeper data**

- Shop memory gated on Beginner Lockpicking.
  [`Mem_Innkeeper_Shop.asset:1`](../../Assets/_Game/Data/NPCs/Innkeeper/Memories/Mem_Innkeeper_Shop.asset#L1)

- Beginner confirm line, loops back to the teach menu.
  [`Text_Innkeeper_Lockpicking_ConfirmBeginner.asset:1`](../../Assets/_Game/Data/NPCs/Innkeeper/Teachings/Lockpicking/Text_Innkeeper_Lockpicking_ConfirmBeginner.asset#L1)

- Shop topic → shop node.
  [`Start_Innkeeper_Shop.asset:1`](../../Assets/_Game/Data/NPCs/Innkeeper/Dialogues/Shop/Start_Innkeeper_Shop.asset#L1)

**Peripherals**

- Memory sync fallback fix (trailing slash; skips non-NPC folders).
  [`NPCMemoriesAutoSync.cs:69`](../../Assets/_Game/Scripts/Editor/NPCMemoriesAutoSync.cs#L69)

- Shop docs; stale memory effects removed.
  [`DIALOGUE.md:129`](../../Assets/_Game/Data/NPCs/DIALOGUE.md#L129)
  [`MEMORIES.md:56`](../../Assets/_Game/Data/NPCs/MEMORIES.md#L56)

- Pure stock-rule tests.
  [`NpcShopEditorTests.cs:15`](../../Assets/Tests/EditMode/NpcShopEditorTests.cs#L15)
