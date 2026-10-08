---
title: 'Neutral NPC Sneak Warnings & Speech Bubbles'
slug: 'neutral-npc-sneak-warnings-speech-bubbles'
created: '2026-10-08'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'uGUI + TextMeshPro (screen-space HUD, no CanvasScaler)', 'NavMeshAgent', 'PhysX raycasts', 'NUnit EditMode tests']
files_to_modify: ['Assets/_Game/Scripts/AI/EntityBrain.cs', 'Assets/_Game/Scripts/AI/EntityPerception.cs', 'Assets/_Game/Scripts/AI/TargetRegistry.cs', 'Assets/_Game/Scripts/Stealth/StealthDetection.cs', 'Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs', 'Assets/_Game/ScriptableObjects/Entities/Entity.cs', 'Assets/_Game/Data/Config/StealthConfig.asset', 'Assets/_Game/Data/Entities/Entity_HumanoidNPC.asset', 'Assets/_Game/Prefabs/Entities/Entity_base.prefab', 'Assets/_Game/Prefabs/UI/UICanvas.prefab', 'Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs', 'NEW Assets/_Game/ScriptableObjects/Events/GameEventSO_SpeechBubbleRequest.cs', 'NEW Assets/_Game/Data/Events/OnSpeechBubbleRequested.asset', 'NEW Assets/_Game/ScriptableObjects/Dialogue/BarkSetSO.cs', 'NEW Assets/_Game/Data/NPCs/Barks/Barks_SneakWarning.asset', 'NEW Assets/_Game/Scripts/UI/HUD/SpeechBubbleUI.cs', 'NEW Assets/_Game/Scripts/UI/HUD/SpeechBubbleEntryUI.cs', 'NEW Assets/_Game/Scripts/UI/HUD/SpeechBubbleRules.cs', 'NEW Assets/_Game/Prefabs/UI/SpeechBubble.prefab', 'NEW Assets/Tests/EditMode/SpeechBubbleRulesTests.cs', 'NEW Assets/Tests/EditMode/BarkSetTests.cs', 'Assets/Tests/EditMode/StealthDetectionTests.cs']
code_patterns: ['GameEventSO<T> payload struct + concrete channel in its own file (Game.Core, ScriptableObjects/Events)', 'Enum switch state machine in EntityBrain; Transition* methods own agent/animator side effects', 'EntityPerception ticked by brain, never changes brain state', 'Screen-space HUD card anchored via WorldToScreenPoint + CanvasGroup fade (InteractionPromptUI)', 'Config SOs for tunables, per-entity values on Entity SO', 'GameLog + TAG, no per-frame allocations, pool spawned UI', 'Pure static helpers for testable logic']
test_patterns: ['NUnit EditMode in Assets/Tests/EditMode (Tests.EditMode asmdef references Game)', 'Pure static helper tests (InteractionPromptUITests.ClampToScreen, StealthDetectionTests)', 'Class naming [SystemName]Tests']
---

# Tech-Spec: Neutral NPC Sneak Warnings & Speech Bubbles

**Created:** 2026-10-08

## Overview

### Problem Statement

Since `tech-spec-stealth-mode-vision-detection`, hostile entities detect a sneaking player through
`EntityPerception`. Neutral NPCs, however, do not react at all: `Entity_HumanoidNPC` has
`DetectionRange = 0` (perception inactive), and perception only acquires **hostile** stealth targets
(`TargetRegistry.FindClosestHostileStealthTarget`). A player can sneak right behind a villager with no
consequence. There is also no way for any NPC to "say" something in the world outside of the dialogue
UI, which future systems (sneak abuse, pickpocketing, catching the player lockpicking) will need.

### Solution

1. A **generic, system-agnostic speech bubble system**: any code raises a `SpeechBubbleRequest`
   (speaker transform, text, duration, priority) on a `GameEventSO` channel; a pooled screen-space HUD
   layer (styled like `InteractionPromptUI`: faded black rectangle, white text) shows the bubble above
   the speaker and fades it out.
2. **Neutral witness awareness**: non-hostile humanoid NPCs build awareness of the player **only while
   the player is sneaking**, reusing the existing cone / LOS / proximity math.
3. A new **Watching** reaction: on full awareness the NPC stops, turns to face the player, says a random
   warning line in a bubble ("What are you doing here?", "Why are you sneaking around?", ...), and
   keeps facing the player until they leave a watch range or break line of sight, then resumes
   Idle/Patrol. A per-NPC cooldown prevents repeat warnings, and a runtime `WarnedCount` is recorded as
   the hook for future escalation.

### Scope

**In Scope:**

1. **Speech bubble system (generic)**
   - `SpeechBubbleRequest` payload struct + `GameEventSO_SpeechBubbleRequest` channel (own file).
   - `SpeechBubbleUI` HUD component on `UICanvas`: pooled bubble cards anchored to the speaker's head
     (screen-space, constant size), fade in / hold / fade out, hidden behind the camera, beyond a max
     visible distance, or while the cursor is unlocked (menus).
   - One active bubble per speaker; a new request for the same speaker replaces it (or is ignored if lower
     priority than the visible one).
   - Visual style: rounded/plain rectangle, black background with alpha fade, white TMP text, wraps to a
     max width.
2. **Bark lines data** — a reusable `BarkSetSO` (list of lines, `GetRandom()` that avoids repeating the last
   line). One asset `Barks_SneakWarning` with 1–3+ lines for now.
3. **Neutral witness perception**
   - `EntityPerception` gains a witness mode for entities that are not hostile to the player: only tracks
     the player **while sneaking**, using new `Entity` SO field(s) (e.g. `WitnessRange`; 0 = off).
   - Enabled on `Entity_HumanoidNPC` (all non-hostile humanoid NPCs). Monsters/hostiles unchanged.
