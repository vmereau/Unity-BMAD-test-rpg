---
title: 'Armor neck continuity for shared outfits and character heads'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
baseline_commit: 'd604c924c10fc4c5f46a67af5f69577f2be9fda9'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent â€” do not modify unless human renegotiates">

## Intent

**Problem:** Leather armor preserves the player's face, but its neck bridge creates a flat shelf, stretched texture patches and an exposed collar opening. Future guards sharing uniforms need different heads without an outfit carrying another character's neck skin.

**Approach:** Keep exposed neck skin with the head, extending the textured neck beneath the garment opening. Repair the leather collar around that neck and establish a reproducible canonical neck interface with Blender preparation and Unity validation. Retain body replacement and one skeleton.

## Boundaries & Constraints

**Always:** Preserve the original source models, skeleton, avatar, sockets, animation controller and item behavior. Derived head/body meshes must remain continuous when unequipped. Preserve skin UVs, corner normals, tangents and weights through preparation, import and bake. Exposed neck skin uses the head's material. Outfit fitting and head fitting target the same canonical base skeleton and neck profile. Save derived mesh assets in place to preserve their GUIDs. Restore preview state.

**Ask First:** Changing the character silhouette beyond the neck/collar, introducing additional equipment slots, or migrating production NPC prefabs to an equipment system.

**Never:** Full character replacement, per-frame seam welding, altered gameplay stats, automatic compatibility claims for arbitrary Mixamo rigs, or outfit-owned skin tinting. No production NPC art or customization UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Equipped | Leather armor on canonical rig | Original face and textured neck; garment opening covers the lower neck boundary | Validation reports coverage/geometry defects |
| Unequipped | Armor removed or visual absent | Original head/neck and body meet without overlap artifacts or gaps | Existing safe fallback retained |
| Shared uniform | Two distinct compatible head fixtures on canonical rig | Same outfit; head skin remains independent of outfit material | Validate both before claiming reuse |
| Incompatible part | Wrong bind skeleton, missing bones or incompatible neck profile | Authoring validation fails with actionable measurements | No silent certification based on names alone |
| Invalid seam | Missing coverage, mismatched boundary weights/normals or degenerate UV bridge | Diagnostic identifies failed condition | Preserve existing assets on rejection |

</frozen-after-approval>

## Code Map

- `Tools/Blender/split_head_body.py` â€” existing planar split and normal transfer.
- `Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs` â€” canonical mesh-space conversion and asset persistence.
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs`, `SkinnedBodyPart.cs`, `SkinnedMeshRebinder.cs` â€” body-only swap and bone binding; preserve working behavior.
- `Assets/_Game/Art/Characters/Humanoids/models/test base hero/` â€” original model, split FBX, baked head/body and skin material.
- `Assets/_Game/Art/Characters/Humanoids/models/humanoid leather armor/` â€” source/fitted FBX and baked leather mesh.
- `Assets/_Game/Prefabs/Items/Armors/LeatherArmor_Visual.prefab`, `Assets/_Game/Prefabs/Player/Player.prefab` â€” visual references.

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/prepare_armor_neck.py` â€” add repeatable preparation using explicit source objects and canonical neck parameters; replace the malformed bridge with a fitted garment opening and retain the head's textured neck below it. Preserve originals and validate before export.
- [x] `Tools/Blender/split_head_body.py` â€” support the agreed lower head/neck boundary and preserve source surface attributes accurately; regenerate derived head/body FBX and baked assets. Keep unequipped surfaces complementary.
- [x] `Assets/_Game/Editor/Characters/SkinnedNeckValidator.cs` â€” add reusable authoring diagnostics for canonical bind-space compatibility, boundary correspondence, weights/normals, UV degeneracy and posed neck/collar coverage. Distinguish exact skin joins from garment overlaps; do not treat height checks as seam validation.
- [x] Derived FBX/mesh assets in the model folders above â€” regenerate the repaired head, body and leather armor through Blender and the Unity baker, preserving references and material ownership. Update the two visual prefabs only where needed.
- [x] `Assets/Tests/EditMode/SkinnedNeckValidatorTests.cs` â€” test genuine malformed-input failures and matching versus mismatched seam influences; exercise compatible distinct-head fixtures and incompatible skeleton data.
- [x] `Tools/Blender/README.md`, humanoid `CLAUDE.md`, player prefab `CLAUDE.md` â€” document repeatable preparation, precise interface measurements, validation limits and the NPC reuse contract.

