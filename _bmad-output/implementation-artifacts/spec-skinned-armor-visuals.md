---
title: 'Skinned armor visuals — head/body split and body-replacing armor'
type: 'feature'
created: '2026-11-03'
status: 'done'
baseline_commit: '36b949b0d826818c31bd50213aa07de78c2431f1'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Equipping armor only swaps the whole body material (`EquipmentVisuals.RefreshBody`); the player's shape never changes, and the head is part of the same Meshy mesh, so nothing can replace the body while keeping the face.

**Approach:** Split the base hero once (Blender) into a Head mesh and a Body mesh on the same Mixamo skeleton. An armor is a skinned mesh covering everything below the neck, baked into the base body's mesh space and bindposes by an editor tool; at equip time it is rebound by bone name to the player skeleton and hides the Body. The head never changes. The armor's neck ring is joined exactly onto the head's neck ring in Blender.

## Boundaries & Constraints

**Always:** Keep the single full-body `EquipmentSlot.Armor`. Never touch the player skeleton, avatar, sockets or Animator — armor and head reuse the existing bones. Head SMR is always visible. Bone matching is by name (`mixamorig:*`). The Body/Head/armor meshes all live in the base body's mesh space with the base body's bindposes. Blender work runs via the Blender MCP; edits to the base FBX go to a new file (originals untouched).

**Ask First:** Neck cut height / angle before exporting the split (show a render). Any change to the `Character` nested-prefab structure beyond swapping the body mesh and adding a `Head` SMR. Hand-fitting the first armor if the Meshy leather armor zip is not yet in the project.

**Never:** No full-model swap, no rigid prefab overlay for armor, no blend shapes / cloth sim. The reusable per-armor Blender fit-pass script is out of scope (next spec) — the first armor is fitted by hand through the MCP. No torso/legs/boots slot split.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Equip skinned armor | `ArmorSO.equipVisualPrefab` holds an SMR | Armor instantiated under `Character`, bones rebound by name, root bone = player Hips, Body SMR disabled, Head visible | N/A |
| Unequip | Armor slot emptied | Armor instance destroyed, Body SMR re-enabled | N/A |
| Armor without visual | `equipVisualPrefab == null` | Body stays visible, stats still apply | `GameLog.Info` once per equip |
| Missing bone | Armor bone name absent from player skeleton | Armor not shown, Body stays visible | `GameLog.Warn` listing missing names |
| Bake: size drift | Armor mesh height not within ±3% of base body | Bake refused | Editor dialog with measured vs expected height |
| Bake: foreign bones | Imported mesh uses a bone the base body lacks | Bake refused | Dialog listing bones |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- `RefreshBody()` material swap, `_bodyRenderer`, `_armorPlaceholderMaterial` to replace
- `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs` / `ArmorSO.cs` -- `equipVisualPrefab` already exists, reused for armor
- `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` -- dev tool wiring `_armorPlaceholderMaterial` (Beta_Surface era)
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `Character` = nested `rigged test hero with textures.fbx`, body SMR `base_hero`
- `Assets/_Game/Art/Characters/Humanoids/models/test base hero/` -- base FBX, `M_TestBaseHero.mat`
- `Assets/_Game/Data/Items/Armor_TestArmor.asset` -- test armor (slot 2), `equipVisualPrefab` null
- `Assets/_Game/Prefabs/Items/Armors/` -- armor visual prefabs go here
- `Tools/Blender/` -- Blender-side scripts + README

## Tasks & Acceptance

