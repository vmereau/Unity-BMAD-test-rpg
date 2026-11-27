# CLAUDE.md — Assets/_Game/Prefabs/World/DockGate

> The Area 1 dock gate (`StartingTown` → `Area1_Blockout/DockGate`): a script-built Blender model wrapped in a
> generated prefab. Look and build brief: `Art/Environment/Camp/QUAY_CONCEPTS.md`; wood rules:
> `Art/Environment/Camp/CLAUDE.md`. Spec: `spec-area-1-warehouse-quay`.

---

## Pipeline

`DockGate.prefab` is **generated**: never edit it or its scene instance by hand.

| Step | What | Writes |
|------|------|--------|
| 1 | `Art/Environment/Camp/models/dock gate/Source~/build_dock_gate.py` in Blender (`blender -b -P`, or in a session `runpy.run_path(path, run_name="__main__")`) | `DockGate.fbx`, `DockGate.json` (parts file), `Source~/DockGate.blend` |
| 2 | `Tools/World/Build Dock Gate Prefab` (`Editor/World/DockGateBuilder`) | `DockGate.prefab` |
| 3 | `Tools/World/Build Area 1 Sites` places it at the layout's `dock-gate`, on the lane's ground | the scene root |

- The script loads the warehouse's (`models/warehouse/Source~/build_warehouse.py`) as a library: mesh helpers,
  materials, export. A size is written once, in the script; the builder reads colliders, Not Walkable volumes, the
  opening and the fire's point from the parts file (`version` 1).
- **Materials:** the model importer holds the warehouse's remaps (`Wh_*` → `M_Camp_*`), with `Wh_Frame` on
  `M_Camp_PalisadeLog`: the round logs take the palisade's wood. A new material slot in the library needs a remap
  here too, or the build refuses the model.
- A missing model, a model object the parts file names, a malformed parts file or a non-`M_Camp_` material stops
  the build before saving. Without the prefab, Sites logs an error and builds the graybox gate
  (`Area1CampInterior.BuildGate`), which the other gates still use.
- No ground capture: the lane is flat at the gate, and both posts are modelled 2.2 m deep (the river post stands in
  the falling bank).

## Frame and contract

Local frame: x east (out of the camp, along the bank path), y up, z north (the river); origin on the lane in the
middle of the opening (world (24.5, −0.18, 59)).

- **Root children:** `Model` (`Gate`, `Leaves`, `LeanTo`, `Brazier`, all left out of the NavMesh bake), `Colliders`
  (`Gate`, `Leaves`, `LeanTo`, `Brazier`), `NavMesh` (Not Walkable volumes: both leaves, the bench, the fire
  basket), `BrazierFire`.
- **Opening** 2.5 m between the posts, 3.2 m under the lintel. The test sweeps a cart-high box from 3 m inside the
  camp to 7 m along the path: the leaves stand open beside it, the lean-to's eave stops 0.1 m short of it, and the
  fire basket stands north of it.
- **Lean-to** local x 1.8..4.1, z −2.9..−1.4, open to the north. It fits the flat ground: east of x 4.2 the bank
  rises, and rocks stand at z −2.8 from x 3.5.
- **`BrazierFire`:** a point light and a glow quad on `ClockNightLight` (off by day and in edit mode). Values: the
  constants at the top of `DockGateBuilder`; they can only be judged in Play. The light is unshadowed: it reaches
  the retaining wall through the gateway and the palisade.
- Tests: `Tests/EditMode/DockGateTests` (clear gateway, posts / lintel / leaves, fire, bake modifiers, malformed
  parts refused).

## Known limits

- `hand-edits.json` still pins eight parts of the old graybox gate (`DockGate/Post_*`, `Lintel`, `Leaf_*`,
  `GuardPost…`): a Sites run warns that they match nothing. Untick them in the Hand Edits window.
- The leaves do not move: shutting or guarding the gate is later gameplay.
- The palisade's panels beside the gate (`dock-fence-upper/Panel_148`, `dock-fence-lower/Panel_149`) keep their own
  closing posts next to the gate's.
- The other gates (compound, ramp, fields, inner, arrival) are still graybox.
