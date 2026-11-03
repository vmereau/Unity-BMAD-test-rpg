---
title: 'SFX system: sound cues, pooled player, mixer with SFX volume, combat and footstep sounds'
type: 'feature'
created: '2026-11-03'
status: 'done'
baseline_commit: 'e95bd10'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game has music but no sound effects. Combat feels weightless, and the GDD asks for "every block has a crunch, every perfect block has a distinct sharp ring."

**Approach:** Add data-driven `SoundCueSO`s (random clip variations, pitch and volume jitter) played by a pooled `SfxPlayer` on Core's `AudioManager`. Gameplay requests sounds through a `GameEventSO` channel. Add an `AudioMixer` (Music / SFX) with an SFX volume slider. The first sounds are player footsteps and melee combat: swing, flesh hit, block, perfect block, draw, sheathe and player hurt. Valentin generates the clips on ElevenLabs from prompts we provide.

## Boundaries & Constraints

**Always:** Gameplay raises `OnSfxRequested` (cue + position); only `SfxPlayer` owns `AudioSource`s for one-shots. Cue assets work with zero clips (silent, warn once per cue). The SFX volume lives in `PlayerPrefs` (`settings.sfxVolume`, default 0.8), like music. Mixer parameters are applied in `Start` and on change, never in `Awake` (Unity ignores `SetFloat` there).

**Ask First:** Changing hit, block or swing *logic*. NPC footsteps. Swing sounds for AI attackers.

