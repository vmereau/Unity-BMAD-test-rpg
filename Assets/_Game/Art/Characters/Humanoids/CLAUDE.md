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
skin texture. Skin tone is never changed, so heads still match the bare body and hands.

1. **Config:** `Tools/Blender/configs/heads/<name>.json`. `deforms` = soft ellipsoid scale / offset regions (jaw, brow,
   nose, cheeks, skull). `paint` = layers `skin` (bald / receding / shaved), `grey` / `tint` (hair recolour), `fill`
   (beard, scar), `darken` (eye bags), `iris` (eye colour); any layer takes `noise`, `detail`, `mirror_x`, `exclude`.
   Recolour the brows (`BROW` region) whenever the hair colour changes.
2. **Mockup:** `Tools/Blender/make_head_variant.py` `run(config)` renders front / ¾ / side / eye close-up to
   `Temp/HeadVariants/`. Get approval, then `run(config, export=True)` → `models/heads/<Name>/Head_<Name>.fbx` +
   `T_Head_<Name>_BaseColor.png`.
3. **Bake:** `Tools/Characters/Bake Head Variants` (`SkinnedHeadVariantBaker.BakeAll()`) bakes every `heads/` folder against
   the canonical target. It validates the exact join with `base_hero_body_baked` plus neck UV and bind space, then
   writes `Head_<Name>_baked.asset`, `M_Head_<Name>.mat` (copy of `M_TestBaseHero` with the variant texture) and
   `Data/Characters/Heads/HeadAppearance_<Name>.asset`.
4. **Assign:** `NPCEntity._head`. `NpcHeadAppearance` on `NPC_base Variant` swaps the Head SMR in `Awake`.
5. **Verify:** `Tools/Blender/verify_head_variants.cs` (posed join gap + neck / portrait sheets, bare and Company armor).

Landmarks (game metres, face toward −Y in Blender): seam 1.585, chin underside ≈ 1.595, mouth 1.635, nose tip 1.68,
iris centres x ±0.0353 / z 1.7197 (r ≈ 3 mm), brows z 1.735, crown 1.86. Shipped: Warden, Cook, Blacksmith, TentMate.

Gotchas:
- Frozen = within 12 mm of any vertex at or below the seam (distance-based; the chin overhangs the neck).
  Read smooth normals from a mesh **copy**: zeroing custom normals on the real mesh re-bases its fans and shifts the
  frozen normals by ~1°.
- The Meshy atlas is fragmented, so painting goes through a baked texel → rest-position map (`Temp/HeadVariants/_posmap.npz`).
  Coverage comes from a margin-0 **colour** bake (Cycles fills alpha over the whole image), dilated 3 px, minus every
  texel a body face covers (otherwise speckles appear on the chest).
- Base hair is about 0.4 luminance darker than skin, and its edges are blurred. Recolour hair as a skin/hair mix with a
  luminance ratio capped at 1, or the hairline gets a light halo or a cyan cast.
- Shape changes above ~9 mm stretch the texture's eye / nose detail. For bigger silhouettes, use new geometry
  (image-to-3D + fitting).

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
