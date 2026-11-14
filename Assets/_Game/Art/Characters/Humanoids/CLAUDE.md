# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids

> Humanoid character art: animations, controllers, overrides and AI-generated models (`models/`).
> Covers the character concept prompt template and the Meshy → Mixamo → Unity pipeline.
> Use `/characters:concept-prompt <outfit>` to get a ready-to-use prompt.
> Animator rules live in `Controllers/CLAUDE.md`; attack clip timings in `Animations/Combat/CLAUDE.md`.

---

## Character Concept Prompt (image → Meshy image-to-3D)

The concept image is the input for Meshy image-to-3D, so it is written for **geometry and rigging**,
not for beauty. Art direction comes from `Art/ART_DIRECTION.md` (realistic, dark medieval,
muted earthy palette, grounded and worn). Keep every line **verbatim** — only `{VIEW}`, `{OUTFIT}`
and the optional reference line change.

**Base hero (first test, 2026-10-09):** front / back views in `models/`
(`test hero front view.png`, `test hero back view.png`) → `models/test base hero/` (current player body).
Generated with Gemini 2.5 Flash Image, free tier.

### Prompt template

```
Full-body character reference sheet, {VIEW}, of a realistic adult male human for a dark
medieval fantasy RPG in the style of Gothic 1 and 2.
{REFERENCE}
Strict T-pose: standing straight, arms extended horizontally to the sides at shoulder height,
palms facing down, fingers straight and slightly apart, legs straight with feet shoulder-width
apart, head facing forward, neutral expression.
Average build, lean and wiry with realistic proportions, about 30 years old, a weathered commoner,
not a hero and not a bodybuilder.
Short cropped dark brown hair close to the scalp, short stubble beard, no long hair.
{OUTFIT}
No weapons, no shield, no cape, no accessories held in the hands.
Realistic texture detail, muted earthy colors, slightly dirty and sun-worn.
Flat, even, shadowless studio lighting from the front, no rim light, no dramatic shadows.
Centered, entire body visible from head to toes with margin around it, orthographic-like camera
at chest height with no perspective distortion.
Plain solid light gray background, no floor shadow, no text, no frame.
```

| Slot | Value |
|------|-------|
| `{VIEW}` | `front view` — or `back view, seen from behind` (second image for Meshy multi-image) |
| `{REFERENCE}` | Empty line for a new character. With a reference image attached (image-to-image): `Same man as the reference image: same face, hair, beard, skin tone and body proportions.` |
| `{OUTFIT}` | Base hero: `Shirtless. Wearing only plain knee-length undyed rough linen trousers that fit close to the legs, held by a simple cord at the waist. Barefoot. No armor, no belt pouches, no jewelry.` |

### Writing `{OUTFIT}`

Two or three sentences: **each garment's material + fit + one wear cue**, head to toe. Rules come
from what image-to-3D and auto-rigging can handle:

- **Close-fitting only.** Loose robes, skirts, tabards past mid-thigh and capes fuse the legs or
  arms into one blob and rig badly. Capes / cloaks are separate assets.
- **Keep the armpits, the gap between the legs and the hands clear** — no huge pauldrons, no bulky
  sleeves at the wrist.
- **No helmet or hood** when the face must stay the same (helmets are rigid socket items —
  `EquipmentVisuals._helmetSocket`).
- **End with a neck cue** (`a padded leather collar`, `a mail coif lowered around the neck`) — a
  collar hides the seam if the head is later swapped onto another body (see *Head / body split*).
- Concrete materials and wear only, never "magical / legendary / epic" (they trigger glow and
  ornament). Muted colors — no saturated dyes.

| Outfit | `{OUTFIT}` |
|--------|------------|
| Leather armor | `Wearing a fitted dark brown boiled-leather cuirass over a short-sleeved padded linen gambeson, close-fitting dark wool trousers and knee-high laced leather boots. Plain leather bracers on the forearms, a thick leather collar around the neck. Scuffed and stained with use.` |
| Light mail | `Wearing a short-sleeved iron mail shirt reaching mid-thigh over a padded gambeson, belted at the waist, fitted leather trousers and plain leather boots. A lowered mail coif around the neck. Dull, slightly rusted iron.` |

---

## Face Portrait Prompt (NPC portrait faces → `make_head_variant.py` `portrait`)

Two images per NPC, a front and a side profile of the same man. They feed MediaPipe landmarks (shape) and a texture
projection (face detail), so they are written for **measurement**, not mood. Keep every line verbatim; only
`{VIEW}`, `{FACE}` and the reference line change. Generate the front first, then the side **image-to-image from
the front** so it is the same man. Save as `Tools/Blender/configs/heads/refs/<name>_front.png` / `<name>_side.png`
(square, ≥ 1024 px).

