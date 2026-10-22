---
title: '/items:create — build a generic pickable item from a Meshy model folder'
type: 'feature'
created: '2026-10-22'
status: 'done'
baseline_commit: '00b689887227b49814aa883c949675f730358f9a'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/ScriptableObjects/Items/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/Items/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Turning a downloaded Meshy folder (FBX + PBR PNG maps, optional icon) into a usable in-game item is a manual, error-prone chain of moves, importer settings, material, prefab, SO and icon wiring.

**Approach:** A Claude command `/items:create <folder> [name]` collects item values (asking for any not given), then calls a deterministic editor builder that organizes the assets and creates a generic `ItemSO`, a URP Lit material and a pickable world prefab. If the folder has no icon, the command runs `Tools/Items/Generate Missing Icons`. First use: the lockpick folder.

## Boundaries & Constraints

**Always:**
- Layout: model + maps → `Art/Items/<Name>/` (`<Name>.fbx`, `<Name>_BaseColor/_Normal/_Metallic/_Roughness.png`); icon → `Art/UI/Items/Item_<Name>_Icon.png` (Sprite, Max Size 256); material `Art/Items/<Name>/M_<Name>.mat`; SO `Data/Items/Item_<Name>.asset`; prefab `Prefabs/Items/Item_<Name>.prefab`.
- Move assets with `AssetDatabase.MoveAsset` (GUIDs and `.meta` files are preserved).
- Material: URP Lit with base map, normal map and metallic map, plus a constant smoothness. The roughness map is moved but not used.
- World prefab root: layer Interactable (8), `ItemPickup` (via `Configure`), `Rigidbody`, `BoxCollider` fitted to the renderers, `InteractionHighlight` (`_targets` empty). The model goes in a child named `Model`, scaled so its largest dimension equals the requested size in metres.
- `ItemSO` fields: `itemName`, `description`, `maxStacks`, `buyValue`, `sellValue`, `worldItemPrefab`, and `icon` when an icon PNG was found.
- The command asks for any of name / description / maxStacks / buy / sell / size the user didn't give, proposing defaults.
- Never call `generate_image`.

**Ask First:**
- Any target asset (SO, prefab, material, model folder, icon) already exists.
- The folder holds 0 or ≥2 FBX files, or ≥2 non-Meshy PNGs (icon is ambiguous).