**Execution:**
- [x] Blender MCP -- import base hero FBX, cut the mesh at the neck with a clean planar loop (just above the collarbone), separate into `base_hero_head` / `base_hero_body`, export `models/test base hero/base hero split.fbx` (meshes + armature, no animation) -- clean ring = exact join target for armors; save the steps as `Tools/Blender/split_head_body.py`
- [x] `Assets/_Game/Scripts/Inventory/SkinnedMeshRebinder.cs` -- static pure helper: map an SMR's bone names to a skeleton's transforms (dictionary by name), return missing names; apply bones + root bone on success -- unit-testable core of the runtime bind
- [x] `Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs` -- `Tools/Characters/Skinned Mesh Baker`: takes an imported skinned mesh + the player base body SMR, re-expresses vertices/normals/tangents in the base body's mesh space (both in rest pose at origin), remaps bone weights to the base body's bone order, copies its bindposes, validates height ±3% and bone names, writes a `.asset` mesh next to the source -- absorbs FBX axis/scale/bone-orientation drift between Mixamo, Blender and Unity
- [x] `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- replace material swap: `_bodyRenderer` becomes `SkinnedMeshRenderer`, add `_skeletonRoot` (Hips); `RefreshBody()` instantiates the armor prefab under `Character`, rebinds via `SkinnedMeshRebinder`, toggles Body; remove `_armorPlaceholderMaterial` + `_originalBodyMaterial` -- per I/O matrix
- [x] `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` -- drop the placeholder-material wiring, wire `_skeletonRoot`
- [x] `Player.prefab` -- `base_hero` SMR uses the baked body mesh; add sibling `Head` SMR (baked head mesh, same bones/root bone/material); wire `_bodyRenderer` + `_skeletonRoot`
- [x] First armor (needs the user's Meshy leather-armor zip → Mixamo) -- Blender MCP: align to base hero (height, hips, neck), delete above the neck, bridge the cut onto the head's neck ring (ring vertices = head ring positions, normals copied), transfer weights from `base_hero_body`, bind to base armature, export; bake; create `Prefabs/Items/Armors/LeatherArmor_Visual.prefab` (SMR + URP Lit material from Meshy textures); assign to `Armor_TestArmor`
- [x] `Assets/Tests/EditMode/SkinnedMeshRebinderTests.cs` -- all bones found, missing bone reported and nothing applied, root bone resolved
- [x] Docs -- `Art/Characters/Humanoids/CLAUDE.md` (head/body split now implemented, armor fit recipe), `Scripts/Inventory/CLAUDE.md` (`EquipmentVisuals` armor row, `SkinnedMeshRebinder`), `Prefabs/Player/CLAUDE.md` (Head SMR), `Tools/Blender/README.md`

**Acceptance Criteria:**
- Given no armor, when the game runs, then the player looks identical to before the split (no visible neck seam in idle and combat animations).
- Given the leather armor is equipped, when the player runs, attacks and dodges, then the armor deforms with the body, the head is the base hero's, and the neck shows no gap or lighting crease.
- Given armor is equipped then unequipped, when checking the hierarchy, then no armor instance remains and the Body SMR is enabled.

## Design Notes

Why bake into base-body space: Meshy → Mixamo gives each upload its own skeleton, and Blender's FBX round-trip can change bone orientations. Copying the base body's bindposes is only valid when vertices share its mesh space, so the baker transforms vertices (rest pose, world → base-body local) instead of trusting the FBX. Result: every body-part mesh is interchangeable on the same bones — the runtime only swaps `bones[]`.

Neck join: the split leaves a planar boundary loop on the head. The armor's cut edge is bridged to a duplicate of that loop (same positions), with custom normals copied from the head loop so shading is continuous. The prompt's leather collar remains as a visual safety margin.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass, including `SkinnedMeshRebinderTests`
- Unity MCP `read_console` -- expected: no compile errors

**Manual checks (if no CLI):**
- Play mode: equip / unequip `Armor_TestArmor` from the inventory; screenshot idle, run, attack, close-up of the neck.

## Suggested Review Order

**Equip flow — armor replaces the body**

- Entry point: armor slot builds / reuses a skinned visual, hides the body on success only.
  [`EquipmentVisuals.cs:166`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L166)

- Instantiate under Character, bind by bone name against the body's own bones, copy render settings.
  [`EquipmentVisuals.cs:190`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L190)

- All-or-nothing bind; refuses name/bind-pose count mismatch so the torso never vanishes.
  [`SkinnedMeshRebinder.cs:66`](../../Assets/_Game/Scripts/Inventory/SkinnedMeshRebinder.cs#L66)

- Skeleton-less prefab root: bone names only, written by the baker.
  [`SkinnedBodyPart.cs:11`](../../Assets/_Game/Scripts/Inventory/SkinnedBodyPart.cs#L11)

**Baking into the base body's space**

- One bind-pose transform re-expresses the source in base_hero mesh space (FBX rest pose untrusted).
  [`SkinnedMeshBaker.cs:92`](../../Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs#L92)

- Safeguard: every weighted shared bone must agree, else "not bound to the target skeleton".
  [`SkinnedMeshBaker.cs:98`](../../Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs#L98)

- Size safeguard: ±3% vertical extent against the baked base body.
  [`SkinnedMeshBaker.cs:185`](../../Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs#L185)

- Prefab writer: per-submesh materials, bounds grown to fit bulky armor.
  [`SkinnedMeshBaker.cs:240`](../../Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs#L240)

**Blender split (head / body)**

- Level neck cut, both halves keep the uncut smooth normals → no seam crease.
  [`split_head_body.py:65`](../../Tools/Blender/split_head_body.py#L65)

**Peripherals**

- Dev wiring tool: placeholder material dropped, skeleton root wired.
  [`WireEquipmentVisuals.cs:104`](../../Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs#L104)

- Rebinder tests incl. bind-pose count mismatch and bone-only indexing.
  [`SkinnedMeshRebinderTests.cs:98`](../../Assets/Tests/EditMode/SkinnedMeshRebinderTests.cs#L98)

- Pipeline + armor-fit recipe for the next armors.
  [`CLAUDE.md:97`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L97)
