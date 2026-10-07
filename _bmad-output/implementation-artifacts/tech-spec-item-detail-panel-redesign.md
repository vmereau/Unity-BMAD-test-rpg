---
title: 'Item Detail Panel Redesign'
slug: 'item-detail-panel-redesign'
created: '2026-10-07'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'URP 17.x', 'Unity UI (UGUI)', 'TextMeshPro (LiberationSans SDF)', 'Unity Test Framework (EditMode)']
files_to_modify:
  - 'Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs (NEW: pure static category/stat/price logic)'
  - 'Assets/_Game/Scripts/UI/Inventory/ItemStatLine.cs (NEW: readonly struct + StatPolarity enum)'
  - 'Assets/_Game/Scripts/UI/Inventory/ItemPriceContext.cs (NEW: enum Value/Buy/Sell)'
  - 'Assets/_Game/Scripts/UI/Inventory/ItemStatRowUI.cs (NEW: one label/value row)'
  - 'Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs (rewrite: header/stats/description/price)'
  - 'Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs (labels become Buy / Sell)'
  - 'Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs (Show(item, Buy|Sell))'
  - 'Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs, EquipmentUI.cs, ContainerUI.cs (verify only: default Value context)'
  - 'Assets/_Game/Prefabs/UI/Items/ItemStatRow.prefab (NEW)'
  - 'Assets/_Game/Prefabs/UI/Items/ItemDetailPanel.prefab (rebuild internal hierarchy, keep ActionsContainer)'
  - 'Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab, Trade/NPCTradeUI.prefab, Container/ContainerUI.prefab (strip orphaned child overrides on the nested panel)'
  - 'Assets/_Game/Data/Items/Item_Health_Potion.asset (description becomes cosmetic)'
  - 'Assets/Tests/EditMode/ItemDetailFormatterTests.cs (NEW)'
  - 'Assets/_Game/Scripts/UI/Inventory/CLAUDE.md (ItemDetailPanelUI notes)'
code_patterns:
  - 'Shared display panel + per-host actions prefab nested statically in ActionsContainer'
  - 'Display logic split: pure static formatter (testable) + thin MonoBehaviour painter'
  - 'Optional [SerializeField] refs with null guards; CanvasGroup-based Hide()'
  - 'Stat rows pooled in place (reuse children, SetActive(false) extras): no destroy per Show'
  - 'TMP_Text only; StringBuilder for multi-part strings; GameLog not Debug.Log'
  - 'Editor-driven prefab edits via MCP; never raw YAML + refresh_unity force'
test_patterns:
  - 'EditMode NUnit tests for pure logic in Assets/Tests/EditMode (Tests.EditMode asmdef references Game)'
  - 'ScriptableObject.CreateInstance<T>() for item fixtures, DestroyImmediate in TearDown'
  - 'UI layout verified by manual playtest (no UI layout tests, per project-context)'
---

# Tech-Spec: Item Detail Panel Redesign

**Created:** 2026-10-07

## Overview

### Problem Statement

The item viewer (`ItemDetailPanel.prefab` + `ItemDetailPanelUI.cs`) is already one shared component, nested once
in `InventoryUI.prefab`, `NPCTradeUI.prefab` and `ContainerUI.prefab`. Each host adds its own action buttons into
`ActionsContainer`. Its content is weak, though:

- **No price.** `ItemDetailPanelUI` never shows `ItemSO.buyValue` / `sellValue`. The price only appears in the
  trade context, as the label of the Buy/Sell buttons (`TradeDetailActions`: `Buy (Xg)` / `Sell (Xg)`). The
  inventory and containers show no value at all.
- **Inconsistent layout.** Font sizes vary from text to text (18 / 25 / 36 / auto-size). Stats are spread over fixed
  text objects (`Damage`, `Bonus Armor`, `Armor Type`, `EquipableStatBonusText` as one multi-line string). Some
  placeholder texts are still in the prefab (`'Armor: 10'`).
- **Missing information.** The potion heal amount, the consumable/stack info and the item category are not shown. A
  plain `ItemSO` (e.g. spider parts) shows only its name and description. The health potion hard-codes its heal
  value in its description text.

### Solution

Redesign the internals of the shared `ItemDetailPanel.prefab` into a clean layout:
**header** (icon + name + category line) → **stat rows** (uniform label/value rows spawned from a row prefab, shown
only when there are stats) → **description** → **footer** (a price line that depends on the context, then
`ActionsContainer`). `ItemDetailPanelUI.Show` gets a price-context parameter so each host can choose the price
label/value. The trade buttons lose their embedded price and become plain `Buy` / `Sell`.

### Scope

**In Scope:**

