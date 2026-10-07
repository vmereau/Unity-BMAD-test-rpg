# CLAUDE.md — Assets/_Game/Prefabs/Entities

> Loaded when Claude accesses entity prefabs. Covers `Entity_base.prefab`, the NPC variant, and the
> variant-editing gotchas. Monster specifics: `Monsters/CLAUDE.md`.

---

## Entity_base.prefab

Every entity is a variant of `Entity_base.prefab` (`Humanoids/NPC_base Variant`,
`Monsters/Monster_DarknessSpider Variant`). The base carries:

- **Root on Characters (Layer 6)** — `LockOnSystem._lockOnLayerMask` (`m_Bits: 64`) detects it.
- `EntityPresence` (`Game.World.IInteractable`) on the root + an **`InteractionCollider`** child
  (Layer 8 Interactable, trigger CapsuleCollider r 0.5 / h 2 / center y 1). Every entity is therefore
  hoverable (name/HP `EntityUI`) by default; `GetComponentInParent<IInteractable>()` climbs from the
  collider to the root.
- **`InventorySystem`** on the root (empty `_startingItems`) — every entity inherits exactly one. Per-
  instance stock (e.g. the shopkeeper) is a **scene `_startingItems` override**, not a prefab edit.
- `GoldSystem` is **not** on the base — only `NPC_base Variant` adds it (`_startingGold: 500`).
- Monsters inherit `EntityPresence` + collider unchanged → hover UI, no `[E]` until dead with loot.

---

## NPC_base Variant (`Humanoids/`)

All StartingTown NPCs are direct scene instances of this variant.

- **Replaces the inherited presence:** removes the base `EntityPresence` + base `InteractionCollider`
  and adds its own `NPCPresence : EntityPresence` + a hip-pinned `InteractionCollider` (referenced by
  `HumanoidAIAnimationDriver._transformsToPinToHips` — do **not** delete it). `NPCPresence._onLootRequested`
  must be wired on the variant itself (the base wiring is removed with the base component).
- **Two colliders, never collapse them:** `Hitbox` (Layer 7, trigger capsule — hurtbox for `WeaponHitbox`
  sweeps) and `InteractionCollider` (Layer 8, trigger — `InteractionSystem` / dialogue).
- **Attack wiring:**
  - Root: `EntityMeleeAttacker` (`_hitboxes = [{ "Unarmed", UnarmedHitbox }]`, `_selfFactionMember` = root
    `FactionMember`, `_animationDriver` = root `HumanoidAIAnimationDriver`); `EntityBrain._meleeAttacker` = it.
  - `Character` (Animator GO): `EntityAnimationEventReceiver` with `_attacker` assigned.
  - `.../WeaponSocket/UnarmedHitbox`: **active** but dormant (SphereCollider disabled, window closed).
    **No Rigidbody** — `HumanoidAIAnimationDriver` treats every Rigidbody under the Animator as ragdoll.
- NPC Entity SOs: `AttackRange` 1.5, `EngageStoppingDistance` 1.3, combo 1–3. Jabs land at 1.0 m; the
  uppercut (step 2) mostly whiffs vs a standing target (upper-body-masked clip — known limitation).

---

## Variant-Editing Gotchas

**Replacing an inherited component with a subclass.** Unity can't re-type an inherited component. The
variant must add the base component's `fileID` to `m_RemovedComponents` (so the root keeps exactly one
`IInteractable`) and, if the subclass needs its own copy of a base child, add that child's `fileID` to
`m_RemovedGameObjects` and keep its own added child.

**Moving a variant-added component down to the base.** (a) Delete the variant's added copy (otherwise the
root has two and `GetComponent<T>()` may resolve the wrong one); (b) **retarget scene `m_Modifications`**
that pointed at the old added-component `fileID`. The inherited component gets a fresh variant-local
stripped `fileID` (the moved `InventorySystem` became `76150843049530146`, neither the base nor the old
added id), so re-author overrides **through the Editor on the scene instance**. Capture the override data
before removing the added component — the Editor drops the orphaned override on reload. Hand-edited
fileIDs silently break overrides.
