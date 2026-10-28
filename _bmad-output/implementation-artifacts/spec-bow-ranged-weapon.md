---
title: 'Bow ranged weapon with arrow ammo and projectiles'
type: 'feature'
created: '2026-10-28'
status: 'done'
baseline_commit: '55f296c57556b777fe50fe48c7d26cd39e14e875'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game only has melee weapons. The player needs a bow that can be drawn/sheathed like any weapon and fires arrows consumed from the inventory.

**Approach:** Add a `BowSO : WeaponSO` (ammo item, projectile prefab, speed, cooldown, lifetime). When a bow is equipped and drawn, `PlayerCombat` bypasses the melee combo: an attack press consumes one arrow and stamina, then spawns an `ArrowProjectile` aimed at the screen-centre point. No firing animation for now; simple behaviour, improved later.

## Boundaries & Constraints

**Always:**
- Assets: bow item + `_World`/`_Visual` prefabs from `Assets/Hivemind/HouseForge_01/HDRP (Default)/Art/Prefabs/Props/SM_WeaponBow_01a.prefab`; arrow item (stackable, `maxStacks` 99) + world prefab + projectile prefab from `SM_Arrow_01a.prefab`.
- Bow follows the weapon two-prefab / `Drawn`/`Sheathed` / normalized-mesh conventions, new `Archetype_Bow` sheathing on the **Back** socket (first-guess poses). Bow `Drawn` has **no** `WeaponHitbox`.
- Fire gate order: `CanAttack()` (drawn) → cooldown elapsed → stamina `HasEnough(attackStaminaCost)` → arrow in inventory → consume arrow + stamina → fire. No arrow → raise `OnCombatNotification("Out of arrows")`, no stamina spent, no projectile.
- Aim: ray from camera viewport centre (0.5, 0.5) against Default + CharacterHitbox, skipping the player's own colliders; no hit → point at max range along the ray. Projectile launched from the equipped bow towards that point, straight line, constant speed, no gravity.
- Damage (computed at fire time) = `CombatConfigSO.attackDamage` + max(0, (Dexterity − baseDexterity) × `damagePerDexterity`) + bow `damageBonus`. No Strength, no Power Strike.
- Hit on a character: `TryReceiveHit(player)` → `TakeDamage` only on `NotBlocked`, sneak-attack multiplier applied like melee; arrow destroyed.
- Hit on world geometry: arrow sticks at the impact pose as a pickable `Item_Arrow` world item (kinematic, Interactable, `ItemPickup`).
- Projectile sweeps each frame (`Physics.SphereCastNonAlloc`/`RaycastNonAlloc`, explicit layer mask, triggers collide); destroyed after `projectileLifetime` if it hits nothing.
- Stock: merchant (`NPC_Merchant`) sells 1 bow + 50 arrows; the chest beside him holds 1 bow + 15 arrows.

**Ask First:** Changing the melee combo flow or animator controller; adding any new input action.

