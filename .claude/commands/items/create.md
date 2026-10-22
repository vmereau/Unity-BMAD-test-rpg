---
description: Create a generic pickable item (ItemSO + world prefab + material + icon) from a Meshy FBX/texture folder
argument-hint: <folder under Assets/ with the FBX + textures> [PascalCase name]
---

Your task is to turn a downloaded model folder (Meshy FBX + PBR PNG maps, optionally an icon PNG) into a
generic in-game item: `ItemSO`, pickable world prefab, URP Lit material and inventory icon. The work is done
by the editor builder `Game.Editor.ItemAssetBuilder` (`Assets/_Game/Editor/Items/ItemAssetBuilder.cs`) —
this command collects the values, runs it and verifies the result. Never call `generate_image`.

## Step 1 — Load the rules

Read `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` and `Assets/_Game/Prefabs/Items/CLAUDE.md`.

## Step 2 — Resolve folder and name

`$ARGUMENTS` = `<folder> [Name]`. If the folder is missing, ask for it. Accept `\` or `/`, and paths with
or without the `Assets/` prefix; normalize to `Assets/...` with `/`.

`Name` is the PascalCase asset name (short, letters/digits only — `lockpicking set` → `Lockpick`). If not
given, propose one from the folder / icon / FBX name and let the user confirm it in Step 4.

## Step 3 — Inspect the folder

List the folder (ignore `.meta`). The builder expects:

| Role | File |
|------|------|
| Model | exactly one `*.fbx` |
| Base color (required) | `*_texture.png` |
| Normal / Metallic / Roughness (optional) | `*_texture_normal.png`, `*_texture_metallic.png`, `*_texture_roughness.png` |
| Icon (optional) | at most one other `.png` / `.jpg` |

Show the user which file will become the icon (if any) and confirm it — a single non-Meshy image may be
a preview render or an extra map (`_emission`, `_ao`), not an icon. Files in sub-folders are ignored:
move them up first.

**Ask first (HALT)** if: 0 or ≥2 FBX, ≥2 icon candidates, or any target already exists —
`Art/Items/<Name>/`, `Art/UI/Items/Item_<Name>_Icon.png`, `Data/Items/Item_<Name>.asset`,
`Prefabs/Items/Item_<Name>.prefab`. Never overwrite or delete existing assets.

## Step 4 — Collect values

Ask in **one** numbered message for every value the user did not give, each with a proposed default:

1. `Name` (asset name) and display name
2. Description — one or two sentences of flavour text in the Gothic tone (no numbers; stats are shown by the detail panel)
3. `maxStacks` (1 = not stackable; small tools / materials / consumables usually 50)
4. `buyValue` / `sellValue` (sell is usually ~¼ of buy)
5. Target size — the model's largest dimension in metres (lockpick 0.15, potion 0.2, sword 1.0)

Verify every answer; re-ask only missing ones.

## Step 5 — Build

Run with `execute_code` (escape quotes in strings):

```csharp
var r = Game.Editor.ItemAssetBuilder.Build(new Game.Editor.ItemBuildRequest {
    SourceFolder = "Assets/...", Name = "Lockpick", DisplayName = "Lockpick",
    Description = "...", MaxStacks = 50, BuyValue = 20, SellValue = 5, TargetSize = 0.15f });
return r.ToString();
```

If it returns `FAILED: ...`, report the error and stop — the builder validates first and rolls back every
move / created asset on a later failure.

## Step 6 — Icon (only if `Icon: MISSING`)

Run `execute_menu_item` `Tools/Items/Generate Missing Icons`. It renders the world prefab preview
asynchronously to `Art/UI/Items/Item_<Name>_Icon.png` (and also fills any other item missing an icon).
The render happens on later editor frames, so check in a **separate** `execute_code` call that
`AssetDatabase.LoadAssetAtPath<Game.Inventory.ItemSO>(path).icon != null`. If null, read the console for
`[ItemIconGenerator] Done` / warnings and re-check up to 3 times (one call each). If still null (e.g. a domain
reload interrupted it), tell the user to run the menu item again from the editor.

## Step 7 — Verify

- `read_console` (errors / warnings): no errors from `[ItemBuilder]` or `[ItemIconGenerator]`.
- `manage_prefabs get_info` on the prefab: root on layer Interactable with `ItemPickup`, `Rigidbody`,
  `BoxCollider`, `InteractionHighlight`, and a `Model` child.

## Step 8 — Report

List the created / moved paths (item, prefab, material, model folder, icon), the values used and any
builder warnings (e.g. a missing normal map). Remind the user to test in Play mode: drop the prefab in a
scene, pick it up, check the inventory icon, drop it again. Offer `/perso:commit`.