4. **Watching state in `EntityBrain`**
   - Full witness awareness → `Watching`: stop agent, face the player (turn speed from config), raise one
     warning bubble from the NPC's `BarkSetSO`.
   - Exit when the player leaves `watchRange` or LOS is lost for a short grace time → back to
     Idle/Patrol (same waypoint).
   - Per-NPC warn cooldown (config); runtime `WarnedCount` incremented per warning (not saved).
   - Never entered while dead, in combat, or in dialogue; damage reaction rules unchanged.
5. **Debug** — the existing gizmos / F3 overlay show the Watching state name and witness awareness (the
   state name comes for free via `DebugStateName`).
6. EditMode tests for the pure parts (bark picking, bubble replacement/priority rules, witness gating).

**Out of Scope:**

- Escalation / aggression after repeated warnings, private areas / ownership zones.
- Pickpocketing, lockpicking detection, crime / bounty system.
- Voice audio, bubble icons, NPC-to-NPC chatter, localization.
- Saving `WarnedCount` or cooldowns.
- Head-only look-at IK (the whole body turns).
- Reacting to a non-sneaking (walking) player.

## Context for Development

### Codebase Patterns

**Neutral NPCs today**
- One shared SO: `Data/Entities/Entity_HumanoidNPC.asset` (faction `Faction_Neutral`, `_detectionRange: 0`).
  All StartingTown NPCs are scene instances of `Prefabs/Entities/Humanoids/NPC_base Variant.prefab`, a variant
  of `Entity_base.prefab`, which carries `EntityBrain`, `EntityPerception` (`_config` = StealthConfig wired on
  the base), `FactionMember`, `EntityHealth`, `NavMeshAgent`. The brain resolves `_perception` via
  `GetComponent` in `Awake` if unassigned.
- `DetectionRange <= 0` = passive: `EntityPerception.IsActive` is false (`IsActive => enabled && _config != null
  && Entity != null && Entity.DetectionRange > 0f`), so `Tick` is a no-op; brain skips the attacker error and
  `HandleHealthChanged` returns before fighting back (`if (DetectionRange <= 0f) return;`). The radius scan
  `TryAcquireTarget` with range 0 finds nothing.
- `Faction_Neutral` is not hostile to `Faction_Player`; `FactionSO.IsHostileTo(other)` = list membership.

**EntityPerception** (`Scripts/AI/EntityPerception.cs`, `Game.AI`)
- `Tick(dt, engaged)`: every `targetScanInterval` scans `TargetRegistry.FindClosestHostileStealthTarget(faction,
  pos, max(DetectionRange, DisengageRange))`; LOS raycast every `losCheckInterval` from `EyePosition` to
  `IStealthTarget.VisibilityPoint` against `lineOfSightMask`. Not engaged → `StealthDetection.ComputeVisibility`
  (sneak-adjusted sight range / proximity) → `FillPerSecond` → `StepAwareness`. Engaged → `CanSeeTarget = LOS &&
  distance <= DisengageRange`, awareness held at 1. Updates `LastSeenPosition`, `TimeSinceSeen`.
- Read-outs: `Awareness`, `IsFullyAware`, `IsSuspicious`, `Target`, `CanSeeTarget`, `HasLineOfSight`,
  `DistanceToTarget`, `SightRange` (= `Entity.DetectionRange`), `ProximityRadius`, `CurrentSightRange`, etc.
  `ResetPerception()`, `ForceAware()`, `SetAwareness()`. Static `Active` list for the F3 overlay. Editor gizmos
  bail when `entity.DetectionRange <= 0`.
- `StealthDebugOverlay` (`Scripts/Debug/`) skips `!p.IsActive` and draws cones from `p.SightRange` and
  `sight * sneakSightRangeMultiplier`.

**TargetRegistry** — `FindClosest(myFaction, origin, maxRange, StealthFilter)` filters `IsHostileTo`; has
`StealthFilter.Only` for stealth targets. No non-hostile query exists.

**EntityBrain** (`Scripts/AI/EntityBrain.cs`, 716 lines)
- `enum EntityState { Idle, Patrolling, Suspicious, Warning, Engaging, Attacking, Searching, Dead }`;
  `STATE_NAMES` from `Enum.GetNames` → `DebugStateName`. Update: death check → `_perception.Tick(dt,
  IsAlertedOnStealthTarget())` when `PerceptionActive` → switch.
- `TryDetectFromNonCombat()` (Idle/Patrol): `TryTakePerceptionTarget()` → `RespondToDetectedTarget()`
  (engage/warn); `_perception.IsSuspicious` → `TransitionToSuspicious()`; radius scan.
- `HandleSuspicious()`: full awareness → `RespondToDetectedTarget()`; awareness 0 → `ResumeNonCombat()`; else
  `FacePoint(LastSeenPosition, StealthConfig.suspiciousTurnSpeed)`.
- `FacePoint(point, degPerSecond)` = Y-only `RotateTowards` (agent stopped). `ResumeNonCombat()` resumes the
  current waypoint or Idle at `_idleOrigin`. `_disengageState` captured on entry from Idle/Patrolling.
- `IsUnawareOf` (sneak attack): only Idle/Patrolling/Suspicious count as unaware.
- `SetCombatState(bool)` drives `IsInCombat` (polled by `NPCPresence` — combat blocks dialogue).

**UI**
- `InteractionPromptUI` (`Scripts/UI/HUD/`, `Game.UI`): HUD card in `UICanvas/Game/InteractionPrompt` (own
  Canvas + CanvasGroup). `LateUpdate`: anchor = target collider `bounds.center + up * extents.y` (fallback
  transform), `Camera.WorldToScreenPoint`, hidden when `screen.z <= 0` or `!CursorManager.IsLocked`;
  `ClampToScreen(pos, size, pivot, screen, margin)` (public static, tested) then
  `ScreenPointToLocalPointInRectangle(parent, pos, null, out local)` → `anchoredPosition`; alpha via
  `Mathf.MoveTowards(..., _fadeSpeed * Time.unscaledDeltaTime)`. `_screenOffset (0, 24)`, margin 16.
  Camera cached in Awake (error + disable if missing).