- Rebuild the `ItemDetailPanel.prefab` hierarchy (header / stats / description / footer) with consistent text styles.
- Data-driven stat rows from a new `ItemStatRow` prefab:
  - Equipment: weapon damage, STR/DEX/END/INT/DEF bonuses (only non-zero; positive values green, negative values
    red); the armor slot goes in the category line
  - Potion: heal amount
  - Skill tome: the skill taught + LP cost as rows; the skill description stays as secondary text
  - Consumable / reusable tag, and "Stack up to N" for stackable items
- A category line under the name.
- A price footer that depends on the context:
  - Inventory / equipment slot / container: `Value  Xg` (`sellValue`)
  - Trade, NPC side: `Price  Xg` (`buyValue`)
  - Trade, player side: `Sells for  Xg` (`sellValue`)
- `TradeDetailActions` button labels become plain `Buy` / `Sell` (interactable/affordability logic unchanged).
- Strip the orphaned child overrides on the nested `ItemDetailPanel` instances in the three host prefabs (keep only
  the root placement).
- Health potion description changed to cosmetic text; the heal value is shown as a stat row.
- EditMode tests for the formatter logic.
- Update `Scripts/UI/Inventory/CLAUDE.md` (ItemDetailPanelUI notes).

**Out of Scope:**

- Comparison with the currently equipped item (stat deltas): possible follow-up spec.
- Hover tooltips (the panel stays click-to-select).
- New item data fields (weight, rarity, category override, etc.).
- Changes to action-button behavior (Drop/Use/Equip/Take/Put/Buy/Sell logic).
- The trade right-click context menu labels (`NPCTradeUI.cs:229,240`, `Buy (Xg)` / `Sell (Xg)`): kept as-is.

## Context for Development

### Codebase Patterns

- **Shared panel, per-host actions.** `ItemDetailPanel.prefab` (`ItemDetailPanelUI`, display-only) is nested once
  in `InventoryUI.prefab`, `NPCTradeUI.prefab` and `ContainerUI.prefab`. Each host statically nests its actions
  prefab (`InventoryDetailActions` / `TradeDetailActions` / `ContainerDetailActions`) under the panel's
  `ActionsContainer` child. That child **must survive the rebuild with its fileID** (move/reparent it, never
  delete and recreate it), or the host-nested actions prefabs lose their parent.
- **Callers of `Show(ItemSO)`** (all must keep compiling):
  - `InventoryUI.UpdateDetailPanel` (`InventoryUI.cs:279-283`)
  - `EquipmentUI.OnSlotClicked` (`EquipmentUI.cs:74-78`, field `_itemDetailPanel`)
  - `NPCTradeUI.UpdateDetailPanel(item, slotIndex, TradeSide side)` (`NPCTradeUI.cs:154-159`)
  - `ContainerUI.UpdateDetailPanel` (`ContainerUI.cs:238-242`)
  - `Hide()` is called from every host's `ClearSelection()`.
- **Current prefab state (to be replaced):**
  - Root `ItemDetailPanel`: Image `rgba(0.1,0.1,0.1,0.85)` + `ItemDetailPanelUI`, SizeDelta y=800 (placement set by
    the hosts).
  - Children:
    - `Icon` 150×150
    - `ItemName` (font 25)
    - `Description` (18)
    - `EquipableItemWrapper`:
      - `Weapon` (`Damage` 18, `WeaponDamageBonusText` auto-size)
      - `Armor` (`Armor Type` 18, `Bonus Armor` placeholder 'Armor: 10')
      - `EquipableStatBonusText` (36, one multi-line string)
    - `UsableItemWrapper` → `SkillItem` (`SkillName`, `LearningPointsCost`, `Description`, all 18)
    - `ActionsContainer` (VerticalLayoutGroup + ContentSizeFitter vertical)
  - Font: `LiberationSans SDF`.
- **Host overrides:** each host overrides about 15 child `RectTransform` properties (anchoredPosition / anchors /
  sizeDelta) on the nested instance, plus 7 `m_TextStyleHashCode` overrides in `InventoryUI`. Deleting the old
  children orphans them, so strip every modification whose target isn't the nested root GameObject/RectTransform
  or `ActionsContainer`.
- **Item data available** (`Game.Inventory`):
  - `ItemSO`: `itemName`, `description`, `icon`, `maxStacks`, `IsStackable`, `buyValue`, `sellValue`
  - `EquipableItemSO`: `strengthBonus`, `dexterityBonus`, `enduranceBonus`, `intelligenceBonus`, `defenseBonus` (int)
  - `WeaponSO`: `damageBonus` (float); `archetype` has **no display name**, so the category is just `Weapon`
  - `ArmorSO`: `slot` (`EquipmentSlot`)
  - `UsableItemSO`: `consumable`
  - `PotionItemSO`: `HealAmount` (float)
  - `SkillItemSO.Skill` → `SkillSO.displayName`, `.description`, `.lpCost` (`ScriptableObjects/Skills/SkillSO.cs`)
