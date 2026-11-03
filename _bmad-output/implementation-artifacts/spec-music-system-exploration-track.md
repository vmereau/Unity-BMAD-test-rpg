---
title: 'Music system: global exploration track with a music volume setting'
type: 'feature'
created: '2026-11-03'
status: 'done'
baseline_commit: 'b2ee362'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game has no music. `Core.unity` has an empty `AudioManager` stub, and the Options panel is a placeholder. A first track, `Horizon's Last Light.mp3` (generated with Suno), sits in `Assets/_Game/Art/Music/`.

**Approach:** Add a music player on the `AudioManager` GO that loops one global exploration track with a fade-in, and a persisted music volume (0–100 %) set from a slider in the Options panel. Volume changes apply live.

## Boundaries & Constraints

**Always:** One track, played from `Core.unity` so it keeps going across region loads, save loads and death. Music keeps playing while the game is paused (`timeScale = 0`); fades use unscaled time. The volume is stored in `PlayerPrefs` (a device setting, not save-game data) and applied before the first audible frame. `Game.Audio` namespace under `Scripts/Audio/`. A `GameLog` warning (not an exception) when the clip or AudioSource is missing.

**Ask First:** Adding an `AudioMixer`. Adding context switching (town/night/combat). Adding SFX or master volume sliders.

**Never:** No new singleton (project rule: only `WorldStateManager` / `SaveSystem`). No `DontDestroyOnLoad`. No music state in save files. No 3D/spatial music source.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First launch | No pref stored | Volume defaults to 0.7, track fades in from 0 over ~3 s and loops | N/A |
| Saved pref | Pref = 0.25 | Track plays at 0.25 from the first frame of the fade | N/A |
| Slider moved | Options open, slider → 0.4 | Music volume changes immediately; pref saved | N/A |
| Muted | Slider → 0 | Silence; track keeps advancing (unmuting resumes mid-track) | N/A |
| Bad pref | Stored value -3 or 7 | Clamped into [0, 1] | Clamp, no log |
| Missing clip | `_explorationTrack` null | No playback | `GameLog.Warn`, component stays harmless |
| Load game / death | Region reloads | Music continues without restarting | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scenes/Core.unity` -- `AudioManager` GO (empty stub, always loaded) gets the music player
- `Assets/_Game/Scripts/UI/Screens/OptionsUI.cs` -- placeholder `IScreenPanel`, Game Menu sub-panel opened by `GameMenuUI.ShowOptions`
- `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- `OptionsUI` GO (inactive, one child) under the Game Menu
- `Assets/_Game/Art/Music/Horizon's Last Light.mp3` -- the track (~4.8 MB, no `.meta` yet)
- `Assets/Tests/EditMode/` -- pure-logic tests (e.g. `DayNightMathTests`)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Art/Music/` -- rename the file to `Music_HorizonsLastLight.mp3` (no apostrophe or spaces). Import as Load Type *Streaming*, Compression *Vorbis* (quality ~70), Load In Background on -- avoids a 4.8 MB decompressed clip in memory and an awkward file path
- [x] `Assets/_Game/Scripts/Audio/MusicVolumeSettings.cs` -- static class: `Get()` (PlayerPrefs `"settings.musicVolume"`, default 0.7, clamped), `Set(float)` (clamp, save, raise `static event Action<float> Changed`), pure `Clamp01Volume` -- single owner of the setting, shared by the player and the UI
- [x] `Assets/_Game/Scripts/Audio/MusicPlayer.cs` -- MonoBehaviour (`RequireComponent(AudioSource)`): `[SerializeField] AudioClip _explorationTrack`, `_fadeInSeconds = 3`. Configures the source as 2D, loop, no play-on-awake. On Start it plays and fades in with unscaled time. It subscribes to `MusicVolumeSettings.Changed` in OnEnable and unsubscribes in OnDisable. The effective volume is fade factor × setting
- [x] `Assets/_Game/Scenes/Core.unity` -- add `AudioSource` + `MusicPlayer` to `AudioManager` and assign the clip
- [x] `Assets/_Game/Scripts/UI/Screens/OptionsUI.cs` -- `[SerializeField] Slider _musicSlider`, `TMP_Text _musicValueLabel`. On open, set the slider without notify to `Get()`. On change, call `Set` and update the label (`"70 %"`). Null-guard serialized refs. Replace the placeholder logs
- [x] `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- under `OptionsUI` add a "Music Volume" row (label, Slider 0–1, value label) styled like the existing Game Menu widgets; wire the refs
- [x] `Assets/Tests/EditMode/MusicVolumeSettingsTests.cs` -- clamp edge cases (-3, 0, 0.5, 1, 7, NaN → default) and label format
- [x] `Assets/_Game/Scripts/Audio/CLAUDE.md` (new) + `Scenes/CLAUDE.md` (`AudioManager` is now scripted) + `Scripts/UI/Screens/CLAUDE.md` (`OptionsUI` row) -- doc the new system