**Never:**
- Usable/equippable behaviour, a lock or door integration, or placing anything in a scene.
- Packing roughness into the smoothness channel.
- Overwriting or deleting existing assets.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full folder | 1 FBX, 4 Meshy maps, 1 other PNG | All assets created; icon = that PNG | N/A |
| No icon | 1 FBX + maps only | SO created without icon; command runs the icon generator and verifies `icon` is assigned | Report if still null |
| Missing map | normal or metallic map absent | Material built without that map | Warning logged |
| No base map | `_texture.png` absent | Nothing created | Error returned, abort |
| Name collision | `Item_<Name>.asset` exists | Nothing created | Error returned → command asks the user |
| Bad folder | path not under `Assets/` or doesn't exist | Nothing created | Error returned |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/ItemIconGenerator.cs` -- `Tools/Items/Generate Missing Icons`; async (EditorApplication.update), writes `<assetName>_Icon.png`
- `Assets/_Game/Editor/WeaponCreatorWindow.cs` -- reference world-prefab build (lines 278–307)
- `Assets/_Game/Editor/WeaponModelUtility.cs` -- `StripForEquip`, `TryGetRendererBounds` to reuse
- `Assets/_Game/Scripts/Inventory/ItemPickup.cs` -- `Configure(ItemSO)`
- `Assets/_Game/ScriptableObjects/Items/ItemSO.cs` -- fields to fill
- `.claude/commands/items/icon-prompt.md` -- command style reference
- `Assets/_Game/Art/UI/Items/lockpick/` -- first input (Meshy "Rustbound Hookblades" + `lockpicking set.png`)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Editor/Items/ItemAssetBuilder.cs` -- Static `ItemAssetBuilder` (namespace `Game.Editor`, `Game.Editor` asmdef) containing:
  - a pure `ClassifySourceFiles(IEnumerable<string>)` that splits the input into fbx / base / normal / metallic / roughness / icon candidates (Meshy suffixes: `_texture.png`, `_texture_normal.png`, `_texture_metallic.png`, `_texture_roughness.png`);
  - `Build(ItemBuildRequest)`, which validates (no partial output on failure), moves and renames assets, sets importers (normal map type; metallic/roughness sRGB off; icon Sprite 256; FBX animation import off and embedded materials remapped to `M_<Name>`), creates the material, the prefab and the SO, and returns a result with the paths, warnings and `IconMissing` -- deterministic core callable through `execute_code`.
- [x] `Assets/Tests/EditMode/ItemAssetBuilderTests.cs` -- tests for `ClassifySourceFiles`: full set, no icon, two icons, missing base map, `.meta` files ignored -- covers the matrix rows that don't need asset I/O.
- [x] `.claude/commands/items/create.md` -- The command. Steps:
  1. Read the item CLAUDE.md files.
  2. Resolve the folder and `<Name>` (PascalCase, e.g. `Lockpick`).
  3. List the files and check the Ask-First cases.
  4. Ask for missing values with defaults.
  5. Run `Build` via `execute_code`.
  6. If `IconMissing`, run the icon menu item, wait, and confirm the icon is set.
  7. Check `read_console`.
  8. Report the created assets.
- [x] Lockpick run -- run the command on `Art/UI/Items/lockpick` with values Lockpick / maxStacks 50 / buy 20 / sell 5 / size 0.15 m and a short flavour description. Afterwards, reuse old GUID `e3c7aabe8059c7e4ab1079453d05042d` in `Item_Lockpick_Icon.png.meta` and delete the emptied `lockpick/` folder.
- [x] Docs -- add `/items:create` to the root `CLAUDE.md` skill table, the `ScriptableObjects/Items/CLAUDE.md` intro, and `Art/UI/Items/CLAUDE.md` (models live in `Art/Items/`).

**Acceptance Criteria:**
- Given the lockpick assets were created, when the prefab is dropped in a scene and the game is played, then the item falls with physics, is outlined when focused, and picking it up adds one "Lockpick" with its icon to the inventory.
- Given the lockpick is in the inventory, when it is dropped, then the world prefab spawns and can be picked up again, and stacks up to 50.
- Given `Item_Lockpick.asset` exists, when the `ItemCatalog` sync runs, then the catalog contains it.

## Design Notes

The builder does the work and the command only orchestrates it: asset creation is reproducible, can be reviewed as C#, and doesn't depend on chaining MCP calls. URP Lit with a metallic map reads smoothness from the map's alpha; Meshy maps have no alpha (alpha = 1), so `_Smoothness` (0.3) acts as a constant. Set the `_NORMALMAP` and `_METALLICSPECGLOSSMAP` keywords.

## Verification

**Commands:**
- `run_tests` EditMode `ItemAssetBuilderTests` -- expected: all pass
- `read_console` (errors) after compile and after the lockpick run -- expected: none

**Manual checks:**
- `Item_Lockpick.prefab` root: layer 8, Rigidbody, BoxCollider fits the mesh, ItemPickup → `Item_Lockpick`, InteractionHighlight. The model is about 0.15 m and textured.

## Suggested Review Order

**Entry point — build flow**

- Whole pipeline: validate, move, import, material, SO, prefab, catalog — with rollback.
  [`ItemAssetBuilder.cs:126`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L126)

- Any failure undoes moves and deletes generated assets (no partial output).
  [`ItemAssetBuilder.cs:201`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L201)

**Validation & classification**

- All checks before touching assets, incl. non-imported files and existing targets.
  [`ItemAssetBuilder.cs:247`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L247)

- Pure role sorting by Meshy suffix; unit-tested.
  [`ItemAssetBuilder.cs:94`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L94)

**Asset generation**

- Remaps embedded and already-remapped FBX materials to the new URP Lit material.
  [`ItemAssetBuilder.cs:374`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L374)

- Constant smoothness — Meshy maps carry no alpha.
  [`ItemAssetBuilder.cs:350`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L350)

- World prefab: pickup, rigidbody, highlight, scaled nested model, fitted box.
  [`ItemAssetBuilder.cs:404`](../../Assets/_Game/Editor/Items/ItemAssetBuilder.cs#L404)

**Orchestration (Claude command)**

- Folder inspection, Ask-First HALTs, icon confirmation.
  [`create.md:23`](../../.claude/commands/items/create.md#L23)

- execute_code call and icon-generator fallback.
  [`create.md:54`](../../.claude/commands/items/create.md#L54)

**Peripherals**

- Classifier tests.
  [`ItemAssetBuilderTests.cs:1`](../../Assets/Tests/EditMode/ItemAssetBuilderTests.cs#L1)

- Generated lockpick item.
  [`Item_Lockpick.asset:1`](../../Assets/_Game/Data/Items/Item_Lockpick.asset#L1)