**Acceptance Criteria:**
- Given leather armor, when viewed from front, rear and both sides in Unity at rest and through idle, locomotion, attack and dodge poses, then no neck shelf, exposed hole, skin texture discontinuity, clipping or lighting crack appears at the join.
- Given no armor, when inspecting the same views and poses, then head/neck and body retain a continuous skin surface.
- Given two distinct compatible head fixtures, when wearing the same leather visual, then the face and exposed neck remain specific to each head without outfit material edits.
- Given repeated equip/unequip and an invalid visual, when checking renderers and hierarchy, then head visibility, body restoration and safe fallback remain correct without accumulating armor instances.

## Design Notes

Investigation already checked 137 bottom-ring samples in Unity: head-to-armor maximum rest gap approximately 0.000017 mm, normal difference 0 degrees, weight difference zero and identical bindposes. Blender independently confirms matching boundary data. The actual bridge has UVs at (0,0) and a leather-only material. Unity front/side/rear captures show a flat textured shelf and collar opening (`Temp/ArmorSeamAudit/before_0.png` through `before_2.png`). Matching ring vertices therefore does not establish surface or material continuity.

Record the lower boundary and collar clearance from measured geometry and posed coverage. Vary face shape and skin material above the preserved interface for the second head fixture; this does not certify unrelated imported rigs.

## Verification

- Run focused Unity EditMode validator and existing rebinder tests; check compilation and console errors.
- Measure rest and posed boundary correspondence after Unity import/bake; exact exposed skin joins target at most 0.1 mm positional error and 1 degree normal difference, with identical canonical influences within numerical tolerance.
- Capture textured and neutral-material Unity close-ups before/after from four sides, equipped and unequipped; sample idle, locomotion, attacks and dodge and inspect maximum deformation frames.
- Exercise actual equip/unequip and failure fallback; inspect both compatible head fixtures. Record results and limitations in this spec before marking done.



## Implementation Evidence

- The head owns its skin neck down to 1.48 m; its hidden lower stump is closed. The original body conceals the inset extension when bare. The exposed skin join remains at 1.585 m.
- The damaged collar is reconstructed from its exterior winding boundary, with rounded rim and inner lining. Its profile is sampled at 1.56 m with 6 mm authored clearance. Rim weights interpolate the actual head triangle, rather than choosing the nearest vertex.
- SkinnedNeckBaker.BakeLeather validates all three transient parts before saving. Imported UV repairs precede canonical normal/tangent-frame alignment, including tangent handedness. Existing mesh GUIDs and prefab/item references remain. Runtime equipment code is unchanged.
- Rest measurements: 274 bidirectional skin-ring samples; gap below 0.000019 mm; weight and UV differences zero; armor bind-pose difference zero. Final aligned normal and tangent differences are zero, with no handedness mismatches.
- Focused Unity EditMode results: 25/25 passed (19 validator/baker cases plus six existing rebinder tests), job f97f2cad31bb4c319ec5b1a81c0be31e. The formerly pending large-mesh test now uses a valid skinned fixture and passes.
- Preview verification: scan 441 source animation frames to select maximum rim/head deformation frames; capture those extrema plus regular samples from idle, walk, run, overhead attack and dodge roll; front/right/rear/left captures; repeated for bare body and a temporary altered-face/skin-material fixture. Maximum exposed join separation below 0.001 mm. Neutral-material captures distinguish geometry from texture shading.
- The final head has zero geometric open lower-neck boundary vertices. Ray coverage is not applicable to this closed stump; this does not certify all garment collisions. The helper retains ray diagnostics for open-neck parts.
- Live Play mode: actual inventory Equip/Unequip, repeated Refresh, missing visual, missing-bone fixture and end-of-frame cleanup passed. One armor instance equipped, zero unequipped; head enabled throughout; body restored on both failures. Temporary inventory addition and runInBackground restored; Play mode exited.
- Evidence: Temp/ArmorSeamAudit/pose-report.txt, poses_*.png, bare/, alternate/, neutral/. Tools/Blender/verify_neck_unity.cs reproduces isolated preview checks through Unity MCP.
- Limits: finite sampled poses and compatible head fixtures, not unrelated external rigs or all future animations. Existing shoulder/arm deformation and source head shading are outside the neck repair. The rebuilt collar has revised dark leather lining and requires normal human visual acceptance.


