---
title: 'NPC head variants (Blender-shaped faces on the canonical neck)'
type: 'feature'
created: '2026-11-14'
baseline_commit: 'ee68d9d3c260646f1bd93e4678fa60cad3569806'
status: 'done'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/Entities/CLAUDE.md'
  - '{project-root}/Tools/Blender/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every NPC renders the player's head (`base_hero_head_baked` + its material from `Humanoid_BaseHero.prefab`), so the cast looks like clones. The canonical neck interface allows other heads, but nothing produces them and an NPC can't choose one.

**Approach:** In Blender, derive four faces from the canonical base head by reshaping the face/skull above a frozen neck ring and repainting a copy of the skin texture. Render mockups for approval, then export, bake and validate them in Unity. An NPC picks its head through a head appearance asset on its `NPCEntity`, applied at spawn.

Faces (first batch; others deferred):
- **Warden Voss** (late 40s): heavy jaw, thick brow, broken nose, shaved head, grey-flecked stubble
- **Aldric, Cook** (late 40s): round fuller cheeks, soft jaw, receding hairline, painted brown-grey short beard
- **Edric, Blacksmith** (mid-50s): broad face, wide nose, grey hair, painted full grey beard (slight jaw volume), small burn scar
- **TentMate** (early 60s): gaunt, sunken cheeks, thinning white hair, white stubble

## Boundaries & Constraints

**Always:** Start from the derived, neck-prepared head in `base hero split.fbx` (same skeleton, bind pose, bone weights). The neck ring and every vertex within 12 mm of it stay identical in position, UV and weights, and in normal within the 1° seam tolerance (custom-normal re-encoding noise ≈ 0.02°). The deformation fades in beyond it; the overhanging chin may move. Skin tone stays the base hero's; only shape, hair, beard, age marks and scars change. Each head gets its own copy of the base color texture (normal map shared) and its own URP Lit material. Each head must pass the exact-join, neck-UV and bind-space checks against the canonical body before it is saved. Shape and paint settings live in a committed config per face, so a head can be regenerated. Never touch the original source FBX files.

**Ask First:** HALT after the Blender mockup renders (front, three-quarter, side) for all four faces, and wait for the user's approval or tweaks before exporting anything to Unity. Also ask before adding geometry beyond reshaping (separate hair, beard, ears) or changing the body material.

**Never:** Image-to-3D heads, per-instance scene overrides of the Head SMR, changes to `Player.prefab` or the player's head, skin-tone variation, heads for NPCs outside the four above, customization UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Assigned head | NPC whose `NPCEntity` references a head asset | Its Head SMR shows that mesh and material at spawn | N/A |
| No head | `NPCEntity` head field empty, or entity not an `NPCEntity` | Default base-hero head kept | No log |
| Incompatible mesh | Head mesh bone count or bindposes differ from the renderer's | Default head kept | `GameLog.Warn` naming the NPC and asset |
| Armor worn | Guard with `Armor_CompanyGrunt` + a variant head | Body hidden, variant head + neck visible, no gap at the collar | N/A |

</frozen-after-approval>

## Code Map

- `Tools/Blender/split_head_body.py`, `prepare_armor_neck.py` -- produced the neck-prepared head; reuse their neck heights (seam 1.585 m, stump 1.48 m)
- `Assets/_Game/Art/Characters/Humanoids/models/test base hero/` -- `base hero split.fbx` (input), Meshy base color + normal PNGs, `M_TestBaseHero.mat`, `base_hero_body_baked.asset` (join reference)
- `Assets/_Game/Editor/Characters/SkinnedNeckBaker.cs` -- `BakeLeather` = template: `SkinnedMeshBaker.Bake`, `RepairNeckUV`, `AlignSeamTangents`, validators, rollback save
- `Assets/_Game/Editor/Characters/SkinnedNeckValidator.cs` -- `ValidateExactJoin`, `ValidateNeckUV`, `ValidateBindSpace`
- `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` -- add the head field
- `Assets/_Game/Scripts/AI/NpcArmament.cs` -- pattern: reads `PersistentID.Entity as NPCEntity` in `Awake`/`Start`
- `Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab` -- owns the `Head` SMR (`Character/Head`)
- `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- add the appearance component on the root
- `Assets/_Game/Data/NPCs/{Warden,Cook,BlackSmith,TentMate}/NPC_*.asset` -- assign heads

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/make_head_variant.py` + `Tools/Blender/configs/heads/{warden,cook,blacksmith,tentmate}.json` -- import the split head, freeze the neck, apply the config's region deforms (jaw, brow, nose, cheeks, skull) with smooth falloff, paint the texture copy (hair/stubble colour, hairline, beard, scars), check that the frozen vertices didn't change, render mockups to `Temp/HeadVariants/`, and on request export `Head_<Name>.fbx` + `T_Head_<Name>_BaseColor.png` -- reproducible faces
- [x] **CHECKPOINT** -- show the mockups, HALT for approval (Ask First)
- [x] `Assets/_Game/Art/Characters/Humanoids/models/heads/<Name>/` -- the exported FBX, texture, `M_Head_<Name>.mat` (URP Lit, shared normal map, same settings as the base head material), `Head_<Name>_baked.asset` -- head assets
- [x] `Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs` -- `Tools/Characters/Bake Head Variants`: bake every folder under `heads/` against the canonical target, repair the cap UVs, align the seam tangents to the canonical body, validate, and save with rollback (GUIDs kept) -- validated bake
- [x] `Assets/_Game/ScriptableObjects/Characters/HeadAppearanceSO.cs` (own file) -- `Mesh` + `Material`; `NPCEntity._head` (nullable) + getter -- data
- [x] `Assets/_Game/Scripts/AI/NpcHeadAppearance.cs` -- root component with `_headRenderer`. In `Awake`, check compatibility (bone count + bindposes) and swap `sharedMesh`/`sharedMaterial`, or warn and keep the default. Wire it on `NPC_base Variant` -- runtime swap
- [x] `Data/Characters/Heads/HeadAppearance_<Name>.asset` ×4, assigned on the four `NPC_*.asset` -- assignment
- [x] `Assets/Tests/EditMode/NpcHeadAppearanceTests.cs` -- the matrix rows, plus each shipped head is compatible with the variant's Head SMR -- regression guard
- [x] `Art/Characters/Humanoids/CLAUDE.md`, `Prefabs/Entities/CLAUDE.md`, `Tools/Blender/README.md` -- recipe (config → mockup → export → bake → assign), limits -- docs

