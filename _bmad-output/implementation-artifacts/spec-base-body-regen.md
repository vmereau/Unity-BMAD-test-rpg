---
title: 'Regenerated base body with a working 5-finger hand'
type: 'feature'
created: '2026-11-24'
status: 'done'
baseline_commit: '7895c6da818a1cd351543798ae7c506b2c65c865'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/PIPELINE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/HEADS_SKIN.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The base-hero body (player + every `Humanoid_BaseHero` NPC) is a free-tier Meshy mesh with a low-res texture, and its hands break: the four fingers are fused, Mixamo placed `Middle1` mid-palm and the Index chain near the fingertips, ring/pinky have no bones. Every fist and grip folds the palm and tears the fingertips.

**Approach:** Regenerate the **same man** with a better Meshy tier from new concept images with clearly spread fingers. Fit the new mesh onto the **canonical skeleton** in Blender (every non-finger bone and bind pose unchanged, neck seam at 1.585 m kept), with 5 finger chains placed at the real knuckles. Then rebake the body and the outfits that show hands, and swap the player and NPC model to it. Hand poses are spec 2 (deferred).

## Boundaries & Constraints

**Always:**
- Same man: new concept images are image-to-image from `models/test hero front view.png` / `back view.png`, keeping face, build and skin tone.
- The canonical head (`base_hero_head_baked`), every NPC head and its neck stump stay unchanged. The new body's neck boundary is snapped to the canonical exposed seam (positions, normals) and passes `SkinnedNeckValidator`.
- New rig: all existing bone names and rest transforms identical except the finger chains. Thumb / Index / Middle / Ring / Little, 3 bones + tip each, both hands, mapped in the Humanoid avatar.
- Immutable inputs stay untouched: `rigged test hero with textures.fbx`, `base hero canonical split.fbx`, Meshy sources. New files go in `models/base hero v2/`.
- Baked meshes are rebaked **in place** (GUIDs kept), so prefabs and `HeadAppearance_*` keep their references.
- The base head keeps its old atlas: the body gets its own material and texture.
- Prefab edits go through the Editor API, never raw prefab YAML.

**Ask First:**
- Every Meshy spend: show the cost and the model / texture pair before calling (planned: multi-image meshy-7.1 + meshy-7, 4K, 30 credits). Credit cap for this spec is 70.
- Concept images: give the prompts, then wait. The user generates them.
- The generated fingers come out fused or mangled: stop and propose the hand graft (a dedicated hand generation, about 30 credits).
- Any clip, Animator or combat-range change.

**Never:**
- Hand-pose layer, new tool grips (spec 2). Changing heads or the neck contract. A Mixamo re-rig (different proportions would break every fitted asset).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unarmed guard / jab | Player or NPC, fists up | Fingers curl into a closed hand: no torn tips, no palm fold | Capture fails → fix weights, recapture |
| Two-hand idle / attack | `2hand Idle`, attacks | Hands wrap the grip, no spikes | Same |
| NPC Meshy head on new body | Tam, Storekeeper… | Neck join closed, skin tone continuous through idle / attacks / dodge | Validator fails → re-snap seam |
| Company grunt / leather armor | Worn on new body | Gloves / bare hands deform with the fingers, collar unchanged | Rerun the fit / bake |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/models/test base hero/` -- current rig FBX (avatar), `base_hero_body_baked` / `_head_baked`, `M_TestBaseHero` (body + base head share it), `T_SkinMask_BaseHero`
- `Tools/Blender/split_head_body.py`, `prepare_armor_neck.py` (+ `configs/leather_neck.json`) -- seam 1.585 m, neck stump, leather outfit neck
- `Tools/Blender/fit_company_grunt.py` -- fits an unrigged Meshy outfit onto the canonical armature with barycentric weight transfer (pattern to reuse)
- `Tools/Blender/make_skin_masks.py` + `configs/skin_masks.json` -- body / outfit skin masks, `_SkinRefLuminance`
- `Assets/_Game/Editor/Characters/SkinnedNeckBaker.cs`, `SkinnedHeadVariantBaker.cs`, `CompanyGruntArmorBuilder.cs`, `SkinnedNeckValidator.cs` -- bake target = the rig FBX path
- `Assets/_Game/Prefabs/Player/Player.prefab` (`Character`), `Prefabs/Characters/Humanoid_BaseHero.prefab`, `Prefabs/Items/Armors/LeatherArmor_Visual.prefab`
- `Assets/Tests/EditMode/NpcModelPrefabTests.cs` -- asserts the hero FBX avatar and body mesh name

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` -- concept template: hands line → spread fingers with clear gaps, thumb apart; give the front + back prompts (image-to-image, same man) and HALT for the images in `models/base hero v2/refs/` -- source images
- [x] Meshy (Ask First) -- multi-image generation, download to scratchpad, trim + decimate (~40k tris), check the fingers -- new mesh; graft gate
- [x] `Tools/Blender/fit_base_body.py` + `configs/base_body_v2.json` -- align to the canonical armature, deform into canonical proportions (bind to a joint-matched armature copy, pose it to canonical rest, apply), drop everything above the seam and snap the neck boundary to the canonical seam, place the 5 finger chains at the fitted knuckles, weights (body transferred from the canonical body, hands auto-weighted on finger bones), export `models/base hero v2/base hero v2.fbx` (armature + body + canonical head) and the 4K texture -- reproducible fit
- [x] `models/base hero v2/*.meta`, `M_BaseHeroV2Body.mat` -- Humanoid, Create From This Model, scale per PIPELINE, every finger mapped, normals per PIPELINE; SG_SkinLit with new texture + mask -- import
- [x] `Tools/Blender/configs/skin_masks.json` -- body v2 entry, run `make_skin_masks.py` -- NPC skin tones on the new body
- [x] `SkinnedNeckBaker.cs`, `SkinnedHeadVariantBaker.cs`, `CompanyGruntArmorBuilder.cs`, `fit_company_grunt.py`, `prepare_armor_neck.py` config -- target the v2 rig / body; rerun body, leather and grunt bakes and the head-variant bake; validators pass -- outfits follow the new fingers
- [x] `Player.prefab`, `Humanoid_BaseHero.prefab` -- swap `Character` to the v2 FBX per PIPELINE *Swapping the player body* (sockets by bone name and offset, avatar, `_bodyRenderer`, Head SMR rebound by name) -- player + NPCs use it
- [x] `Assets/Tests/EditMode/NpcModelPrefabTests.cs` -- v2 FBX path; assert all 30 finger human bones are mapped on the avatar -- regression guard
- [x] `PIPELINE.md`, `HEADS_SKIN.md`, `Humanoids/CLAUDE.md`, `Tools/Blender/README.md` -- v2 body, fit script, rig contract (5 fingers), material split -- docs