- Prompt style (`Prefabs/UI/InteractionPrompt.prefab`): `Card` Image black `a 0.6`, no sprite,
  HorizontalLayoutGroup padding 14 / spacing 10, white TMP texts 17–20 pt.
- `UICanvas` has **no CanvasScaler** (fixed pixel sizes); HUD lives under `UICanvas/Game`; `UICanvas` is nested
  in `Player.prefab`. `NotificationToastUI` is the precedent for instantiating entry prefabs under a container.
- `EntityUI` is a world-space billboard per entity (name + health) — not reused for bubbles.

**Events** — `GameEventSO<T>` (`ScriptableObjects/Events/GameEventSO.cs`, `Game.Core`): `Raise`,
`AddListener`, `RemoveListener`. Payload struct + concrete class in one file per type (e.g.
`GameEventSO_InteractionFocus.cs` declares `InteractionFocusData` + `[CreateAssetMenu(menuName =
"Game/Events/...")] class GameEventSO_InteractionFocus : GameEventSO<InteractionFocusData> { }`). Runtime scene
refs may travel in payloads (never stored in the SO). Assets in `Data/Events/On*.asset`.

**Project rules that apply** (`project-context.md`): cross-system only through `GameEventSO` (AI → UI must use
a channel); subscribe in `OnEnable` / unsubscribe in `OnDisable`; tunables in config SOs; pool frequently
spawned UI ("floating text"); no per-frame allocations; `Camera.main` cached; `GameLog` + TAG; enum switch
state machines; SO assets `PascalCase_Description`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | State machine to extend with `Watching`; detection entry points, `FacePoint`, `ResumeNonCombat` |
| `Assets/_Game/Scripts/AI/EntityPerception.cs` | Awareness meter to extend with witness mode; `IsActive`, `SightRange`, gizmos |
| `Assets/_Game/Scripts/AI/TargetRegistry.cs` | Add a non-hostile stealth-target query |
| `Assets/_Game/Scripts/Stealth/StealthDetection.cs` | Pure math; add witness visibility gate |
| `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` + `Data/Config/StealthConfig.asset` | Global witness tunables |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` + `Data/Entities/Entity_HumanoidNPC.asset` | Per-entity witness range + bark set |
| `Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs` + `Prefabs/UI/InteractionPrompt.prefab` | Anchoring / fade / style reference for bubbles |
| `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` | Entry-prefab instantiation precedent |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_InteractionFocus.cs` | Payload + channel file pattern |
| `Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs` | F3 overlay; uses `SightRange` and `IsActive` |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | Wire the bubble channel on `EntityBrain` |
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | Host `SpeechBubbleLayer` under `Game` |
| `Assets/Tests/EditMode/StealthDetectionTests.cs`, `InteractionPromptUITests.cs` | Test style |

### Technical Decisions

- **Screen-space pooled bubbles** (not world-space canvases): constant readable size, one HUD layer under
  `UICanvas/Game`, same look as InteractionPrompt. Prefab pool, prewarmed, capped.
- **Generic channel**: `SpeechBubbleRequest { Transform speaker; string text; float duration; int priority;
  float anchorHeight }` raised on `OnSpeechBubbleRequested`. The UI knows nothing about stealth; the brain knows
  nothing about UI. Anchor = `speaker.position + up * anchorHeight` (default 0) — explicit and predictable, no
  collider guessing.
- **Bubble placement (screenshot-verified)**: on humanoids the InteractionPrompt sits at **chest height** (it anchors
  on the short, hip-pinned `InteractionCollider`), and the world-space `EntityUICanvas` (name tag + health bar) sits
  at local y 2.2, 1 m tall (≈ 1.7–2.7 m). The bubble goes **above the name tag**: a new `SpeechAnchor` child on
  `Entity_base` at local y 2.8 is passed as the request's `speaker`; the bubble's pivot is bottom-centre with an 8 px
  screen offset, so it grows upward from the anchor. The name tag is world-space (shrinks with distance) while the
  bubble is constant-size, so a world anchor above the tag's top keeps the bubble clear at every distance. The
  prompt is unchanged — the two never overlap.
- **Bark lines as data**: `BarkSetSO` (`Game.Dialogue`, `ScriptableObjects/Dialogue/`) — reusable by future
  systems (pickpocket caught, lockpick caught, ambient). Referenced from the `Entity` SO (`WitnessBarks`) so
  each NPC type can say different things.
- **Witness mode inside `EntityPerception`** (not a new component): same cone / LOS / awareness code; active when
  `DetectionRange <= 0 && WitnessRange > 0`. Only non-hostile stealth targets, only while sneaking, sight range =
  `WitnessRange` (no extra sneak multiplier — it only ever sees sneaking targets). Once Watching, tracking
  continues even if the player stands up.
- **Brain**: `RespondToDetectedTarget()` routes to `TransitionToWatching()` in witness mode, so the existing
  Suspicious state (turn toward last seen point) is reused for free by neutral NPCs. `Watching` is not combat
  (`IsInCombat` stays false → the player can still talk to the NPC) and is not "unaware" (no sneak-attack bonus).
- **Trigger only while sneaking**, **cooldown + runtime `WarnedCount`**, no persistence.
- Applies to all non-hostile humanoids via the shared `Entity_HumanoidNPC` SO.


## Implementation Plan

### Tasks

**A. Speech bubble system (generic, no AI knowledge)**

