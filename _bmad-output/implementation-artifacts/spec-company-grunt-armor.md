---
title: 'Company grunt armor fitted to the player'
type: 'feature'
created: '2026-11-11'
status: 'done'
baseline_commit: '2aea90863ae944f7f9044dd9febef38f04d6a484'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-armor-neck-continuity.md'
---

<frozen-after-approval reason="human-owned intent â€” do not modify unless human renegotiates">

## Intent

**Problem:** The downloaded Company grunt is a static full-character Meshy model, not an equippable outfit. It needs fitting and skinning to the player's canonical skeleton while retaining the player's head.

**Approach:** Preserve the downloaded source, derive a fitted and weighted outfit in Blender, finish its collar against the existing head-owned neck, and create a separate Company Guard Armor item and visual using the leather armor integration pattern.

## Boundaries & Constraints

**Always:** Preserve source FBX, textures and concept image. Preserve existing player head/body meshes, rig, sockets, avatar, animations and leather armor. Keep Company colors and chest emblem. Use the canonical skeleton and bind space, not just matching bone names. Keep exposed neck skin owned by the player head; remove the generated head from the outfit. Preserve garment UVs and texture detail. Restore editor scenes and temporary verification state.

**Ask First:** Major redesign of the supplied outfit, replacement of existing player assets, paid external rigging, new runtime equipment behavior or distribution through shops/quests.

**Never:** Claim a successful fit from bone names or rest pose alone. Replace the player with the generated guard. Migrate production NPCs or add new equipment slots.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Static source | Mesh with no groups or armature | Outfit fitted and weighted to canonical rig | Reject export if vertices lack usable weights |
| Equipped | Company armor on player | Original head, Company outfit, base body hidden | Existing equipment fallback remains intact |
| Unequipped | Company armor removed | Original body and head restored | No persistent temporary armor objects |
| Deformation | Movement, attacks and dodge | Collar stays below chin; limbs and split hem deform plausibly | Inspect and correct fitting/weights before acceptance |
| Existing outfit | Leather armor equipped afterward | Existing leather appearance retained | Do not overwrite leather assets |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/` â€” immutable source FBX, concept and four PBR maps; derived outputs belong here with distinct names.
- `Tools/Blender/prepare_armor_neck.py` â€” finishing logic for already fitted outfits, not an automatic fitter; avoid its leather-specific output and head replacement assumptions.
- `Tools/Blender/configs/leather_neck.json` â€” canonical rig and neck measurements.
- `Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs` â€” validates shared bind transforms and bakes into canonical mesh space.
- `Assets/_Game/Editor/Characters/SkinnedNeckValidator.cs` â€” neck/interface diagnostics.
- `Assets/_Game/Prefabs/Items/Armors/LeatherArmor_Visual.prefab` â€” equipment visual pattern.
- `Assets/_Game/Data/Items/Armor_TestArmor.asset` â€” existing leather item and provisional balance reference.

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/fit_company_grunt.py` â€” reproducibly import source and canonical body in an isolated scene; align torso, limbs and feet, remove generated head, preserve useful garment details and transfer normalized weights. Inspect hands separately; use canonical hands if generated skin cannot fit without visible defects.
- [x] `Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/CompanyGrunt_Fitted.fbx` â€” export fitted outfit on canonical skeleton, with collar lining and head-owned neck coverage; preserve source texture coordinates outside deliberate finishing work.
- [x] `Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs` â€” prepare imports/material, bake and validate mesh, create visual and world pickup prefabs plus independent armor item through Unity APIs. Use unique save ID and existing catalog sync. Start with leather-equivalent stats/prices as provisional values, clearly recorded. Provide a cropped source-concept icon; do not assign the entire character sheet as an inventory icon.
- [x] `Assets/_Game/Prefabs/Items/Armors/CompanyGruntArmor_Visual.prefab`, `CompanyGruntArmor_World.prefab` and `Assets/_Game/Data/Items/Armor_CompanyGrunt.asset` â€” wire equipment, pickup, physics and outline behavior using existing conventions.
- [x] `Tools/Blender/README.md` and humanoid `CLAUDE.md` â€” document preparation, inputs and observed limits.

**Acceptance Criteria:**
- Given the Company armor item, when equipped through actual inventory, then the player retains their head and wears the supplied Company design at correct scale.
- Given equipped and bare states, when inspected from four sides through idle, walk, run, attack and dodge samples, then no visible neck hole, chin flap, disconnected hand or gross clipping appears.
- Given repeated equip/unequip and switching to leather armor, when inspecting renderers and hierarchy, then exactly one outfit is active and the base body restores correctly.
- Given the dropped armor, when picked up again, then the same item returns to inventory and remains equippable.

## Design Notes

Blender audit: source contains one mesh, 45,143 vertices, zero vertex groups and no armature; dimensions approximately 1.852 Ã— 0.380 Ã— 1.885 m. Rigging is required, but a separate Mixamo pass is unnecessary if weights can be transferred and corrected on the canonical rig. This requires fitting before transfer. Existing neck tooling explicitly assumes an already fitted/weighted outfit and cannot do this initial step.