**Acceptance Criteria:**
- Given hand close-ups rendered on the new body (bind pose, `Unarmed Idle Guard`, `lead jab`, `2hand Idle`, `Sword Braced Guard`, a bow clip), when inspected, then no torn tips, spikes or folded palm; five separate fingers in bind pose.
- Given StartingTown in play mode, when the player fights unarmed, swings a two-hander and NPCs brawl, then hands read as closed and weapons stay in the sockets (same grip position as before, ±1 cm).
- Given every `HeadAppearance_*` and skin tone, when `verify_head_variants.cs` / `verify_skin_tones.cs` run, then the neck join and tone checks pass on the new body.
- Given the EditMode suite, when run, then all tests pass.

## Design Notes

Keeping the canonical skeleton is what makes this safe: heads, sockets, clip retargeting and the ragdoll all key on bone names and rest transforms. Only the finger bones move, and only the body, the hands of outfits and the avatar depend on them. Snapping the new neck boundary to the old seam mirrors the existing head-variant contract.

## Spec Change Log

- 2026-11-24 — Meshy cap raised to 100 credits by the user (88 used): the first 3D generation (`pose_mode` t-pose) fused
  index + middle; regenerated without it from palms-forward concepts (user-approved).
- 2026-11-24 — User decision: slim the fingers 15 % now; the closed-fist shape (clips curl ~300°, fingertips pass through
  the palm) moves to spec 2 (hand-pose layer). The first AC (no torn tips in the guard / jab fists) is therefore not met
  in this spec.
- 2026-11-24 — Meshy spend detail (all user-approved per call): 2 concept images (image-to-image `nano-banana-pro`,
  9 credits each) + two multi-image 3D generations with Ultra geometry + 4K (35 each, +5 over the planned 30).