- [x] Task 1: Bubble request payload + channel
  - File: NEW `Assets/_Game/ScriptableObjects/Events/GameEventSO_SpeechBubbleRequest.cs`
  - Action: In `namespace Game.Core`, declare `[System.Serializable] public struct SpeechBubbleRequest { public
    Transform speaker; public string text; public float duration; public int priority; public float anchorHeight; }`
    and `[CreateAssetMenu(menuName = "Game/Events/Speech Bubble Request", fileName = "NewSpeechBubbleRequestEvent")]
    public class GameEventSO_SpeechBubbleRequest : GameEventSO<SpeechBubbleRequest> { }` (both in this one file,
    like `GameEventSO_InteractionFocus.cs`). XML-doc each field: `speaker` = runtime scene ref, never stored in an
    SO, and the bubble's anchor point (pass a dedicated anchor transform such as an entity's `SpeechAnchor`) — it is
    also the identity for "one bubble per speaker"; `duration <= 0` → the UI default; higher `priority` wins;
    `anchorHeight` = extra metres above `speaker.position` (default 0).
  - Then create the asset `Assets/_Game/Data/Events/OnSpeechBubbleRequested.asset` (via MCP
    `manage_scriptable_object` or the Create menu).

- [x] Task 2: Pure bubble rules
  - File: NEW `Assets/_Game/Scripts/UI/HUD/SpeechBubbleRules.cs` (`namespace Game.UI`, `public static class`)
  - Action:
    - `bool ShouldReplace(bool hasActive, int activePriority, int newPriority)` → `!hasActive || newPriority >= activePriority`.
    - `float ResolveDuration(float requested, float defaultDuration)` → `requested > 0 ? requested : defaultDuration`,
      then `Mathf.Max(0.1f, …)`.
    - `float Lifetime(float duration, float fadeIn, float fadeOut)` → `fadeIn + duration + fadeOut` (negative fades
      treated as 0).
    - `float Alpha(float elapsed, float duration, float fadeIn, float fadeOut)`: `elapsed >= Lifetime` → 0;
      `elapsed < fadeIn` → `elapsed / fadeIn`; hold phase → 1; fade-out phase → `1 - (elapsed - fadeIn - duration) /
      fadeOut`; clamped 0..1. `fadeIn <= 0` skips the fade-in; `fadeOut <= 0` skips the fade-out.
    - `bool IsExpired(float elapsed, float duration, float fadeIn, float fadeOut)` → `elapsed >= Lifetime(...)`.

- [x] Task 3: Bubble entry view
  - File: NEW `Assets/_Game/Scripts/UI/HUD/SpeechBubbleEntryUI.cs` (`Game.UI`, TAG `[SpeechBubble]`)
  - Action: `[SerializeField] RectTransform _rect; CanvasGroup _canvasGroup; TMP_Text _text; LayoutElement
    _textLayout; float _maxTextWidth = 320f`. Public: `RectTransform Rect`; `void SetText(string text)` — sets the
    text, then `_textLayout.preferredWidth = Mathf.Min(_text.GetPreferredValues(text).x, _maxTextWidth)` (short lines
    hug, long lines wrap), then `LayoutRebuilder.ForceRebuildLayoutImmediate(_rect)`; `void SetAlpha(float a)` (writes
    only when changed); `void SetVisible(bool v)` → `gameObject.SetActive(v)`. Missing refs → `GameLog.Error` in Awake
    + `enabled = false`.

- [x] Task 4: Bubble entry prefab
  - File: NEW `Assets/_Game/Prefabs/UI/SpeechBubble.prefab`
  - Action: Root `SpeechBubble` (RectTransform, anchors min/max (0,0), pivot (0.5, 0)) with `CanvasGroup` (alpha 0,
    `interactable` / `blocksRaycasts` false), `Image` black `a 0.6`, no sprite, `raycastTarget` false (same as the
    InteractionPrompt `Card`), `HorizontalLayoutGroup` (padding L/R 14, T/B 8, child control width + height, no force
    expand), `ContentSizeFitter` (both preferred), `SpeechBubbleEntryUI`. Child `Text` (TextMeshProUGUI, white, 18 pt,
    centre-aligned, word wrapping on, `raycastTarget` false) + `LayoutElement`. Wire the entry fields.