## Verification

- Validate imported weights, canonical bind agreement, scale, neck UVs and collar coverage; run existing rebinder and relevant neck-validator EditMode tests if their paths are exercised.
- Capture textured four-sided rest and animation previews, including collar, shoulders, hands and hem. Record any finite-pose limitations.
- Exercise real inventory equip/unequip, leather switching and drop/pickup; restore temporary inventory additions and editor state.
- Check Unity C# compilation and console after import/build. Record evidence before marking done.

## Implementation Evidence

- Source FBX is unrigged; Company preparation fits it and transfers normalized barycentric weights from the canonical body. No external rigging pass was needed. Derived FBX has 39,559 Blender vertices; confirmed final Unity bake has 52,966 vertices and 40 canonical bind poses. Main garment UVs, source files, original player assets and leather assets remain unchanged.
- Generic finishing has opt-in concentric-opening/name/UV settings; its leather defaults are preserved. Company adds a conforming 4 mm cloth shell over the actual lower-head surface, retaining canonical influences and a top sloped below the front jaw and higher at the sides/back. This covers skin exposed by shoulder/head motion without altering the head. Final export reimports the authoritative skeleton to avoid accumulated FBX bind drift.
- Builder validates bind space, neck UVs and feet/collar extents before creating the separate item, visual and compact torso pickup. Icon is cropped from the supplied concept. Unique item ID is `70e9d64414a940d491d8f9467363c95e`; catalog synced. Provisional balance matches leather: defense 5, endurance 1, buy 50, sell 25. No distribution changes.
- Focused EditMode run: 25/25 passed, no failures/skips, job `3d4731284dac4586a4f78c4e1b2c4edb`. First filter attempt used nonexistent namespaces and selected zero tests; only the corrected 25-test run is counted.
- Actual Play checks passed: inventory Add/Equip; original Head enabled and Body hidden; exactly one Company instance; repeated Refresh; Unequip returns Body and removes outfit after a frame; leather/Company switching; actual `InventoryUI.DropItem` followed by `ItemPickup.Interact`; same item returned exactly once and pickup removed; collected armor re-equipped. Original inventory/equipment and background setting restored, Play exited.
- `Tools/Blender/verify_company_grunt.cs` reproduces four-sided full and neck sheets for idle, walk, run, overhead attack and dodge, four time samples each. Final evidence is `Temp/CompanyArmorAudit/final_full_*.png` and `final_neck_*.png`. Preview renderers explicitly force skin-matrix recalculation; without it, edit-mode pictures incorrectly showed stationary sleeves despite moving bones.
- Visual limits: finite sampled poses, source mesh polygon quality, small uneven exposed-skin edges at collar sides and a visibly layered mantle finishing patch in close-up. The large neck wedge and trial radial UV fan were corrected. This is a functional prototype outfit, not a claim of final production tailoring or compatibility with arbitrary external heads/rigs.
- Console investigation distinguished the pre-existing player OnEnable animation-driver initialization exception from builder-time skeleton-less bounds warnings in the shared prefab helper. Company builder now clones the existing leather visual template and replaces mesh/material/part metadata, avoiding that bounds operation without changing runtime/shared code.
- Final builder revision compiled successfully and rebuilt the 52,966-vertex/40-bone Company assets successfully after the bounds-warning correction. Editor remained in Edit mode on Core. Verifier destroys optional cloned mesh/material resources in its finalizer; Blender audit scenes were removed and the user's original active Blender scene restored.


## Review Triage

Three independent reviewers checked the change: blind adversarial, edge cases, and acceptance. Patched preview-helper native mesh/material cleanup. No additional integration or acceptance blocker identified. Acceptance reviewer confirmed the final collar captures no longer contain radial UV stretching. Minor pale skin tabs at the collar edge remain a documented prototype polish limitation.

## Suggested Review Order

**Fitting and neck construction**

- Fit the source to the canonical rig and preserve its Company identity.
  [fit_company_grunt.py:1](../../Tools/Blender/fit_company_grunt.py#L1)

**Equipment integration**

- Build independent equipment assets using existing player binding and pickup behavior.
  [CompanyGruntArmorBuilder.cs:17](../../Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs#L17)

- Inspect the separate armor item and its provisional leather-equivalent balance.
  [Armor_CompanyGrunt.asset:1](../../Assets/_Game/Data/Items/Armor_CompanyGrunt.asset#L1)

**Reproduction and visual checks**

- Render sampled poses with refreshed skin matrices for truthful previews.
  [verify_company_grunt.cs:1](../../Tools/Blender/verify_company_grunt.cs#L1)

- Follow preparation instructions and understand finite-pose verification limits.
  [README.md:1](../../Tools/Blender/README.md#L1)
