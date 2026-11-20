# Fact objects — world objects driven by facts and the clock

> Read when touching `WorldFactTrigger(Rules)`, `FactRemovedObject(Rules)`, `ContainerTakeFact` or
> `ClockNightLight`. Fact types and keys: `Data/Facts/CLAUDE.md`.

---

## WorldFactTrigger

Sets `_fact` (WorldFact) once when the player (`TargetRegistry.Player`) stands inside any child `BoxCollider` (trigger,
Ignore Raycast; center / size only). Polled every `_pollInterval` with `RestrictedAreaRules.IsInsideBox`: the project
has no `OnTriggerEnter`. Idles while the fact is true.

Optional gates, all checked in the same poll as the box (spec-gated-world-fact-trigger):

- **required fact** (`_requiredFact`, any `Fact` via `GetFact`, null = no gate; spec-barn-guard-night-gate, e.g.
  `BarnDoorTrigger` needs `QuestFact_TitheBarnShortfall_Started`),
- **hour window** (`_useHours`, start incl. / end excl., wraps midnight, read from `GameClock.Active`; **no clock →
  never fires**, unlike `RestrictedArea`),
- **required NPC** (`_requiredNpc` scene `EntityBrain`: present = non-null + active in hierarchy, not `IsDead` /
  `IsUnconscious`, placed per `NpcPlacementGate`: `Anywhere`, `InRoutineBlock` = its `NpcRoutine` (same GO or a child)
  is not `IsSuspended` and `CurrentBlock.label` equals `_routineBlockLabel` (passes while still walking there; no
  block with that label → warning), `NearSpot` = flat distance to `_spot` ≤ `_spotRadius`, the one for "he is really
  there").

Misconfigured gate (no `EntityHealth` on the NPC, NearSpot without spot, InRoutineBlock without label / routine) →
error, disabled; hours start == end → warning (all day); no clock → one info log. Pure `WorldFactTriggerRules`
(`WorldFactTriggerRulesTests`). Indexed by the Quest Explorer as a setter (`_fact` only).

---

## FactRemovedObject / FactRemovedObjectRules

Hides `_target` while `_fact` (**any** `Fact`, stored or computed: World, Dialogue, Quest, `ElapsedFact`…) is true,
shows it again when false — only a target **it** hid (`_hiddenByMe`), so another system's hiding is never undone
(spec-fact-driven-npc-removal).

- Read through `WorldStateManager.GetFact` on `Start`, on `OnWorldFactChanged` (key match) and every `_pollInterval`
  (1 s): Quest / Skill / Stat facts raise no fact event. An `ElapsedFact` raises its event when its delay ends
  (`WorldStateManager.Update`), so a sleep skip lands on the next frame, and the `Start` read re-registers the deadline
  after a load. A matching fact event only zeroes the poll timer: the hide happens on the next `Update`, outside the
  dispatch, so the NPC's own `NPCMemoryComponent` still hears the change.
- Sits on a parent of the target, so it keeps listening. Facts load before the region, so `Start` covers a load.
  Without Core the target stays until one loads.
- **NPC target** (`EntityHealth` on `_target` itself — a wrapper with the entity below logs an error and is treated as
  a blocker; e.g. Venn / Pim leaving 12 h after a report): never touched once dead (`IsDead` or its `KilledFact` set:
  corpse and loot stay with `PersistentID` / `SaveableObject`), **waits** while `EntityBrain.IsInCombat`,
  `IsChasingThief` (Pursuing / Confronting: not combat, and the scold line has no requester to close) or
  `IsUnconscious` (re-checked by the poll), then `NPCPresence.RequestDialogueClose()` (closes its dialogue and its
  trade window) and `SetActive(false)`. `EntityBrain.OnDisable` gets it out of bed and frees the `Bed`;
  `SaveableObject.Restore` only reactivates killed entities, so it stays gone after a reload. Not a kill: no
  KilledFact, no XP.
- Pure `FactRemovedObjectRules.Decide` → `None / Hide / Show / Wait` (`FactRemovedObjectRulesTests`). Indexed by the
  Quest Explorer as a **reader** (`[Removes]`).
- Optional `_runtimeCarve`: a `NavMeshObstacle` left disabled in the scene and enabled in `Awake`, because a bake cuts
  an enabled carving obstacle into the NavMesh.

---

## ContainerTakeFact

Listens to `OnContainerItemTaken`, matches its sibling inventory and authored item allowlist, and sets its WorldFact
once. It never derives progress from inventory-change or container-open events, which also happen on restore or
unsuccessful attempts. Quest Explorer indexes these setters in loaded scenes.

---

## ClockNightLight

Fades a `Light` and its additive glow quads with `1 - DayNightMath.Daylight`, polled from `GameClock.Active` (region
scenes can't reference Core), so skips and loads follow on the next frame. Off without a clock (logs once), and picks
one up later. Hours come from `DayNightConfigSO`. Uses a `MaterialPropertyBlock`; the glow's quads stay
`enabled = false` in the scene, so render-mesh bakes skip them. Pure `NightLevel` (`ClockNightLightTests`).