- 2026-11-24 — Task deviations (from the acceptance audit), each explained under Design Notes: the exported FBX holds
  armature + body only (the canonical head is untouched, nothing to export); the avatar is `BaseHeroV2Avatar.asset`
  built from a flattened hierarchy, not the FBX's own; the neck seam is validated with UV / tangent checks replaced by
  `RingUvError` (ring UVs must be one of the head's) because the head ring is UV-split; outfits get grafted v2 hands
  instead of a `fit_company_grunt.py` / `prepare_armor_neck` refit; `SkinnedHeadVariantBaker` keeps the old rig target.

## Design Notes (implementation)

- **In-place rebind, not a prefab swap:** `Humanoid_BaseHero` is a variant of the old FBX nested in `NPC_base Variant`
  with added components; the Player's `Character` is an FBX instance. `BaseHeroV2Applier` overrides the finger bones,
  adds Ring / Pinky, rebinds the `base_hero` SMR and swaps the avatar; sockets, ragdoll and components are untouched.
- **Avatar asset:** the v2 FBX avatar expects an `Armature.001` node above Hips (Blender exports skinned meshes beside
  the armature), so `BaseHeroV2Avatar.asset` is built from a flattened hierarchy with the FBX's human mapping.
- **Neck band on the head's atlas:** the Meshy texture under the chin is Meshy's own chin shadow and its neck UVs are
  fragmented, so faces above 1.53 m on the canonical surface use `M_TestBaseHero` with projected canonical UVs
  (submesh 1), and the v2 colour is baked to the old texture over 1.47–1.53 m. The seam check accepts, per ring vertex,
  any of the head's UVs at that position (UV-split head vertices); tangents off.
- **Outfit hands grafted, not refitted:** both outfits had their own bare Meshy hands (fused / misplaced fingers);
  `OutfitHandGraft` replaces them with the v2 hands (submesh 1) inside the outfit bakers.
- **Heads unchanged:** the Head SMR keeps the old bone order; `SkinnedHeadVariantBaker` still targets the old rig FBX
  (non-finger bind poses are identical), so it needed no retarget.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- `SkinnedNeckValidator` via the bakers, `verify_head_variants.cs`, `verify_skin_tones.cs`, `verify_company_grunt.cs` -- expected: no failures

**Manual checks:**
- Hand close-up sheet from the edit-mode capture (same 4 clips as the diagnosis, plus braced guard and bow); in-game screenshots of an unarmed fight and a two-hand swing.

## Suggested Review Order

**Prefab rebind and avatar (Unity entry point)**

- Validates both prefabs, then avatar, material, body asset, prefabs — in that order.
  [`BaseHeroV2Applier.cs:53`](../../Assets/_Game/Editor/Characters/BaseHeroV2Applier.cs#L53)
- Finger bones overridden, Ring/Pinky added, SMR rebound by name; dry run before commit.
  [`BaseHeroV2Applier.cs:285`](../../Assets/_Game/Editor/Characters/BaseHeroV2Applier.cs#L285)
- Avatar rebuilt from a flattened hierarchy: the FBX's own one doesn't bind.
  [`BaseHeroV2Applier.cs:82`](../../Assets/_Game/Editor/Characters/BaseHeroV2Applier.cs#L82)

**Neck seam contract**

- Body saved in place with canonical bind poses snapped; join validated against the head.
  [`BaseHeroV2Applier.cs:172`](../../Assets/_Game/Editor/Characters/BaseHeroV2Applier.cs#L172)
- Replaces the strict UV check: ring UVs must be one of the head's.
  [`BaseHeroV2Applier.cs:259`](../../Assets/_Game/Editor/Characters/BaseHeroV2Applier.cs#L259)
- Head bakes now validate against an in-memory canonical body, not the v2 asset.
  [`SkinnedHeadVariantBaker.cs:75`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L75)

**Outfit hands**

- Drops outfit hand skin, appends v2 hands as the last submesh, v2 bone order.
  [`OutfitHandGraft.cs:33`](../../Assets/_Game/Editor/Characters/OutfitHandGraft.cs#L33)
- Called from both outfit bakers, so re-bakes stay grafted.
  [`SkinnedNeckBaker.cs:59`](../../Assets/_Game/Editor/Characters/SkinnedNeckBaker.cs#L59)
  [`CompanyGruntArmorBuilder.cs:63`](../../Assets/_Game/Editor/Characters/CompanyGruntArmorBuilder.cs#L63)

**Blender fit**

- Pipeline order: fit, neck, zip, sheets, fingers, weights, thinning, bake, neck UVs, export.
  [`fit_base_body.py:1328`](../../Tools/Blender/fit_base_body.py#L1328)
- Finger weights: knuckle ramp, perpendicular radial falloff, two-chain web blend.
  [`fit_base_body.py:1236`](../../Tools/Blender/fit_base_body.py#L1236)
- Zip pairs loops by arc length (angle sort folded the throat).
  [`fit_base_body.py:487`](../../Tools/Blender/fit_base_body.py#L487)
- Neck band on the head's atlas; old colour baked into the v2 texture below it.
  [`fit_base_body.py:752`](../../Tools/Blender/fit_base_body.py#L752)
  [`fit_base_body.py:674`](../../Tools/Blender/fit_base_body.py#L674)
- Removes Meshy's thumb-web sail before finger detection.
  [`fit_base_body.py:558`](../../Tools/Blender/fit_base_body.py#L558)

**Peripherals**

- Avatar / finger-bone regression guards.
  [`NpcModelPrefabTests.cs:69`](../../Assets/Tests/EditMode/NpcModelPrefabTests.cs#L69)
- v2 body mask skips the neck-band faces.
  [`make_skin_masks.py:165`](../../Tools/Blender/make_skin_masks.py#L165)
- Pipeline doc for the whole flow.
  [`PIPELINE.md:29`](../../Assets/_Game/Art/Characters/Humanoids/PIPELINE.md#L29)
