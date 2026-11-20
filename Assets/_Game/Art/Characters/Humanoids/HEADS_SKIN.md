# NPC Heads and Skin — head variants, portrait faces, skin tones

> Read when making or editing an NPC head (painted variant or portrait face), its skin mask, or skin tones
> (`SG_SkinLit`, `SkinToneSO`). Run the portrait flow with `/characters:portrait-face`. Portrait prompt: `CLAUDE.md`
> → *Face Portrait Prompt*; neck contract: `PIPELINE.md` → *Head / body split*.

---

## NPC head variants (spec-npc-head-variants)

Per-NPC faces derived from the neck-prepared base head, with no new geometry: reshaping plus repainting a copy of the
skin texture. The painted skin keeps the base hero's colour; per-NPC skin colour comes from *NPC skin tones* below.
Every variant head has its own skin mask (spec-npc-faces-second-batch), so a variant face takes the NPC's tone like the
body; hair, beard, eyes and ink keep their colour.

1. **Config:** `Tools/Blender/configs/heads/<name>.json`. `deforms` = soft ellipsoid scale / offset regions (jaw, brow,
   nose, cheeks, skull). `paint` = layers `skin` (bald / receding / shaved), `grey` / `tint` (hair recolour), `fill`
   (beard, scar), `darken` (eye bags), `iris` (eye colour), `decal` (image projected along ±X: the Company neck brand);
   any layer takes `noise`, `detail`, `mirror_x`, `exclude`; an ellipsoid takes `rot_y` (degrees, tilt in the face
   plane: diagonal scars). Each layer also sets the **skin weight** of the texels it paints (see the mask gotchas below):
   per-op default, or `skin` 0–1 (scars 0.5, freckles 0.8, bruises 0.4 keep part of their hue once toned). Optional
   `preview_tone` (`#RRGGBB`) renders the mockup through the `SG_SkinLit` maths. Recolour the brows (`BROW` region)
   whenever the hair colour changes.
2. **Mockup:** `Tools/Blender/make_head_variant.py` `run(config)` renders front / ¾ / side / eye close-up to
   `Temp/HeadVariants/`. Get approval, then `run(config, export=True)` → `models/heads/<Name>/Head_<Name>.fbx` +
   `T_Head_<Name>_BaseColor.png` + `T_SkinMask_Head_<Name>.png`. `export=["mask"]` / `["texture", "mask"]` rewrites only
   those files (a shipped head's FBX never needs re-exporting for a paint or mask change). Renders include a left-neck
   close-up (brand side).
3. **Bake:** `Tools/Characters/Bake Head Variants` (`SkinnedHeadVariantBaker.BakeAll()`) bakes every `heads/` folder against
   the canonical target. It validates the exact join with `base_hero_body_baked` plus neck UV and bind space, then
   writes `Head_<Name>_baked.asset`, `M_Head_<Name>.mat` (copy of `M_TestBaseHero` with the variant texture, the variant's
   mask imported Single Channel R / linear, and the **base** `_SkinRefLuminance`: same tone transform on both sides of the
   neck seam) and
   `Data/Characters/Heads/HeadAppearance_<Name>.asset`.
4. **Assign:** `NPCEntity._head`. `NpcHeadAppearance` on `NPC_base Variant` swaps the Head SMR in `Awake`.
5. **Verify:** `Tools/Blender/verify_head_variants.cs` (posed join gap + neck / portrait sheets, bare and Company armor);
   `Tools/Blender/verify_head_tones.cs` (untoned diff of every head = 0, tone sheet of each planned face + tone).

Landmarks (game metres, face toward −Y in Blender): seam 1.585, chin underside ≈ 1.595, mouth 1.635, nose tip 1.68,
iris centres x ±0.0353 / z 1.7197 (r ≈ 3 mm), brows z 1.735, crown 1.86; side of the neck x ≈ 0.062 at y 0–0.03,
z 1.60–1.64 (the Company collar hides everything below z ≈ 1.615 from the side: keep a neck decal above it).
Shipped heads: the `HeadAppearanceSO` assets in `Data/Characters/Heads/` (who uses which: `NPCEntity.Head` / `_head`).
Several NPCs can share a head's mesh / material (17 assets, 13 distinct meshes — e.g. a placeholder copy until
`/characters:portrait-face`). Company brand only on the rank-and-file guards' heads, not on the Warden (officer).

Gotchas:
- Frozen = within 12 mm of any vertex at or below the seam (distance-based; the chin overhangs the neck).
  Read smooth normals from a mesh **copy**: zeroing custom normals on the real mesh re-bases its fans and shifts the
  frozen normals by ~1°.
- The Meshy atlas is fragmented, so painting goes through a baked texel → rest-position map (`Temp/HeadVariants/_posmap.npz`).
  Coverage comes from a margin-0 **colour** bake (Cycles fills alpha over the whole image), dilated 3 px, minus every
  texel a body face covers (otherwise speckles appear on the chest).
