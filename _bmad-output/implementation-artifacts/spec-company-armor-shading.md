---
title: 'Company armor directional shading'
type: 'bugfix'
created: '2026-11-11'
status: 'done'
baseline_commit: '6da3482a8cb53a7fc19a1b10a0c9c2dda3d1d074'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Company Guard Armor appears flat or shaded even under direct sunlight. Its fitted FBX contains invalid calculated vertex normals, which the equipment mesh bake preserves.

**Approach:** Rebuild the Company's normal and tangent frames at canonical player mesh scale, validate the result, and regenerate its equipped and pickup meshes. Preserve the outfit design and equipment behavior.

## Boundaries & Constraints

**Always:** Preserve the existing StartingTown scene edits and Company artwork additions. Keep source FBX, textures, player rig, head, weights, UVs, materials and leather armor intact. Work through Unity asset APIs, preserve derived asset GUIDs, and clean up temporary preview resources.

**Ask First:** Changes to shared mesh baking behavior, source geometry, scene lighting or texture appearance.

**Never:** Brighten the texture or add emission to conceal the defect. Change global sunlight, ambient light or shadows as an armor workaround. Save diagnostic scene objects into production scenes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Company rebuild | Fitted source contains fallback normals | Derived mesh has geometric normal directions and regenerated tangent frames | Refuse saving if frame validation fails |
| Repeated rebuild | Existing derived assets and references | Identical geometry and stable GUIDs | Report builder failure without claiming success |
| Equipment and pickup | Shared outfit material | Both respond to light direction with retained textures | Inspect both outputs before acceptance |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs` — imports source, canonical bake, UV repair, saves equipped and pickup meshes.
- `Assets/_Game/Editor/Characters/SkinnedMeshBaker.cs` — transforms imported normal vectors; shared behavior remains unchanged.
- `Assets/_Game/Editor/Characters/SkinnedNeckBaker.cs` — neck UV repair retains original normals/tangents; final tangent generation must follow repair.
- `Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/CompanyGrunt_baked.asset` — equipped mesh.
- `Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/CompanyGrunt_Pickup.asset` — pickup mesh, derived from equipped mesh.
- `Tools/Blender/verify_company_grunt.cs` — isolated character pose preview pattern.
- `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` — pipeline guidance.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs` — regenerate normals after canonical transformation, preserve them through UV finishing, and regenerate Mikk-compatible tangents afterward; check finite, complete usable frames and face agreement before saving. Limit behavior to Company armor.
- [x] `Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/CompanyGrunt_baked.asset` and `CompanyGrunt_Pickup.asset` — regenerate through the revised builder, preserving asset identity and geometry data unrelated to shading.
- [x] `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` — record verified small-coordinate importer failure and Company frame-generation ordering.
- [x] `Temp/ArmorShading/` — capture corrected textured and neutral comparisons in an isolated preview; rotate light relative to a fixed pose to isolate lighting from animation.

**Acceptance Criteria:**
- Given the current fitted FBX, when building Company armor, then normals no longer share the incorrect import fallback direction across the garment and their area-weighted disagreement with triangle faces drops substantially from the measured 50.7% baseline.
- Given a fixed equipped pose, when light direction changes, then front/back/side surfaces respond plausibly and the head and outfit belong to the same lighting environment.
- Given the generated pickup, when inspected under directional light, then its frame data matches the corrected outfit surface.
- Given the original mesh, when comparing the regenerated result, then positions, UVs, indices, skin weights and bind poses remain unchanged.
- Given existing scene and artwork edits, when verification finishes, then those edits remain preserved and no temporary objects remain.

## Design Notes

Live inspection found the same 50.7% area disagreement in the fitted FBX and final baked mesh; the canonical hero is approximately 0.00042%. Imported mesh coordinates are approximately 1/100 of canonical size and many imported normals are exactly (0,1,0). The display scale is 100. Scale-sensitive importer behavior is the leading explanation; its internal numerical cause is not proven.

A temporary isolated preview at `Temp/ArmorShading/final_full_Idle.png` compares current, normal-map-disabled, white/no-normal-map, and recalculated-frame variants across four views. Frame correction clearly restores garment volume. Those initial columns sample different idle times, so final verification must hold pose constant. Existing source albedo includes painted darkness; correcting normals should retain intended dark cloth.

## Verification

Use Unity MCP to rebuild and check compilation/console. Compare pre/post geometry channels and asset GUIDs, measure frame validity and area-weighted normal agreement, and capture fixed-pose directional-light comparisons. Verify pickup data against the corrected source. Use temporary mesh/material clones and preview scenes with finally cleanup. No production scene lighting edits are needed.


## Implementation Evidence

- Builder compiled and completed successfully. Equipped mesh: 52,966 vertices / 40 bones; pickup: 31,925 vertices.
- Exact pre/post serialized comparisons of positions, UV0, triangles, bind poses, full weights/counts and GUIDs passed for both meshes.
- Final normal opposition: equipped 0.0100348%; pickup 0.0161956%; no referenced zero normals. Original equipped baseline: 50.7089%.
- Fixed idle pose, four light directions and four views captured in Temp/ArmorShading/before_directional_Idle.png, after_directional_Idle.png and neutral_directional_Idle.png. Corrected garment and head respond consistently.
- No compile/runtime errors after rebuild; two pre-existing QuestEditActions obsolete-API warnings remain. StartingTown SHA256 unchanged.



## Spec Change Log

- Review found that calculating normals after UV corner splitting can introduce collar facets. Amend generation order: calculate smooth normals at canonical scale before UV repair, let repair copy them, then regenerate tangents after final UVs. KEEP Company-only scope, geometry invariants, validation and fixed-light previews. This corrects the non-frozen implementation ordering.




## Review Triage

Three independent reviewers checked the diff: blind adversarial, edge cases, acceptance. Corrected UV-split faceting by preserving pre-split smooth normals. Four zero cancellation normals receive isolated-face fallback; final tangents follow repaired UVs. Reviewers found no remaining blockers. Additional generic validator hardening suggestions were rejected as unnecessary for this deterministic engine-generated frame pipeline and its measured asset validation.

Final rebuild is byte-identical on repeat for both mesh assets. Geometry, UV0, triangles, full skin weights/counts, bind poses and GUID comparisons remain exact. Final directional and neutral sheets reflect the smoothing correction; pickup inspection also passed. No production scene objects or lighting were changed.

## Suggested Review Order

- Rebuild the outfit's shading at canonical scale while preserving collar smoothing.
  [CompanyGruntArmorBuilder.cs:38](../../Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs#L38)
- Reject unusable shading frames before replacing derived mesh assets.
  [CompanyGruntArmorBuilder.cs:140](../../Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs#L140)
- Inspect fixed-pose light directions; columns rotate the light, rows rotate the view.
  [Corrected preview](../../Temp/ArmorShading/after_directional_Idle.png)
- Read the import-scale guidance for future outfit work.
  [CLAUDE.md:145](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L145)