**Never:** No new singleton or static service locator. No `AudioSource` added to entity or weapon prefabs. No footstep animation events (blend trees double-fire them). No sound settings in save files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Variation pick | Cue with 4 clips | Random clip, never the same as the last pick | 1 clip → always it |
| Empty cue | 0 clips | Nothing plays | `GameLog.Warn` once per cue |
| Pool exhausted | All sources busy | Steals the source that started earliest | N/A |
| Cooldown | Same cue twice within `cooldown` | Second request dropped | N/A |
| Player walks / runs | Grounded, horizontal speed > 0.5 m/s | A step every `walkStride` / `runStride` m (run cue above `runThreshold`) | N/A |
| Teleport / airborne | Δ > 2 m in a frame, or not grounded | No step; the accumulator resets | N/A |
| SFX slider | 0 → 1 | dB = 20·log10(v), floored at −80 | NaN → default |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs` -- template for a struct-payload channel (namespace `Game.Core`)
- `Scripts/Audio/MusicPlayer.cs`, `MusicVolumeSettings.cs` -- current volume path (source volume × setting)
- `Scripts/Combat/PlayerCombat.cs` -- `ResolveHit` (:556, player hits incl. arrows), `OnHitboxEnable` (:833), `OnWeaponSwapEvent` (:888), `_currentWeaponSO`
- `Scripts/AI/EntityMeleeAttacker.cs:218` -- `HandleHit` switch on `HitResult` (AI hits on the player and NPCs)
- `Scripts/Player/PlayerHealth.cs:79` -- `TakeDamage`
- `ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` + `Data/Items/Weapons/Archetypes/*.asset` -- per-family data
- `Scripts/UI/Screens/OptionsUI.cs` + `Prefabs/UI/UICanvas.prefab` -- the Music Volume row to mirror
- `Scenes/Core.unity` `AudioManager`; `Prefabs/Player/Player.prefab` (CharacterController root)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Audio/SoundCueSO.cs` -- `clips[]`, `volume`, `volumeJitter`, `pitchMin/Max`, `spatialBlend` (default 1), `minDistance` 1 / `maxDistance` 25, `cooldown` -- a sound's data
- [x] `ScriptableObjects/Audio/CombatAudioSO.cs` -- cues: `unarmedSwing`, `hitFlesh`, `block`, `perfectBlock`, `draw`, `sheathe`, `playerHurt` -- one shared asset, `Data/Audio/CombatAudio.asset`
- [x] `ScriptableObjects/Events/GameEventSO_SfxRequest.cs` + `Data/Events/OnSfxRequested.asset` -- `SfxRequest { SoundCueSO cue; Vector3 position; }`
- [x] `Scripts/Audio/SoundCuePicker.cs` -- pure: no-repeat index pick (injected random), jittered volume/pitch, cooldown check
- [x] `Scripts/Audio/SfxPlayer.cs` -- on `AudioManager`: about 24 child sources routed to the SFX group (`Linear` rolloff); listens to `OnSfxRequested` in `OnEnable`/`OnDisable`; picks via `SoundCuePicker`; steals the oldest source
- [x] `Scripts/Audio/SfxVolumeSettings.cs` -- mirror of `MusicVolumeSettings` (key `settings.sfxVolume`, default 0.8)
- [x] `Scripts/Audio/AudioMixerVolumes.cs` -- on `AudioManager`: pure `LinearToDecibels`; applies `MusicVolume` / `SfxVolume` exposed parameters in `Start` and on both `Changed` events
- [x] `Audio/MainMixer.mixer` (under `Art/Sounds/`) -- groups Master → Music, SFX; expose `MusicVolume`, `SfxVolume`. Create with the menu item, then reflection via `execute_code`; if that fails, HALT and give Valentin the clicks
- [x] `Scripts/Audio/MusicPlayer.cs` -- output to the Music group; source volume = fade only (the mixer applies the setting)
- [x] `ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` + archetype assets -- `SoundCueSO swingSound` (sword/axe → `SFX_SwingLight`, two-handed → `SFX_SwingHeavy`, bow none)
- [x] `Scripts/Combat/PlayerCombat.cs` -- `[SerializeField] CombatAudioSO`, channel. Swing in `OnHitboxEnable` after its guard (archetype cue, or `unarmedSwing`). `ResolveHit`: NotBlocked → `hitFlesh`, Blocked → `block`, PerfectBlock → `perfectBlock` at `hitPoint`. Draw/sheathe in `OnWeaponSwapEvent` while switching with a weapon equipped
- [x] `Scripts/AI/EntityMeleeAttacker.cs` -- same hit, block and perfect-block cues at `hitPoint` (no swing)
- [x] `Scripts/Player/PlayerHealth.cs` -- `playerHurt` cue on damage taken while alive
- [x] `Scripts/Audio/FootstepStride.cs` (pure) + `FootstepEmitter.cs` -- on the Player root: horizontal distance from `CharacterController` while grounded; walk/run cues and strides serialized
- [x] `Scripts/UI/Screens/OptionsUI.cs` + `UICanvas.prefab` -- "Effects Volume" row under Music
- [x] `Art/Sounds/SFX/` -- 11 empty cue assets (`Cue_<Name>`) wired into CombatAudio, archetypes and the emitter
- [x] `Assets/Tests/EditMode/` -- `SoundCuePickerTests`, `FootstepStrideTests`, `LinearToDecibels` + `SfxVolumeSettings` clamp tests
- [x] `Scripts/Audio/CLAUDE.md`, new `Art/Sounds/CLAUDE.md` (naming `SFX_<Name>_<NN>`, import recipe, ElevenLabs prompt table) -- docs

**Acceptance Criteria:**
- Given the player is armed, when they attack, then a whoosh plays at the hit window, and a flesh impact plays when the hit lands.
- Given an NPC attacks a blocking player, when the hit is blocked or perfect-blocked, then the block crunch or the ring plays at the contact point.
- Given the Effects slider is at 0, when any SFX fires, then it is silent, while music is unaffected.
- Given no clips are assigned yet, when the game runs, then there are no errors, only one warning per empty cue.

## Design Notes

Footsteps are distance-based, not animation events: locomotion is a blend tree, so events from blended clips double-fire, and NPC animators sharing `Humanoid_Template` would log "no receiver". Strides start at walk 1.4 m and run 1.9 m, tuned in play.

Import recipe for short SFX: Decompress On Load, ADPCM, Force To Mono for 3D cues, Preload on.

Prompts (ElevenLabs Sound Effects, prompt influence ~0.6; generate 3–4 takes each):

| File | Dur | Prompt |
|------|-----|--------|
| `SFX_FootstepDirtWalk` | 0.5 s | single footstep, leather boot on packed dirt, soft crunch, dry, close mic, no reverb |
| `SFX_FootstepDirtRun` | 0.5 s | single heavy running footstep, leather boot on dirt and gravel, quick crunchy impact, close mic, dry |
| `SFX_SwingLight` | 0.6 s | fast sword swing whoosh, steel blade cutting air, short, sharp, dry |
| `SFX_SwingHeavy` | 0.9 s | heavy two-handed greatsword swing, deep slow whoosh, weighty air displacement, dry |
| `SFX_UnarmedSwing` | 0.4 s | quick punch swing, cloth rustle and air whoosh, short |
| `SFX_HitFlesh` | 0.7 s | sword slash hitting flesh and leather armor, wet meaty impact, brutal, medieval combat, dry |
| `SFX_Block` | 0.8 s | sword blocked by sword, heavy metal clang with crunch, steel on steel impact, short ring tail |
| `SFX_PerfectBlock` | 1.2 s | perfect sword parry, sharp bright high-pitched metallic ring, clean steel ping with shimmering tail |
| `SFX_Draw` | 1.0 s | drawing a steel sword from a leather scabbard, metallic scrape, ending with a ring |
| `SFX_Sheathe` | 1.0 s | sliding a steel sword into a leather scabbard, scrape ending with a soft thud |
| `SFX_PlayerHurt` | 0.6 s | male warrior pain grunt, short, gritty, medieval, no words |

## Verification

**Commands:**
- `run_tests` EditMode (`SoundCuePickerTests`, `FootstepStrideTests`, `SfxVolumeSettingsTests`, `MusicVolumeSettingsTests`) -- expected: all pass
- `read_console` after compile and in play -- expected: no errors

**Manual checks:**
- Play from `Core.unity` with clips assigned: steps follow the walk/run rhythm, combat cues fire, and the Effects slider changes only SFX.

## Suggested Review Order

**Playback core**

- Entry point: one pooled player handles every one-shot from the event channel; drops out-of-range 3D cues
  [`SfxPlayer.cs:68`](../../Assets/_Game/Scripts/Audio/SfxPlayer.cs#L68)

- Listener-range cull so far-off fights don't burn voices or cooldowns
  [`SfxPlayer.cs:114`](../../Assets/_Game/Scripts/Audio/SfxPlayer.cs#L114)

- Voice stealing: a free source, else the earliest-started
  [`SfxPlayer.cs:125`](../../Assets/_Game/Scripts/Audio/SfxPlayer.cs#L125)

- Gameplay-facing API: null cue = silent, so optional sounds need no guards
  [`GameEventSO_SfxRequest.cs:24`](../../Assets/_Game/ScriptableObjects/Events/GameEventSO_SfxRequest.cs#L24)

- No-repeat variation pick and null-slot skip, pure and tested
  [`SoundCuePicker.cs:15`](../../Assets/_Game/Scripts/Audio/SoundCuePicker.cs#L15)

**Mixer & volume**

- Mixer params applied in Start (SetFloat is ignored in Awake) and on change
  [`AudioMixerVolumes.cs:32`](../../Assets/_Game/Scripts/Audio/AudioMixerVolumes.cs#L32)

- Linear slider to dB, floored at −80
  [`AudioMixerVolumes.cs:56`](../../Assets/_Game/Scripts/Audio/AudioMixerVolumes.cs#L56)

- Music source now carries only the fade; the setting moved to the mixer
  [`MusicPlayer.cs:63`](../../Assets/_Game/Scripts/Audio/MusicPlayer.cs#L63)

- Effects slider mirrors the music row
  [`OptionsUI.cs:61`](../../Assets/_Game/Scripts/UI/Screens/OptionsUI.cs#L61)

**Combat hooks**

- Hit / block / parry sound at the contact point, before damage resolution
  [`PlayerCombat.cs:565`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L565)

- Swing whoosh when the hit window opens; the weapon is read live, not from the stale cache
  [`PlayerCombat.cs:847`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L847)

- Draw / sheathe only on the real clip event (not watchdog / interrupt nets)
  [`PlayerCombat.cs:895`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L895)

- AI hits use the same mapping (player blocking an NPC)
  [`EntityMeleeAttacker.cs:231`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L231)

- HitResult → cue lives on the data asset (no Audio↔Combat script coupling)
  [`CombatAudioSO.cs:30`](../../Assets/_Game/ScriptableObjects/Audio/CombatAudioSO.cs#L30)

- Hurt grunt, skipped on the killing blow
  [`PlayerHealth.cs:94`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L94)

**Footsteps**

- Grounded grace for isGrounded dropouts; state gate mutes rolls / attacks / death
  [`FootstepEmitter.cs:59`](../../Assets/_Game/Scripts/Audio/FootstepEmitter.cs#L59)

- Pure stride accumulator: teleport/airborne reset, stalls only pause
  [`FootstepStride.cs:19`](../../Assets/_Game/Scripts/Audio/FootstepStride.cs#L19)

- Player implements the gate (Player → Audio interface, no reverse dependency)
  [`PlayerStateManager.cs:75`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L75)

**Wiring, tests & docs**

- `SfxPlayer` + `AudioMixerVolumes` on the AudioManager; music routed to the Music group
  [`Core.unity:710`](../../Assets/_Game/Scenes/Core.unity#L710)

- Effects Volume row (duplicate of the music row)
  [`UICanvas.prefab:4876`](../../Assets/_Game/Prefabs/UI/UICanvas.prefab#L4876)

- Tests: picker, stride, dB / clamp
  [`SoundCuePickerTests.cs`](../../Assets/Tests/EditMode/SoundCuePickerTests.cs)
  [`FootstepStrideTests.cs`](../../Assets/Tests/EditMode/FootstepStrideTests.cs)
  [`SfxVolumeSettingsTests.cs`](../../Assets/Tests/EditMode/SfxVolumeSettingsTests.cs)

- System docs and the clip layout / import recipe / ElevenLabs prompts
  [`Scripts/Audio/CLAUDE.md`](../../Assets/_Game/Scripts/Audio/CLAUDE.md)
  [`Art/Sounds/CLAUDE.md`](../../Assets/_Game/Art/Sounds/CLAUDE.md)
