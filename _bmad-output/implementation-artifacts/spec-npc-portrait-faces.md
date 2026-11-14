---
title: 'NPC portrait faces (AI portrait → landmark-fitted shape + projected face texture), pilot of 3'
type: 'feature'
created: '2026-11-14'
status: 'done'
baseline_commit: '499e901a6bdf909e3f9f669fae1cc1ca11cd2a29'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
  - '{project-root}/Tools/Blender/README.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-npc-faces-second-batch.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** All 13 variant heads still look like the same man. They share the base Meshy face texture (eyes, lids, nose, lips), and shape changes are capped at ~9 mm because larger ones stretch that detail.

**Approach:** Per NPC, the user generates a front portrait and a side profile from prompts I write. MediaPipe landmarks on the portrait (and on a render of the base head) drive a landmark-fitted warp of the head above the frozen neck. The side profile sets depth. The front portrait is then projected onto the warped face as its texture, skin-normalised so tones still work. Pilot: **Warden** (untoned), **Tam** (Fair), **Storekeeper** (Deep); their existing heads are replaced in place.

## Boundaries & Constraints

**Always:**
- Neck contract unchanged: freeze within 12 mm of the seam, then exact-join / neck-UV / bind-space bake checks, same vertex count and bind poses (no new geometry, hair / beard / ears stay painted).
- Shape change ≤ 25 mm per vertex.
- The projected face is normalised to the base skin's luminance and chroma statistics. Large-scale lighting is removed from it (keep the detail, drop the baked shading).
- Each head's mask also follows the portrait: eyes, hair, brows and beard count as not skin; lips are partly skin.
- The untoned diff vs URP Lit stays 0.
- Everything is reproducible from committed files: the config, the reference images and the landmark JSON.
- MediaPipe runs in an isolated `uv` Python 3.12 environment, never in Blender's Python.
- Portrait prompts follow `ART_DIRECTION.md` (banned words included).

**Ask First:**
- HALT after writing the 6 prompts, until the user has placed the images.
- HALT after the Blender mockups (front, ¾, side, eye close-up, side-by-side with the portrait) for approval or tweaks.
- HALT after the Unity tone sheet, before committing.
- Ask before adding geometry, or before exceeding 25 mm.

**Never:**
- Calling `generate_image` / OpenRouter.
- Image-to-3D heads.
- Touching the other 10 heads, `Player.prefab` or the player's head.
- Per-tone textures.
- Runtime code changes (heads are already assigned).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Config with `portrait` | Front + side refs + landmark JSON | Landmark-fitted shape + projected face, then paint layers on the scalp | N/A |
| Config without `portrait` | Any of the other 10 configs | Output byte-identical to the current pipeline | N/A |
| No face found | MediaPipe finds no face, or more than one | Landmark tool exits non-zero naming the image | Clear error, no JSON written |
| Missing reference | `portrait` names a missing file or landmark JSON | `run()` raises before touching the scene | Clear error |
| Warp too big | A fitted vertex would move more than `max_mm` | Displacement clamped, max reported in the result | Warning in the result |

</frozen-after-approval>

## Code Map