- [x] Task 5: Bubble layer (pool + anchoring)
  - File: NEW `Assets/_Game/Scripts/UI/HUD/SpeechBubbleUI.cs` (`Game.UI`, TAG `[SpeechBubble]`)
  - Action:
    - Fields: `[Header("Event Channels")] GameEventSO_SpeechBubbleRequest _onSpeechBubbleRequested`;
      `[Header("Pool")] SpeechBubbleEntryUI _entryPrefab; RectTransform _container; int _prewarmCount = 4;
      int _maxBubbles = 6`; `[Header("Timing")] float _defaultDuration = 3f; float _fadeInTime = 0.15f;
      float _fadeOutTime = 0.4f`; `[Header("Layout")] Vector2 _screenOffset = new(0f, 8f); float _screenMargin = 16f;
      float _maxVisibleDistance = 25f` (the entry prefab's pivot is bottom-centre, so the bubble grows upward from the
      anchor).
    - Private `class ActiveBubble { SpeechBubbleEntryUI entry; Transform speaker; float anchorHeight; float elapsed; float duration; int priority; }`; `List<ActiveBubble> _active`,
      `Stack<SpeechBubbleEntryUI> _pool` and a `Stack<ActiveBubble>` record pool, prewarmed in Awake, so neither a
      request nor a frame allocates in normal play.
    - Awake: validate `_entryPrefab` / `_container` (Error + disable); cache `Camera.main` (Error + disable if null);
      prewarm `_prewarmCount` inactive entries under `_container`. Missing channel → `GameLog.Warn` (bubbles never show).
    - OnEnable / OnDisable: Add / RemoveListener `HandleSpeechBubbleRequested` (null-guarded); OnDisable also releases
      every active bubble.
    - `HandleSpeechBubbleRequested(SpeechBubbleRequest r)`: `r.speaker == null` or empty text → `GameLog.Warn`, return.
      Existing bubble for the same speaker → `SpeechBubbleRules.ShouldReplace(true, existing.priority, r.priority)`;
      false → ignore; true → reuse that bubble (new text, priority, duration, `elapsed = Mathf.Min(elapsed, _fadeInTime)`
      so a visible bubble does not flash). No existing → take an entry from the pool (instantiate only when the pool
      is empty); if `_active.Count >= _maxBubbles`, first release the bubble with the largest `elapsed`. Duration via
      `SpeechBubbleRules.ResolveDuration(r.duration, _defaultDuration)`.
    - LateUpdate (iterate backwards): release when the speaker is destroyed / `!activeInHierarchy` or
      `SpeechBubbleRules.IsExpired`; `elapsed += Time.deltaTime` (bubbles pause with the game). Anchor =
      `speaker.position + Vector3.up * anchorHeight`. Visible = `CursorManager.IsLocked && screen.z > 0 && (anchor -
      camera.position).sqrMagnitude <= _maxVisibleDistance²`. Position: `InteractionPromptUI.ClampToScreen((Vector2)screen
      + _screenOffset, rect.size, rect.pivot, new Vector2(Screen.width, Screen.height), _screenMargin)`, then
      `RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, pos, null, out local)` and the same anchorRef
      → `anchoredPosition` conversion as `InteractionPromptUI.LateUpdate` (write only when moved > 0.5 px).
      `entry.SetAlpha(visible ? SpeechBubbleRules.Alpha(...) : 0f)`.
    - Release = `entry.SetAlpha(0)`, `entry.SetVisible(false)`, push entry + record back to their pools.

- [x] Task 6: Host the layer in the HUD
  - File: `Assets/_Game/Prefabs/UI/UICanvas.prefab`
  - Action: Under `Game`, add `SpeechBubbleLayer` placed **before** `InteractionPrompt` in sibling order (the prompt
    draws on top): stretch RectTransform (anchors 0..1, offsets 0), own `Canvas` (no override sorting), no
    GraphicRaycaster, `SpeechBubbleUI` with `_container` = itself, `_entryPrefab` = `SpeechBubble.prefab`,
    `_onSpeechBubbleRequested` = `OnSpeechBubbleRequested.asset`. Edit through MCP / Prefab Mode, not raw YAML.

**B. Bark data**

- [x] Task 7: `BarkSetSO`
  - File: NEW `Assets/_Game/ScriptableObjects/Dialogue/BarkSetSO.cs` (`namespace Game.Dialogue`)
  - Action: `[CreateAssetMenu(menuName = "Game/Dialogue/Bark Set", fileName = "Barks_")]`; `[SerializeField, TextArea]
    private string[] _lines`. `int Count`; `string GetRandomLine(ref int lastIndex)` → `PickIndex(Count, lastIndex,
    Random.value)`, stores the result in `lastIndex`, returns the line (`null` when empty). Pure
    `public static int PickIndex(int count, int lastIndex, float random01)`:
    - `count <= 0` → −1; `count == 1` → 0.
    - `lastIndex` outside `[0, count)` → `Mathf.Min((int)(random01 * count), count - 1)`.
    - Otherwise `i = Mathf.Min((int)(random01 * (count - 1)), count - 2)`; return `i >= lastIndex ? i + 1 : i` (never
      repeats the previous line).
    - No runtime state on the SO (the caller owns `lastIndex`). Editor `OnValidate` warns on empty / whitespace lines.
- [x] Task 8: Sneak warning bark asset
  - File: NEW `Assets/_Game/Data/NPCs/Barks/Barks_SneakWarning.asset`
  - Action: lines `What are you doing here?`, `Why are you sneaking around?`, `I've got my eye on you.`

**C. Witness perception**

- [x] Task 9: Entity SO witness fields
  - File: `Assets/_Game/ScriptableObjects/Entities/Entity.cs`
  - Action: New `[Header("Witness (non-hostile)")]` after Perception: `_witnessRange = 0f` (tooltip: sight range against
    a **sneaking** non-hostile player; 0 = never reacts; only used when DetectionRange ≤ 0), `_witnessWatchRange = 9f`
    (keeps watching while the player is closer than this), `BarkSetSO _witnessBarks`. Properties `WitnessRange`,
    `WitnessWatchRange`, `WitnessBarks`. OnValidate: both ≥ 0; when `_witnessRange > 0` and `_witnessWatchRange <
    _witnessRange` → Warn + clamp to `_witnessRange`.
  - File: `Assets/_Game/Data/Entities/Entity_HumanoidNPC.asset` → `_witnessRange: 6`, `_witnessWatchRange: 9`,
    `_witnessBarks` = `Barks_SneakWarning`.

- [x] Task 10: Global witness tuning
  - File: `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` (+ `Data/Config/StealthConfig.asset`)
  - Action: `[Header("Witness (non-hostile NPCs)")]`: `witnessTurnSpeed = 240f` (deg/s while watching),
    `witnessLoseSightTime = 1.5f` (s without LOS before giving up), `witnessWarnCooldown = 20f` (s between two warnings
    from the same NPC), `witnessBubbleDuration = 3f`, `witnessBubblePriority = 0` (int). OnValidate: clamp the floats ≥ 0.

