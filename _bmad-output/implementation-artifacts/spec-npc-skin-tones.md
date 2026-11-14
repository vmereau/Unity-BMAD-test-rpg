---
title: 'NPC skin tones (Shader Graph skin mask + named tones)'
type: 'feature'
created: '2026-11-14'
baseline_commit: 'aa8f613d21d03bc8d77be80c8ec0751ab34584cb'
status: 'done'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/Entities/CLAUDE.md'
  - '{project-root}/Tools/Blender/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every NPC has the base hero's skin. A tone can't be changed per NPC because the skin lives in three atlases: the body, the head, and each armor that shows hands (Company grunt). Baking a texture per tone per atlas would multiply assets with every new tone or armor.

**Approach:** One Shader Graph (`SG_SkinLit`, URP Lit target) recolours masked skin texels toward a tone colour while keeping their luminance detail. One skin mask per atlas (body + base head, Company grunt hands). A named `SkinToneSO` on `NPCEntity` is applied at runtime through cached material copies: one per source material and tone, no asset files.

Tones: `Tone_Fair` ≈ `#D9B8A0`, `Tone_Olive` ≈ `#B08A66`, `Tone_Weathered` ≈ `#A8704E`, `Tone_Deep` ≈ `#6B4630`. Empty = base hero (unchanged).
Batch (default face): Tam → Fair, Brann → Olive, Storekeeper → Deep, Harl → Weathered (armored), DockGuard → Fair (armored).

## Boundaries & Constraints

**Always:** `SG_SkinLit` keeps the URP Lit property names (`_BaseMap`, `_BaseColor`, `_BumpMap`, `_Smoothness`, `_Metallic`). That way swapping the shader keeps every material's values, and `SkinnedHeadVariantBaker` / `CompanyGruntArmorBuilder` keep working. With no tone (`_SkinToneStrength` = 0), a material looks like it does under URP Lit, so the player and untoned NPCs don't change. Masks are bounded by geometry (positions and bone weights); colour (saturation / darkness) only decides inside narrow geometric bands (waist / hem, hair zones): the undyed linen trousers are about the same colour as light skin. Head, body and armor hands use the same tone transform, so no seam shows at the neck or wrist. Every renderer stays in the SRP Batcher (no `MaterialPropertyBlock`). Mask generation is a committed, reproducible Blender script.

**Ask First:** HALT after Unity close-up renders (face, hands, neck seam, bare and armored) of the four tones, and wait for approval or tone tweaks before assigning them to NPCs. Also ask before falling back to a hand-written HLSL copy of URP Lit if the Shader Graph can't be authored or doesn't match.

