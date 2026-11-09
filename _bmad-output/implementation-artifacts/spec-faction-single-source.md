---
title: 'Faction single source of truth (FactionMember cleanup)'
type: 'refactor'
created: '2026-11-09'
status: 'done'
baseline_commit: 'a3105c865db5be9e7d3ea2b5d8a3b4f154efc554'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Faction data is split across confusing places: every entity inspector shows a `FactionMember._factionOverride` that only the Player uses, three scripts expose a `_selfFactionMember` field that is just same-GO wiring, and the player's faction is defined twice (`Player.prefab` override + `NPCPresence._playerFaction`), so changing one silently breaks NPC dialogue gating.

**Approach:** Entities take their faction only from their `Entity` SO; the serialized faction on `FactionMember` becomes a field valid only on a GO without a `PersistentID` Entity (the Player). The `_selfFactionMember` fields become private `GetComponent` lookups, and `NPCPresence` reads the player's faction live from a new `TargetRegistry.Player`. This also prepares a future runtime "join a faction" (`SetFaction` + save — not in this spec).

## Boundaries & Constraints

**Always:** Keep `Player.prefab`'s `Faction_Player` value (use `FormerlySerializedAs`). Faction values read at query time — nobody caches a copy of the player's faction. Guard optional components (`PersistentID`, Entity) as today.

**Ask First:** Re-serializing any scene (e.g. `StartingTown.unity`) — large diffs.