- [x] Task 11: Pure witness gate + registry query
  - File: `Assets/_Game/Scripts/Stealth/StealthDetection.cs`
  - Action: `public static float ComputeWitnessVisibility(bool targetSneaking, float distance, float angleDeg, float
    witnessRange, float viewAngle, float proximityRadius, float edgeDistanceFactor, float peripheralAngleFactor)` →
    `targetSneaking ? ComputeVisibility(...) : 0f`.
  - File: `Assets/_Game/Scripts/AI/TargetRegistry.cs`
  - Action: Add `FindClosestNonHostileStealthTarget(FactionSO myFaction, Vector3 origin, float maxRange)`: like
    `FindClosestHostileStealthTarget`, but keeps stealth targets whose faction is **not** hostile to `myFaction`.
    Implement by adding a `bool wantHostile` parameter to the private `FindClosest` (existing callers pass `true`).

- [x] Task 12: Witness mode in `EntityPerception`
  - File: `Assets/_Game/Scripts/AI/EntityPerception.cs`
  - Action:
    - `public bool IsWitness => Entity != null && Entity.DetectionRange <= 0f && Entity.WitnessRange > 0f;`
      `IsActive => enabled && _config != null && Entity != null && (Entity.DetectionRange > 0f || IsWitness)`. Update
      the class summary.
    - Awake FactionMember error: also when `IsWitness`.
    - `Tick` witness branch: scan with `FindClosestNonHostileStealthTarget(faction, pos, Mathf.Max(WitnessRange,
      WitnessWatchRange))`. `CurrentSightRange = WitnessRange` (no sneak multiplier), `CurrentProximityRadius =
      EffectiveProximityRadius(ProximityRadius, true, sneakProximityMultiplier)`. Not engaged → visibility from
      `ComputeWitnessVisibility(TargetIsSneaking, …)` (awareness drains while the player walks normally); fill via
      `FillPerSecond(visibility, AwarenessFillTime, TargetIsSneaking, sneakFillRateMultiplier)`. Engaged (watching) →
      `CanSeeTarget = _hasLineOfSight && distance <= WitnessWatchRange`, awareness held at 1 (keeps watching even if the
      player stands up). Hostile branch unchanged.
    - `SightRange` → `IsWitness ? WitnessRange : DetectionRange`; new `SneakingSightRange` → `IsWitness ? WitnessRange :
      DetectionRange * sneakSightRangeMultiplier` (guard null config).
    - Gizmos: bail only when `DetectionRange <= 0 && !IsWitness`; in witness mode draw only the witness cone (solid)
      and the sneaking proximity circle; label prefix `Witness · `.
  - File: `Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs`
  - Action: `DrawPerception` — witness → one solid cone at `p.SneakingSightRange` + the sneaking proximity circle;
    hostile → unchanged, but use `p.SneakingSightRange` for the dim cone.

**D. Watching reaction**

- [x] Task 13: `Watching` state in `EntityBrain`
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    - Enum: add `Watching` before `Dead`. Class summary: mention Watching.
    - Field `[Header("Witness")] [Tooltip("Raised when a non-hostile witness warns the player (speech bubble).")]
      [SerializeField] private GameEventSO_SpeechBubbleRequest _onSpeechBubbleRequested;`, `[Tooltip("Point the
      speech bubble anchors to (above the name tag).")] [SerializeField] private Transform _speechAnchor;`,
      `[Tooltip("Height above the root used when _speechAnchor is unassigned.")] [SerializeField] private float
      _speechAnchorFallbackHeight = 2.8f;`. Awake: `_speechAnchor == null` → `transform.Find("SpeechAnchor")`; still
      null → `GameLog.Warn` once (only when witness mode can trigger). Runtime: `float
      _watchLostTimer; float _lastWarnTime = float.NegativeInfinity; int _lastBarkIndex = -1; bool _warnedMissingBark;`.
      Public `int WarnedCount { get; private set; }` (runtime only — escalation hook).
    - `IsAlertedOnStealthTarget()` → also true in `Watching` (perception tracks 360° and holds awareness).
    - `RespondToDetectedTarget()`: first line `if (PerceptionActive && _perception.IsWitness) { TransitionToWatching();
      return; }` (Suspicious → full awareness → Watching comes for free).
    - `TransitionToWatching()`: capture `_disengageState` from Idle/Patrolling; `_state = Watching`; `_agent.isStopped =
      true`; `_watchLostTimer = 0`; **no** `SetCombatState`; `TryWarn()`; `GameLog.Info`.
    - `TryWarn()`: `Time.time - _lastWarnTime < StealthConfig.witnessWarnCooldown` → return (watch silently). Else
      `_lastWarnTime = Time.time; WarnedCount++`; `line = Entity.WitnessBarks != null ?
      Entity.WitnessBarks.GetRandomLine(ref _lastBarkIndex) : null`; line null or channel null → `GameLog.Warn` once
      (`_warnedMissingBark`) and return; else raise `new SpeechBubbleRequest { speaker = _speechAnchor != null ?
      _speechAnchor : transform, text = line, duration = StealthConfig.witnessBubbleDuration, priority =
      StealthConfig.witnessBubblePriority, anchorHeight = _speechAnchor != null ? 0f : _speechAnchorFallbackHeight }`. Log `"{name} warns the player (#{WarnedCount})"`.
    - `HandleWatching()`: `!PerceptionActive || !IsLive(_currentTarget)` → `EndWatching()`. Flat (XZ) distance >
      `Entity.WitnessWatchRange` → `EndWatching()`. `_perception.CanSeeTarget` → `_watchLostTimer = 0`, else `+=
      Time.deltaTime` and `>= StealthConfig.witnessLoseSightTime` → `EndWatching()`. Otherwise
      `FacePoint(_currentTarget.Transform.position, StealthConfig.witnessTurnSpeed)`.
    - `EndWatching()`: `_currentTarget = null; _perception.ResetPerception(); ResumeNonCombat();` + log.
    - Add `case EntityState.Watching: HandleWatching(); break;` to the switch.
    - Leave `HandleHealthChanged` (passive early return) and `IsUnawareOf` unchanged — Watching is neither combat nor
      unaware.
  - File: `Assets/_Game/Prefabs/Entities/Entity_base.prefab`
  - Action: add an empty child `SpeechAnchor` on the root at local position (0, 2.8, 0) — just above the top of
    `EntityUICanvas` (anchored y 2.2, 100 px × 0.01 scale = 1 m tall → top ≈ 2.7 m). Wire `EntityBrain._speechAnchor`
    = it and `EntityBrain._onSpeechBubbleRequested` = `OnSpeechBubbleRequested.asset` on the base (inherited by all
    variants; verify `NPC_base Variant` and scene instances carry no override). Monster variants may lower the anchor
    later if their name tag is moved (not needed now — monsters never witness).

