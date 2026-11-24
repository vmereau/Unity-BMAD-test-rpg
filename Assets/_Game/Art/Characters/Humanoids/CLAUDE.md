# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids

> Loaded when Claude accesses files in this folder. Humanoid character art: animations, controllers, overrides and
> AI-generated models (`models/`). Holds the character concept and face portrait prompt templates.
> Use `/characters:concept-prompt <outfit>` to get a ready-to-use prompt.
> Animator rules live in `Controllers/CLAUDE.md`; attack clip timings in `Animations/Combat/CLAUDE.md`.

---

## Topic Files

| File | Read when |
|------|-----------|
| `PIPELINE.md` | Importing a Meshy model (Meshy → Mixamo → Unity), swapping the player body, editing `Humanoid_BaseHero.prefab`, the head / body split + neck interface, the Company grunt armor |
| `HEADS_SKIN.md` | NPC head variants, portrait faces (`/characters:portrait-face`), Meshy 3D heads (`/characters:meshy-head`), skin masks and skin tones (`SG_SkinLit`) |

---

## Character Concept Prompt (image → Meshy image-to-3D)

The concept image is the input for Meshy image-to-3D, so it is written for **geometry and rigging**,
not for beauty. Art direction comes from `Art/ART_DIRECTION.md` (realistic, dark medieval,
muted earthy palette, grounded and worn). Keep every line **verbatim** — only `{VIEW}`, `{OUTFIT}`
and the optional reference line change.

**Base hero:** front / back views in `models/`
(`test hero front view.png`, `test hero back view.png`) → `models/test base hero/` (current player body).
Generated with Gemini 2.5 Flash Image.

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
  collar hides the seam if the head is later swapped onto another body (see `PIPELINE.md` → *Head / body split*).
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