- **Current item assets** (`Assets/_Game/Data/Items/`):
  - Health Potion: desc "Restores 30 HP when consumed.", heal 30, stack 10, 10g/5g
  - Mana Potion: **plain `ItemSO`**, desc "Restore a small amount of mana", no use
  - Tome of Power Strike: 1g/1g
  - Darkness Spider Head / Leg: plain `ItemSO`, stack 50, 5g/1g and 15g/5g
  - Test Armor: END+1, DEF+5. Test Helmet: DEF+2. big sword: DMG 20, STR+3, DEX+1. Axe: DMG 5. Test Sword: DMG 5,
    STR+3.
  - Some descriptions are empty (Test Armor / Helmet / Sword), so the description block must hide when empty.
- **No coin sprite** exists in the project. The price value is rendered as gold-tinted text `25g` (avoid the `●`
  glyph: not guaranteed in LiberationSans SDF).
- UI rules (`Scripts/UI/CLAUDE.md`): TMP only, `StringBuilder` for runtime strings, toggle with `SetActive`
  instead of `text = ""`, `GameLog` not `Debug.Log`.
- No existing EditMode test references `ItemDetailPanelUI`, the `TradeDetailActions` labels, `DMG:` or `LP Cost`,
  so there are no stale assertions to update.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs` | Shared display script (to rewrite) |
| `Assets/_Game/Prefabs/UI/Items/ItemDetailPanel.prefab` | Shared panel prefab (to rebuild; keep `ActionsContainer`) |
| `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs` | Buy/Sell labels with the price embedded |
| `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` | Trade host: `UpdateDetailPanel(item, idx, side)`; context-menu labels at :229/:240 |
| `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs`, `EquipmentUI.cs`, `ContainerUI.cs` | Other `Show(item)` callers |
| `Assets/_Game/ScriptableObjects/Items/*.cs`, `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` | Item data fields |
| `Assets/_Game/Data/Items/Item_Health_Potion.asset` | Description to make cosmetic |
| `Assets/Tests/EditMode/InventoryPrimaryActionTests.cs` | Test style reference (SO fixtures, TearDown cleanup) |
| `_bmad-output/implementation-artifacts/tech-spec-itemdetailpanel-actions-prefab-separation.md` | Origin of the shared panel + actions split |

### Technical Decisions

- **Layout:** "Header + stat rows". Confirmed by Valentin.
  - Header: icon (96×96) on the left; name (26, bold) + category line (16, muted) on the right.
  - Then, top to bottom: the stats list → the skill description (16, italic, only for tomes) → the description
    (17, italic, muted) → the footer: the price row (18; label left, gold value right) → `ActionsContainer`.
  - Thin dividers between non-empty blocks. Only three text sizes: 26 / 18 / 16 (+17 for the description).
- **Price (context-aware):** new `ItemPriceContext { Value, Buy, Sell }`.
  - `Value` → "Value" / `sellValue`: inventory, equipment slots, containers (the default)
  - `Buy` → "Price" / `buyValue`: trade NPC side
  - `Sell` → "Sells for" / `sellValue`: trade player side
  - `TradeDetailActions` labels become `Buy` / `Sell`; the affordability/interactable logic is unchanged.
- **Stats (all four sources):** rows of `Label ........ Value`, colored by polarity (positive green, negative red,
  neutral light gray).
  - Weapon: `Damage +20` (positive), then the non-zero bonuses as `Strength +3`, `Dexterity +1`, ...
    (full words: Strength, Dexterity, Endurance, Intelligence, Defense)
  - Armor: non-zero bonuses only. The slot is in the category line (`Armor · Helmet`), not repeated as a row.
  - Potion: `Restores Health +30` (positive)
  - Skill tome: `Teaches Power Strike`, `LP Cost 3`; the skill description is shown as secondary text under the
    stats
  - Usable items: a `Consumable` / `Reusable` row (neutral)
  - Stackable items (`maxStacks > 1`): a `Stack up to 50` row (neutral)
  - A stats block with zero rows is hidden entirely (spider parts → header, description, price only).
- **Category line:**
  - Weapon → `Weapon`
  - Armor → `Armor · <slot>` (Helmet / Body Armor / Ring / Necklace)
  - Potion → `Potion`
  - SkillItem → `Skill Tome`
  - Plain `ItemSO` → `Miscellaneous` (no data field tells a "material" apart from other misc items like the Mana
    Potion; adding one is out of scope)
- **Health potion description** becomes cosmetic: "A red tonic that mends wounds." The value lives in the
  `Restores Health +30` stat row. (Valentin's request, added in Step 2.)
- **Testability:** all decision logic (category, stat lines, price label/value) lives in the pure static
  `ItemDetailFormatter`, covered by EditMode tests. `ItemDetailPanelUI` only paints.
- **Equipped-item comparison:** out of scope. Confirmed.

## Implementation Plan

### Tasks

- [x] Task 1: Add the stat-line data types
  - File: `Assets/_Game/Scripts/UI/Inventory/ItemStatLine.cs` (NEW, namespace `Game.UI`)
  - Action: Declare `public enum StatPolarity { Neutral, Positive, Negative }` and
    `public readonly struct ItemStatLine { public readonly string Label; public readonly string Value; public readonly StatPolarity Polarity; }`
    with a constructor `(string label, string value, StatPolarity polarity = StatPolarity.Neutral)`.
  - File: `Assets/_Game/Scripts/UI/Inventory/ItemPriceContext.cs` (NEW, namespace `Game.UI`)
  - Action: `public enum ItemPriceContext { Value, Buy, Sell }` with an XML doc comment on each member (`Value` =
    `sellValue` shown as "Value", `Buy` = `buyValue` shown as "Price", `Sell` = `sellValue` shown as "Sells for").
  - Notes: These are plain C# types (not SOs), so the one-class-per-file SO memory rule does not apply. Keep one type
    family per file anyway.

- [x] Task 2: Create the pure formatter
  - File: `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` (NEW, `public static class ItemDetailFormatter`,
    namespace `Game.UI`)
  - Action: Implement:
    - `string GetCategory(ItemSO item)`: `null` → `""`; `WeaponSO` → `"Weapon"`; `ArmorSO a` →
      `"Armor · " + ArmorSlotName(a.slot)`; `PotionItemSO` → `"Potion"`; `SkillItemSO` → `"Skill Tome"`; any other
      `UsableItemSO` → `"Usable"`; any other `EquipableItemSO` → `"Equipment"`; anything else → `"Miscellaneous"`.
      The switch order matters: concrete types before abstract bases.
    - `string ArmorSlotName(EquipmentSlot slot)` (private): Helmet → "Helmet", Armor → "Body Armor", Ring1/Ring2 →
      "Ring", Necklace → "Necklace", default → `slot.ToString()`.
    - `void BuildStatLines(ItemSO item, List<ItemStatLine> into)`: `into.Clear()` first; `null` item → return. Order:
      1. Type-specific rows:
         - `WeaponSO w` with `w.damageBonus != 0` → `("Damage", Signed(w.damageBonus), polarity)`
         - `PotionItemSO p` with `p.HealAmount > 0` → `("Restores Health", "+" + p.HealAmount.ToString("F0"), Positive)`
         - `SkillItemSO s` with `s.Skill != null` → `("Teaches", s.Skill.displayName, Neutral)` then
           `("LP Cost", s.Skill.lpCost.ToString(), Neutral)`
      2. If `item is EquipableItemSO e`: one row per non-zero bonus in fixed order Strength, Dexterity, Endurance,
         Intelligence, Defense → `(name, Signed(value), polarity)`.
      3. If `item is UsableItemSO u`: `(u.consumable ? "Consumable" : "Reusable", "", Neutral)` (label-only tag row).
      4. If `item.IsStackable`: `("Stack", "up to " + item.maxStacks, Neutral)`.
    - `Signed(int)` / `Signed(float)` (private): positive → `"+N"`, negative → `"-N"` (`F0` for float); polarity
      Positive if > 0, Negative if < 0.
    - `string GetSkillDescription(ItemSO item)`: the `SkillItemSO.Skill.description` when present and not
      empty/whitespace, else `null`.
    - `string GetPriceLabel(ItemPriceContext ctx)`: Value → "Value", Buy → "Price", Sell → "Sells for".
    - `int GetPrice(ItemSO item, ItemPriceContext ctx)`: `null` → 0; Buy → `item.buyValue`; Value/Sell →
      `item.sellValue`.
  - Notes: No `UnityEngine.Object` lifecycle; must be callable from EditMode tests. Cache the label strings as
    `const`. No LINQ.

- [x] Task 3: Write the EditMode tests for the formatter
  - File: `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` (NEW, namespace `Tests.EditMode`, class
    `ItemDetailFormatterTests`)
  - Action: Create fixtures with `ScriptableObject.CreateInstance<T>()` (`SwordSO`, `ArmorSO`, `PotionItemSO`,
    `SkillItemSO`, `SkillSO`, `ItemSO`), track them in a list, `Object.DestroyImmediate` them in `[TearDown]`. Set
    private serialized fields (`PotionItemSO._healAmount`, `SkillItemSO._skill`, `SkillSO._displayName` /
    `_description` / `_lpCost`) through `UnityEditor.SerializedObject` + `ApplyModifiedPropertiesWithoutUndo()`.
    Cover AC 1–9 (category per type, row order, zero-bonus omission, negative polarity, potion row, tome rows +
    skill description, null skill, the consumable tag, the stack row, plain item → zero rows, price per context,
    `null` item safety, `into` cleared on reuse).
  - Notes: The `Tests.EditMode` asmdef already references `Game`; `UnityEditor` is available (Editor-only asmdef).

- [x] Task 4: Create `ItemStatRowUI`
  - File: `Assets/_Game/Scripts/UI/Inventory/ItemStatRowUI.cs` (NEW, namespace `Game.UI`)
  - Action: A MonoBehaviour with `[SerializeField] TMP_Text _label, _value` and
    `public void Set(string label, string value, Color valueColor)`. `Set` null-guards both refs, sets the texts and
    the value color, and does `_value.gameObject.SetActive(!string.IsNullOrEmpty(value))` (tag rows have no value).

- [x] Task 5: Rewrite `ItemDetailPanelUI`
  - File: `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs`
  - Action: Replace all existing serialized fields and section methods.
    - Serialized fields, all optional with null guards except `_nameText`:
      - `[Header("Header")]` `Image _icon`, `TMP_Text _nameText`, `TMP_Text _categoryText`
      - `[Header("Stats")]` `GameObject _statsSection`, `Transform _statRowsRoot`, `ItemStatRowUI _statRowPrefab`,
        `TMP_Text _skillDescriptionText`
      - `[Header("Description")]` `GameObject _descriptionSection`, `TMP_Text _descriptionText`
      - `[Header("Price")]` `TMP_Text _priceLabelText`, `TMP_Text _priceValueText`
      - `[Header("Colors")]` `Color _positiveColor = new(0.49f,0.80f,0.42f)`, `_negativeColor = new(0.88f,0.42f,0.42f)`,
        `_neutralColor = new(0.88f,0.88f,0.88f)`, `_priceColor = new(0.90f,0.76f,0.36f)`
    - Private state: `readonly List<ItemStatLine> _lines = new()`, `readonly List<ItemStatRowUI> _rows = new()`,
      `CanvasGroup _canvasGroup` (keep the existing Awake lookup + Hide behavior).
    - `public void Show(ItemSO item, ItemPriceContext priceContext = ItemPriceContext.Value)`:
      - `item == null` → `GameLog.Warn(TAG, "Show: item is null")`, `Hide()`, return.
      - Then call `PaintHeader`, `PaintStats`, `PaintDescription`, `PaintPrice`, then the existing CanvasGroup /
        `SetActive(true)` reveal.
    - `PaintHeader`: icon sprite + white/gray tint (existing logic); name; category text (hide the category GO when
      empty).
    - `PaintStats`:
      - Call `ItemDetailFormatter.BuildStatLines(item, _lines)`.
      - Make sure `_rows.Count >= _lines.Count` by instantiating `_statRowPrefab` under `_statRowsRoot`.
      - `Set` and activate the first N rows; `SetActive(false)` the rest.
      - Skill description: show it if `GetSkillDescription` is non-null, else hide its GO.
      - `_statsSection.SetActive(_lines.Count > 0 || skillDesc != null)`.
      - If `_statRowPrefab` or `_statRowsRoot` is null, log a warning once and hide the section.
    - `PaintDescription`: hide `_descriptionSection` when `string.IsNullOrWhiteSpace(item.description)`.
    - `PaintPrice`: label = `GetPriceLabel(ctx)`; value = `$"{GetPrice(item, ctx)}g"` in `_priceColor`.
    - `Hide()`: unchanged behavior.
    - Add `private const string TAG = "[ItemDetailPanelUI]";` (it is used).
    - Delete: `HideTypeSections`, `ShowWeaponSection`, `ShowArmorSection`, `ShowEquipableStatBonuses`, `FormatBonus`,
      `ArmorSlotDisplayName`, `ShowUsableSection`, `ShowSkillSection`, and the `Game.Progression` using if unused.
  - Notes: The default parameter keeps `InventoryUI`, `EquipmentUI` and `ContainerUI` compiling unchanged (Value
    context is correct for all three). Pool rows; never destroy rows on `Show`.

- [x] Task 6: Pass the trade context and strip the price from the trade buttons
  - File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` (`UpdateDetailPanel`, ~line 157)
  - Action: `_detailPanelUI.Show(item, side == TradeSide.NPC ? ItemPriceContext.Buy : ItemPriceContext.Sell);`
  - File: `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs`
  - Action: In `ShowBuy` / `ShowSell`, set the labels to `"Buy"` / `"Sell"` (drop the `({value}g)`). Keep the
    `interactable` affordability checks and listeners as-is.
  - Notes: Do **not** touch the context-menu labels at `NPCTradeUI.cs:229,240` (out of scope).

- [x] Task 7: Build `ItemStatRow.prefab`
  - File: `Assets/_Game/Prefabs/UI/Items/ItemStatRow.prefab` (NEW)
  - Action: Build it in the editor (MCP `execute_code` + `PrefabUtility.SaveAsPrefabAsset`):
    - Root `ItemStatRow`: RectTransform, `HorizontalLayoutGroup` (spacing 8, childControlWidth/Height true,
      childForceExpand false), `LayoutElement` (minHeight 24), `ItemStatRowUI`.
    - Child `Label`: TMP, LiberationSans SDF, size 18, color `#C8C8C8`, left-aligned, `LayoutElement`
      flexibleWidth 1.
    - Child `Value`: TMP, size 18, bold, right-aligned, no wrap.
    - Wire `_label` / `_value`.

- [x] Task 8: Rebuild the `ItemDetailPanel.prefab` internals
  - File: `Assets/_Game/Prefabs/UI/Items/ItemDetailPanel.prefab`
  - Action: In the editor (`PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset` →
    `UnloadPrefabContents`):
    1. **Keep `ActionsContainer`** (same object; reparent/reorder only).
    2. Delete `Icon`, `ItemName`, `Description`, `EquipableItemWrapper`, `UsableItemWrapper` and all their
       descendants.
    3. Root: keep the Image + `ItemDetailPanelUI`; add `VerticalLayoutGroup` (padding 16, spacing 10,
       childControlWidth/Height true, childForceExpandWidth true, childForceExpandHeight false).
    4. Create the children in this order (all text: LiberationSans SDF):
       - `Header` (HLG spacing 12, middle-left):
         - `Icon` (Image, preserveAspect, `LayoutElement` preferred/min 96×96)
         - `Titles` (VLG spacing 2, `LayoutElement` flexibleWidth 1), containing `ItemName` (TMP 26 bold, white,
           wrap) and `Category` (TMP 16, `#A0A0A0`)
       - `StatsSection` (VLG spacing 6), containing:
         - `Divider` (Image, white α 0.15, `LayoutElement` preferredHeight 1)
         - `StatRows` (VLG spacing 4)
         - `SkillDescription` (TMP 16 italic, `#C8C8C8`, wrap)
       - `DescriptionSection` (VLG spacing 6), containing `Divider` and `Description` (TMP 17 italic, `#BEBEBE`, wrap)
       - `Spacer` (`LayoutElement` flexibleHeight 1)
       - `PriceSection` (VLG spacing 6), containing `Divider` and `PriceRow` (HLG). `PriceRow` holds `PriceLabel`
         (TMP 18, `#C8C8C8`, flexibleWidth 1) and `PriceValue` (TMP 18 bold, right-aligned)
       - `ActionsContainer` (moved here as the last sibling; keep its VLG + ContentSizeFitter)
    5. Wire all `ItemDetailPanelUI` serialized fields, including `_statRowPrefab` → `ItemStatRow.prefab`.
  - Notes: Never raw-YAML-edit this prefab. After saving, check that `ActionsContainer` kept its fileID (grep the
    old id `5469822697967422335` in the prefab).

- [x] Task 9: Clean the nested-instance overrides in the host prefabs
  - Files: `Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab`, `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab`,
    `Assets/_Game/Prefabs/UI/Container/ContainerUI.prefab`
  - Action: For each host, via `execute_code`:
    - Load the contents and find the nested `ItemDetailPanel` instance root.
    - Read `PrefabUtility.GetPropertyModifications(instanceRoot)`. Keep only the modifications whose `target` is
      the source prefab's root GameObject or root RectTransform. Write them back with
      `PrefabUtility.SetPropertyModifications`. Save and unload.
    - Then verify the host's nested actions prefab (`InventoryDetailActions` / `TradeDetailActions` /
      `ContainerDetailActions`) is still a child of `ActionsContainer`, and that the host script's
      `_detailPanelUI` / `_itemDetailPanel` / `_invActions` / `_tradeActions` / `_containerActions` references are
      intact.
  - Notes: Do this **after** Task 8. Log the before/after modification counts.

- [x] Task 10: Make the health potion description cosmetic
  - File: `Assets/_Game/Data/Items/Item_Health_Potion.asset`
  - Action: `description` → `A red tonic that mends wounds.` (via `manage_scriptable_object` or `execute_code` with
    `SerializedObject`; if YAML-edited, use `refresh_unity(mode="if_dirty")`).

- [x] Task 11: Update the folder documentation
  - File: `Assets/_Game/Scripts/UI/Inventory/CLAUDE.md`
  - Action:
    - Add `ItemDetailFormatter`, `ItemStatRowUI`, `ItemStatLine`, `ItemPriceContext` to the Scripts table.
    - Replace the "ItemDetailPanelUI Notes" section:
      - The layout is header / stats / description / price / actions.
      - `Show(item, ItemPriceContext)` (trade passes Buy/Sell; the others use the default Value).
      - All display decisions live in `ItemDetailFormatter` (tested).
      - Stat rows are pooled.
      - `ActionsContainer` must never be deleted/recreated (host-nested actions depend on it).
      - Host prefabs must only override the nested panel's root RectTransform.
      - Item descriptions should be flavour text; numeric effects belong in stat rows.
  - File: `Assets/_Game/ScriptableObjects/Items/CLAUDE.md`
  - Action: Under ItemSO, add one line: "Keep `description` as flavour text; numeric effects are shown by the detail
    panel's stat rows (`ItemDetailFormatter`) — add a row there when a new item type gains a numeric field."

### Acceptance Criteria

- [ ] AC 1: Given a `SwordSO` with damageBonus 20, STR +3, DEX +1, when `BuildStatLines` runs, then the rows are
  exactly `[Damage +20 (Positive), Strength +3 (Positive), Dexterity +1 (Positive)]` in that order, and
  `GetCategory` returns `"Weapon"`.
- [ ] AC 2: Given an `ArmorSO` (slot Helmet) with DEF +2 and STR −1, when formatted, then the category is
  `"Armor · Helmet"` and the rows are `[Strength -1 (Negative), Defense +2 (Positive)]`; zero bonuses produce no row.
- [ ] AC 3: Given a `PotionItemSO` (heal 30, consumable, maxStacks 10), when formatted, then the rows are
  `[Restores Health +30 (Positive), Consumable (no value), Stack up to 10]` and the category is `"Potion"`.
- [ ] AC 4: Given a `SkillItemSO` whose skill is "Power Strike" (lpCost 3, description "X"), when formatted, then
  the rows start with `[Teaches Power Strike, LP Cost 3]`, the category is `"Skill Tome"`, and
  `GetSkillDescription` returns `"X"`.
- [ ] AC 5: Given a `SkillItemSO` with no skill assigned, when formatted, then there is no Teaches / LP Cost row,
  `GetSkillDescription` returns `null`, and nothing throws.
- [ ] AC 6: Given a plain `ItemSO` with maxStacks 1, when formatted, then there are zero rows and the category is
  `"Miscellaneous"`; given maxStacks 50, there is exactly one `Stack up to 50` row.
- [ ] AC 7: Given an item with buyValue 15 and sellValue 5, when `GetPrice` / `GetPriceLabel` are called, then
  Value → ("Value", 5), Buy → ("Price", 15), Sell → ("Sells for", 5).
- [ ] AC 8: Given a `null` item, when any formatter method is called, then it returns empty/0/null without
  throwing, and `BuildStatLines` leaves the list empty.
- [ ] AC 9: Given a list that already holds lines, when `BuildStatLines` is called again for another item, then the
  list contains only the new item's lines.
- [ ] AC 10: Given the inventory is open, when the player selects the big sword, then the panel shows:
  - icon + "big sword" + "Weapon"
  - three stat rows in green: Damage +20, Strength +3, Dexterity +1
  - the description "Big sword"
  - "Value 25g" in gold
  - the Equip / Drop buttons below
  - all stat texts at the same size and aligned (labels left, values right)
- [ ] AC 11: Given the inventory is open, when the player selects a Darkness Spider Leg, then the panel shows the
  header ("Miscellaneous"), one "Stack up to 50" row, the description and "Value 5g". No empty gaps from hidden
  sections.
- [ ] AC 12: Given the inventory is open, when the player selects the Health Potion, then the description reads
  "A red tonic that mends wounds." and the stats show "Restores Health +30", "Consumable", "Stack up to 10".
- [ ] AC 13: Given an item with an empty description (Test Helmet), when selected, then the description section
  (and its divider) is hidden.
- [ ] AC 14: Given the trade screen, when the player selects an item on the NPC side, then the panel shows
  "Price {buyValue}g" and the button reads "Buy" (disabled if the player can't afford it); on the player side,
  "Sells for {sellValue}g" and "Sell" (disabled if the NPC can't afford it).
- [ ] AC 15: Given a container or a corpse loot screen, when an item is selected, then the panel shows
  "Value {sellValue}g" with the Take / Put buttons working as before (Put still hidden in take-only mode).
- [ ] AC 16: Given an equipped item in the equipment panel, when the player clicks its slot, then the panel shows
  that item with "Value" pricing and the Unequip button.
- [ ] AC 17: Given the player selects a weapon (3 rows), then a potion (3 rows), then a spider part (1 row), when
  `Show` runs each time, then no new row GameObjects are instantiated after the first 3 (the pool is reused) and
  the stale rows are hidden.
- [ ] AC 18: Given the three host prefabs after Task 9, when opened in the editor, then the nested `ItemDetailPanel`
  has no overrides other than root RectTransform/GameObject ones, the actions prefab still sits under
  `ActionsContainer`, and the console shows no missing-reference errors when opening each screen in Play Mode.
- [ ] AC 19: Given the trade right-click context menu, when opened, then its labels still read `Buy (Xg)` /
  `Sell (Xg)` (unchanged).

## Additional Context

### Dependencies

- No new packages. Uses UGUI, TextMeshPro (LiberationSans SDF already in the project) and the Unity Test Framework.
- Builds on the completed `tech-spec-itemdetailpanel-actions-prefab-separation` (actions nested under
  `ActionsContainer`).
- Unity Editor + MCP connection required for the prefab tasks (7, 8, 9) and the asset edit (10).

### Testing Strategy

- **EditMode (automated):** `ItemDetailFormatterTests` covers AC 1–9. Run with MCP
  `run_tests(mode="EditMode")`; the full EditMode suite must stay green.
- **Manual playtest (Play Mode in `Core.unity` + the gameplay scene):**
  1. Open the inventory with: big sword, Test Helmet, Health Potion, Tome of Power Strike, spider leg, Mana
     Potion. Select each one and check AC 10–13 (visual alignment, single text-size system, gold price, no gaps).
  2. Equip the sword and click the equipment slot (AC 16).
  3. Trade with a merchant: select items on both sides; check the price label, the plain Buy/Sell buttons, and
     the affordability states (AC 14); right-click to check that the context-menu labels are unchanged (AC 19).
  4. Open a world container and loot a corpse (AC 15).
  5. Watch the console for missing references or NullReferenceExceptions on every screen (AC 18).
- Optional: count `StatRows` children in the hierarchy while switching items (AC 17).

### Notes

- **Highest risk: Task 8/9 prefab surgery.** Deleting `ActionsContainer` (instead of moving it), or leaving stale
  child overrides, breaks the action buttons on one or more screens. Mitigation: the fileID check in Task 8, the
  explicit modification filtering in Task 9, and AC 18.
- The root height is still set by each host (about 800); the `Spacer` absorbs the extra space so the price + actions
  stick to the bottom. If a host panel is too short for a tome (the longest content), the description wraps and
  may overflow; acceptable for now, revisit with a ScrollRect if needed.
- `Miscellaneous` is a placeholder category for plain items. A future spec could add an `ItemCategory` field
  (Material, Quest, Junk...) on `ItemSO` and have `GetCategory` prefer it.
- Future: equipped-item comparison (deltas next to each stat row) fits naturally into `ItemStatLine` (add a
  `Delta` field) without touching the layout.
- Mana Potion is a plain `ItemSO` with no effect. It will show only a Stack row (if stackable) and the price, which
  is accurate to its data.

## Implementation Notes

- **Deviation (Task 8):** the `ContentSizeFitter` on `ActionsContainer` was removed instead of kept. The root
  `VerticalLayoutGroup` now drives its height (Unity warns against a fitter under a controlling layout group). The
  host-level `ActionsContainer` `m_SizeDelta.y` overrides in `NPCTradeUI` / `ContainerUI` were stripped with the
  other child overrides in Task 9.
- **Addition (AC 16):** `EquipmentUI._itemDetailPanel` was unassigned at baseline, so clicking an equipment slot
  never showed the panel. It is now wired to the shared panel inside `InventoryUI.prefab`.
- Fractional floats (weapon damage, heal amount) are rounded with `Mathf.RoundToInt` before the omission/polarity
  checks, so `0.4` damage produces no row instead of `+0`.
- Override cleanup counts (before → kept): InventoryUI 41 → 22, NPCTradeUI 42 → 22, ContainerUI 42 → 22.

## Review Notes

- Adversarial review completed
- Findings: 11 total, 6 fixed, 5 skipped
- Resolution approach: auto-fix
- Fixed: F1 (float rounding in stat rows), F2 / F3 (deviation and addition recorded above), F5 (tests for jewelry
  slot names, negative damage, rounding), F7 (warn on null target list), F10 (lazy-cached trade button labels)
- Skipped: F4 (inventory selection stays highlighted when clicking an equipment slot: pre-existing UX), F6 / F8
  (noise), F9 (tome "Consumable" row matches the spec), F11 (process: keep `Core.unity` and
  `.claude/settings.local.json` out of the feature commit)
- Manual Play Mode checks (AC 10–16, 18, 19) still to be done by Valentin.
