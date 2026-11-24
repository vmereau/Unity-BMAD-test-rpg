---
title: 'NPC heads from Meshy 3D generations (route B)'
type: 'feature'
created: '2026-11-24'
status: 'done'
baseline_commit: '8e304eeb974482ce519d5c7388a5b0b5f56ef5f5'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/HEADS_SKIN.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Portrait faces get their shape from 2D landmarks (≤ 25 mm warp, ≤ 8 mm depth) and their texture from flat projections. A Meshy multi-image-to-3D generation from the same portraits may give a better 3D shape and an all-round texture. That can only be judged and shipped if it goes through the canonical-head pipeline: neck join, skin mask, toning and bake.

**Approach:** Use the Meshy head only as a **shape + texture source**. Align it to the base head with the existing ortho renders + `face_landmarks.py`, shrinkwrap the canonical base head onto it (neck frozen), and bake its colours into the base head UVs. The skin is moved to the base skin colour and masked like portrait faces. Export through `make_head_variant.py` as a new head, `StorekeeperMeshy`, from the Storekeeper's existing `refs/` portraits.

## Boundaries & Constraints

**Always:** The canonical head topology, UVs, bones and frozen neck are untouched (`run()` frozen and ring checks pass unchanged). The Meshy route is an optional `"meshy"` config block: existing configs regenerate byte-identical. Every input is validated before the scene is touched. Meshy is called once: `meshy_multi_image_to_3d` with `[storekeeper_front, storekeeper_side]`, `meshy-7.1` + texture `meshy-7`, 4K, `remove_lighting`, GLB, 30 credits. A second generation only if the first is catastrophically wrong (cap 2 generations / 60 credits). Fix minor defects in Blender or the config.

**Ask First:** Assigning `HeadAppearance_StorekeeperMeshy` to `NPC_Storekeeper` (only after the user has compared the sheets). Any second Meshy generation. Any change to the base head, the posmap cache key or the shipped heads.

**Never:** Rigging or using the Meshy mesh / skeleton in game. New head topology. Full-body generation. New AI image generation (reuse `refs/`). Editing `Assets/` assets of other heads.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | config with `meshy.model` + `meshy.landmarks` | mockups / export like a portrait face, report with alignment scale, max shape mm, clamped count | N/A |
| Missing / stale inputs | GLB or landmarks missing, or the landmarks measured on another render | — | raise before `_load()`, naming the file and the fix |
| Bad alignment | fewer than 40 base vertices hit the Meshy surface in the face region, or scale outside 0.7–1.4 | — | raise `RuntimeError` with the numbers |
| Large offsets | shrinkwrap offset > `max_mm` | clamped, counted in report warnings | warning, not failure |
| Both blocks | config has `portrait` and `meshy` | — | raise: only one source |

</frozen-after-approval>

## Code Map

- `Tools/Blender/make_head_variant.py` -- `run()` pipeline. Reuse: `_load`, `_freeze_weights(SEAM_TOL_M)`, `_position_map`, `_paint(project=…)`, `ORTHO` + `render_base_refs` camera setup, `_similarity`, `_delit`, the `skin_map` approach of `_project_portrait`, `_export`, frozen-ring checks
- `Tools/FaceRef/face_landmarks.py` -- landmarks from front + side renders (unchanged)
- `Tools/Blender/configs/heads/storekeeper.json` -- scalp / beard paint to reuse where the Meshy texture is weak
- `Tools/Blender/verify_head_variants.cs`, `verify_head_tones.cs` -- join + tone sheets; `PLAN` lists heads by hand
- `Editor` `SkinnedHeadVariantBaker` (`Tools/Characters/Bake Head Variants`) -- bakes every `models/heads/*` folder

## Tasks & Acceptance