**Never:**
- New faces, hair or beard colours: the second batch is deferred.
- Masks for the four existing head variants: they keep the default tone.
- Per-tone textures or material assets.
- Changes to `Player.prefab` or the player's appearance.
- A character-creator UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Toned bare NPC | `NPCEntity._skinTone` set, no armor | Head + body skin in the tone; hair, eyes, trousers unchanged | N/A |
| Toned armored NPC | Tone + `Armor_CompanyGrunt` | Hands and exposed neck in the tone; armor cloth unchanged | N/A |
| Re-equip | Armor removed / re-added at runtime | The new armor visual gets the toned material | N/A |
| No tone | `_skinTone` empty or entity not an `NPCEntity` | Materials untouched | No log |
| Non-skin material | Renderer whose shader isn't `SG_SkinLit` (weapon, helmet) | Untouched | No log |
| Shared cache | Two NPCs, same tone + material | Same material instance | N/A |
| Variant head on toned NPC | Head variant without a mask (`_SkinMask` empty) | Head stays in the default tone; body toned | `GameLog.Warn` once naming the NPC and head |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/models/test base hero/M_TestBaseHero.mat` -- body + base head material (URP Lit, `_Smoothness` 0.2), shared with the player
- `Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/M_CompanyGrunt.mat` -- armor material (hands are in its atlas)
- `Assets/_Game/Art/Characters/Humanoids/models/heads/*/M_Head_*.mat` -- variant heads, copies of `M_TestBaseHero`
- `Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs:214` -- copies the base material's shader, so variants follow the swap
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs:110` -- `Refresh` → `TryAttachArmor` instantiates the armor visual; NPCs leave the SO channel unwired
- `Assets/_Game/Scripts/AI/NpcHeadAppearance.cs` -- head swap in `Awake`; pattern for reading `NPCEntity`
- `Tools/Blender/make_head_variant.py` -- texel → rest-position map (`_posmap.npz`) and coverage bake to reuse for masks
- `Assets/_Game/Data/NPCs/{BeatenWorker,StashOwner,Storekeeper,RacketGuard,DockGuard}/NPC_*.asset` -- assign tones

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/make_skin_masks.py` + `configs/skin_masks.json` -- body/head atlas mask: body skin minus the trouser band (waist cord to hem), base head minus scalp hair, brows and eyes; Company mask from hand/finger bone weights; soft 2–3 px edges. Writes `T_SkinMask_BaseHero.png` and `T_SkinMask_CompanyGrunt.png` (R8) next to their atlases, plus the mean masked-skin luminance -- reproducible masks
- [x] `Assets/_Game/Shaders/SG_SkinLit.shadergraph` -- URP Lit target, Lit property names, plus `_SkinMask`, `_SkinTone` (colour), `_SkinToneStrength` (0), `_SkinRefLuminance` -- skin recolour
- [x] Materials -- switch `M_TestBaseHero`, `M_Head_*` ×4 and `M_CompanyGrunt` to `SG_SkinLit`; set the mask + reference luminance on base and Company (head variants: no mask) -- opt-in skin
- [x] `Assets/_Game/ScriptableObjects/Characters/SkinToneSO.cs` (own file) -- `Color Tone`, `float Strength` (default 1); `NPCEntity._skinTone` + getter; `Data/Characters/SkinTones/Tone_*.asset` ×4 -- named palette
- [x] `Assets/_Game/Scripts/AI/SkinToneMaterials.cs` -- static cache `(Material, SkinToneSO) → Material` (copy with tone set), cleared on `SubsystemRegistration` -- shared copies
- [x] `EquipmentVisuals.cs` -- read-only `ArmorVisual` (the current armor GO) -- armor hook (polled: project rule forbids a cross-system `event Action`)
- [x] `Assets/_Game/Scripts/AI/NpcSkinTone.cs` -- root component: in `Start` (after the head swap) and when `ArmorVisual` changes (`LateUpdate` poll), replace each `SG_SkinLit` material under the model with its cached toned copy. Wire on `NPC_base Variant` -- runtime tone
- [x] **CHECKPOINT** -- close-ups of the four tones (face, hands, neck seam; bare + Company armor) vs the default; HALT for approval
- [x] The five `NPC_*.asset` -- assign the approved tones -- batch
- [x] `Assets/Tests/EditMode/NpcSkinToneTests.cs` -- matrix rows, cache sharing, untoned material untouched -- regression guard
- [x] `Art/Characters/Humanoids/CLAUDE.md`, `Prefabs/Entities/CLAUDE.md`, `Scripts/AI/CLAUDE.md`, `Scripts/Inventory/CLAUDE.md`, `Tools/Blender/README.md` -- recipe (mask → shader → tone asset), adding an armor's skin mask, limits -- docs

**Acceptance Criteria:**
- Given the swapped shader, when the player and untoned NPCs are viewed in StartingTown, then they look unchanged from before (side-by-side capture, no visible shift).
- Given the five toned NPCs in play mode, when viewed idle / walking / attacking, then skin matches across face, neck seam and hands, hair, eyes, trousers and armor cloth are not tinted, and the console has no new errors.
- Given the Frame Debugger, when toned NPCs render, then their draws stay in SRP Batcher batches.
- Given the EditMode suite, when run, then `NpcSkinToneTests` and all existing tests pass.

## Implementation Evidence

- Shader: `SG_SkinLit` generated from the Shader Graph sample `DefaultMaterial` (Lit blocks, one inline Custom Function), 10 passes, no shader messages, SRP Batcher compatibility code 0 (same as URP Lit).
- Untoned: `verify_skin_tones.cs` renders face / body / armored hand with `SG_SkinLit` vs URP Lit copies: mean and max diff 0/255. The player and untoned NPCs are unchanged.
- Masks: `make_skin_masks.py` twice → identical PNG hashes. Ref luminance (linear): base 0.17907, Company 0.28005. Two fixes during the checkpoint: texels shared by the head's neck stump and the body follow the body rule (an untoned collar band otherwise), and only clearly dark hair is excluded (a hairline halo on Deep otherwise).
- Checkpoint: tone sheet `Temp/SkinTones/tones.png`. User asked to warm Fair (`#D9B8A0` → `#CFA58A`) and darken Deep (`#6B4630` → `#5A3A28`), then approved.
- Play mode StartingTown: the 5 NPCs run on their toned copies (bare: body + head; armored: head + armor), the 8 others keep their materials, cache = 6 copies; captures in `Temp/SkinTones/ingame.png`. DockGuard armor unequip → re-equip: rebuilt visual re-toned on the next `LateUpdate`. Console: no new errors or warnings (7 pre-existing unrelated entries).
- EditMode: 1410/1410; `NpcSkinToneTests` 10/10.
- Deviations from the plan: `SkinToneMaterials` lives in `Scripts/AI` (next to the other NPC appearance code); the armor hook is a polled `ArmorVisual` getter, not an event (project rule).
- Not verified: AC2 "walking / attacking" for tones was checked in the idle pose and in play mode at rest only; toning is per material, so poses can't change it.

## Review Triage

Three reviewers (Blind hunter, Edge case hunter, Acceptance auditor).
- **Intent (resolved with the user):** the colour-gated mask bands. See the Spec Change Log.
- **Patched:**
  - `SG_SkinLit` ignored `_BumpScale`: Company armor (0.65) rendered at full normal strength. Added `_BumpScale` (`UnpackNormalScale`). The verification reference is now a full copy of each material switched to URP Lit (it copied only five properties, so it couldn't catch this), plus an armor torso shot.
  - Toned albedo clamped to 1 (Fair highlights could exceed it).
  - `SkinnedHeadVariantBaker` clears `_SkinMask` / strength on `M_Head_*`: re-baking copied the base hero's mask onto variant heads. Test: no head material has a mask.
  - `NpcSkinTone.Apply` re-tones an already toned NPC and `Apply(null)` restores the sources (`SkinToneMaterials.SourceOf`); `Apply` with a tone enables armor polling; a missing `_model` warns once, not every frame. Tests added.
  - `verify_skin_tones.cs` no longer calls `SkinToneMaterials.Clear()` (would strip live NPCs in play mode).
- **Post-fix evidence:**
  - Untoned vs full-copy URP Lit: face / body / armored hand 0/255, armor torso max 2/255.
  - StartingTown play mode: Bandit, WarehouseGuard (armored), Cook (variant head) and the Player each have mean diff 0.000, max ≤ 1/255. Toned 5/5, no `MaterialPropertyBlock` on any NPC renderer, cache 6, console unchanged.
  - EditMode 1412/1412.
- **Deferred:** the armor builder recreating the material as URP Lit, and the mask script's first-mesh-only outfit import (`deferred-work.md`).
- **Not verified as written:** the Frame Debugger look at AC3. Evidence instead: `SG_SkinLit` SRP Batcher compatibility code 0 and no property blocks on toned renderers.
- **Rejected:**
  - Cache refresh on Play-mode SO edits.
  - Edit-mode `DontSave` swaps on persistent objects (only spawned NPCs call `Apply`).
  - Per-NPC rather than global warning (matches the I/O row).
  - Silent fallback when the shader fails to compile.
  - Blur at atlas borders.
  - Non-square atlases.
  - Orphaned Blender datablocks.
  - Material keyword / tag YAML changes.
- **Noted:** setting `_skinTone` re-serialized `NPC_BeatenWorker` / `NPC_StashOwner` / `NPC_Storekeeper`, which drops the stale `_weapon` key and writes default `_startingEquipment` / hearing radii. No value changes.

## Design Notes

Recolour in the graph: `lum = dot(albedo, (0.299, 0.587, 0.114))`; `toned = tone * lum / _SkinRefLuminance`; `out = lerp(albedo, toned, mask * strength)`. Pores, stubble and painted shadow keep their relative darkness. If a tone reads flat, a small chroma carry-over from the source (ruddy cheeks) is the first tweak. Toning is a plain copy of the material with `_SkinTone` / `_SkinToneStrength` set, so the copy stays compatible with the SRP Batcher. The cache is bounded (tones × skin materials) and needs no destroy. Variant heads have UVs identical to the base atlas, but their paint differs, so they need their own masks (next batch).

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- `make_skin_masks.py` run twice -- expected: identical PNGs

**Manual checks:**
- Before/after captures of the player and an untoned NPC; tone close-ups at the checkpoint; Frame Debugger SRP Batcher check.

## Suggested Review Order

**Runtime: how an NPC gets its tone**

- Entry point: tone read from the NPC asset at spawn; untoned NPCs disable themselves.
  [`NpcSkinTone.cs:35`](../../Assets/_Game/Scripts/AI/NpcSkinTone.cs#L35)

- Swap every masked skin slot to the tone's copy; re-tone and null-restore through the source.
  [`NpcSkinTone.cs:59`](../../Assets/_Game/Scripts/AI/NpcSkinTone.cs#L59)

- Armor rebuilds are polled, not evented (no cross-system `event Action`).
  [`NpcSkinTone.cs:48`](../../Assets/_Game/Scripts/AI/NpcSkinTone.cs#L48)

- One plain material copy per source × tone: shared and SRP Batcher-safe.
  [`SkinToneMaterials.cs:41`](../../Assets/_Game/Scripts/AI/SkinToneMaterials.cs#L41)

- Reverse lookup that makes re-toning possible.
  [`SkinToneMaterials.cs:32`](../../Assets/_Game/Scripts/AI/SkinToneMaterials.cs#L32)

- Read-only hook the poll watches.
  [`EquipmentVisuals.cs:41`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L41)

**Shader: the recolour itself**

- Luminance remap toward the tone, clamped; `_BumpScale` kept for Lit parity.
  [`SG_SkinLit.shadergraph:2164`](../../Assets/_Game/Shaders/SG_SkinLit.shadergraph#L2164)

**Data**

- Named tone: colour + strength.
  [`SkinToneSO.cs:14`](../../Assets/_Game/ScriptableObjects/Characters/SkinToneSO.cs#L14)

- NPC field; empty = base skin.
  [`NPCEntity.cs:33`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L33)

- Variant heads stay unmasked on re-bake (the base mask doesn't fit their paint).
  [`SkinnedHeadVariantBaker.cs:221`](../../Assets/_Game/Editor/Characters/SkinnedHeadVariantBaker.cs#L221)

**Masks (Blender)**

- Body: trousers by height, saturation only inside the hem / waist bands.
  [`make_skin_masks.py:109`](../../Tools/Blender/make_skin_masks.py#L109)

- Head: eyes and clearly dark hair excluded, inside geometric zones.
  [`make_skin_masks.py:124`](../../Tools/Blender/make_skin_masks.py#L124)

- Neck-stump texels shared with the body follow the body rule (collar-band fix).
  [`make_skin_masks.py:185`](../../Tools/Blender/make_skin_masks.py#L185)

- Outfit hands from bone weights: the recipe for any future armor.
  [`make_skin_masks.py:195`](../../Tools/Blender/make_skin_masks.py#L195)

- Approved thresholds.
  [`skin_masks.json:12`](../../Tools/Blender/configs/skin_masks.json#L12)

**Verification, tests, docs**

- Untoned parity against a full URP Lit copy, then the tone sheet.
  [`verify_skin_tones.cs:74`](../../Tools/Blender/verify_skin_tones.cs#L74)

- Re-tone / restore, variant-head warning, shipped assignments.
  [`NpcSkinToneTests.cs:126`](../../Assets/Tests/EditMode/NpcSkinToneTests.cs#L126)

- Recipe, palette and gotchas for the next outfits and tones.
  [`CLAUDE.md:178`](../../Assets/_Game/Art/Characters/Humanoids/CLAUDE.md#L178)