```
Head-and-shoulders photographic portrait, {VIEW}, of a realistic adult man for a dark medieval fantasy RPG
in the style of Gothic 1 and 2.
{REFERENCE}
{FACE}
Neutral relaxed expression, mouth closed, eyes open looking straight ahead, head level, not tilted.
Forehead, eyebrows, ears and jawline fully visible, nothing covering the face, no hood, no hat, no jewelry.
Bare neck and shoulders, no clothing visible.
Realistic skin texture with pores, natural muted skin colour, no makeup.
Flat, even, shadowless studio lighting from the front, no rim light, no dramatic shadows, no specular shine.
Long-lens orthographic-like camera at eye level with no perspective distortion, head centered and filling
about two thirds of the image height, with margin above the hair.
Plain solid light gray background, no text, no frame.
```

| Slot | Value |
|------|-------|
| `{VIEW}` | `straight front view, face square to the camera` — or `exact side profile view, facing left, only one eye visible` |
| `{REFERENCE}` | Front: empty line. Side: `Same man as the reference image: same face, head shape, hair, beard, scars and skin.` |
| `{FACE}` | Age, build of the face (jaw, cheekbones, brow, nose, lips, eyes), then hair / beard, then marks. Sides are the **man's own** left / right. Concrete shapes only, no qualities ("handsome", "menacing"). Hair must match the head config's scalp paint (the projection stops at the hairline). |

---

## Pipeline: Meshy → Mixamo → Unity (learned on the base hero)

1. **Meshy** (image-to-3D, free tier for tests): front image (+ back view if multi-image). Download the
   **full zip** (FBX/OBJ + textures).
2. **Mixamo**: upload the **whole zip** — a bare FBX loses the textures, and a Meshy FBX can fail with
   "unable to map existing skeleton" (it has no skeleton; Mixamo misreads the Blender export). Fallback:
   zipped OBJ with transforms applied. Download *FBX for Unity, T-pose, with skin*.
3. **Unity import** (`ModelImporter`):
   - **Scale Factor 100** — Mixamo reads Meshy's meters as centimeters; the model otherwise imports ~2 cm tall.
   - Rig **Humanoid, Create From This Model**. If the scale is changed *after* a Humanoid import, rebuild
     the avatar (set Generic + `humanDescription = new HumanDescription()`, reimport, back to Humanoid) —
     the old skeleton is kept and animations launch the hips ~100 m up.
   - Normals **Calculate** (60°) — Mixamo exports none. Animation import off.
   - Unity does not extract the embedded texture: remap the material to a URP Lit `.mat` built from the
     Meshy PNGs (base color + normal map; metallic / roughness unused).
4. Mixamo's auto-rig gives **index-finger bones only** (no thumb) — fine for Humanoid, hands look stiff.

### Swapping the player body

The player body is the `Character` child of `Prefabs/Player/Player.prefab` (see `Prefabs/Player/CLAUDE.md`).
To swap: instantiate the new FBX as `Character`, move the hand-added sockets (`WeaponSocket` +
`UnarmedHitbox`, `UndrawnWeaponSocket`, `BackWeaponSocket`, `HelmetSocket`) to the same-named bones with
the same local offsets, set the root `Animator.avatar` and `EquipmentVisuals._bodyRenderer`, delete the
old `Character`. The dev tool `Game/Dev/Wire EquipmentVisuals on Player Prefab` still looks for
`Beta_Surface` — don't run it on a swapped body.

### NPC model prefab

`Prefabs/Characters/Humanoid_BaseHero.prefab` = the base-hero FBX set up like the player's `Character` (body / Head
SMRs, sockets, fist hitbox) + an 11-body ragdoll; nested as `Character` in `NPC_base Variant`
(`Prefabs/Entities/CLAUDE.md`). A body change on the player (mesh, material, socket offsets) is **not** propagated —
redo it on this prefab too. Built once via MCP `execute_code` (no builder script); the ragdoll was copied by bone
name from the old Mixamo dummy (proportions match the hero within ~3 %).

### Head / body split + reusable neck interface

The player keeps **Head** and **Body** (`base_hero`) SMRs on the original skeleton. Armor hides only Body;
Head includes the character's exposed neck skin. All parts use the original base-hero mesh space and bind poses.

- **Immutable input:** `models/test base hero/base hero canonical split.fbx` is the original 1.585 m split,
  preserved as a reproducible seed. Never overwrite it or the original Mixamo models.
- **Neck preparation:** `Tools/Blender/prepare_armor_neck.py` with `configs/leather_neck.json` generates the
  derived `base hero split.fbx` and `leather armor neck.fbx`. It retains source skin UVs, adds an inset, closed neck
  stump down to 1.48 m, and leaves the complete original body outside it. The body conceals the extra skin when
  unequipped. At the exposed 1.585 m skin join, positions, normals, UVs and weights are snapped to the canonical
  body interface. Changing geometry changes Blender's custom-normal basis: restore the saved vectors afterward.
- **Garment:** the sample rebuilds the damaged collar from its exterior boundary (identified by winding around
  the neck axis), with a rounded rim and inner lining. Top profile 1.56 m, authored clearance 6 mm. The collar
  has proper leather UVs; no outfit-owned skin bridge or collapsed UV (0,0) fan. This is a fitting pass on an
  already canonical outfit, not an automatic fitter for arbitrary Meshy models. Outputs are staged before copying.
