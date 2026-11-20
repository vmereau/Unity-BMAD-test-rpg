# Weapon Tools — Weapon Creator and Weapon Pose Tuner

> Read when creating a new weapon or tuning a weapon's grip / archetype poses. Prefab conventions, normalized frame and
> archetypes: `Prefabs/Items/Weapons/CLAUDE.md`.

---

## Weapon Creator (`Tools/Items/Weapon Creator`)

Use it for every new weapon instead of hand-building prefabs. Inputs: name, C# type, base model, **archetype**
(required), **grip point** (model local), **blade axis**, **edge axis** (must be on different axes). It:

- strips every `Collider`, `Rigidbody`, `LODGroup` and `_LOD1+` child from the model (keeps LOD0),
- builds `_Visual` (no Rigidbody; root → `Drawn` [**disabled** `BoxCollider` fitted to the renderers + `WeaponHitbox`]
  → `Mesh`; `Sheathed` → `Mesh`), both `Mesh` children normalized, `Drawn`/`Sheathed` left at identity,
- builds `_World` (un-normalized model, root `BoxCollider` fitted to **all** renderers),
- writes `Data/Items/Weapons/{Family}/Weapon_{Name}.asset` and `Prefabs/Items/Weapons/{Family}/{Name}/`
  (`{Family}` = archetype name without `Archetype_`), sets `archetype`, leaves `comboSteps = 0` /
  `animatorOverrideController = null` (inherit) unless edited, and generates the icon.

**Always verify grip and edge direction afterwards** with the Weapon Pose Tuner (below). The fitted hitbox
covers the whole mesh — shrink it by hand for head-only hitboxes (e.g. the Axe keeps a head-only box).

---

## Weapon Pose Tuner (`Tools/Combat/Weapon Pose Tuner`)

`Editor/WeaponPoseTunerWindow.cs`, edit mode only. Select the Player (scene or Prefab Mode) → **Use Selection**, pick a
`WeaponSO`, mode **Drawn / Sheathed**, a clip (the weapon's override clips, or any clip in **Clip Override**), scrub frames.
The `_Visual` is previewed (`HideFlags.DontSave`) on the socket `EquipmentVisuals` would use, with the archetype poses; in
Drawn mode the `Drawn` hitbox box is drawn in orange.

- **Select Pose Node** → move / rotate `Drawn` / `Sheathed` → **Save Pose to Archetype** (whole family; saved in socket
  space from world, so moving the preview root is harmless; Undo-able).
- **Select Grip (Mesh)** → **Save Grip to Weapon** writes that local pose to **both** `Drawn/Mesh` and `Sheathed/Mesh` of
  the `_Visual` prefab (saved in place, GUID kept; **not Undo-able**, refused while that prefab is open in Prefab Mode).
  **Revert** rebuilds the preview from the assets.
- Save Pose is disabled while the preview sits on a fallback socket (Left / Back asked but unassigned). The sampled pose
  is stopped and the rig restored before any scene / prefab save, then re-sampled. Edit mode only.
- **`AnimationMode.StopAnimationMode` does not revert bones posed by a Humanoid clip** — the tuner snapshots the rig's
  local transforms before sampling and restores them itself.
- **Two-hand grip archetype, Drawn mode:** the preview is re-solved like the runtime (after each sample and every
  repaint); Save Pose / Select Pose Node are disabled — edit `gripRoll` / `gripSlide` on the archetype and watch the
  preview. Grip (Mesh) and the Sheathed pose stay tunable as usual.
- **After tuning in the Player's Prefab Mode, discard the stage changes** — only the archetype / weapon prefab are meant to
  change. Saving the Player stage writes Cinemachine's edit-mode camera position and UI layout values into `Player.prefab`.