**E. Tests & docs**

- [x] Task 14: EditMode tests
  - Files: NEW `Assets/Tests/EditMode/SpeechBubbleRulesTests.cs`, NEW `Assets/Tests/EditMode/BarkSetTests.cs`,
    `Assets/Tests/EditMode/StealthDetectionTests.cs`
  - Action: see Testing Strategy. Run all EditMode tests (MCP `run_tests`).

- [x] Task 15: Folder docs
  - Files: `Assets/_Game/Scripts/AI/CLAUDE.md` (Watching state, witness mode, `WarnedCount`),
    `Assets/_Game/Scripts/Stealth/CLAUDE.md` (witness formula + new config rows), `Assets/_Game/Scripts/UI/HUD/CLAUDE.md`
    (`SpeechBubbleUI` row + `OnSpeechBubbleRequested` channel; "make any object say something": raise a
    `SpeechBubbleRequest`), `Assets/_Game/Prefabs/UI/CLAUDE.md` (hierarchy: `Game/SpeechBubbleLayer`),
    `Assets/_Game/Data/NPCs/CLAUDE.md` (`Barks/` folder).

### Acceptance Criteria

- [ ] AC 1: Given a neutral NPC idling and the player sneaking inside its view cone within 6 m with clear LOS, when
  awareness fills to 1, then the NPC stops, turns to face the player, and a bubble with one of the
  `Barks_SneakWarning` lines appears above its head.
- [ ] AC 2: Given the same NPC, when the player walks (not sneaking) through its cone, then awareness never rises and
  the NPC keeps its routine.
- [ ] AC 3: Given the player sneaks into the NPC's cone, when awareness is ≥ the suspicion threshold but below 1, then
  the NPC enters Suspicious (stops, turns toward the last seen position) and returns to its routine when awareness
  drains to 0.
- [ ] AC 4: Given an NPC is Watching, when the player moves around it within 9 m with LOS (sneaking or standing), then
  the NPC keeps rotating to face the player and does not patrol.
- [ ] AC 5: Given an NPC is Watching, when the player moves beyond `WitnessWatchRange` (9 m), then the NPC resumes Idle /
  the same patrol waypoint it had.
- [ ] AC 6: Given an NPC is Watching, when the player breaks LOS for 1.5 s, then the NPC resumes its routine; when LOS
  returns before 1.5 s, it keeps watching.
- [ ] AC 7: Given an NPC warned the player less than 20 s ago, when it detects the sneaking player again, then it watches
  without a new bubble and `WarnedCount` is unchanged; after 20 s a new detection shows a line different from the
  previous one and increments `WarnedCount`.
- [ ] AC 8: Given an NPC is Watching, when the player presses E on it, then dialogue opens normally (`IsInCombat` false).
- [ ] AC 9: Given an NPC is Watching, when the player attacks it, then no sneak-attack multiplier applies.
- [ ] AC 10: Given hostile entities (bandits, spiders), when the player sneaks or walks near them, then detection,
  warning, engage and search behave exactly as before.
- [ ] AC 11: Given a bubble is visible, when the speaker goes behind the camera, beyond 25 m, or a menu unlocks the
  cursor, then the bubble is hidden; when the speaker dies or is disabled, the bubble is released.
- [ ] AC 12: Given a bubble for speaker A is visible, when a lower-priority request arrives for A, then it is ignored; an
  equal or higher priority replaces the text in place (one bubble per speaker).
- [ ] AC 13: Given 6 bubbles are visible, when a 7th speaker requests one, then the oldest bubble is recycled.
- [ ] AC 14: Given a request with a null speaker or empty text, when raised, then a warning is logged and nothing shows.
- [ ] AC 15: Given a short line, the bubble hugs the text; given a line wider than 320 px, it wraps onto several lines
  inside a black 60 % rectangle with white text and stays inside the 16 px screen margin.
- [ ] AC 16: Given the F3 overlay or detection gizmos are on, when looking at a neutral NPC, then its witness cone (6 m)
  and state name (`Watching` / `Suspicious`) are shown.
- [ ] AC 17: Given `Entity_HumanoidNPC` has no `WitnessBarks`, when the NPC detects the player, then it still watches and
  logs one warning (no exception).
- [ ] AC 18: Given a humanoid NPC shows a bubble while hovered (name tag, health bar and `[E] Talk` prompt visible),
  when viewed from 2 m to 25 m, then the bubble sits above the name tag and overlaps neither the name tag, the health
  bar nor the interaction prompt.

## Additional Context

### Dependencies

- Builds on `tech-spec-stealth-mode-vision-detection` (completed): `IStealthTarget`, `PlayerSneak`, `EntityPerception`,
  `StealthDetection`, `StealthConfigSO`, the F3 overlay.
- No new packages (uGUI and TextMeshPro already in use).

### Testing Strategy

**EditMode (NUnit):**
- `SpeechBubbleRulesTests`: `ShouldReplace` (no active → true; lower → false; equal → true; higher → true);
  `ResolveDuration` (≤ 0 → default, positive kept, tiny → 0.1); `Alpha` at 0, mid fade-in, hold, mid fade-out, end;
  zero fade times; `IsExpired` at the lifetime boundary.
