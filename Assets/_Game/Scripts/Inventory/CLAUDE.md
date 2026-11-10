# CLAUDE.md — Assets/_Game/Scripts/Inventory

> Loaded when Claude accesses files in this folder. Inventory, equipment, action bar,
> and world pickups. Namespace: `Game.Inventory`.

---

## What's here

| File | Role |
|------|------|
| `InventorySystem` | Stacked inventory. Backed by an immutable `InventorySlot` struct (item + count) — mutate by replacing the slot in the list, never in place. Hosted on `Player.prefab` **and** the `Entity_base.prefab` root (every NPC and monster — trade stock, corpse loot); per-instance stock is a scene `_startingItems` override (`Prefabs/Entities/CLAUDE.md`). Optional `_onInventoryChanged` (`OnPlayerInventoryChanged`) is raised after every successful Add / Remove / Decrement / Move / Restore (not for `Awake` starting items) — **wired on `Player.prefab` only**; NPC / monster inventories stay silent. |
| `EquipmentSystem` | Equipped items per `EquipmentSlot`. `GetEquipped(slot)` is the query other systems use (e.g. `PlayerCombat` casts the Weapon slot to `WeaponSO`). |
| `EquipmentVisuals` | Spawns/reparents equipped-item visuals on the player rig. Raises `OnVisualsRefreshed` SO at the **end** of `Refresh()`; owns combat-state socket swap via `SetCombatState()`. Applies the weapon archetype's `Drawn`/`Sheathed` poses (`ApplyArchetypePoses`), picks the drawn hand (`ResolveDrawnSocket`: `_weaponSocket` right or `_leftWeaponSocket`, right fallback + one warning) and the sheath socket (`ResolveSheathSocket`: `_undrawnWeaponSocket` hip or `_backWeaponSocket` on Spine2, hip fallback + one warning). Null archetype → prefab poses, hip. Animator override comes from `WeaponSO.ResolvedAnimatorOverride`. **Armor:** instantiates the `ArmorSO.equipVisualPrefab` (`SkinnedBodyPart`) under `Character`, binds it to `_skeletonRoot` (Hips) by bone name and disables `_bodyRenderer` (the naked body SMR); the Head SMR is never touched. No prefab / missing bones → body stays visible (Info / Warn). Re-wire with `Game/Dev/Wire EquipmentVisuals on Player Prefab`. |
| `SkinnedBodyPart` + `SkinnedMeshRebinder` | Skeleton-less skinned visual (armor): the prefab stores bone names only; `SkinnedMeshRebinder` (static, tested) resolves them on the wearer's skeleton — all-or-nothing, a partial bind would tear the mesh. Meshes must be baked into the base body's space first (`Editor/Characters/SkinnedMeshBaker`, `Art/Characters/Humanoids/CLAUDE.md`). |
| `TwoHandGripConstraint` + `TwoHandGripMath` | Two-handed weapons (`archetype.twoHandedGrip`): LateUpdate (order 250) re-aims the drawn weapon's `Drawn` child through both hand sockets every frame. Reads `EquipmentVisuals.CurrentArchetype / IsDrawn / DrawnSocket / LeftHandSocket` (read-only accessors). Static `Apply` is shared with the Weapon Pose Tuner preview. Details: `Prefabs/Items/Weapons/CLAUDE.md` |
| `ActionBarSystem` | Action-bar slot bindings (`ActionBarSlotData`). |
| `ItemPickup` | `IInteractable` world item — adds to inventory on interact. |

---

## Rules

- **Gold loot:** `GoldItemSO` stays stacked in containers/NPC inventories. Only the player has `_collectGoldAsCurrency` and `OnGoldLootRequested` wired: `AddItem` raises an acknowledged `GoldLootRequest` addressed to its own GameObject; its sibling `GoldSystem` credits one gold and accepts it. Missing/disabled receiver or overflow rejects the addition, preserving source loot. Starting currency belongs on `GoldSystem`; starting item initialization and slot restoration never pay out. `ItemPickup` consumes itself only after a successful addition.
- **Cross-system events use `GameEventSO<T>` only** — no plain C# events across `Game.Inventory` → `Game.Combat`/UI boundaries (project-context.md).
- `EquipmentVisuals` raises `OnVisualsRefreshed` **after** `_weaponVisual` is assigned, so `ActiveWeaponGO` is valid in listeners. `PlayerCombat` binds its hitbox off this SO, NOT `OnEquipmentChanged` (component-order race) — details in `Scripts/Combat/CLAUDE.md`.
- `GameEventSO<T>.Raise()` iterates listeners **last-added-first** — never rely on listener ordering for correctness.