- **Bake:** after Blender generation run `Tools/Characters/Bake Leather Neck Parts`, or
  `Game.Editor.Characters.SkinnedNeckBaker.BakeLeather()` through Unity MCP. It bakes all three transient meshes,
  validates the exposed skin join and canonical bind space, repairs collapsed imported garment UVs inside the
  neck region, regenerates tangents, aligns the exposed head/body normal-map frames, and validates UVs before saving meshes in place (GUIDs retained).
  Import new scripts and request compilation before calling them through MCP; a clear console alone does not
  prove a newly written script was compiled. FBX import can reintroduce a collapsed UV triangle despite Blender checks.
- **General baker:** `Tools/Characters/Skinned Mesh Baker` remains useful for other parts. Its size/bone checks
  alone do not certify a neck fit. `SkinnedNeckValidator` supplies separate exact-skin-join, name-remapped bind,
  neck UV and posed boundary-coverage checks. Coverage rays are a boundary check, not a whole-surface collision proof;
  inspect textured and neutral close-ups through idle, movement, attacks and dodge as well.

**NPC/shared-uniform contract:** different faces may share this outfit only after fitting/baking to this canonical
skeleton and neck interface. Preserve the neck geometry and its influences when changing faces; exposed neck skin
uses the head material so skin tone can vary independently of the uniform. Matching bone names on a separately
proportioned Mixamo rig is insufficient. For bare variants, also match the body skin material. Other exposed skin
on an outfit (e.g. hands) needs its own compatible skin treatment; this neck repair does not implement that or NPC
inventory/equipment migration. NPC faces built on this contract: *NPC head variants* below.

Collar regression guard: sample below the original exposed seam (current profile 1.56 m); reject a sampled radius above 0.10 m. Generated rim weights exclude Head influence and renormalize toward Neck; existing garment weights blend into that rim. Inspect the under-chin silhouette in addition to join diagnostics.

The sample damaged-collar trim radius is 0.16 m, covering the original folded rear rim. The inner lining descends to 1.485 m and tapers into the closed skin stump. Its raised lip must remain below the canonical exposed seam.

### NPC head variants (spec-npc-head-variants)

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
Shipped (13): Warden (portrait face), Cook, Blacksmith, TentMate, Tam (portrait), Brann, Storekeeper (portrait), Harl, DockGuard, GuardLeft /
GuardRight (`CompanyGuard_ArrivalLeft` / `Right`), WarehouseGuard, Bandit. Company brand on the five rank-and-file
guards (Harl, DockGuard, both arrival guards, WarehouseGuard), not on the Warden (officer).

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

### NPC portrait faces (spec-npc-portrait-faces)

Fixes the "same man, different hair" look: the face's eyes, nose, lips and skin come from a portrait instead of the
shared Meshy texture, and its shape follows the portrait's landmarks. Same neck contract and bake as the head variants
above. Pilot: Warden, Tam, Storekeeper.

1. **Portraits:** prompts from *Face Portrait Prompt* above (the filled `{FACE}` lines are kept in `refs/PROMPTS.md`);
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

### NPC skin tones (spec-npc-skin-tones)

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
  one material copy per source × tone, SRP Batcher-safe). Shipped: Tam Fair, Brann Olive, Storekeeper Deep, Harl Weathered,
  DockGuard Fair, CompanyGuard_ArrivalLeft Fair, CompanyGuard_ArrivalRight Olive, WarehouseGuard Deep, Bandit Weathered.
  Warden, Cook, Blacksmith and TentMate are untoned (their heads are toneable).
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

### Company grunt

`Tools/Blender/fit_company_grunt.py` derives `models/Company/Grunt armor/CompanyGrunt_Fitted.fbx`
from the unrigged Meshy source and canonical player rig. It transfers barycentric weights after
fitting, preserves the original files/emblem UVs and keeps the player head unchanged. A conforming
cloth neck sleeve uses the canonical skin's actual influences, including shoulder blends; a single
sampled ring alone does not cover the lower neck in raised-arm poses. Unity
`Game.Editor.Characters.CompanyGruntArmorBuilder.Build()` bakes and wires the separate armor item.
See `Tools/Blender/README.md` for reproduction, provisional balance and verification limits.

Company shading: the fitted FBX has tiny mesh coordinates beneath a x100 transform;
Unity's calculated import normals were mostly the same fallback direction, despite
the correct displayed size. The Company builder regenerates normals and tangents
at canonical scale before neck UV repair (which copies the smooth normals), then
rebuilds tangents after UV repair and validates finite usable frames and
area-weighted agreement with triangle faces before saving. The pickup copies these
corrected frames. Do not compensate with brighter textures or scene lighting, or
apply this repair to the head's deliberately matched seam normals.

Preview gotcha: after sampling an animation in an isolated edit-mode scene, set
`SkinnedMeshRenderer.forceMatrixRecalculationPerRender = true` on every renderer before rendering.
Without this, skin matrices can stay stale while bone transforms move, producing misleading
pose captures. `Tools/Blender/verify_company_grunt.cs` includes this setting.
