Your task is to create or edit an NPC shop — the shop dialogue topic, its stock, and the conditions that decide when the
shop topic is offered — following all project conventions.

## Step 0 — Read the rules

Before doing anything else, read in parallel:
- `Assets/_Game/Data/NPCs/CLAUDE.md`
- `Assets/_Game/Data/NPCs/DIALOGUE.md` (section **Shop Topics**)
- `Assets/_Game/Data/NPCs/MEMORIES.md`
- `Assets/_Game/Data/Skills/CLAUDE.md` (only if a skill-based condition is involved)

These are mandatory. Do not skip them.

### How shops work (system limits — tell the user when a request goes beyond them)

- A shop is a topic: `StartDialogueNode` → `ShopDialogueNode`. Reaching the shop node shows its line, then opens the
  trade window on the NPC's **whole** `InventorySystem`.
- **Stock** = `InventorySystem._startingItems` on the NPC's **scene instance** (a prefab-instance override, not data in
  the NPC folder). Prices are the item's own `buyValue` / `sellValue`. Stock does not restock and is also the NPC's loot.
- **Conditions** exist only at topic level: the shop memory's `unlockConditions` (ALL true) and
  `invalidationConditions` (ANY true → closed for good), using `Fact` assets (`SkillFact`, `StatFact`, `DialogueFact`,
  `KilledFact`, `WorldFact`).
- **Not supported** (would need runtime code — say so and ask before going further): per-item visibility conditions,
  price modifiers / discounts, restocking, several separate shops on one NPC with different stock.

---

## Step 1 — Identify the NPC

