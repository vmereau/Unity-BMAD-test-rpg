# CLAUDE.md — Assets/_Game/Prefabs/Entities

> Loaded when Claude accesses entity prefabs. Covers `Entity_base.prefab`, the NPC variant, and the
> variant-editing gotchas. Monster specifics: `Monsters/CLAUDE.md`.

---

## Entity_base.prefab

Every entity is a variant of `Entity_base.prefab` (`Humanoids/NPC_base Variant`,
`Monsters/Monster_DarknessSpider Variant`). The base carries:

- **Root on Characters (Layer 6)**. `LockOnSystem._lockOnLayerMask` = Characters + CharacterHitbox (`m_Bits: 192`): humanoids
  have no collider on Layer 6 (their solid / ragdoll colliders are Default), so lock-on finds them through the `Hitbox`
  hurtbox (Layer 7). Only entities whose faction is hostile to the player's (`FactionSO.AreHostile`) are lockable.
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
- **Model = nested `Prefabs/Characters/Humanoid_BaseHero.prefab`** (child `Character`, spec-npc-base-hero-model;
  replaced the Mixamo `Idle.fbx` dummy). The model prefab owns the Animator (base-hero avatar, `Humanoid_Base`, root
  motion off), body `base_hero` + `Head` SMRs (player meshes / materials), the player's 5 sockets (same bones / offsets),
  the fist hitboxes `WeaponSocket/UnarmedHitbox` + `LeftHand/LeftWeaponSocket/UnarmedHitbox_L` (both active, dormant, no
  Rigidbody) and the 11-body ragdoll (layer 0). The variant adds only `EntityAnimationEventReceiver`
  on `Character` and the `Hitbox` child (L7). Placeholder until real NPC bodies exist. Every StartingTown NPC is an instance
  of this variant (Brann was re-instanced 2026-11-13 — keep NPCs as instances, never unpack them). Swapping the model: instantiate the new model prefab as `Character` in the variant's
  contents (`PrefabUtility.LoadPrefabContents`), re-add the receiver + `Hitbox`, retarget every root reference that
  pointed into the old `Character` (bridge `_animator`, driver `_transformsToPinToHips`, attacker `Unarmed`), delete the
  old one, then `PrefabUtility.RemoveUnusedOverrides` on the scene NPC instances. `NpcModelPrefabTests` guards it.
- **Face:** root `NpcHeadAppearance` (`_headRenderer` = `Character/Head`) swaps that SMR's mesh and material in `Awake` from
  `NPCEntity._head` (spec-npc-head-variants; empty = base head). Re-wire `_headRenderer` after a model swap.
- **Skin tone:** root `NpcSkinTone` (`_model` = `Character`, `_visuals` = root `EquipmentVisuals`) tones every masked
  `SG_SkinLit` material under `Character` in `Start` from `NPCEntity._skinTone` (spec-npc-skin-tones; empty = base skin),
  and re-tones a rebuilt armor visual (polls `EquipmentVisuals.ArmorVisual` in `LateUpdate`). Re-wire `_model` after a model swap.
- **Two colliders, never collapse them:** `Hitbox` (Layer 7, trigger capsule — hurtbox for `WeaponHitbox`
  sweeps) and `InteractionCollider` (Layer 8, trigger — `InteractionSystem` / dialogue).
- **Attack wiring:**
  - Root: `EntityMeleeAttacker` (`_hitboxes = [{ "Unarmed", UnarmedHitbox }, { "UnarmedLeft", UnarmedHitbox_L }]`, `_animationDriver` = root `HumanoidAIAnimationDriver`); `EntityBrain._meleeAttacker` = it.
    At runtime `NpcArmament` swaps the two fists ↔ `Weapon` (the equipped sword's hitbox) — never both registered.
  - Root equipment (spec-npc-equipment): `EquipmentSystem` (`_inventorySystem` = root inventory, `_npcOwned`),
    `EquipmentVisuals` (sockets / `base_hero` / `mixamorig:Hips` / `Character` Animator under the nested model,
    `_defaultAnimatorController` = `Humanoid_Base`, both GameEvent channels **empty**), `NpcArmament` (those + root
    inventory + `CombatConfig`). Re-wire all three after a model swap (`Scripts/AI/RANGED_EQUIPMENT.md` → NPC equipment).
  - `Character` (Animator GO): `EntityAnimationEventReceiver` with `_attacker` assigned. Root motion stays off (set in the
    model prefab, see above); `HumanoidAIAnimationDriver.Awake` forces it off + warns.
  - Both fist hitboxes (`UnarmedHitbox`, `UnarmedHitbox_L`): **active** but dormant (SphereCollider disabled, window closed).
    **No Rigidbody** — `HumanoidAIAnimationDriver` treats every Rigidbody under the Animator as ragdoll.
- NPC Entity SOs: `AttackRange` 1.5, `EngageStoppingDistance` 1.3, combo 1–3. Swings step forward through root
  `EntityAttackStep` (`NavMeshAgent.Move`, stops `_contactMargin` short of the target's body —
  `.claude/rules/attack-pipeline.md`); the reach figures below ignore that step (worst case). Fist hitbox centre at
  full extension, measured on the Player rig (same skeleton, 2026-11-16): lead jab 0.79 m forward, hook
  0.62–0.70 m (0.25–0.4 m to the right), uppercut 0.56–0.65 m. + radius 0.25 + `_reachPadding` 0.3 → every step reaches
  the player hurtbox (r 0.3) at the 1.3 m stopping distance; the uppercut has the least margin (~0.1 m). Not re-verified
  on an NPC in play. Re-measure (Animator `cullingMode = AlwaysAnimate` — a culled off-camera Animator doesn't pose
  bones) if clips or ranges change.

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