**Acceptance Criteria:**
- Given the approved mockups, when the four NPCs are viewed in StartingTown play mode, then each shows its own face, the other NPCs keep the base head, and the console has no new errors.
- Given each variant head, when it idles, walks, attacks and dodges, bare or in Company armor, then no gap, crack or shading seam shows at the neck.
- Given the EditMode suite, when run, then `NpcHeadAppearanceTests` and all existing tests pass.

## Spec Change Log

- 2026-11-14, review (Acceptance auditor), human-renegotiated: the Always rule "everything at or below the chin underside stays bit-identical" contradicted the approved mockups. The overhanging chin tip below 1.595 m moves up to 6.7 mm (Warden / Blacksmith / Cook jaw changes), and Blender custom normals carry ~0.02° re-encoding noise. User chose to amend the wording: the frozen region is the neck ring plus 12 mm, normals within the 1° seam tolerance. Avoids losing the approved jaw / chin changes. KEEP: the distance-based freeze, the throwaway-copy smooth normals, and all four approved configs unchanged.

## Design Notes

The deformation weight is 0 within 12 mm of any vertex at or below the 1.585 m seam, and eases to 1 over the next 35 mm. The freeze is distance-based because the chin overhangs the neck. Each region deform is a displacement field: a centre, a radius and a scale/offset, with weights eased by distance. All head UVs sit in one atlas, so painting works through UV-island masks on the head region of the texture copy. Body texels are left untouched.

Because the baked meshes share the canonical bone order, the runtime swap needs no rebinding. The compatibility check guards against a stale or foreign mesh.

## Implementation Evidence

