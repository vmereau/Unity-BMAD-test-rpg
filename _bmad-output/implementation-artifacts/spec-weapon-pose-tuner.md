---
title: 'Weapon Pose Tuner editor window'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: '82bf41aa0b37a1d22ed77c8db494b3cb0ee69a2c'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A weapon family's in-hand / sheathed pose (`WeaponArchetypeSO.drawnPose` / `sheathedPose`) and a weapon's grip (`Mesh` child of its `_Visual` prefab) can only be tuned by trial and error — values typed blind, checked in Play mode, lost on exit. The Flamberge does not fit the 2H clips.

**Approach:** An edit-mode window `Tools/Combat/Weapon Pose Tuner` (modeled on `HitboxTunerWindow`): pick the Player + a `WeaponSO` + a clip, preview the weapon on the right socket while scrubbing the clip, move / rotate it with the normal scene gizmos, then save the pose to the archetype or the grip to the weapon prefab.

## Boundaries & Constraints

**Always:** Editor-only (`Assets/_Game/Editor/`, `Game.Editor`). The preview weapon is a temporary `HideFlags.DontSave` instance, destroyed — and `AnimationMode` stopped — on every exit path (window close, target / weapon / mode change, Play mode, domain reload), so the Player is never left posed, dirtied or carrying an extra child. Saves are Undo-recorded (archetype) and keep asset GUIDs (prefab saved in place). Sockets come from the target's `EquipmentVisuals` through `ResolveDrawnSocket` / `ResolveSheathSocket`.

**Ask First:** Any change to runtime code (`EquipmentVisuals`, `WeaponArchetypeSO`, `WeaponSO`).

**Never:** No Play-mode support. No pose editing for weapons without an archetype (show a message). No changes to `HitboxTunerWindow`. No hitbox / event authoring (Hitbox Tuner owns that).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tune drawn pose | Player + Flamberge, mode Drawn, clip `2hand attack` frame N, `Drawn` rotated | Save Pose writes `Archetype_TwoHanded.drawnPose` = node pose in socket space | Undo restores the old value |
| Tune sheathed pose | Mode Sheathed | Preview on the archetype's sheath socket (Back / Hip); Save writes `sheathedPose` | Back socket missing → hip + warning label |
| Tune grip | `Mesh` child moved | Save Grip writes that local pose to **both** `Drawn/Mesh` and `Sheathed/Mesh` of the `_Visual` prefab | Prefab missing a child → error, nothing written |
| No archetype | Weapon with null archetype | Preview with prefab poses; Save Pose disabled + help box | — |
| Invalid target | Selection without `EquipmentVisuals` / `Animator` | "No target" message, controls disabled | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/HitboxTunerWindow.cs` -- reference: target resolution (`ResolveTargetRoot`, Prefab Mode), `AnimationModeDriver` scrubbing, exit-path cleanup
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- private serialized sockets (`_weaponSocket`, `_leftWeaponSocket`, `_undrawnWeaponSocket`, `_backWeaponSocket`), static `ResolveDrawnSocket` / `ResolveSheathSocket` / `ApplyArchetypePoses`
- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponPose.cs` -- pose struct (localPosition + localEulerAngles)
- `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md` -- `_Visual` / `Drawn` / `Sheathed` / `Mesh` convention

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Editor/WeaponPoseTunerWindow.cs` -- new window: Target (Use Selection; requires `EquipmentVisuals` + `Animator`), Weapon (`WeaponSO` field; archetype shown), Mode (Drawn / Sheathed), Clip (dropdown from `ResolvedAnimatorOverride.animationClips`, else the Animator's controller; plus a clip override field), frame slider (AnimationMode sampling), buttons Select Pose Node / Select Grip (Mesh) / Save Pose to Archetype / Save Grip to Weapon / Revert. Preview = instantiate `equipVisualPrefab` under the resolved socket, root at identity, archetype poses applied, only the mode's child active. Save Pose = node world pose converted to socket space (robust if the root was moved). Scene GUI: wire box of the `Drawn` shape collider in Drawn mode -- one-stop pose tuning
- [x] `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md` -- document the tool in the archetype / Weapon Creator sections ("verify grip" step) -- discoverability
- [x] `Assets/_Game/Scripts/Combat/CLAUDE.md` -- one line under Hit-window tooling pointing to the tuner -- discoverability

**Acceptance Criteria:**
- Given the Player prefab in Prefab Mode and the Flamberge, when the user scrubs `2hand attack` and rotates `Drawn`, then the blade follows the hands at every frame and Save Pose updates `Archetype_TwoHanded.drawnPose`.
- Given a preview in progress, when the window is closed, Play mode entered or the target changed, then no preview object remains under the Player and its bones are back to their unposed state.
- Given Save Grip, then the `_Visual` prefab keeps its GUID and both `Mesh` children share the new local pose.
- Given the Hunting Bow (Left hand, Back socket), when switching modes, then the preview sits on `LeftWeaponSocket` / `BackWeaponSocket`.

## Design Notes

Pose = what the runtime applies: `ApplyArchetypePoses` writes the archetype pose as the `Drawn` / `Sheathed` **local** transform under a visual root parented at identity to the socket — so the saved value is the node's pose in socket space. A family pose affects every weapon of the archetype; the grip (Mesh child) is per weapon — the window labels both buttons accordingly.

## Verification

**Commands:**
- Unity compile + `read_console` errors -- expected: none
- `run_tests` EditMode -- expected: all green (no new tests: editor UI only)

**Manual checks:**
- Open the tuner on the Player prefab with the Flamberge; scrub, rotate, save, reopen — value persisted; close window — Player clean (no `Flamberge_Visual` child, prefab not dirty).

## Suggested Review Order

**Preview lifecycle (never leave the Player posed or carrying a child)**

- Entry point: preview built like EquipmentVisuals.RefreshWeapon, DontSave, on the resolved socket.
  [`WeaponPoseTunerWindow.cs:345`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L345)

- Humanoid bones snapshotted/restored manually — StopAnimationMode doesn't revert them.
  [`WeaponPoseTunerWindow.cs:519`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L519)

- Scene / prefab saves restore the rig first, resample after.
  [`WeaponPoseTunerWindow.cs:147`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L147)

- Exit paths: disable, reload, play mode, stage closing, deleted preview.
  [`WeaponPoseTunerWindow.cs:102`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L102)

**Target and socket resolution**

- Target = selection's EquipmentVisuals + the Animator it drives.
  [`WeaponPoseTunerWindow.cs:282`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L282)

- Same socket rules as runtime; fallback socket blocks Save Pose.
  [`WeaponPoseTunerWindow.cs:392`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L392)

**Saving**

- Pose saved in socket space from world; Undo-recorded, Undo re-applies to the preview.
  [`WeaponPoseTunerWindow.cs:561`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L561)

- Grip written to both Mesh children; refuses non-prefabs and an open Prefab Mode.
  [`WeaponPoseTunerWindow.cs:592`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L592)

**Clips and docs**

- Clips chosen by index with file labels; legacy / zero-fps clips rejected.
  [`WeaponPoseTunerWindow.cs:463`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L463)

- Tool usage documented with the weapon prefab conventions.
  [`Weapons/CLAUDE.md`](../../Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md)