**Acceptance Criteria:**
- Given the game starts from `Core.unity`, when the player spawns, then the exploration track fades in and loops without a gap at the loop point other than what the file itself contains.
- Given the Game Menu is open (paused), when the player moves the music slider, then volume changes audibly at once and persists after restarting the game.
- Given a region scene is reloaded (save load or death restart), when it finishes, then the music has not restarted.

## Design Notes

A static settings class rather than a singleton: the setting is device data with no scene lifetime, and the UI and player are in different prefabs and scenes. `Changed` is a static event, so `MusicPlayer` must unsubscribe in OnDisable (domain-reload-off safety). Linear 0–1 volume is fine for now; a perceptual curve or mixer can come with SFX volume.

## Verification

**Commands:**
- Unity EditMode tests (`run_tests`, filter `MusicVolumeSettingsTests`) -- expected: all pass
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play from `Core.unity`: music fades in. Esc → Options → slider changes volume live. Restart play: the value is kept.

## Suggested Review Order

**Playback**

- Entry point: loops the track from always-loaded Core, so it never restarts on region loads
  [`MusicPlayer.cs:47`](../../Assets/_Game/Scripts/Audio/MusicPlayer.cs#L47)

- Fade uses unscaled time, step capped at 0.1 s so a long first frame can't skip it
  [`MusicPlayer.cs:62`](../../Assets/_Game/Scripts/Audio/MusicPlayer.cs#L62)

- 2D source, ignores listener pause, priority 0 (never virtualised by SFX)
  [`MusicPlayer.cs:31`](../../Assets/_Game/Scripts/Audio/MusicPlayer.cs#L31)

- Live volume = fade × setting
  [`MusicPlayer.cs:69`](../../Assets/_Game/Scripts/Audio/MusicPlayer.cs#L69)

**Volume setting**

- Static PlayerPrefs owner; `Set` doesn't flush (it's called every drag frame)
  [`MusicVolumeSettings.cs:35`](../../Assets/_Game/Scripts/Audio/MusicVolumeSettings.cs#L35)

- Static event reset for domain-reload-off play sessions
  [`MusicVolumeSettings.cs:21`](../../Assets/_Game/Scripts/Audio/MusicVolumeSettings.cs#L21)

- NaN/inf → default, otherwise clamp to [0, 1]
  [`MusicVolumeSettings.cs:48`](../../Assets/_Game/Scripts/Audio/MusicVolumeSettings.cs#L48)

**Options UI**

- Syncs the slider without notify on open; flushes prefs on close
  [`OptionsUI.cs:35`](../../Assets/_Game/Scripts/UI/Screens/OptionsUI.cs#L35)

- Listener in OnEnable/OnDisable (project subscription rule)
  [`OptionsUI.cs:25`](../../Assets/_Game/Scripts/UI/Screens/OptionsUI.cs#L25)

- New Music Volume row (label, built-in-sprite slider, value label), sibling of Title
  [`UICanvas.prefab:579`](../../Assets/_Game/Prefabs/UI/UICanvas.prefab#L579)

**Wiring & assets**

- `MusicPlayer` + `AudioSource` on the former `AudioManager` stub
  [`Core.unity:584`](../../Assets/_Game/Scenes/Core.unity#L584)

- Streaming / Vorbis import of the Suno track
  [`Music_HorizonsLastLight.mp3.meta:8`](../../Assets/_Game/Art/Music/Music_HorizonsLastLight.mp3.meta#L8)

**Tests & docs**

- Clamp, label format and PlayerPrefs round-trip with `Changed`
  [`MusicVolumeSettingsTests.cs:32`](../../Assets/Tests/EditMode/MusicVolumeSettingsTests.cs#L32)

- New audio folder doc (rules + music import recipe)
  [`CLAUDE.md`](../../Assets/_Game/Scripts/Audio/CLAUDE.md)
