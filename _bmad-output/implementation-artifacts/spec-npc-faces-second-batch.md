---
title: 'NPC faces, second batch (toned heads, blonde / red hair, scars, Company neck brand)'
type: 'feature'
created: '2026-11-14'
baseline_commit: 'cc2bb8471094dd965e309024a3cd228614e24ea8'
status: 'done'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
  - '{project-root}/Tools/Blender/README.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-npc-head-variants.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-npc-skin-tones.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 9 of 13 NPCs still wear the player's face. The 4 variant heads have no skin mask, so they can't take a tone (a toned NPC with one keeps the base colour and logs a warning).

**Approach:** Extend `make_head_variant.py` so each head writes its own skin mask from its paint layers, plus a `decal` op that projects the Company emblem as an ink brand. Then author 9 new faces and give the 4 existing heads masks (the Warden also gets a lip scar). Same pipeline as before: config → mockup → approval → export → bake → assign.

New faces (tone in brackets; ★ = Company neck brand):
- **Tam** (Fair): red hair + stubble, freckles, black eye, eye bags
- **Brann** (Olive): dark brown hair, fuller cheeks, neat short beard
- **Storekeeper** (Deep): short black hair greying at the temples, short beard, narrow face
- **Harl** ★ (Weathered): dirty-blonde cropped hair, stubble, brow scar
- **DockGuard** ★ (Fair): ash-blonde, clean-shaven, younger angular face
- **CompanyGuard_ArrivalLeft** ★ (Fair, 20s): blonde, clean-shaven, a light stubble shadow
- **CompanyGuard_ArrivalRight** ★ (Olive, 30s–40s): dark cropped hair, stubble, broken nose
- **WarehouseGuard** ★ (Deep): shaved head, short black beard
- **Bandit** (Weathered): unkempt black hair, heavy stubble, gaunt cheeks, cheek slash scar

Existing heads (no tone assigned): Cook, Blacksmith and TentMate get a mask only; the Warden gets a mask and a lip scar.

## Boundaries & Constraints

**Always:**
- Every head-variant rule from spec-npc-head-variants still applies: frozen neck within 12 mm, shape ≤ ~9 mm, the exact-join / neck-UV / bind-space bake checks, a committed config per face, no new geometry.
- A head mask matches the base mask outside head-only texels.
- Inside head-only texels, the mask is a skin weight tracked through the paint layers:
  - hair, beard, brows, iris and brand count as not skin
  - bald or shaved paint counts as skin
  - scars, freckles and bruises are partly skin, so they keep some of their hue
- Variant head materials use the base `_SkinRefLuminance`, so the tone transform is identical at the neck seam.
- The brand is dark ink from `Art/Artworks/Company/Emblem.png` (luminance = coverage). It sits on the left side of the neck, in head-only texels, and stays visible above the Company armor collar.
- Mask PNGs are reproducible: two runs must produce identical hashes.

**Ask First:**
- HALT after the Blender mockups (front, three-quarter, side, eye close-up, plus a neck close-up for branded heads) of all 9 new faces and the Warden's scar, and wait for approval or tweaks.
- HALT again after the Unity tone close-ups of the new faces, before assigning them.
- If light blonde shows a halo or cast the edge blending can't fix, ask before falling back to darker blonde.

**Never:**
- New geometry (separate hair, beards, ears)
- Tones on Warden, Cook, Blacksmith or TentMate
- Changes to `Player.prefab` or the player's head
- Per-tone textures or materials
- Image-to-3D heads
- Customization UI

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Toned variant head | NPC with a head + tone | Face and neck in the tone; hair, beard, eyes and brand keep their colour; no seam at the neck | No log |
| Untoned variant head | Head with a mask, no tone | Unchanged from URP Lit (diff 0) | No log |
| Branded guard in armor | Company guard + `Armor_CompanyGrunt` | Brand visible above the collar, not clipped by the armor | N/A |
| Head without a mask | Legacy material, `_SkinMask` empty | Existing behaviour: head keeps its own tone | Existing one-time warning |

</frozen-after-approval>

## Code Map