- `BarkSetTests` (`BarkSetSO.PickIndex`): count 0 → −1; count 1 → 0 for any last index; count 3 with last = 1 never
  returns 1 for random 0, 0.49, 0.5, 0.99, 1.0; last −1 / out of range → a valid index over the full range.
- `StealthDetectionTests`: `ComputeWitnessVisibility` → 0 when not sneaking; equals `ComputeVisibility` when sneaking.

**Manual (StartingTown, F3 on):**
1. Sneak up behind a villager → no reaction until inside the cone or within 0.7 m; then Suspicious → Watching + bubble.
2. Circle around it → it tracks you. Step behind a wall → it resumes after ~1.5 s. Walk 9 m away → it resumes.
3. Re-trigger within 20 s → silent watch; after 20 s → a different line.
4. Talk to it while it watches → dialogue opens. Open the inventory → the bubble hides.
5. Sneak near a bandit / spider → unchanged hostile behaviour.

### Notes

- **Risk — perception becomes active on passive NPCs.** `PerceptionActive` is now true for every humanoid NPC.
  Checked paths: `TryAcquireTarget` uses range 0 (no hits), `HandleHealthChanged` returns early for `DetectionRange <=
  0`, Searching / Warning are unreachable (witness routes to Watching). Re-verify in play that no NPC enters Warning
  or Engaging.
- **Placement.** Bubble = above the name tag (`SpeechAnchor`, y 2.8); name tag + health bar = `EntityUICanvas`
  (≈ 1.7–2.7 m); interaction prompt = chest height (hip-pinned `InteractionCollider`). No overlap by construction.
  If `EntityUICanvas` is ever moved, move `SpeechAnchor` with it. When the name tag is hidden (not hovered) the
  bubble simply floats a little higher than the head — intended.
- Neutral NPCs rotate in place without a turn animation (same limitation as Suspicious).
- `WarnedCount` and the cooldown are runtime only (reset on load) — fine until escalation exists.
- **Future (out of scope):**
  - Escalation: `WarnedCount >= N` inside a private area → hostile reaction or a guard call.
  - Generic witness query for crimes: pickpocket / lockpick systems ask nearby perceptions whether they currently see
    the player (`CanSeeTarget`), then raise their own bubble with their own `BarkSetSO` at a higher priority, so it
    overrides a warning line.
  - Ambient barks (greetings) through the same channel at low priority.
  - Optional voice / sound hook on `SpeechBubbleRequest`.

## Review Notes

- Adversarial review completed (inline self-review of the diff).
- Findings: 6 total, 4 fixed, 2 skipped (F5 noise — bubbles keep aging while hidden, as specified; F6 info — unrelated
  working-tree changes left out of the feature).
- Resolution approach: auto-fix.
  - F1: `PlayerStateManager.SetInDialogue(true)` now clears sneaking (NPC conversations only — pickups, doors and
    containers keep the stance), so a witness never reacts mid-dialogue.
  - F2: a replaced bubble restarts from its current alpha (`SpeechBubbleRules.RestartElapsed`, tested) — no snap from
    fade-out to full opacity.
  - F3: `WarnedCount` increments only when a bubble is actually raised.
  - F4: F3 overlay summary reads "no entity perceiving the player" (witnesses included).
- Added beyond the File List: `Assets/_Game/Scripts/Player/PlayerStateManager.cs`, `Assets/_Game/Scripts/Player/CLAUDE.md`.

## Post-Review Fix — Witness Mode Per Target + Witness Profile (2026-10-08)

Play testing showed no NPC ever witnessed the player. **Root cause:** the spec assumed every neutral NPC used the
shared `Entity_HumanoidNPC` (`DetectionRange 0`); in fact it is only the `NPC_base Variant` default — each scene NPC
references its own `NPCEntity` asset (`NPC_Villager`, …) with `DetectionRange 8` (they fight monsters). Witness mode,
gated on `DetectionRange <= 0`, never switched on, and hostile-mode perception ignores the non-hostile player.

Changes:
- **Per-target mode:** `EntityPerception.ScanForTarget` takes the closest hostile stealth target first, else (enabled
  witness profile) the closest non-hostile one and sets `IsWitnessing`. `EntityBrain.RespondToDetectedTarget` routes
  a witnessed target to Watching and a hostile one to Warning / Engaging. Watching now also scans for hostiles
  (radius, throttled) and fights back when hit; an unknown-source hit never targets the witnessed player.
- **`WitnessProfileSO`** (`Game.NPC`, `ScriptableObjects/Entities/NPC/`): range 6, watch range 9, barks. Asset
  `Data/Entities/WitnessProfile_Humanoid`. The witness fields moved from `Entity` to `NPCEntity._witnessProfile`
  (`Entity.WitnessProfile` is virtual, null for monsters). Assigned to all 7 `NPCEntity` assets (bandit included —
  harmless, the player is hostile to it). Tuning = swap the profile. `NPCEntity.Reset()` auto-assigns it from the
  Create menu; `NPC:create` sets it explicitly.
- Debug: witness cone drawn in cyan next to the hostile cones; `Witness · ` label prefix while witnessing.
- Tests: `WitnessProfileTests` (610 EditMode tests pass). Play Mode check: Villager → Suspicious → Watching →
  warning bubble "Why are you sneaking around?" (#1).
- Additional files: `ScriptableObjects/Entities/NPC/WitnessProfileSO.cs`, `NPCEntity.cs`,
  `Data/Entities/WitnessProfile_Humanoid.asset`, all `Data/NPCs/*/NPC_*.asset`,
  `Scripts/Stealth/StealthDebugGeometry.cs`, `Assets/Tests/EditMode/WitnessProfileTests.cs`,
  `.claude/commands/NPC/create.md`.
