# CLAUDE.md — Assets/_Game/Scripts/World/Story

> Loaded when Claude accesses files in this folder. Scripted story sequences (`ArrivalSequence`,
> `ArrivalSequenceRules`). Namespace: `Game.World`. Scene layout of the arrival: `Scenes/STARTING_TOWN.md` → Arrival sequence.

---

## Rules

- **Story sequences drive components directly.** Like `TollGate`, `ArrivalSequence` drives AI / Player / Core
  components directly (story-sequence exception to the event-channel rule: it needs synchronous status polling).
- `SaveSystem.BlockSaving(this)` while running; nothing saved.
- Pure decisions go in a `*Rules` static class (`ArrivalSequenceRules`, `ArrivalSequenceRulesTests`).

---

## ArrivalSequence / ArrivalSequenceRules

event-001 (`spec-area-1-arrival-escort-beating` part 1, `spec-area-1-arrival-hut-briefing` part 2), scene root
`ArrivalSequence` in StartingTown, data `ArrivalSequenceSO` (`ScriptableObjects/World/`, asset
`Data/World/ArrivalSequence_Event001`).

**Part 1.** Runs on region load while `completedFact` is unset: suspends the cast's `NpcRoutine`s, escorts the player
down two lane routes (guards wait for a lagging player; a straying one is warned, then a guard intercepts and brawls
them; a player knocked out on the way is teleported to `_watchSpot`), holds the player
(`PlayerStateManager.SetScriptedHold`), opens the Warden's forced topic, then `StartBrawl(Tam)` → Tam unconscious →
closing bubble → hold released, Warden / Tam routines resumed.

**Part 2.** The same escort (a `Leg` switches lane pair, drag spot and arrival action) leads to the hut, where the tent
mate (Hobb, routine suspended and snapped to `_tentMateSpot` at the start) opens `hutBriefing` with
`RequestForcedTopic(node, thenTopics: true)` once the player is within `briefingDistance` (`EvaluateBriefing`; timeout
counted only while it could open, not while Hobb fights; Hobb down → skipped; a player stopping short hears Hobb's
`tentMateCallBarks`; a knockout restarts the briefing). Closing waits for a knocked-out player to get up; on the hut leg
the Warden doesn't hold the escort. The briefing ends on his topic list; when the player leaves (cursor relocked) →
fact set, guards / Hobb routines resumed. No hut wiring → warning, the arrival ends at the stage.

**Details**

- Stray distance is measured from the lane (nearer of the current / previous lane point), never from the guards (a
  chasing guard drags their centre).
- The hold is only applied while the player is calm (not fighting, not down); the speech timeout only counts while it
  could open.
- `Release` ends the Warden's brawl (`EntityBrain.EndBrawl`). `OnDisable` (region unload) mid speech / briefing
  requests the speaker's dialogue close (`NPCPresence.RequestDialogueClose`) before `Release`.
- Dev: `_devStart` (None / SkipAll / SkipToHutEscort, Editor / `Debug.isDebugBuild`; an editor play scenario other
  than Open Scenes overrides it — `PlayScenarios.ArrivalOverrideFor`, see `Scenes/CLAUDE.md` → Boot flow) or context menus
  **Dev: Skip Arrival** / **Dev: Skip to Hut Escort**.
