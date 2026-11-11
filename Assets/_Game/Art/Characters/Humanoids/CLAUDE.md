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
inventory/equipment migration. No new production NPC head assets are included.

Collar regression guard: sample below the original exposed seam (current profile 1.56 m); reject a sampled radius above 0.10 m. Generated rim weights exclude Head influence and renormalize toward Neck; existing garment weights blend into that rim. Inspect the under-chin silhouette in addition to join diagnostics.

The sample damaged-collar trim radius is 0.16 m, covering the original folded rear rim. The inner lining descends to 1.485 m and tapers into the closed skin stump. Its raised lip must remain below the canonical exposed seam.

### Company grunt

`Tools/Blender/fit_company_grunt.py` derives `models/Company/Grunt armor/CompanyGrunt_Fitted.fbx`
from the unrigged Meshy source and canonical player rig. It transfers barycentric weights after
fitting, preserves the original files/emblem UVs and keeps the player head unchanged. A conforming
cloth neck sleeve uses the canonical skin's actual influences, including shoulder blends; a single
sampled ring alone does not cover the lower neck in raised-arm poses. Unity
`Game.Editor.Characters.CompanyGruntArmorBuilder.Build()` bakes and wires the separate armor item.
See `Tools/Blender/README.md` for reproduction, provisional balance and verification limits.

Preview gotcha: after sampling an animation in an isolated edit-mode scene, set
`SkinnedMeshRenderer.forceMatrixRecalculationPerRender = true` on every renderer before rendering.
Without this, skin matrices can stay stale while bone transforms move, producing misleading
pose captures. `Tools/Blender/verify_company_grunt.cs` includes this setting.