From `$ARGUMENTS`, extract the NPC name and match a subfolder of `Assets/_Game/Data/NPCs/` (exact, then case-insensitive
partial). If none or several match, list candidates and ask. Then follow the **NPC Identity Check** in
`Data/NPCs/CLAUDE.md` — read `<NPCName>/CLAUDE.md` fully (the shop line must be in the NPC's voice).

---

## Step 2 — Determine the mode(s)

One prompt can combine several modes — run them in this order:

| Mode | Prompt examples | What changes |
|------|-----------------|--------------|
| **CREATE** | "add a shop to X", "X should sell…" (no shop yet) | `Dialogues/Shop/` chain + `Mem_<NPC>_Shop` |
| **CONDITIONS** | "only after…", "hide the shop once…", "always show" | `Mem_<NPC>_Shop` unlock / invalidation facts |
| **STOCK** | "add 10 lockpicks", "set ale to 5", "remove the axe" | scene `_startingItems` via `NpcShopEditor` |
| **TEXT** | "change the shop line / topic label" | `Start_<NPC>_Shop.text` / `Shop_<NPC>.text` |
| **LIST** | "what does X sell?" | nothing — report stock + conditions |

The shop exists when `<NPCName>/Memories/Mem_<NPC>_Shop.asset` exists (older NPCs, e.g. Merchant, may use other names:
also grep `<NPCName>/` for a `ShopDialogueNode` — `m_EditorClassIdentifier: ...ShopDialogueNode`). CREATE on an NPC that
already has a shop → ask whether to edit the existing one instead. If the mode is unclear, ask.

---

## Step 3 — Gather missing values

Ask (numbered list, with proposed defaults) only for what the prompt did not give:

- **CREATE:** topic label (player's voice, default "What do you have for sale?"), shop line (NPC voice, ≤ 300 chars,
  written from the identity), initial stock, conditions (default: always available).
- **STOCK:** each `ItemSO` (resolve under `Assets/_Game/Data/Items/` by name; ask if ambiguous or missing — new items
  are made with `/items:create`) and its count (0 = remove).
- **CONDITIONS:** for each condition, the fact and whether it unlocks or invalidates. Reuse an existing `Fact` asset when
  one matches (grep `Assets/_Game/Data/` for its type/name). A missing `SkillFact` is created in `Assets/_Game/Data/Player/`
  as `SkillFact_<Skill>_Learned` per `Data/Skills/CLAUDE.md`.

---

## Step 4 — Plan and confirm

```
[CREATE / CONDITIONS / STOCK / TEXT] — <NPC> shop

Assets to create:
  - Dialogues/Shop/Start_<NPC>_Shop.asset  (text: "<label>", dialogueFact: null)
  - Dialogues/Shop/Shop_<NPC>.asset        ("<shop line>")
  - Memories/Mem_<NPC>_Shop.asset          (unlock: [..], invalidation: [..])
  - Data/Player/SkillFact_<Skill>_Learned.asset   ← if needed

Assets to modify:
  - Mem_<NPC>_Shop.asset                   ← conditions
  - <Scene>.unity — <NPC> InventorySystem  ← stock: +Item_X x10, -Item_Y

Visibility: shop shown when <plain-language summary of the conditions>.
```

Remind the user that the topic stays visible after the stock sells out. A sold-out shop opens an empty trade window.

If STOCK is part of the plan, check before asking for confirmation:
- the NPC's scene is loaded and **not dirty** (`SceneManager.GetSceneAt(i).isDirty`);
- the editor is not in Play mode.

Otherwise, ask the user to fix that first, so the run doesn't stop halfway after the assets were already created.

Ask: "Does this look right? Shall I proceed?"

---

## Step 5 — Execute

**Assets** (MCP `manage_scriptable_object` / `manage_asset`), in this order:
1. Create missing folders (`Dialogues/Shop/`, `Memories/`).
2. Create needed `Fact` assets.
3. `Shop_<NPC>` (`ShopDialogueNode`, `nextNode` null — the trade window closes the dialogue).
4. `Start_<NPC>_Shop` (`nextNode` → `Shop_<NPC>`, `dialogueFact` null so the shop is repeatable).
5. `Mem_<NPC>_Shop` with `effects.startdialog` → `Start_<NPC>_Shop` and the conditions. Set array elements directly
   (`unlockConditions.Array.data[0]`); `Array.size` patches are unsupported. To clear a condition list, set it to an
   empty array through `execute_code` (`SerializedObject`) rather than patching.
6. Check `NPCEntity.memories` picked up the memory (auto-sync; fallback menu `Game/Dev/Sync All NPC Memories`).

**Stock** — through `execute_code` with `NpcShopEditor` (`Assets/_Game/Editor/NPCs/NpcShopEditor.cs`), outside Play mode.
The NPC's scene must be open: find it with `grep -rl --include=*.unity <NPC_asset_guid> Assets/_Game/Scenes`; if not loaded, open it with
`EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)` (`manage_scene load` can't reach sub-folder scenes) and
close it afterwards if you opened it.

```csharp
var npc  = AssetDatabase.LoadAssetAtPath<Game.NPC.NPCEntity>("Assets/_Game/Data/NPCs/<NPCName>/NPC_<NPCName>.asset");
var item = AssetDatabase.LoadAssetAtPath<Game.Inventory.ItemSO>("Assets/_Game/Data/Items/Item_<Name>.asset");
var result = Game.Editor.NpcShopEditor.SetStock(npc, item, 10);   // 0 = remove
return result.ToString() + "\n" + Game.Editor.NpcShopEditor.DescribeStock(npc);
```

`SetStock` saves the scene, so it refuses a scene that already has unsaved changes. In that case ask the user to save
or discard them. Do not save it yourself. Results:
- `Added` / `Updated` / `Removed`: done, and the scene is saved.
- `Unchanged`: nothing to do.
- `NotFound` (removing an absent item): tell the user that nothing changed.
- `Error`: stop and report the message. Causes are Play mode, a dirty scene, no scene instance or several of them, or a count outside 0..999.
  - With several instances, list them and stop. The user must remove the duplicate first, because the helper only edits a single instance.
  - The message may say the change was APPLIED but not saved. If so, tell the user the scene is dirty with that change.

`DescribeStock` reports the authored **starting** stock, not what is left after trades in Play mode.

After each step, check `read_console` for errors.

---

## Step 6 — Verify

- No new console errors.
- `DescribeStock` output matches the requested stock.
- Run `quest_report` (or Quest Explorer validator) for the NPC's memories — no new issues.
- List every asset created / modified, and restate when the shop topic is visible.
- If `<NPCName>/CLAUDE.md` has an Asset Reference table, add the shop topic and memory there.