**Never:** Add `SetFaction` / save support. Give the Player an `Entity` SO or `PersistentID`. Change `FactionSO` hostility data or `TargetRegistry` search semantics. Delete `FactionMember`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Entity | `PersistentID.Entity.Faction` set, serialized `_faction` null | Faction = Entity faction | N/A |
| Player | No Entity, `_faction` = `Faction_Player` | Faction = `Faction_Player`; registered as `TargetRegistry.Player` | N/A |
| Misconfigured entity | Entity present **and** `_faction` set | Faction = Entity faction (field ignored) | `GameLog.Error` once |
| No faction | Neither source | Member disabled (as today) | `GameLog.Error` (as today) |
| Player gone | Player member disabled/destroyed | `TargetRegistry.Player` null → NPC not considered hostile to player | none |
| Second stealth-target member registers | Player already set | Keeps the first | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/FactionMember.cs` -- faction resolution, registry handle; `_factionOverride` → `_faction`, resolved value → `_resolvedFaction`
- `Assets/_Game/Scripts/AI/TargetRegistry.cs` -- static member set; gains `Player`
- `Assets/_Game/Scripts/AI/EntityBrain.cs`, `EntityPerception.cs`, `EntityMeleeAttacker.cs` -- serialized `_selfFactionMember` (same-GO wiring)
- `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` -- duplicated `_playerFaction`
- `Assets/Tests/EditMode/TargetRegistryTests.cs` -- reflects `_factionOverride` / `_faction`
- Prefabs: `Entity_base`, `NPC_base Variant`, `Monster_DarknessSpider Variant`, `Player` -- hold now-dead serialized lines
- Docs: `Scripts/AI/CLAUDE.md`, `Prefabs/Entities/CLAUDE.md`, `Prefabs/Entities/Monsters/CLAUDE.md`

## Tasks & Acceptance

**Execution:**
- [x] `FactionMember.cs` -- rename serialized `_factionOverride` → `_faction` (`[FormerlySerializedAs("_factionOverride")]`, tooltip "Only for a GameObject without a PersistentID Entity (the Player). Entities use their Entity SO faction."); rename resolved field → `_resolvedFaction`; Entity faction wins when an Entity exists, error if `_faction` is also set; auto-resolve `_persistentID` via `GetComponent` if null; add `public bool IsPlayer` = an `IStealthTarget` component exists (raw, regardless of enabled) -- single source per GO
- [x] `TargetRegistry.cs` -- `public static FactionMember Player { get; private set; }`; `Register` sets it when `member.IsPlayer` (warn if a different one already set); `Unregister` clears it; reset on play -- live player-faction lookup
- [x] `EntityBrain.cs`, `EntityPerception.cs`, `EntityMeleeAttacker.cs` -- `_selfFactionMember` becomes private non-serialized, assigned by `GetComponent` in `Awake` (existing missing-component handling unchanged) -- remove wiring fields
- [x] `NPCPresence.cs` -- remove `_playerFaction` and its warning; `IsHostileToPlayer` uses `TargetRegistry.Player?.Faction` (Unity-null-safe) -- one player-faction source
- [x] `TargetRegistryTests.cs` -- update reflection names; add tests: player member sets/clears `TargetRegistry.Player`; a non-player member does not -- cover registry change
- [x] Prefabs listed in Code Map -- after compile, `AssetDatabase.ForceReserializeAssets` on those 4 prefabs only; verify `Player.prefab` keeps `_faction: Faction_Player` -- drop dead YAML lines
- [x] Docs listed in Code Map -- update `FactionMember` row, `NPCPresence` row, and remove `_selfFactionMember` wiring steps -- keep docs accurate

**Acceptance Criteria:**
- Given the project compiles, when inspecting an NPC / monster / Player prefab, then no `_selfFactionMember` or `_playerFaction` field is shown and the Player's `FactionMember` shows `Faction = Faction_Player`.
- Given play mode in StartingTown, when the player approaches a friendly NPC and the bandit, then the friendly NPC offers "Talk" and the alive bandit offers no prompt.
- Given play mode, when a hostile monster sees the player, then it detects and attacks as before.

## Design Notes

Player identity uses the presence of an `IStealthTarget` component (only `PlayerSneak` implements it), not its enabled state — a disabled `PlayerSneak` must not hide the player's faction from NPC dialogue gating.

## Verification

**Commands:**
- `run_tests` (EditMode) -- expected: all pass, incl. `TargetRegistryTests`, `FactionTests`
- `read_console` after refresh -- expected: no compile errors, no new faction errors

**Manual checks (if no CLI):**
- Play StartingTown: friendly NPC "Talk", bandit no prompt, bandit/spider still aggro the player.

## Suggested Review Order

**Single faction source per GameObject**

- Entity SO wins; serialized `_faction` only for the Player, misconfig logged once.
  [`FactionMember.cs:87`](../../Assets/_Game/Scripts/AI/FactionMember.cs#L87)

- Renamed field keeps the Player's value via `FormerlySerializedAs`.
  [`FactionMember.cs:23`](../../Assets/_Game/Scripts/AI/FactionMember.cs#L23)

- Player marker = an `IStealthTarget` component exists, even disabled.
  [`FactionMember.cs:57`](../../Assets/_Game/Scripts/AI/FactionMember.cs#L57)

**Live player faction lookup**

- New `TargetRegistry.Player`, set on register.
  [`TargetRegistry.cs:24`](../../Assets/_Game/Scripts/AI/TargetRegistry.cs#L24)

- Unregister promotes another player member (new Player enabled before old one left).
  [`TargetRegistry.cs:46`](../../Assets/_Game/Scripts/AI/TargetRegistry.cs#L46)

- NPC dialogue gating reads the player's faction live; warns once if no player.
  [`NPCPresence.cs:27`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L27)

**Wiring fields removed**

- Same-GO lookup replaces the serialized `_selfFactionMember` (same in perception / attacker).
  [`EntityBrain.cs:210`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L210)
  [`EntityPerception.cs:122`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L122)
  [`EntityMeleeAttacker.cs:74`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L74)

**Peripherals**

- Registry `Player` tests (set / clear / promote / keep-first / disabled sneak).
  [`TargetRegistryTests.cs:231`](../../Assets/Tests/EditMode/TargetRegistryTests.cs#L231)

- Player prefab keeps `Faction_Player` under the new field name.
  [`Player.prefab:793`](../../Assets/_Game/Prefabs/Player/Player.prefab#L793)
