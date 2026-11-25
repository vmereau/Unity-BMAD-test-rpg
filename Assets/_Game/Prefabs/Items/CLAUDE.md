# CLAUDE.md — Assets/_Game/Prefabs/Items

> Loaded when Claude accesses item prefabs. Covers world-item (dropped / pickup) prefabs.
> Weapons have their own two-prefab convention: `Weapons/CLAUDE.md`. Shields (`Shields/<Name>/`) use the same `_World` /
> `_Visual` split without a hitbox — see Shield prefabs below. Item data (incl. `ShieldSO` fields): `ScriptableObjects/Items/CLAUDE.md`.

---

## World Item Prefabs (`ItemSO.worldItemPrefab`)

A prefab assigned to `ItemSO.worldItemPrefab` **must** have, on its root:

- a **Rigidbody** — `InventoryUI.DropItem()` calls `AddForce` right after `Instantiate`; missing → NRE;
- **Layer Interactable (8)** with a collider — `InteractionSystem` raycasts only Layer 8;
- **`ItemPickup`** with `_item` pre-assigned;
- **`InteractionHighlight`** (`_targets` empty = outline all child meshes) — otherwise the focused item gets
  a prompt card but no outline.

**Drop:** `InventoryUI.DropItem` instantiates the prefab 1.5 m in front of / 0.5 m above the player and
applies `AddForce(forward * 2f + Vector3.up * 1f, ForceMode.Impulse)`. An item without `worldItemPrefab`
is not droppable (the drop is skipped with a `GameLog` warning).

---

## Torch prefabs (`Torch/`, spec-torch-offhand-item)

`Torch_World` (builder output, moved here: `Model` laid on its side, rot (0, 0, 90), box fitted) and `Torch_Visual`
(root → `Drawn` / `Sheathed` → `Mesh`; `Drawn/Flame` = `TorchLight` + `FireFX` (`Multistory Dungeons 2` `Torch_Fire_FX_01`,
URP particles, scale 0.4) + point `Light` (`#E0A35A`, 4, 8 m, soft shadows)). `Mesh` frame: **shaft +Y, head up, grip at the
origin** (Meshy FBX 0.01 units at scale 55 = 0.55 m, `Mesh` pos y +0.12). No collider on the visual.

## Shield prefabs (`Shields/<Name>/`)

`<Name>_World` (Rigidbody, BoxCollider, `ItemPickup`, `InteractionHighlight`, layer 8)
and `<Name>_Visual` (root → `Drawn` / `Sheathed` → `Mesh`, **no hitbox**). The `Mesh` child normalizes the art: **front +Z, up +Y,
grip (back of the handle) at the origin** (Hivemind shields face +X: `Mesh` rot (0, −90, 0), pos = −(rot × grip)).

`ShieldSO.backPose` (`Sheathed` pose on `BackWeaponSocket`) offsets: bind pose: face backward, world-up, Kite (0, −0.47, −0.016)
puts its top at the neck, Round (0, −0.08, 0.018); both clear the player and `NPC_base` bodies by ~1.5–2 cm; meshes at 0.8 scale.