- Base hair is about 0.4 luminance darker than skin, and its edges are blurred. Recolour hair as a skin/hair mix with a
  luminance ratio capped at 1, or the hairline gets a light halo or a cyan cast.
- Shape changes above ~9 mm stretch the texture's eye / nose detail (painted variants). Portrait faces re-project
  the texture after the warp, so they go further (see *NPC portrait faces*). For bigger silhouettes, use new geometry
  (image-to-3D + fitting).
- **Head masks:** the skin weight starts from the **unblurred** base head rule (`_base_head_skin`, shared with
  `make_skin_masks.py`) and is blurred once at the end. Tracking from the blurred base PNG leaves a half-masked, untoned
  band wherever paint moves a hair edge (receding / shaved hairlines).
- Mixed hair / skin edge texels: for **dark** hair, tone everything but clearly-hair texels (toning a mixed texel keeps it
  dark; leaving it untoned shows its light skin share as a halo on dark tones). For **light** hair (blonde, grey, white),
  the reverse: a toned light texel turns tone-coloured (salmon band on Deep), so a noticeable hair share stays untoned.
  `grey` / `tint` pick the range from the hair colour's lightness (`skin_hair_range` overrides).
- Light / red hair: also recolour the base hero's jaw beard shadow, or a brown "beard" shows under blonde hair.
  Clean-shaven = a `skin` layer with a tight `hair_range` over the jaw and lower sideburns (see `dockguard.json`).