- Blender: `make_head_variant.py` + 4 configs. Every head froze 802 vertices with a max shift of 1.2e-7 m and a max normal delta of ≤ 0.00056; max deform 7.8–9.0 mm. User approved the mockups (`Temp/HeadVariants/lineup.png`, `eyes.png`) after a second round that added eye colour and matching brows (op `iris`, brow layers). Skin tone unchanged.
- Bake: `Tools/Characters/Bake Head Variants` → 4 heads, all exact-join / neck-UV / bind-space checks passed. Same 4151 vertices and 40 bind poses as `base_hero_head_baked`, compatible with the model's Head SMR.
- `verify_head_variants.cs` (preview scene, NPC model): 137 join pairs per head, max posed exposed-join gap ≤ 0.013 mm over idle / walk / run / overhead attack / dodge × 8 samples. Neck sheets bare and in Company armor show no seam. Portrait shadow artifacts also appear on the default head (preview lighting, no ambient).
- Play mode StartingTown: Warden / Cook / Blacksmith / TentMate use their `Head_<Name>_baked` + `M_Head_<Name>`, the 9 other NPCs keep the base head; in-game captures in `Temp/HeadVariants/ingame.png`. Console: no new errors or warnings (7 pre-existing unrelated entries).
- EditMode: 1397/1397 pass; `NpcHeadAppearanceTests` 8/8.
- Side effect: assigning `_head` re-serialized the 4 NPC assets, dropping the stale `_weapon` key and writing defaults for `_startingEquipment` / hearing radii that existed in code but were absent from the YAML. No value changes.
- Limits: shape changes stay ≤ ~9 mm (larger stretches the texture's eye / nose detail). Brows are recoloured paint (flecked, not strands). Eye colour is subtle at gameplay distance.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- `Tools/Characters/Bake Head Variants` -- expected: 4 heads baked, all join/UV/bind checks pass

**Manual checks:**
- Blender mockups approved by the user; Unity close-ups of each head, front and side, bare and armored, through idle / walk / attack / dodge.

## Review Triage

Three reviewers (Blind hunter, Edge case hunter, Acceptance auditor).
- **Intent (resolved with the user):** the chin-underside freeze wording. See the Spec Change Log.
- **Patched — baker:**
  - Every input (FBX, skinned mesh, texture) is checked before any write; a folder without an FBX is skipped with a warning.
  - Rollback now snapshots and restores meshes, materials and appearance assets, saves after restoring, and keeps the original error.
  - The shader is re-applied on re-bake; the importer cast is safe; neck constants are named.
- **Patched — runtime:**
  - The renderer bone count must match the mesh's bind poses; a mesh with no bone weights is rejected.
  - Uses `TryGetComponent`.
- **Patched — Python generator:**
  - No NaN from degenerate ranges or zero radii; clear errors on empty input.
  - The texel-map cache is keyed on the source FBX, texture and size; the map size comes from the texture.
  - Cleanup in `try`/`finally`, orphan purge between runs.
  - A mirrored op reads the same input positions for both sides.
  - The docstring is fixed.
  - Regression: the regenerated heads match the exported FBX within 0.0003 mm and the PNG within 0.5/255, so no re-export was needed.
- **Patched — tests:**
  - Fake-null fix and named asserts.
  - New cases: the warning is logged, renderer bone-count mismatch, out-of-range bone index, no bone weights, mesh swapped when there is no material.
  - The verification script fails on fewer than 12 join pairs or missing renderers.
- **Post-fix evidence:**
  - Re-bake passed with all GUIDs kept.
  - Missing-texture run threw before any write.
  - EditMode 1400/1400.
  - Play mode: the 4 faces apply, no `[NpcAppearance]` warnings.
- **Deferred:** posed / armor neck checks as an automated test (`deferred-work.md`).
- **Rejected (by design):**
  - Head applied only in `Awake` from `PersistentID.Entity`: NPCs are prefab instances with serialized data.
  - Material slot 0 only: one material.
  - Culling bounds: variants are within 9 mm.
  - Absolute project path: same as the other MCP-driven Blender tools.

## Suggested Review Order

**Runtime: how an NPC gets its face**

- Entry point: reads `NPCEntity.Head` at spawn, swaps mesh + material.
  [`NpcHeadAppearance.cs:24`](../../Assets/_Game/Scripts/AI/NpcHeadAppearance.cs#L24)

- Guarded apply: incompatible heads warn and keep the default.
  [`NpcHeadAppearance.cs:31`](../../Assets/_Game/Scripts/AI/NpcHeadAppearance.cs#L31)

- Compatibility = same bind poses, valid weights, matching renderer bones.
  [`NpcHeadAppearance.cs:54`](../../Assets/_Game/Scripts/AI/NpcHeadAppearance.cs#L54)

- Data hook on the NPC asset; empty means base head.
  [`NPCEntity.cs:28`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L28)

- Mesh + material pair produced by the baker.
  [`HeadAppearanceSO.cs:11`](../../Assets/_Game/ScriptableObjects/Characters/HeadAppearanceSO.cs#L11)

**Editor bake: validated, all-or-nothing**

- Validate every head and texture before writing anything.
  [`SkinnedHeadVariantBaker.cs:51`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L51)

- Exact skin join against the canonical body: the neck contract.
  [`SkinnedHeadVariantBaker.cs:87`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L87)

- Snapshot / restore rollback for mesh, material and appearance.
  [`SkinnedHeadVariantBaker.cs:132`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L132)

**Blender generator: shape and paint**

- Distance-based neck freeze (the chin overhangs the neck).
  [`make_head_variant.py:92`](../../Tools/Blender/make_head_variant.py#L92)

- Soft ellipsoid deforms; mirrored sides read the same input.
  [`make_head_variant.py:122`](../../Tools/Blender/make_head_variant.py#L122)

- Frozen corners keep normals; smooth normals come from a throwaway copy.
  [`make_head_variant.py:139`](../../Tools/Blender/make_head_variant.py#L139)

- Hair recolour as a skin/hair mix: no halo, no cyan cast.
  [`make_head_variant.py:376`](../../Tools/Blender/make_head_variant.py#L376)

- Guard that the frozen neck is unchanged before export.
  [`make_head_variant.py:552`](../../Tools/Blender/make_head_variant.py#L552)

- One approved face config (shape + paint layers).
  [`warden.json:4`](../../Tools/Blender/configs/heads/warden.json#L4)

**Verification, tests, docs**

- Posed join gap + bare/armor captures; fails on missing join pairs.
  [`verify_head_variants.cs:80`](../../Tools/Blender/verify_head_variants.cs#L80)

- Shipped heads assigned and compatible with the NPC model.
  [`NpcHeadAppearanceTests.cs:199`](../../Assets/Tests/EditMode/NpcHeadAppearanceTests.cs#L199)

- Recipe, landmarks and gotchas for the next faces.
  [`CLAUDE.md:143`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L143)