## Review Triage

- Three independent reviewers covered acceptance, edge cases and implementation. Fixed shared-corner UV changes, tangent preservation, index-format/submesh promotion, alias protection, independent export rollback, Unity batch persistence rollback and preview cleanup. Added regression tests for shared neighbours and large meshes.
- Pose checks now use common world space, require correspondence samples and fail on invalid joins. Full-frame scans select deformation extrema; they do not certify collision-free motion.
- Interactive Blender is explicitly required. Exact-join validation checks a specified skin cross-section; separate topology checks verify the intentionally closed lower neck. Neither is presented as universal topology certification.
- Existing Burst compiler named-pipe errors are an editor environment limitation; C# compilation and focused EditMode tests pass.

## Chin Collar Regression

User reported and authorized correction of a new under-chin flap. Measured the sampled head profile: forward radius 58.55 mm at 1.585 m but 101.93 mm at 1.595 m. The latter follows the underside of the chin. Front generated rim weights also contain approximately 86% Head influence. Lower the collar to the true neck and remove Head influence from generated collar weights, blending toward Neck from existing garment weights. Preserve head/body assets and rig contract. Add profile radius rejection so a misplaced sampling height cannot silently generate another chin flap. Regenerate derived assets and visually inspect equipped/bare animation views.

## Chin Regression Verification

- Lowered collar profile to 1.56 m; generated rim excludes Head influence. Radius limited to 0.10 m and raised lip must stay strictly below the original skin seam. Final Blender max profile radius 0.091492 m.
- Expanded the damaged-neck trimming radius to 0.16 m to remove retained folded rear collar geometry. Added inner lining terminating inside the closed skin stump at 1.485 m, rather than an exposed short lining edge.
- Final native meshes baked successfully: 34 hidden-cap UV repairs, zero garment UV repairs. All skin/interface/bind-space checks passed.
- Re-ran all five clips (441 source frames) with extrema close-ups. Maximum exposed skin join gap 0.000597 mm. Rim silhouette and zero Head influence assertions pass. Repeated for bare, alternate head and neutral materials.
- Review patches: reject profiles whose raised lip crosses the skin seam; identify generated rim by explicit atlas rows rather than radius filtering; normalize namespaced and plain Head bone names consistently.

Final scoped acceptance review: updated idle and dodge captures show the chin flap removed and continuous rear lining; no concrete remaining neck defect identified. Finite pose sampling remains the stated limitation.

## Suggested Review Order

**Repeatable asset preparation**

- Generate both derived assets before replacing production files.
  [prepare_armor_neck.py:457](<../../Tools/Blender/prepare_armor_neck.py#L457>)

**Canonical baking**

- Validate skin interfaces and persist the mesh batch with rollback.
  [SkinnedNeckBaker.cs:23](<../../Assets/_Game/Editor/Characters/SkinnedNeckBaker.cs#L23>)

- Isolate repaired UV corners while preserving neighbouring faces and material submeshes.
  [SkinnedNeckBaker.cs:111](<../../Assets/_Game/Editor/Characters/SkinnedNeckBaker.cs#L111>)

**Verification**

- Check positions, skin influences and complete shading frames at the canonical interface.
  [SkinnedNeckValidator.cs:75](<../../Assets/_Game/Editor/Characters/SkinnedNeckValidator.cs#L75>)

- Inspect animation extrema and four-sided equipped, bare and alternate-head previews.
  [verify_neck_unity.cs:1](<../../Tools/Blender/verify_neck_unity.cs#L1>)

**Supporting files**

- Exercise malformed interfaces, UV isolation and large-mesh repairs.
  [SkinnedNeckValidatorTests.cs:1](<../../Assets/Tests/EditMode/SkinnedNeckValidatorTests.cs#L1>)

- Keep canonical source paths and neck dimensions explicit.
  [leather_neck.json:1](<../../Tools/Blender/configs/leather_neck.json#L1>)

- Follow the reusable pipeline and compatible-head contract.
  [README.md:1](<../../Tools/Blender/README.md#L1>)
