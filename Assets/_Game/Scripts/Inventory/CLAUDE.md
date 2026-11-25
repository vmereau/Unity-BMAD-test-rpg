# CLAUDE.md — Assets/_Game/Scripts/Inventory

> Loaded when Claude accesses files in this folder. Inventory, equipment, action bar,
> and world pickups. Namespace: `Game.Inventory`.

---

## What's here

| File | Role |
|------|------|
| `InventorySystem` | Stacked inventory: `AddItem` (stacks stackable items), `RemoveItem` (removes the whole slot), `DecrementStack` (removes one unit), `MoveItem`. Backed by an immutable `InventorySlot` struct (item + count) — mutate by replacing the slot in the list, never in place. Hosted on `Player.prefab` **and** the `Entity_base.prefab` root (every NPC and monster — trade stock, corpse loot); per-instance stock is a scene `_startingItems` override (`Prefabs/Entities/CLAUDE.md`). Optional `_onInventoryChanged` (`OnPlayerInventoryChanged`) is raised after every successful Add / Remove / Decrement / Move / Restore (not for `Awake` starting items) — **wired on `Player.prefab` only**; NPC / monster inventories stay silent. |
| `EquipmentSystem` | Equipped items per `EquipmentSlot`. `GetEquipped(slot)` is the query other systems use (e.g. `PlayerCombat` casts the Weapon slot to `WeaponSO`); `EquippedShield` = the Offhand `ShieldSO`. Slot rules live in the pure `EquipmentRules` (`EquipmentSystemTests`). |
| `EquipmentRules` | Static, tested (spec-shield-offhand-blocking): `TryGetTargetSlot` (weapons → Weapon, `ShieldSO` / `TorchSO` → **Offhand**, rings → free ring, armor → its slot; armor authored for Weapon / Offhand is rejected), `TryGetConflictingSlot` (a `WeaponSO.OccupiesOffhand` weapon — two-handed-grip archetype or any `BowSO` — and a shield push each other back to the inventory, in one `Equip` / one `_onEquipmentChanged`), `FitsSlot` + `HasOffhandConflict` (a restored save with both keeps the weapon, drops the shield, warns). A torch is exempt from both conflicts (`CoexistsWithTwoHands`): it stays equipped next to a two-hander / bow. |
| `EquipmentVisuals` | Spawns / reparents equipped-item visuals (weapon, armor, shield) on the player rig — see EquipmentVisuals below. `OffhandArmPose` (read-only) = the Offhand item's `offhandArmPose` (a torch's only while `TorchInHand`), polled by `HandPoseDriver` for the `LeftArm` layer. |
| `TorchLight` | On the torch visual's `Drawn/Flame`: `SetLit(bool)` plays / stops-and-clears the flame particles and enables the point light; flickers intensity (Perlin, ±`_flickerAmount`) and jitters the light 1 cm while lit; `_castShadows` (soft, default on). Lazy init: `SetLit` may run before `Awake`. |
| `SkinnedBodyPart` + `SkinnedMeshRebinder` | Skeleton-less skinned visual (armor): the prefab stores bone names only; `SkinnedMeshRebinder` (static, tested) resolves them on the wearer's skeleton — all-or-nothing, a partial bind would tear the mesh. Meshes must be baked into the base body's space first (`Editor/Characters/SkinnedMeshBaker`, `Art/Characters/Humanoids/PIPELINE.md` → Head / body split). |
| `TwoHandGripConstraint` + `TwoHandGripMath` | Two-handed weapons (`archetype.twoHandedGrip`): LateUpdate (order 250) re-aims the drawn weapon's `Drawn` child through both hand sockets every frame. Reads `EquipmentVisuals.CurrentArchetype / IsDrawn / DrawnSocket / LeftHandSocket` (read-only accessors). Static `Apply` is shared with the Weapon Pose Tuner preview. Details: TwoHandGripConstraint below |
| `ActionBarSystem` | Action-bar slot bindings (`ActionBarSlotData`). Using a slot: `UsableItemSO` → `OnUse`; `EquipableItemSO` → equip in place of the current item (already equipped → nothing; gated by `PlayerStateManager.CanChangeEquipment()`). Drawn + weapon / shield → raises `OnEquipSwapRequested` (`GameEventSO_Item`) and `PlayerCombat` sheathes → equips → draws (spec-action-bar-equip-swap). `ValidateSlots` **keeps** a slot whose item is equipped (it left the inventory) with `InventoryIndex = EQUIPPED_INDEX` (-1); `IsSlotEquipped` drives the bar highlight (`ActionBarSlotUI._equippedColor`, refreshed on `OnEquipmentChanged`). |
| `ItemPickup` | `IInteractable` world item — calls `InventorySystem.AddItem(_item)` on interact and destroys itself (only after a successful addition). |

---

## EquipmentVisuals

Spawns/reparents equipped-item visuals on the player rig. Raises `OnVisualsRefreshed` SO at the **end** of `Refresh()`; owns combat-state socket swap via `SetCombatState()`.