**Never:** Bow animations, aiming camera/zoom, arrow gravity, pooling, arrow save persistence, AI archers, damage via `WeaponHitbox`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fire | Bow drawn, ≥1 arrow, stamina ok, cooldown elapsed | 1 arrow + stamina consumed, projectile flies to crosshair | N/A |
| No arrows | Bow drawn, 0 arrows | Toast "Out of arrows", nothing consumed | Info log |
| Sheathed | Bow sheathed, click | Nothing happens (same as melee) | N/A |
| Spam | Click during cooldown | Ignored, nothing consumed | N/A |
| Low stamina | Stamina < cost | Nothing consumed, no toast | Warn log |
| Hit entity | Arrow overlaps enemy hurtbox | TryReceiveHit → damage on NotBlocked, arrow destroyed | Dead target ignored |
| Hit wall | Arrow hits Default geometry | Arrow sticks, pickable as 1 `Item_Arrow` | N/A |
| Miss | Nothing hit within lifetime | Projectile destroyed | N/A |
| Misconfig | BowSO missing ammo/projectile | No fire | GameLog.Error once per press |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- attack input entry (`TryAttack`), `_currentWeaponSO` cache, `ComputeEffectiveDamage`, `OnWeaponHit` hit-resolution template
- `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` / `Weapons/SwordSO.cs` -- weapon SO base / concrete pattern
- `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` -- `baseDexterity`, `damagePerStrength` (add `damagePerDexterity`)
- `Assets/_Game/Scripts/Player/PlayerStats.cs` -- `Dexterity`
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` -- `CountOf`, `TryConsume`
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- `ActiveWeaponGO` (launch origin)
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- per-channel toast handlers (`_onSleepRefused` pattern)
- `Assets/_Game/Editor/WeaponCreatorWindow.cs`, `WeaponModelUtility.cs` -- prefab build recipe (strip/normalize) to replicate via `execute_code`
- `Assets/_Game/Editor/NPCs/NpcShopEditor.cs` -- `SetStock(npc, item, count)` for merchant stock
- `Assets/_Game/Scripts/World/ContainerSystem.cs` -- chest contents
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- category per item type

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Items/Weapons/BowSO.cs` -- new `BowSO : WeaponSO` (`ammoItem`, `projectilePrefab`, `projectileSpeed`=40, `fireCooldown`=0.8, `projectileLifetime`=5, `maxAimDistance`=100) -- bow data
- [x] `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` -- add `damagePerDexterity` = 2 -- Dex scaling
- [x] `Assets/_Game/Scripts/Combat/RangedDamage.cs` -- pure static `Compute(baseDamage, dex, baseDex, perDex, bonus)` -- testable formula
- [x] `Assets/_Game/Scripts/Combat/ArrowProjectile.cs` -- `Launch(owner, direction, speed, lifetime, damage, onHit/sneak config, stuckPickupPrefab)`; per-frame sweep, owner skip, IDamageable resolution, stick-as-pickup, lifetime destroy -- projectile
- [x] `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- branch in `TryAttack` when `_currentWeaponSO is BowSO`; `TryFireBow()` with gate order above; refs `_inventory` (InventorySystem), `_onCombatNotification`; cache `Camera.main` in Awake -- fire logic
- [x] `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` + `Data/Events/OnCombatNotification.asset` -- new String channel → `Show(msg)` -- toast
- [x] `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` (+ tests) -- "Bow" category if weapon categories are per type -- detail panel (no change: categories are not per weapon type; BowSO inherits the WeaponSO "Weapon" category + Damage row)
- [x] Unity assets via MCP -- `Archetype_Bow`, `Data/Items/Weapons/Bow/Weapon_HuntingBow.asset`, `Prefabs/Items/Weapons/Bow/HuntingBow/{_Visual,_World}`, `Data/Items/Item_Arrow.asset`, `Prefabs/Items/Item_Arrow.prefab`, `Prefabs/Combat/Projectile_Arrow.prefab`, icons, ItemCatalog sync; wire Player prefab refs + Core toast UI -- content
- [x] `StartingTown.unity` -- merchant stock (bow ×1, arrows ×50), chest beside merchant (bow ×1, arrows ×15) -- availability
- [x] `Assets/Tests/EditMode/RangedDamageTests.cs` -- formula: base only, Dex above/below base, bonus -- tests
- [x] Docs: `Scripts/Combat/CLAUDE.md` (ranged section), `ScriptableObjects/Items/CLAUDE.md` (BowSO in hierarchy), `Prefabs/Items/Weapons/CLAUDE.md` (bow mesh frame, archetype) -- knowledge

**Acceptance Criteria:**
- Given the bow equipped and sheathed, when R is pressed, then the bow moves from the back to the hand (and back on second press).
- Given the bow drawn and arrows in inventory, when the player clicks, then the arrow count drops by 1 and an arrow flies toward the crosshair.
- Given a monster under the crosshair, when an arrow hits it, then it loses `attackDamage + Dex bonus + bow damageBonus` HP (×sneak multiplier if unaware).
- Given an arrow stuck in a wall, when the player interacts with it, then 1 arrow is added to the inventory.
- Given the merchant trade window, then a bow and arrows are listed; given the chest beside him, then it contains a bow and arrows.

## Design Notes

Stuck arrow = instantiate `ammoItem.worldItemPrefab` at the impact pose with `Rigidbody.isKinematic = true`, then destroy the projectile — reuses the existing pickup pipeline instead of a second interactable. Runtime-spawned pickups need no `SaveableObject` (not saved, like dropped items).

The projectile lives in `Game.Combat` and resolves hits itself (same `TryReceiveHit` → `TakeDamage` rule as `OnWeaponHit`); damage is snapshotted at fire time so a mid-flight unequip doesn't change it.

## Verification

**Commands:**
- Unity EditMode tests (`run_tests`, `RangedDamageTests` + `ItemDetailFormatterTests`) -- expected: all pass
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play: buy/loot bow, equip, R to draw, fire at a monster and a wall, pick the stuck arrow up, empty quiver → "Out of arrows" toast.

## Suggested Review Order

**Fire flow (entry point)**

- Bow routing reads the equipment slot, bypassing combo and melee hitbox entirely
  [`PlayerCombat.cs:263`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L263)

- Gate order — ammo checked before stamina spent; refused shots cost nothing
  [`PlayerCombat.cs:474`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L474)

- Spawns projectile, snapshots damage into the hit closure
  [`PlayerCombat.cs:512`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L512)

- Crosshair ray skipping player, camera-side hits, and non-hurtbox triggers
  [`PlayerCombat.cs:542`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L542)

**Hit resolution**

- Melee and arrows share TryReceiveHit → TakeDamage + sneak multiplier
  [`PlayerCombat.cs:444`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L444)

- Closest valid hit from unsorted sweep; triggers only on CharacterHitbox
  [`ArrowProjectile.cs:96`](../../Assets/_Game/Scripts/Combat/ArrowProjectile.cs#L96)

- World hit becomes a kinematic pickup moved into the region scene
  [`ArrowProjectile.cs:138`](../../Assets/_Game/Scripts/Combat/ArrowProjectile.cs#L138)

- Per-frame sweep and lifetime (also for never-launched instances)
  [`ArrowProjectile.cs:67`](../../Assets/_Game/Scripts/Combat/ArrowProjectile.cs#L67)

**Data & damage**

- Bow data: ammo, projectile, speed, cooldown, lifetime, aim range
  [`BowSO.cs:10`](../../Assets/_Game/ScriptableObjects/Items/Weapons/BowSO.cs#L10)

- Pure formula: base + Dex bonus + bow bonus, clamped ≥ 0
  [`RangedDamage.cs:13`](../../Assets/_Game/Scripts/Combat/RangedDamage.cs#L13)

- New Dex scaling value
  [`ProgressionConfigSO.cs:31`](../../Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs#L31)

**Toast & peripherals**

- New verbatim toast channel
  [`NotificationToastUI.cs:110`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L110)

- Layer-name constant replacing string literals
  [`GameConstants.cs:37`](../../Assets/_Game/Scripts/Core/GameConstants.cs#L37)

- Formula tests
  [`RangedDamageTests.cs:1`](../../Assets/Tests/EditMode/RangedDamageTests.cs#L1)
