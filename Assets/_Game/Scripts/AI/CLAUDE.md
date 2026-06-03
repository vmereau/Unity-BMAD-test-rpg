# CLAUDE.md — Assets/_Game/Scripts/AI

> Loaded when Claude accesses files in this folder. Entity brains, health, faction
> targeting, and NPC presence/memory components. Namespace: `Game.AI`.

---

## What's here

| File | Role |
|------|------|
| `EntityBrain` | Generic state machine: Idle → Patrolling → (Engaging → Attacking) → Dead. Implements `ICombatStateProvider`. Finds targets via `TargetRegistry`. |
| `EntityHealth` | Generic health for any entity (enemy/NPC/neutral). Implements `IDamageable`. On death: stops NavMeshAgent (optional), calls `PersistentID.RegisterDeath()`, triggers death anim. Body stays in scene (ragdoll, no `SetActive(false)`). |
| `FactionMember` | Tags a GO as a targetable faction participant. Self-registers with `TargetRegistry` on enable. Faction from `PersistentID.Entity.Faction`, or `_factionOverride` (Player uses override — no PersistentID). |
| `TargetRegistry` | **Static** runtime registry of live `FactionMember`s. Query this instead of `FindGameObjectWithTag`. Reset on play-mode enter via `SubsystemRegistration`. |
| `ICombatStateProvider` | Read-only "is this entity in combat?". Implemented by `EntityBrain`, polled by `NPCPresence`. |
| `NPCPresence` | `: EntityPresence` (base lives in `Game.World`, on `Entity_base.prefab`). Overrides the dialogue-specific `Interact()` (raises `_onDialogueRequested`), `InteractPrompt` ("Talk") and `CanInteract` (`IsAliveAndOutOfCombat`). Name-tag + alive/combat gating helper are inherited from `EntityPresence`. |
| `NPCMemoryComponent` / `NPCDialogueGraphComponent` | Hold an NPC's active memory set and resolve available dialogue start nodes / choices. |
| `SMB_DeathState` | StateMachineBehaviour on the death animator state. |

---

## Rules

- **No `event Action` across system boundaries** (project-context.md). `ICombatStateProvider` is polled via `GetComponent`, not subscribed. Use a `GameEventSO<T>` channel if push is ever needed.
- `EntityHealth.MaxHealth` is driven by `PersistentID.Entity.BaseHealth` — falls back to `100f` when no `PersistentID`/Entity is assigned.
- `PersistentID`, `AIAnimationDriver`, and `NavMeshAgent` are all **optional** on an entity — guard every access (`TryGetComponent` / null check).

> AI animation polymorphism (`AIAnimationDriver` base, Brain/Health → Driver → Bridge contract) → `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`