**Execution:**
- [x] Meshy (MCP) -- show the cost and wait for OK. Generate, then download the GLB to `Tools/Blender/sources/meshy_heads/storekeeper/` -- source kept reproducible outside `Assets/`
- [x] `Tools/Blender/make_head_variant.py` -- add `render_meshy_refs(config)`: import the GLB, coarse-place it (bbox top → crown 1.86, facing −Y), flat ortho front / side renders with the `ORTHO` cameras, and save the coarse transform in a JSON beside them. Add `_meshy_inputs`, `_meshy_fit` and `_project_meshy`:
  - `_meshy_inputs`: validation.
  - `_meshy_fit`: similarity from the Meshy front landmarks (`MP_ALIGN`) to the base landmarks, plus a y offset from the side glabella. Ray-cast each non-frozen base vertex along ± its normal onto the aligned mesh. Displacement × freeze weight × region weight (face + scalp; ears and the back of the skull fade to 0), Laplacian-smoothed, capped at `max_mm`.
  - `_project_meshy`: for each warped texel, nearest Meshy surface point → Meshy UV → colour. Skin goes to the base skin via white balance + luminance (as in `skin_map`), skin weight from that, eyes 0. Region blend, fading to the painted base above the seam (z 1.585 → 1.61).
  - Hook into `run()` beside `portrait` -- reuses the export / bake contract.
- [x] `Tools/Blender/configs/heads/storekeepermeshy.json` -- name `StorekeeperMeshy`, `preview_tone` `#5A3A28`, `meshy` block, paint only where needed -- the new head
- [x] `Tools/Blender/configs/heads/refs/storekeeper_meshy_landmarks.json` -- from `face_landmarks.py` on the Meshy renders
- [x] Unity: `Bake Head Variants`, then add `StorekeeperMeshy` (Tone_Deep) to `verify_head_tones.cs` `PLAN` and run both verify scripts -- side-by-side with `Storekeeper`
- [x] `HEADS_SKIN.md` (new *NPC Meshy heads* section), `.claude/rules/meshy-mcp.md` (multi-image head quirks found), `Humanoids/CLAUDE.md` topic table -- document the route

**Acceptance Criteria:**
- Given the Storekeeper portraits, when the Meshy head is generated and `run(storekeepermeshy.json)` renders, then the mockups show the Meshy face shape and texture on the canonical head, with no visible seam at the neck or the region border.
- Given `run(..., export=True)` and `Bake Head Variants`, when the baker validates `StorekeeperMeshy`, then the exact join, neck UV and bind checks pass and `HeadAppearance_StorekeeperMeshy.asset` exists.
- Given `verify_head_tones.cs`, when the head is rendered untoned, then the diff is 0, and with Tone_Deep the face skin is toned while eyes and hair are not.
- Given any existing head config, when it is re-run with `export=["texture","mask"]`, then its outputs are byte-identical to the shipped files.

## Design Notes

Why shrinkwrap instead of using the Meshy mesh: every downstream contract (exact neck join, bind space, neck UVs shared with the body, skin mask, `_SkinRefLuminance`) is tied to the base head topology. Shrinkwrap + texture bake keeps all of them, so only the Blender step is new. Accepted limit: volumes the base topology can't follow (thick beard, large ears) stay texture-only.

Texel positions for the colour lookup = posmap rest positions passed through the same displacement field (interpolated from vertices, like `warp` in the portrait route). The posmap cache is never rebuilt.

**As built (deviations from the task list, found during implementation and review):**
- The raw GLB (~60 MB) is not kept. `meshy_trim_source` writes a 200k-face OBJ (LFS) + 2048 base colour under `sources/meshy_heads/`.
- No coarse placement. The refs are framed on the source's own bounding box, and `_frame.json` records the pixel→source mapping plus hashes of the source and both renders. `render_meshy_refs` deletes stale landmarks. The matrix's 0.7–1.4 bound is checked as a head-size ratio (aligned crown above the eye line vs the base: `head_ratio_range`), because the raw similarity scale is in arbitrary source units.
- Depth: the median front-plane gap over the upper face (`depth_band_m` 1.66–1.78, above a sculpted beard), not the side glabella.
- Region weight: the whole head is shrinkwrapped (the back of the skull is not faded), except `meshy.exclude` ellipsoids. The ears are excluded and moved onto the Meshy ears by a config `deforms` offset (−19 mm).
- Eyes: colours come from a straight front projection inside the eyes (`eye_front_scale`). `_paint(force_eyes=False)` lets the Meshy lids over the base eyeball tone. Sclera and grey hair stay untoned through the skin weight (lighter than skin **and** neutral grey), checked on Fair as well as Deep.
- After the user compared the sheets, they asked for the head to be assigned: `NPC_Storekeeper._head` = `HeadAppearance_StorekeeperMeshy` (Tone_Deep kept).