- The Blender mockup lights wash Fair / Olive out (both are lighter than the base hero's average skin); judge tones on
  the Unity sheet (`verify_head_tones.cs`).

---

## NPC portrait faces (spec-npc-portrait-faces)

Run it end to end with `/characters:portrait-face [NPC]` (prompts → landmarks → config → mockups → bake → verify).

Fixes the "same man, different hair" look: the face's eyes, nose, lips and skin come from a portrait instead of the
shared Meshy texture, and its shape follows the portrait's landmarks. Same neck contract (`PIPELINE.md` → *Head / body split*) and bake as the head variants
above.

1. **Portraits:** prompts from `CLAUDE.md` → *Face Portrait Prompt* (the filled `{FACE}` lines are kept in `refs/PROMPTS.md`);
   the user generates `refs/<name>_front.png` and `refs/<name>_side.png` (side made image-to-image from the front),
   8-bit PNG, face pointing left on the side image.
2. **Landmarks:** `uv run Tools/FaceRef/face_landmarks.py <front> <side> --out
   Tools/Blender/configs/heads/refs/<name>_landmarks.json`. Check the `_debug_front` / `_debug_side` overlays (gitignored).
   Base landmarks (`refs/_base_landmarks.json`) come from `render_base_refs()` (flat ortho renders of the base head); redo
   them only if the base head changes.
3. **Config:** `"portrait": {"landmarks": ".../refs/<name>_landmarks.json"}` (+ overrides of `PORTRAIT_DEFAULTS`), `deforms: []`,
   paint only for what the portrait doesn't cover: scalp hair, the back of the head, sideburns. Clear the base hero's own
   sideburn with a `skin` layer if the portrait has none (`tam.json`).
4. Then mockup → export → `Bake Head Variants` → `verify_head_variants.cs` / `verify_head_tones.cs`, as for any variant.

How it works (`make_head_variant.py`, portrait section):
- **Shape:** front landmarks are aligned on the eye corners + nasion (so the face's own proportions survive), then a 3D
  RBF warp (kernel r + affine, ~130 landmark anchors ray-cast on the base head) moves them; back of the skull and crown
  pinned, the neck held by the freeze weight. Ears and the back of the head are not anchored. Depth: midline landmarks follow the side profile
  (scaled crown → nose tip, calibrated on the base), re-centred on the median, capped at `max_depth_mm` (8), chin underside
  skipped. Whole warp capped at `max_mm` (25).
- **Texture:** front portrait where the face points forward (MediaPipe oval, raised to the hairline; tight edge at the
  jaw, wide at the forehead), side profile on the cheeks / jaw below the eyes (both sides unless `side_mirror` is off),
  painted texture elsewhere. Lighting
  gradients divided out (`delight`). Only **skin** texels move to the base skin colour (they get toned in game); hair,
  beard and eyes keep the portrait's colour. Skin weight (the mask) from the portrait: eyes 0, lips 0.7.

Gotchas:
- **Seam tolerance:** the neck ring is stored at z = 1.5850001, so a strict `<= 1.585` test left it out of the frozen seed
  and a large warp moved it ~0.3 mm (bake: "No compatible opposite vertex"). Portrait configs seed within `SEAM_TOL_M`
  (legacy configs keep the strict seed, so they regenerate unchanged); `run()` fails on any config if the ring moves.
- **Side mirroring:** the side projection ignores x, so the profile (the man's left) also lands on his right cheek. Set
  `"side_mirror": false` when a one-sided mark (scar, bruise) falls in the side region (cheeks / jaw, below the eyes).
- **Inputs are checked against their landmarks:** a portrait replaced or resized after `face_landmarks.py` ran is refused
  (size mismatch). The landmark tool refuses a profile with no nose point on its left edge (facing right: mirror it),
  a crown or nose touching the image edge, and zero or several faces. Always look at the `_debug_side` overlay: the nose
  tip window is a fraction of the image height, so a jutting beard or chest can win it on loose framing.
- **Generated profiles are not exact** (slight pitch, a brow level with the nose tip): never trust a single profile point.
  A nasion-based scale was 1.7x off on the Warden (broken nose). Crown → tip is robust.
- Side exposure: match skin to skin (lighter half of each projection). Matching on the overlap band uses the front's
  shaded grazing edge and darkens the side.
- Per-channel std matching tints dark hair teal; auto contrast overshoots. Use white balance + mean luminance only.
- Identity test: `_base_landmarks.json` as its own portrait → warp 0, texture within ~1.3/255 of the base.
- Blender lights wash out Fair tones; judge on the Unity sheet.
- **Base hero beard / sideburn behind the side projection:** the side region stops in front of the ear, so the base
  hero's own sideburn and jaw-angle beard survive there. A `grey` / `tint` beard or sideburn layer recolours them into a
  light band (white hair: a white stripe at the jaw angle). When the portrait shows skin there, clear them first with
  `skin` + `hair_only` layers (one over the sideburn, one centred on the jaw angle ≈ x 0.066, y −0.04, z 1.625), placed
  **before** the beard layer (`tentmate.json`). Light hair: keep `grey` `contrast` ≤ 0.5 or the base hair's dark detail
  shows as speckles.
- `run()` renders only the man's left; to see his right, exec the script with `("r34", -40), ("rside", -90)` added to
  the `_render` angle tuple in memory (rendering after `run()` returns gives magenta: the preview image is freed).

---

## NPC skin tones (spec-npc-skin-tones)

Skin colour is a shader parameter, not a texture: no per-tone textures or material assets.

- **Shader:** `Assets/_Game/Shaders/SG_SkinLit.shadergraph` (`Game/SG_SkinLit`, URP Lit target, Lit property names) +
  `_SkinMask` (R8, 1 = skin), `_SkinTone`, `_SkinToneStrength` (0 = untoned), `_SkinRefLuminance`. Masked texels become
  `tone × luminance / ref` (detail kept), so average skin lands exactly on the tone colour. Untoned it renders
  pixel-identical to URP Lit. Used by `M_TestBaseHero` (body + base head, player included), every `M_Head_*` (own mask, base ref luminance) and
  `M_CompanyGrunt`. Generated by a scratch script from the Shader Graph sample `DefaultMaterial` (Lit blocks + one inline
  Custom Function); edit it in the Shader Graph window.
- **Masks:** `Tools/Blender/make_skin_masks.py` + `configs/skin_masks.json` → `T_SkinMask_BaseHero.png`,
  `T_SkinMask_CompanyGrunt.png` (import: Single Channel Red, not sRGB). Body = everything outside the trouser band
  (hem 0.28–0.31 m, waist 1.00–1.09 m, where saturation decides: the waistline isn't level); head = all but eyes and
  clearly dark hair; outfit = vertices weighted to hand / finger bones. Copy each printed `ref_luminance` to its material's
  `_SkinRefLuminance`. **New outfit that shows skin:** add an `armor` entry, run, assign the mask + ref luminance — any number
  of tones then works on it.
- **Tones:** `SkinToneSO` (`Data/Characters/SkinTones/Tone_*`: Fair `#CFA58A`, Olive `#B08A66`, Weathered `#A8704E`, Deep
  `#5A3A28`) on `NPCEntity._skinTone`. Runtime: `NpcSkinTone` (on `NPC_base Variant`) → `SkinToneMaterials` (static cache,
  one material copy per source × tone, SRP Batcher-safe). Which NPC uses which
  tone: `NPCEntity.SkinTone` / `_skinTone` on each `NPC_*.asset` (null = untoned; every baked head is toneable).
- **Verify:** `Tools/Blender/verify_skin_tones.cs` → `Temp/SkinTones/` (untoned diff must be 0).

Gotchas:
- The head's inset neck stump reuses the body's skin UVs: shared texels follow the body rule. Subtracting both
  coverages from each other leaves an untoned collar band on the chest.
- Excluding hair too eagerly (soft threshold near skin luminance) leaves a light halo at the hairline on dark tones.
  Exclude only clearly dark hair (≥ 35–55 % darker than the forehead): dark hair stays dark when toned.
- Blender: set a generated image's colour space **before** writing its pixels; changing it afterwards resets the buffer
  (the PNG saves all black).
- Editor preview renders: set `ShaderUtil.allowAsyncCompilation = false`, or a not-yet-compiled Shader Graph variant
  renders nothing on its first use.