- `Tools/Blender/make_head_variant.py` -- paint ops (`_paint_layer`), texel → position map, export; add skin tracking, the `decal` op and mask export
- `Tools/Blender/make_skin_masks.py` -- `_head_mask` (base rule) and `_save_mask` / `_blur`; reuse them, don't fork (moved into `make_head_variant.py` as `_base_head_skin` / `_blur` / `_save_mask`, used by both)
- `Tools/Blender/configs/heads/*.json` -- 4 approved configs: the Warden's gets the lip scar, the Blacksmith's burn scar gets `skin: 0.5` (mask only, paint unchanged), the rest are untouched
- `Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs:221` -- currently clears `_SkinMask`; assign the head's mask + base ref luminance instead
- `Assets/Tests/EditMode/NpcSkinToneTests.cs:190` -- the variant-without-mask test uses `M_Head_Cook`; switch it to a synthetic unmasked material
- `Assets/Tests/EditMode/NpcHeadAppearanceTests.cs` -- shipped-head compatibility list
- `Assets/_Game/Data/NPCs/*/NPC_*.asset` -- 9 head assignments + 4 new tones (Bandit, the 2 arrival guards, WarehouseGuard)

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/make_head_variant.py`:
  - skin weight carried through every layer (per-op default, overridable per layer with `skin`)
  - `decal` op: planar projection of an image, with ink colour, opacity and a fade into skin detail
  - writes `T_SkinMask_Head_<Name>.png` on export
  - adds a neck close-up render
  -- per-head masks, brand
- [x] `Tools/Blender/configs/heads/{tam,brann,storekeeper,harl,dockguard,guardleft,guardright,warehouseguard,bandit}.json` + `warden.json` lip scar -- faces
- [x] **CHECKPOINT** -- mockups of all faces; HALT for approval
- [x] Export the 13 heads → `models/heads/<Name>/` (FBX + texture + mask; existing FBXs regenerate identically, only Warden's texture changes) -- assets
- [x] `SkinnedHeadVariantBaker.cs` -- mask import settings (Single Channel R, not sRGB), assign `_SkinMask` + base `_SkinRefLuminance`; `Bake Head Variants` -- baked heads
- [x] **CHECKPOINT** -- Unity tone sheet of the 9 toned faces (bare, plus armored for guards) and the untoned diff of the 4 old heads; HALT for approval
- [x] `HeadAppearance_*` ×9 on the NPC assets + tones for Bandit / arrival guards / WarehouseGuard -- assignment
- [x] `NpcSkinToneTests.cs`, `NpcHeadAppearanceTests.cs` -- every shipped head material has a mask and the base ref luminance; the no-mask warning uses a synthetic material; all 13 heads are compatible and assigned -- regression guard
- [x] `Art/Characters/Humanoids/CLAUDE.md`, `Tools/Blender/README.md`, `deferred-work.md` (remove the resolved batch entry) -- docs

**Acceptance Criteria:**
- Given StartingTown play mode, when all 13 NPCs are viewed, then each has a distinct face, toned NPCs match across face, neck and hands, and the console shows no new errors or `[NpcSkinTone]` mask warnings.
- Given each new head, when it idles, walks, attacks and dodges, bare and in Company armor, then `verify_head_variants.cs` reports no neck gap.
- Given the EditMode suite, when run, then all tests pass.

## Spec Change Log

- 2026-11-14, review (Acceptance auditor). Design Notes and Code Map synced to the as-built behaviour; no code reverted. Every deviation came from a user-approved checkpoint and is recorded in Implementation Evidence:
  - the light / dark hair skin range, from the mockup halo fixes
  - the brand's size and position, from the Unity collar check
  - the Blacksmith scar's skin weight
  - the shared helpers
  KEEP: all 13 approved configs, the unblurred-start tracking, the opacity-weighted decal skin, the base ref luminance on every head.

## Review Triage

Three reviewers (Blind hunter, Edge case hunter, Acceptance auditor).
- **Spec sync (no loopback):** brand numbers, the hair skin range and the Blacksmith config. See the Spec Change Log.
- **Patched — generator:**
  - The decal skin weight follows the painted ink, opacity included. Before, 22 % of the light skin under the ink stayed untoned, a ghost on dark tones.
  - The decal reads the image's alpha and caches on path + mtime. It rejects a midline centre, `mirror_x`, a non-positive size and a degenerate crop.
  - The skin weight is clamped after each layer, a negative `strength` is rejected, and the eyes are re-excluded after the last layer (a later region could re-tone them).
  - The base ref luminance is read from `M_TestBaseHero.mat` (no copied constant).
  - `utf-8-sig` for `skin_masks.json`. `export="mask"` accepts a string, and an empty list errors. `preview_tone` is validated before painting. The texture save is in `try`/`finally`.
  - The docstrings match the code.
- **Patched — baker:** a mask PNG that is on disk but not imported is imported first, and the bake fails if it still can't be, instead of wiping the head's mask. The class doc says import settings are a contract, not rolled back.
- **Patched — tests:** each head's mask is checked by name, 13 heads are pinned, and Warden / Cook / Blacksmith / TentMate stay untoned (`FirstBatchFaces_StayUntoned`).
- **Patched — verification:** `verify_head_tones.cs` flags FAIL on a non-zero untoned diff and throws clear errors for missing heads or tones.
- **Post-fix evidence:**
  - All 13 masks re-exported; Harl's paint is unchanged (≤ 0.5/255); masks reproducible.
  - Re-bake passed. Untoned 13 heads vs URP Lit: 0/255.
  - WarehouseGuard's brand on Deep has no ghost.
  - EditMode 1414/1414.
- **Rejected:**
  - Double blur at UV gutters: gutters are 0 in both pipelines.
  - Stale ref on existing materials: `CopyPropertiesFromMaterial` re-syncs it.
  - Warn-once leak in tests: the set is per component, and the cache is cleared in TearDown.
  - `_lum` mismatch: the base masks regenerate byte-identical.
  - Edit-mode cache growth in the verify script: by design, same as `verify_skin_tones.cs`.
  - Partial writes on a failed export: the files are regenerable from the config.

## Design Notes

Skin weight per texel starts from the **unblurred** base head rule (`_base_head_skin`). It blends layer by layer as `s = s·(1−m) + k·m`, where `k` is the layer's skin value, and is clamped after each layer. After the last layer the eyes are excluded again and the result is blurred once. Defaults:
- `skin` = 1
- `grey` / `tint` = 1 − smoothstep(raw hair fraction). The range depends on the hair: [0.6, 0.95] for dark hair (tone mixed edge texels, which keeps them dark) and [0.35, 0.7] for light hair (keep a noticeable hair share untoned, which avoids a salmon band on dark tones).
- `fill` = 0 (beard). Scars use `skin: 0.5`, freckles 0.8, bruises 0.4.
- `darken` keeps `s`
- `iris` = 0
- `decal` = 0 where ink is painted (coverage × opacity)

Toning replaces chroma with `tone × lum / ref`, so a partly masked scar keeps part of its pink.

Brand (as shipped, measured on the mesh and raised above the Company collar):
- 24 × 22 mm at x 0.061, y 0.012, z 1.628, projected along +X
- dark blue-black ink at 0.78 opacity, over the skin detail so pores show through
- the emblem is cropped to its glyph, which leaves out the generator's sparkle watermark

Blonde: the hair recolour targets `col × strand`. The cap is relative to hair luminance, so light blonde comes from the colour itself. If the edges halo, first widen `fraction_range` and add `detail` noise.

## Implementation Evidence

- Generator: all 4 shipped heads repaint within 0.5/255 of their committed textures (the paint is unchanged; only the Warden's lip scar is new). Every head freezes the neck with a max shift of 1.2e-7 m. Max deform 0.45–8.4 mm.
- Masks: `T_SkinMask_BaseHero` / `T_SkinMask_CompanyGrunt` regenerate byte-identical after the shared-helper refactor. The head masks hash the same on a second run.
- Mockup rounds:
  - Hairline band on Cook → track from the unblurred base rule.
  - Light-hair salmon edge on Deep → the light/dark `skin_hair_range` split.
  - Bandit "unkempt" noise layer dropped (camo blotches).
  - Sideburns shortened; the base jaw shadow recoloured for red/blonde; clean-shaven jaw on DockGuard.
  - Tam's black eye strengthened.
- User approved.
- Unity checkpoint: `Temp/HeadTones/heads.png`. In Company armor the collar hid half the brand, so it moved from z 1.615 to 1.628 (24 × 22 mm, x 0.061, y 0.012). User approved.
- Bake: 13 heads, all exact-join / neck-UV / bind-space checks pass. Every `M_Head_*` has its own BC4 mask, ref 0.179067 (base), strength 0.
- `verify_head_tones.cs`: all 13 head materials untoned vs URP Lit, mean 0.000 / max 0/255.
- `verify_head_variants.cs`: 137 join pairs per head, max posed gap ≤ 0.013 mm (Warden; others ≤ 0.0072 mm) over 5 clips × 8 poses.
- Play mode StartingTown: 13/13 NPCs on their own head. The 9 toned NPCs use toned head + body copies; the cache holds 17. No `[NpcSkinTone]` warnings and no new console errors (pre-existing unrelated entries only).
- EditMode: 1413/1413 (one new test: `MaskedVariantHead_IsToned_LikeTheBody`).
- Deviations from the plan:
  - Brand size / position: the plan said ~28 × 16 mm at x 0.05 / z 1.61; it ships as measured on the mesh and raised for the collar.
  - Cook / Blacksmith / TentMate keep their FBX and texture (mask export only).
  - The 5 branded heads were re-exported as texture + mask after the brand move (FBX unchanged).
  - New verification script `Tools/Blender/verify_head_tones.cs`, used instead of `verify_skin_tones.cs`, which only covers the base head.
- Side effect: setting `_head` / `_skinTone` re-serialized `NPC_Bandit`, writing the code-default hearing radii and dropping the stale `_weapon` key. No value changes.
- Known limits:
  - Light hair on dark tones keeps a slightly pale edge (per-texel mask).
  - GuardLeft's short sideburn ends a bit squarely.
  - The front-view jaw streak in the preview sheet is the preview-lighting artifact already noted in spec-npc-head-variants.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- `make_head_variant.py` export twice -- expected: identical mask PNG hashes
- `Tools/Characters/Bake Head Variants` -- expected: 13 heads, all checks pass

**Manual checks:**
- `verify_head_variants.cs` posed-gap report; `verify_skin_tones.cs` untoned diff 0 for heads with masks; in-game captures of all 13 NPCs.

## Suggested Review Order

**Skin mask: what tones and what doesn't**

- Entry point: tracking starts from the unblurred base rule, eyes re-excluded, blurred once.
  [`make_head_variant.py:577`](../../Tools/Blender/make_head_variant.py#L577)

- Per-op skin defaults; every layer blends the weight, clamped after each.
  [`make_head_variant.py:390`](../../Tools/Blender/make_head_variant.py#L390)

- Light vs dark hair edge rule: avoids halos on dark tones either way.
  [`make_head_variant.py:420`](../../Tools/Blender/make_head_variant.py#L420)

- Base head rule shared with the base-mask generator (one source).
  [`make_head_variant.py:526`](../../Tools/Blender/make_head_variant.py#L526)

**Company brand (decal)**

- Ink over the skin detail; skin weight follows the painted ink.
  [`make_head_variant.py:437`](../../Tools/Blender/make_head_variant.py#L437)

- Projection along ±X, crop, alpha and the input guards.
  [`make_head_variant.py:484`](../../Tools/Blender/make_head_variant.py#L484)

- Shipped brand placement: above the Company collar.
  [`harl.json:241`](../../Tools/Blender/configs/heads/harl.json#L241)

**Unity bake: mask onto the material**

- The head's own mask, never the base's; base ref luminance kept.
  [`SkinnedHeadVariantBaker.cs:252`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L252)

- Mask import contract; an unimported PNG is imported, never silently dropped.
  [`SkinnedHeadVariantBaker.cs:136`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L136)

**Mockup tooling**

- Preview through the game's tone maths, ref read from the material.
  [`make_head_variant.py:615`](../../Tools/Blender/make_head_variant.py#L615)

- Export subsets: mask-only for shipped heads, no FBX churn.
  [`make_head_variant.py:739`](../../Tools/Blender/make_head_variant.py#L739)

**Data, tests, verification, docs**

- Example assignment: head + tone on the NPC asset.
  [`NPC_Bandit.asset:58`](../../Assets/_Game/Data/NPCs/Bandit/NPC_Bandit.asset#L58)

- Masked variant head tones like the body; no warning.
  [`NpcSkinToneTests.cs:215`](../../Assets/Tests/EditMode/NpcSkinToneTests.cs#L215)

- Each head has its own mask; 13 heads pinned.
  [`NpcSkinToneTests.cs:248`](../../Assets/Tests/EditMode/NpcSkinToneTests.cs#L248)

- First-batch faces stay untoned (Never rule).
  [`NpcSkinToneTests.cs:270`](../../Assets/Tests/EditMode/NpcSkinToneTests.cs#L270)

- All 13 shipped heads assigned and compatible.
  [`NpcHeadAppearanceTests.cs:22`](../../Assets/Tests/EditMode/NpcHeadAppearanceTests.cs#L22)

- Untoned parity (must be 0) + tone sheet.
  [`verify_head_tones.cs:80`](../../Tools/Blender/verify_head_tones.cs#L80)

- Recipe and mask gotchas for the next faces.
  [`CLAUDE.md:191`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L191)
