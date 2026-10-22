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

**Base hero (first test, 2026-10-09):** front / back views in `Art/UI/Items/tests/`
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

### Head / body split (keeping the same face)

Image-to-image with a reference keeps the face *similar*, but Meshy regenerates the whole mesh — the
3D head is never identical between two generations. To reuse the exact head (Gothic 1/2 do this:
separate head and body meshes), both meshes must be skinned to the **same skeleton**, cut at the neck,
with the seam hidden by the outfit's collar. Not implemented yet — needs either a Blender pass per
outfit or a Unity editor tool (split triangles by Head/Neck bone weights, rebind by bone name).
