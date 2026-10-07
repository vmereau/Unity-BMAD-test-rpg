# CLAUDE.md — Assets/_Game/Prefabs/Items

> Loaded when Claude accesses item prefabs. Covers world-item (dropped / pickup) prefabs.
> Weapons have their own two-prefab convention: `Weapons/CLAUDE.md`. Item data: `ScriptableObjects/Items/CLAUDE.md`.

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
