---
title: 'Origin artwork: hero-in-his-past illustration on the origin pick'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '9ba63681ed1e98b3ef22cd146ca80b80f1e863b6'
context: ['{project-root}/Assets/_Game/Art/ART_DIRECTION.md', '{project-root}/docs/World/player-origins.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The origin pick (`spec-player-origins`) is text only. The player can't see who the hero was before the Company, so the belongings bait (the hammer, the tool roll) has no picture behind it.

**Approach:** Each origin gets a 3:4 painted illustration of the base hero at work in his trade before the war. Write a reusable illustration prompt template plus three filled prompts. The user generates the images (image-to-image from the base hero front view). Add an `artwork` sprite to `OriginSO` and a portrait Image beside the detail text that follows the selection.

## Boundaries & Constraints

**Always:**
- Prompts follow `ART_DIRECTION.md`: open with the Gothic 1 and 2 style anchor, describe materials, form and wear, restate the muted palette, use none of the banned words, and use no pure black or white.
- Same man as `Art/Characters/Humanoids/models/test hero front view.png`: lean, wiry, about 30, short cropped dark brown hair, stubble. The `{REFERENCE}` line asks for that image attached.
- Show the moment before the war, in his trade. Blacksmith: at a village forge, with the smith's hammer and leather apron visible. Joiner: at a workbench fitting a lock into an oak strongbox, with the chisel tool roll and joiner's plane visible. Wretch: nothing to show, so a burnt village with the hero in rags and the war present.
- 3:4 portrait frame, hero at medium-full length within his scene. Painterly realistic illustration, warm firelight against cold daylight, no text and no frame.
- The pick panel keeps the MainMenu window style and size (Window 1000×920).

**Ask First:** resizing the shared `Window` or restyling the pick panel beyond adding the image.

**Never:** generate the images (the user does that). Add artwork anywhere but the origin pick. Change the save format or the origin data other than the sprite.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Artwork set | Origin with a sprite selected | Image shows that sprite, aspect preserved | N/A |
| Switch origin | Select a different origin | Image swaps to the new origin's sprite | N/A |
| No artwork | Origin with a null sprite | Image hidden, details layout unchanged, no gap artifact | Silent (images ship later) |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Origins/OriginSO.cs` -- origin data; add the sprite and its `Init` parameter.
- `Scripts/UI/Screens/OriginPickUI.cs` -- `Select(origin)` sets name and details; add the image.
- `Scenes/MainMenu.unity` -- `Window/OriginPick`: Title, OriginButtons (y −90), OriginName (y −190), Details (860×432, stretched), BeginButton.
- `Assets/Tests/EditMode/PlayerOriginTests.cs` -- origin tests.
- `Art/UI/CLAUDE.md` -- sprite import recipe (§ Import Into Unity) to reuse.

## Tasks & Acceptance

**Execution:**
- [x] `Art/UI/Origins/CLAUDE.md` -- create: purpose, the **origin illustration prompt template** (verbatim lines; slots `{REFERENCE}`, `{SCENE}`), the three filled `{SCENE}` values, save paths `Art/UI/Origins/Origin_<Id>.png` (3:4, ≥ 896×1200: the shipped Gemini size, 2.7× the 324 px display), import settings (Sprite (2D and UI), Single, no mipmaps, Max Size 2048, high-quality compression), and assigning it to `Origin_<Id>.asset` -- prompt source of truth.
- [x] `Art/ART_DIRECTION.md` -- add an *Illustrations* entry under By Asset Type (mood art: firelight allowed, painterly) and a Template Index row -- discoverability.
- [x] `ScriptableObjects/Origins/OriginSO.cs` -- `[SerializeField] Sprite _artwork` + `artwork` getter + optional `Init` parameter -- data.
- [x] `Scripts/UI/Screens/OriginPickUI.cs` -- `[SerializeField] Image _artworkImage`. In `Select`, assign the sprite with `preserveAspect`, and set the Image GO active only when the sprite is non-null -- UI.
- [x] `Scenes/MainMenu.unity` -- add `OriginPick/Artwork` (Image, about 324×432, left-aligned in the Details band) and narrow `Details` to the remaining width with a 24 px gap. If the Blacksmith text overflows, enable TMP auto-size (min 18). Wire `_artworkImage` -- layout.
- [x] `Assets/Tests/EditMode/PlayerOriginTests.cs` -- `Init` with artwork round-trips the sprite; null by default -- data coverage.
- [x] Docs: root `CLAUDE.md` folder index row for `Art/UI/Origins/CLAUDE.md`; the `OriginPickUI` row in `Scripts/UI/Screens/CLAUDE.md` mentions the artwork -- discoverability.

**Acceptance Criteria:**
- Given `Art/UI/Origins/CLAUDE.md`, when a reader follows it, then they get three complete prompts (one per origin) ready to paste, each naming the reference image to attach.
- Given the MainMenu origin pick, when an origin with artwork is selected, then its picture shows left of the details, and the details text stays fully readable without clipping.

## Design Notes

The illustration is the first mood-art asset type: generation-sheet rules (flat light, gray background) don't apply. Lighting is warm firelight against cold daylight, per the World lighting. The template keeps shared lines verbatim so the three images read as one set. Only `{SCENE}` changes: setting, his clothing (materials and wear), what his hands do, and the belongings in view.

## Verification

**Commands:**
- `run_tests` EditMode -- all pass, including the new artwork test.
- `read_console` after compile -- no errors.

**Manual checks:**
- MainMenu → New Game: with a temporary sprite assigned to Blacksmith, the image shows and swaps on selection. Wretch with no sprite: the image is hidden. Capture via `UILayoutCapture` if useful.

## Suggested Review Order

**Pick screen display**

- Entry point: every selection drives the artwork, null origin included.
  [`OriginPickUI.cs:93`](../../Assets/_Game/Scripts/UI/Screens/OriginPickUI.cs#L93)

- Show for a sprite, hide the GameObject when there is none (no white box).
  [`OriginPickUI.cs:97`](../../Assets/_Game/Scripts/UI/Screens/OriginPickUI.cs#L97)

- Lost scene wiring warns instead of failing silently.
  [`OriginPickUI.cs:41`](../../Assets/_Game/Scripts/UI/Screens/OriginPickUI.cs#L41)

- Artwork Image left of Details, saved inactive; Details narrowed, auto-size 26→18.
  [`MainMenu.unity:533`](../../Assets/_Game/Scenes/MainMenu.unity#L533)

**Data**

- Optional sprite on the origin; the only origin data change.
  [`OriginSO.cs:38`](../../Assets/_Game/ScriptableObjects/Origins/OriginSO.cs#L38)

- Shipped images assigned to the three origins.
  [`Origin_Blacksmith.asset:29`](../../Assets/_Game/Data/Origins/Origin_Blacksmith.asset#L29)

**Prompts and art direction**

- Verbatim illustration template; only `{SCENE}` changes per origin.
  [`CLAUDE.md:20`](../../Assets/_Game/Art/UI/Origins/CLAUDE.md#L20)

- New Illustrations asset type: the one 2D type with scene lighting.
  [`ART_DIRECTION.md`](../../Assets/_Game/Art/ART_DIRECTION.md)

**Peripherals**

- Artwork round-trip and shown / hidden / re-shown on selection.
  [`PlayerOriginTests.cs:214`](../../Assets/Tests/EditMode/PlayerOriginTests.cs#L214)
  [`PlayerOriginTests.cs:223`](../../Assets/Tests/EditMode/PlayerOriginTests.cs#L223)
