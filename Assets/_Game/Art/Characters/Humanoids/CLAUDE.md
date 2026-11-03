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

### Head / body split + skinned armor (implemented 2026-11-03)

The player is a **Head** SMR + a **Body** SMR (`base_hero`, below the neck) on the original Mixamo skeleton.
An armor is a skinned mesh covering everything below the neck; equipping it hides the Body
(`EquipmentVisuals.RefreshBody`, `Scripts/Inventory/CLAUDE.md`). All three meshes live in the **original
base_hero mesh space with its bind poses**, so they bend identically and share one `bones[]` order.

- **Split** (done once): `Tools/Blender/split_head_body.py`, level cut at **1.585 m** (Neck bone 1.55, Head 1.63) →
  `models/test base hero/base hero split.fbx`. Both sides get the smooth normals of the uncut mesh (import with
  Normals **Import**, scale 100, Generic / no avatar, animation off) — the seam has no crease.
- **Bake** (every part): `Tools/Characters/Skinned Mesh Baker` (`Editor/Characters/SkinnedMeshBaker`). Target =
  the base_hero SMR of `rigged test hero with textures.fbx`; it re-expresses the source in the target's mesh space
  through the **bind poses** (never the FBX rest pose — Blender re-exports moved it ~9 mm), remaps weights by bone
  name, refuses weights on bones the target lacks, optional ±3% height check against a reference mesh (armor →
  `base_hero_body_baked.asset`). Optional output: a skeleton-less `SkinnedBodyPart` prefab (bone names only).
- **Armor fit** (per armor, by hand through the Blender MCP so far — seed script kept for the fit-pass spec):
  1. Import base split + armor in a separate scene; clear the armor's `mixamo.com` action and **reset both rigs to
     rest** (the imported Base_Rig pose is not its bind pose).
  2. Uniform scale to the base height, feet on the floor, then set each armor pose bone's armature-space matrix so
     its head lands on the base head and it stretches **along its length only** toward the base child (Stretch To
     constraints don't work: Mixamo bones are unconnected, tails ≠ child heads). Bake the posed mesh.
  3. Bisect at 1.585 m and delete only the **connected head component** above the plane (Meshy meshes can poke above
     it elsewhere — the leather armor's left shoulder did), re-weld the rest of the cut.
  4. Conform the 2 cm band under the cut radially onto the head-ring profile, bridge the outer collar loop to an exact
     copy of the head ring (positions, weights and normals copied from the head) → watertight join.
  5. Data Transfer vertex-group weights from `base_hero_body` (nearest face interpolated), parent to Base_Rig, export.
- Meshy + Mixamo armor imports come in ~2 cm tall with the texture path pointing at Mixamo's server: apply the
  pipeline import settings and remap `Materialmat` to a URP Lit built from the Meshy PNGs (`M_LeatherArmor`).
