# CLAUDE.md — Assets/_Game/Scripts/Audio

> Music playback and audio settings (`Game.Audio` namespace).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `MusicPlayer` | On the `AudioManager` GO in `Core.unity` (+ its `AudioSource`). Loops `_explorationTrack` (2D, priority 0) with an unscaled-time fade-in (per-frame step capped at 0.1 s). Volume = fade × `MusicVolumeSettings`. Missing clip → `GameLog.Warn` + disabled. |
| `MusicVolumeSettings` | Static owner of the music volume (0–1, default 0.7). `PlayerPrefs` key `settings.musicVolume`, a device setting, never in save files. `Set` clamps, stores and raises `static event Changed` (no disk flush — slider drags call it every frame); `Save()` flushes, called by `OptionsUI.OnScreenClose`. `Changed` is reset on `SubsystemRegistration`. Pure `Clamp01Volume` / `FormatPercent` are tested by `MusicVolumeSettingsTests`. Written by `OptionsUI`'s slider. |

---

## Rules

- Music lives in `Core.unity` (always loaded): it survives region loads, save loads and death without restarting.
  Never add `DontDestroyOnLoad` or a singleton for it.
- Music keeps playing while paused (`timeScale = 0`). Fades must use `Time.unscaledDeltaTime`.
- Subscribers to the static `MusicVolumeSettings.Changed` must unsubscribe in `OnDisable` (static events outlive
  scenes and, with domain reload off, play sessions).
- Music clips live in `Art/Music/`, named `Music_<PascalName>` (no spaces or apostrophes). Import them as Load Type
  **Streaming**, **Vorbis** quality ~70, Preload off, Load In Background on. Unity re-encodes the source (`.mp3` from
  Suno is fine).
- No `AudioMixer` yet. Add one (and route `MusicPlayer` through an exposed parameter) when SFX / master volume arrive.