## Verification

**Manual checks:**
- `Temp/HeadVariants/` mockups + Meshy render sheet: face aligned (eyes, nose, mouth on the Meshy features), no stretching at the region border
- `Temp/HeadTones/heads.png` and the `verify_head_variants.cs` sheets: Storekeeper vs StorekeeperMeshy, bare and in Company armor

## Suggested Review Order

**Entry point: the Meshy route in `run()`**

- One face source per head; Meshy validated before the scene, then fit + projection hook like portraits.
  [`make_head_variant.py:1827`](../../Tools/Blender/make_head_variant.py#L1827)

- Legacy / portrait heads keep the forced eye zero; Meshy heads opt out.
  [`make_head_variant.py:1832`](../../Tools/Blender/make_head_variant.py#L1832)

**Shape fit**

- Landmark similarity, head-size ratio guard, depth from the upper face.
  [`make_head_variant.py:1486`](../../Tools/Blender/make_head_variant.py#L1486)

- Shrinkwrap along ± normal, exclude regions, Laplacian smoothing, `max_mm` clamp.
  [`make_head_variant.py:1533`](../../Tools/Blender/make_head_variant.py#L1533)

**Texture bake and skin weight**

- Nearest-surface colour per warped texel, renormalised barycentrics into the Meshy UVs.
  [`make_head_variant.py:1594`](../../Tools/Blender/make_head_variant.py#L1594)

- Front projection inside the eyes (the base eyeball sits behind the Meshy lids).
  [`make_head_variant.py:1612`](../../Tools/Blender/make_head_variant.py#L1612)

- Skin weight: sclera / grey hair excluded only when light AND neutral.
  [`make_head_variant.py:1655`](../../Tools/Blender/make_head_variant.py#L1655)

- `force_eyes` parameter: why Meshy lids must stay toneable.
  [`make_head_variant.py:567`](../../Tools/Blender/make_head_variant.py#L567)

**Inputs, staleness and Blender hygiene**

- Defaults and their meaning (exclude, eye_front_scale, head ratio, depth band, skin patches).
  [`make_head_variant.py:1171`](../../Tools/Blender/make_head_variant.py#L1171)

- Validation before the scene: hashes of source + renders, landmark shape, config types.
  [`make_head_variant.py:1412`](../../Tools/Blender/make_head_variant.py#L1412)

- Refs renders: deletes stale landmarks, records hashes in the frame.
  [`make_head_variant.py:1329`](../../Tools/Blender/make_head_variant.py#L1329)

- GLB trim: Base Color link (not normal map), no global orphan purge.
  [`make_head_variant.py:1236`](../../Tools/Blender/make_head_variant.py#L1236)

- Only datablocks created by the tool are removed.
  [`make_head_variant.py:1198`](../../Tools/Blender/make_head_variant.py#L1198)

**Config, verification, docs**

- Storekeeper config: ear offset deform + ear exclude.
  [`storekeepermeshy.json:5`](../../Tools/Blender/configs/heads/storekeepermeshy.json#L5)

- Tone sheet plan gets the new head.
  [`verify_head_tones.cs:8`](../../Tools/Blender/verify_head_tones.cs#L8)

- Route documentation and gotchas.
  [`HEADS_SKIN.md`](../../Assets/_Game/Art/Characters/Humanoids/HEADS_SKIN.md)
