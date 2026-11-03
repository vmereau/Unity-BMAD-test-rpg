# CLAUDE.md — Assets/_Game/Scripts/Audio

> Music, sound effects, the mixer and audio settings (`Game.Audio` namespace). Spec:
> `spec-sfx-system-combat-footsteps.md`. Clip naming / import / ElevenLabs prompts: `Art/Sounds/CLAUDE.md`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `MusicPlayer` | On the `AudioManager` GO in `Core.unity` (+ its `AudioSource`, Output = `MainMixer/Music`). Loops `_explorationTrack` (2D, priority 0) with an unscaled-time fade-in (per-frame step capped at 0.1 s). Source volume = fade only — the setting is applied on the mixer. Missing clip → `GameLog.Warn` + disabled. |
| `MusicVolumeSettings` / `SfxVolumeSettings` | Static owners of the music (default 0.7, `settings.musicVolume`) and SFX (default 0.8, `settings.sfxVolume`) volumes. `PlayerPrefs`, device settings, never in save files. `Set` clamps, stores, raises `static event Changed` (no disk flush); `OptionsUI.OnScreenClose` flushes. `Changed` reset on `SubsystemRegistration`. |
| `AudioMixerVolumes` | On `AudioManager`. Applies both settings to `MainMixer` exposed params `MusicVolume` / `SfxVolume` via pure `LinearToDecibels` (floor −80 dB) in `Start` and on `Changed`. |
| `SfxPlayer` | On `AudioManager`. Listens to `OnSfxRequested` (`GameEventSO_SfxRequest`); a pool of 24 child sources (`Sfx_NN`, Output = `MainMixer/SFX`, linear rolloff). Fully 3D cues farther than their `maxDistance` from the `AudioListener` are dropped (no voice, no cooldown). Free source, else the earliest-started is stolen. Per-cue (global, not per-emitter) cooldown, no-repeat pick and null-slot skip via pure `SoundCuePicker`. Empty cue → one `GameLog.Warn` per cue. |
| `SoundCueSO` (`ScriptableObjects/Audio/`) | A sound: clip variations, volume + jitter, pitch range, spatial blend, min/max distance, cooldown. Assets `Art/Sounds/SFX/Cue_<Name>.asset`. |
| `CombatAudioSO` (`ScriptableObjects/Audio/`) | `Data/Audio/CombatAudio.asset`: unarmed swing, hit flesh, block, perfect block, draw, sheathe, player hurt. Read by `PlayerCombat`, `EntityMeleeAttacker`, `PlayerHealth`. `ForHit(HitResult)` maps the outcome → cue (Dodged = none). |
| `FootstepEmitter` + pure `FootstepStride` | Player root. Distance-based steps from grounded horizontal travel (walk 1.4 m / run 1.9 m, run ≥ 4.5 m/s). `isGrounded` gets a 0.15 s grace (it drops out on slopes / step edges); teleport (> 2 m/frame) or longer airtime resets; < 0.5 m/s only pauses. An optional `IFootstepGate` on the same GO (`PlayerStateManager.SuppressFootsteps`: dead, dodging, attacking, staggered / knocked down) mutes and resets. |

## Where sounds are raised

| Sound | Raised by |
|-------|-----------|
| Swing | `PlayerCombat.OnHitboxEnable` (after its attacking guard) — equipped weapon read live from `EquipmentSystem` (not the `_currentWeaponSO` cache), `WeaponArchetypeSO.swingSound`, unarmed → `unarmedSwing`. AI swings: none yet. |
| Hit / block / perfect block | `PlayerCombat.ResolveHit` (player melee + arrows) and `EntityMeleeAttacker.HandleHit`, at the hit point |
| Draw / sheathe | `PlayerCombat.OnWeaponSwapEvent` only (not the interrupt nets / watchdog); armed, non-bow; `IsInCombat` = draw |
| Player hurt | `PlayerHealth.TakeDamage` while still alive after the hit (no grunt on the killing blow; 2D cue) |
| Footsteps | `FootstepEmitter` |

---

## Rules

- Gameplay **never owns an `AudioSource` for one-shots** — raise `OnSfxRequested` (`GameEventSO_SfxRequest.Play(cue, pos)`,
  null cue = silent). No singleton / static locator for `SfxPlayer`.
- Audio lives in `Core.unity` (always loaded). Never add `DontDestroyOnLoad` or a singleton for it.
- Music keeps playing while paused (`timeScale = 0`). Fades must use `Time.unscaledDeltaTime`.
- `AudioMixer.SetFloat` is ignored in `Awake` — apply mixer params in `Start` or later.
- Subscribers to the static `*VolumeSettings.Changed` events must unsubscribe in `OnDisable`.
- **No footstep animation events**: locomotion is a blend tree (blended clips double-fire events) and NPC animators sharing
  `Humanoid_Template` would log "AnimationEvent has no receiver".
- `MainMixer.mixer` (`Art/Sounds/`) was built with internal `AudioMixerController` APIs via reflection
  (`CreateMixerControllerAtPath`, `CreateNewGroup`, `AddChildToParent`, `AddExposedParameter` + rename in `exposedParameters`).
  A new group / exposed param: use the Audio Mixer window, or the same reflection.
- Music clips live in `Art/Music/`, named `Music_<PascalName>`. Import as **Streaming**, **Vorbis** ~70, Preload off,
  Load In Background on.