- `Tools/Blender/make_head_variant.py` -- `_deform` (region warp), `_freeze_weights`, `_position_map` (texel → rest position), `_paint`, `_base_head_skin`, `_render`, `_export`, `run`
- `Tools/Blender/configs/heads/{warden,tam,storekeeper}.json` -- current configs: keep scalp hair layers, drop the face-detail layers the portrait replaces
- `Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs` -- re-bake (GUIDs kept), no change expected
- `Tools/Blender/verify_head_variants.cs`, `verify_head_tones.cs` -- posed gap + tone sheet
- `Assets/Tests/EditMode/NpcHeadAppearanceTests.cs`, `NpcSkinToneTests.cs` -- 13 heads pinned; must keep passing
- Landmarks (game m, face −Y): iris x ±0.0353 / z 1.7197, nose tip 1.68, mouth 1.635, chin ≈ 1.595, brows 1.735

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` -- portrait prompt template (front + side, neutral expression, flat light, plain background, same framing); give the user the 6 prompts + `Tools/Blender/configs/heads/refs/<name>_{front,side}.png` paths -- references
- [x] **CHECKPOINT** -- HALT until the images are in place
- [x] `Tools/FaceRef/face_landmarks.py` (PEP 723 `uv run`, Python 3.12, mediapipe; model cached in `Temp/FaceRef/`) -- front: 478 landmarks + iris, side: profile silhouette curve from the plain background -- writes `refs/<name>_landmarks.json`; also runs on the base head renders → `refs/_base_landmarks.json`
- [x] `make_head_variant.py` `portrait` block:
  - front ortho + side ortho renders of the base head for the base landmarks; ray-cast the landmarks to 3D
  - similarity-align the portrait landmarks on the eye corners and nasion; RBF warp in X/Z from the front, depth (Y) from the profile delta (median-centred, capped); frozen neck weight; `max_mm` clamp
  - project the front portrait onto the warped texel positions with a facing + region falloff, and the side profile on the cheeks / jaw (see Spec Change Log)
  - delight + skin-stat normalisation; skin weight from the portrait (eyes from the landmark polygons)
  - mockup sheet with the portrait beside the renders
  -- the feature
- [x] `configs/heads/{warden,tam,storekeeper}.json` -- `portrait` block + trimmed paint layers -- pilot faces
- [x] **CHECKPOINT** -- mockups; HALT
- [x] Export + `Tools/Characters/Bake Head Variants` -- 3 heads replaced (FBX, texture, mask), all checks pass, other 10 unchanged
- [x] **CHECKPOINT** -- `verify_head_tones.cs` sheet + `verify_head_variants.cs` posed gap; HALT
- [x] `Tools/Blender/README.md`, `Humanoids/CLAUDE.md`, `deferred-work.md` (the other 10 faces) -- recipe, limits

**Acceptance Criteria:**
- Given the 3 pilot heads in StartingTown, when compared to each other and to the base head, then the eyes, nose, mouth and face proportions visibly follow their portraits, and the hair, beard and scars read as before.
- Given each pilot head, when posed through idle / walk / attack / dodge, bare and in Company armor, then `verify_head_variants.cs` reports a max gap ≤ 0.02 mm and no seam shows.
- Given Tam (Fair) and Storekeeper (Deep), when toned, then the face, neck and hands match, and the eyes, hair and brand stay untoned.
- Given the EditMode suite, when run, then all tests pass.

## Spec Change Log

- 2026-11-14, Unity checkpoint (human-approved, option 1). Front-only projection left the base hero's texture (incl. his dark beard shadow) on the jaw sides: a visible seam on Tam and the Storekeeper. Amended: the side profile is also projected (cheeks, jaw, below the temples, mirrored to both sides), exposure matched skin to skin. Known-bad states avoided: (a) a nasion-based profile scale, 1.7x off on the Warden (broken nose), replaced by crown -> nose tip calibrated on the base; (b) a single-point depth anchor (generated profiles are slightly pitched: Warden's nose pushed back 26 mm), replaced by the median midline delta, capped at `max_depth_mm` 8, chin underside skipped; (c) overlap-band exposure matching (darkened the sides). Consequence: max reshape 11 / 6 / 11 mm (Warden / Tam / Storekeeper) instead of the mockup's 19 / 25 / 11 mm. KEEP: front alignment on eye corners + nasion, skin-only colour mapping, the approved paint tweaks (Tam sideburn cleared, Storekeeper temples 0.45, Warden crop).
- 2026-11-14, bake (implementation). The neck ring is stored at z = 1.5850001, so the strict `<= 1.585` freeze seed left it out and the portrait warp moved it up to 0.3 mm (exact-join failure). Portrait configs seed within `SEAM_TOL_M` (0.1 mm) and `run()` fails if a seam vertex has a weight. Legacy configs keep the strict seed so they regenerate identical to their shipped FBXs (the I/O matrix row).

## Design Notes

Base landmarks come from MediaPipe run on renders of the base head, so no landmarks are placed by hand. Their 2D positions are ray-cast onto the mesh to give the 3D anchors. The RBF warp (kernel r + affine; ~130 anchors: face oval, brows, eye keys, nose, lips, cheeks, midline) moves them to the aligned portrait positions. Back of the skull and crown are pinned; the neck is held by the freeze weight. Projection runs after the warp: the warped positions come from applying the same RBF to the texel position map, so detail never stretches; this is why the 9 mm limit lifts. The front facing falloff (dot(normal, −Y) 0.25 → 0.55) hands the cheeks over to the side profile (|normal.x| 0.3 → 0.65, z 1.59–1.715, in front of the ear), then to the painted skin. The front region is the MediaPipe oval raised 20 mm toward the hairline: tight 6 mm edge at the jaw (no base beard shows through), 20 mm at the forehead.

## Implementation Evidence

- Landmark tool: 478 points on all 3 portraits + base renders (overlays checked). Identity test (base as its own portrait): warp 0, texture within 1.3/255 mean of the base.
- Mockups approved after one tweak round (Tam's base sideburn cleared, Storekeeper temple grey 0.9 → 0.45, Warden forehead: oval raised + wide top blend, Warden scalp = grey-flecked crop as in his portrait).
- Export: frozen 846 vertices, max shift 1.2e-7 m; PNG hashes identical on a second run. Max reshape: Warden 11.3, Tam 6.2, Storekeeper 11.3 mm.
- Bake: 13 heads, all exact-join / neck-UV / bind-space checks pass. `verify_head_variants.cs`: 0.0004 mm max posed gap on the 3 pilot heads (others unchanged). Untoned: 13 heads vs URP Lit 0/255. Tone sheet `Temp/HeadTones/pilot_v2.png` approved.
- Legacy configs: no code path changes without `portrait` (strict freeze seed, no projection). Cook / Blacksmith geometry regenerates within 0.00024 mm of the shipped FBX (FBX float round-trip; the FBX file embeds an export timestamp, so equality is checked on geometry), and their files are untouched.
- I/O rows exercised: blank image -> "expected exactly one face, MediaPipe found 0", no JSON; mirrored profile -> refused, no JSON; missing landmark JSON and `side_strength: 1.5` -> raise before the scene is touched; `max_mm: 3` -> 269 vertices clamped, warning.
- Right side checked (`Temp/HeadVariants/{Tam,Warden}_right34.png`): the mirrored side projection copies no bruise or scar.
- EditMode 1414/1414.
- Not done: a StartingTown play-mode pass. The runtime path and the assignments are unchanged (same `HeadAppearance_*` GUIDs, baked in place); the NPC model was checked in the preview scenes above.

## Verification

**Commands:**
- `uv run Tools/FaceRef/face_landmarks.py <image>` -- expected: JSON with 478 points, non-zero exit on no face
- `make_head_variant.py run(config, export=True)` twice -- expected: identical PNG hashes
- `Tools/Characters/Bake Head Variants` -- expected: 13 heads, all checks pass
- MCP `run_tests` EditMode -- expected: all pass

**Manual checks:**
- Mockup sheet side by side with the portrait; Unity tone sheet; in-game captures of the 3 NPCs

## Review Triage

Three reviewers (Blind hunter, Edge case hunter, Acceptance auditor). No intent gap or spec defect: no loopback.
- **Patched — generator:**
  - The seam guard tested its own input; replaced by a real check: `run()` fails on any config if the neck ring moves.
  - Inputs: `side_region` deep-merged; every numeric option validated; landmark JSON shape checked; each portrait must match the size its landmarks were measured on; base landmarks must match `ORTHO`; `"portrait": {}` refused.
  - `_delit` raises on an empty mask (was NaN through the whole texture); a degenerate profile (crown -> tip too short) raises instead of clamping the scale.
  - 16-bit / float PNGs refused (Blender stores them linear); alpha composited over the same grey as the landmark tool.
  - Warp chunks 50k -> 10k (memory); clamp count excludes frozen vertices; a warning when config deforms push past `max_mm`.
  - `side_mirror` option (default on, both right sides checked clean); comments and the report key fixed.
  - Regression: the 3 pilot textures and masks regenerate with identical hashes.
- **Patched — landmark tool:** refuses a profile with no nose point on its left edge (facing right), a crown or nose at the image edge; landmark 234 / 454 range clipped; `chin` optional; paths outside the project stored absolute; LA / P alpha composited; docstring (crown -> tip scale).
- **Patched — docs:** ART_DIRECTION template index, `refs/PROMPTS.md` (the 6 `{FACE}` lines), gotchas (ring check, side mirroring, input checks, debug overlay), `uv run` command, design notes, `*.lnk` ignored in `refs/`.
- **Deferred:** ears / temples are not anchored in the warp; StartingTown play-mode pass (both in `deferred-work.md`).
- **Rejected:**
  - `render_base_refs` scene leak: every `run()` builds a fresh scene and world.
  - EEVEE id / `world.use_nodes`: same calls as the shipped `_render` on Blender 5.2.
  - Pinning mediapipe: the landmark JSONs are committed and are the reproducible input.
  - Per-channel side exposure: a skin-to-skin colour match, not the std gain that tinted hair.
- **Open verification gap:** AC1 asks for StartingTown. The heads were baked in place (same `HeadAppearance_*` GUIDs, runtime unchanged) and checked in the Unity preview scenes; no play-mode pass was run.

## Suggested Review Order

**Entry point: how a portrait config runs**

- Portrait inputs checked before the scene is touched; warp, then paint, then project.
  [`make_head_variant.py:1298`](../../Tools/Blender/make_head_variant.py#L1298)

- Every option, landmark file and image validated up front.
  [`make_head_variant.py:832`](../../Tools/Blender/make_head_variant.py#L832)

**Shape: landmark warp + profile depth**

- Similarity alignment on eye corners + nasion, then RBF to ray-cast anchors.
  [`make_head_variant.py:883`](../../Tools/Blender/make_head_variant.py#L883)

- Profile scale crown -> nose tip, calibrated on the base head.
  [`make_head_variant.py:908`](../../Tools/Blender/make_head_variant.py#L908)

- Depth deltas re-centred on their median, capped, chin underside skipped.
  [`make_head_variant.py:927`](../../Tools/Blender/make_head_variant.py#L927)

- Neck contract: the ring may never move, on any config.
  [`make_head_variant.py:1323`](../../Tools/Blender/make_head_variant.py#L1323)

- Seam tolerance only for portraits; legacy heads regenerate unchanged.
  [`make_head_variant.py:106`](../../Tools/Blender/make_head_variant.py#L106)

**Texture: front + side projection**

- Front oval raised to the hairline; tight jaw edge, wide forehead blend.
  [`make_head_variant.py:1032`](../../Tools/Blender/make_head_variant.py#L1032)

- Only skin moves to the base skin; hair, beard and eyes keep the portrait colour.
  [`make_head_variant.py:1068`](../../Tools/Blender/make_head_variant.py#L1068)

- Side profile exposure matched skin to skin.
  [`make_head_variant.py:1113`](../../Tools/Blender/make_head_variant.py#L1113)

- Optional one-sided side projection for asymmetric marks.
  [`make_head_variant.py:1099`](../../Tools/Blender/make_head_variant.py#L1099)

- All tunables and their defaults.
  [`make_head_variant.py:680`](../../Tools/Blender/make_head_variant.py#L680)

**Landmarks (outside Blender)**

- Front landmarks + crown row; exactly one face required.
  [`face_landmarks.py:58`](../../Tools/FaceRef/face_landmarks.py#L58)

- Profile features and the facing / crop checks.
  [`face_landmarks.py:98`](../../Tools/FaceRef/face_landmarks.py#L98)

- Base-head landmark renders (flat ortho, front + side).
  [`make_head_variant.py:690`](../../Tools/Blender/make_head_variant.py#L690)

**Configs and docs**

- Pilot config: portrait block, paint trimmed to scalp / sideburns.
  [`tam.json:168`](../../Tools/Blender/configs/heads/tam.json#L168)

- Portrait prompt template.
  [`CLAUDE.md:71`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L71)

- Recipe, how it works, gotchas.
  [`CLAUDE.md:235`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L235)