- **Weapon:** Applies the weapon archetype's `Drawn`/`Sheathed` poses (`ApplyArchetypePoses`), picks the drawn hand (`ResolveDrawnSocket`: `_weaponSocket` right or `_leftWeaponSocket`, right fallback + one warning) and the sheath socket (`ResolveSheathSocket`: `_undrawnWeaponSocket` hip or `_backWeaponSocket` on Spine2, hip fallback + one warning). Null archetype → prefab poses, hip. Animator override comes from `WeaponSO.ResolvedAnimatorOverride`.
- **Armor:** instantiates the `ArmorSO.equipVisualPrefab` (`SkinnedBodyPart`) under `Character`, binds it to `_skeletonRoot` (Hips) by bone name and disables `_bodyRenderer` (the naked body SMR); the Head SMR is never touched. No prefab / missing bones → body stays visible (Info / Warn). `ArmorVisual` exposes the current armor GO (polled by `NpcSkinTone`). Re-wire with `Game/Dev/Wire EquipmentVisuals on Player Prefab`.
- **Shield** (`RefreshShield` / `PlaceShield`, after the weapon): the `ShieldSO._Visual` goes to the hand opposite the drawn weapon (`ResolveOffhandSocket`) while `ShieldInHand` (shield + drawn weapon visual), else to `BackWeaponSocket` (hip fallback + one warning); its `Drawn` / `Sheathed` children take `ShieldSO.heldPose` / `backPose`. `SetCombatState` re-places it. Fists / sheathed / no weapon = back.
- **Torch** (`RefreshTorch` / `PlaceTorch`, spec-torch-offhand-item): the `TorchSO._Visual` sits in `LeftWeaponSocket` (`Drawn`
  child, `heldPose`) whenever `ShouldHoldTorch` — equipped, except while a two-hander / bow is drawn. Stowed, the whole visual
  is **hidden** (`SetActive(false)`, parented to the hip so `Sheathed` + `stowPose` can be shown later). `TorchLit` = held and
  not doused; `SetTorchDoused` is called by `PlayerStateManager.SetSwimming` (and the death reset), so douse always follows
  `IsSwimming`. Activate the visual **before** `TorchLight.SetLit`: particles only play on an active hierarchy.

---

## TwoHandGripConstraint

Two-handed weapon prefabs / archetypes: `Prefabs/Items/Weapons/CLAUDE.md` → Weapon Archetypes.

- **Two-hand grip (`twoHandedGrip`, spec `spec-two-hand-grip-constraint.md`):** the Mixamo 2H clips don't keep a constant
  hand relationship (hand line drifts ~50° within one swing; `2hand blocking` holds the other way round), so no rigid
  `drawnPose` fits. `TwoHandGripConstraint` (Player root, `[DefaultExecutionOrder(250)]`, after `BowAimCorrection` 200)
  re-aims `Drawn` every frame while drawn: grip on `DrawnSocket`, blade (+Y) = left socket → right socket line, edge (+Z) =
  `drawnPose`'s edge projected ⊥ blade then rolled `gripRoll`°, moved `gripSlide` m along the blade (`TwoHandGripMath`,
  tested). The result is **blended** with the rigid `drawnPose` by `GripWeight`: 1 for hands 0.10–0.40 m apart (measured
  clips: 0.19–0.35), easing to 0 as the left hand lets go (0.55 m — sheathe, knockdown, death) or the solve degenerates
  (hands < 5 cm, edge reference near the blade), so the hand-off never pops. `drawnPose` matters for its **edge
  direction** and as that rigid pose. No arm IK: the clips place the hands. `drawnHand = Left` + `twoHandedGrip` is
  unsupported (warns once). `WeaponHitbox` runs at order **300** so its sweep sees the re-aimed blade.

---

## Rules

- **Gold loot:** `GoldItemSO : ItemSO` represents one gold coin (`GoldValue = 1`); `Item_Gold.asset` is stackable for container/NPC stock, is not manually usable or stored in player slots, detail category Currency, filter Misc; `Item_Gold.prefab` is a coin-shaped placeholder with normal pickup physics; icon prompt `docs/Gold-Icon-Prompt.md`. `GoldItemSO` stays stacked in containers/NPC inventories. Only the player has `_collectGoldAsCurrency` and `OnGoldLootRequested` wired: `AddItem` raises an acknowledged `GoldLootRequest` addressed to its own GameObject; its sibling `GoldSystem` credits one gold and accepts it. Missing/disabled receiver or overflow rejects the addition, preserving source loot. Starting currency belongs on `GoldSystem`; starting item initialization and slot restoration never pay out. `ItemPickup` consumes itself only after a successful addition.
- **Cross-system events use `GameEventSO<T>` only** — no plain C# events across `Game.Inventory` → `Game.Combat`/UI boundaries (project-context.md).
- `EquipmentVisuals` raises `OnVisualsRefreshed` **after** `_weaponVisual` is assigned, so `ActiveWeaponGO` is valid in listeners. `PlayerCombat` binds its hitbox off this SO, NOT `OnEquipmentChanged` (component-order race) — details in `Scripts/Combat/CLAUDE.md`.
- `GameEventSO<T>.Raise()` iterates listeners **last-added-first** — never rely on listener ordering for correctness.
