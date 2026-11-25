"""Retime a Mixamo clip with a piecewise-linear time map and export it as an armature-only FBX
(spec-sword-shield-take-combo: the sword finisher's ending is slowed so it doesn't end abruptly).

    blender -b --factory-startup -P Tools/Blender/mixamo_retime.py -- Tools/Blender/configs/sword/<config>.json

Config keys:
  input_fbx   source FBX (kept untouched, the revert)
  output_fbx  written FBX (armature only, every bone keyed on every output frame)
  time_map    [[output_frame, source_frame], ...], increasing output frames, Blender numbering of the source action
              (first frame 1). Between two knees the source time runs linearly; a flat segment holds a pose. Output
              frames run from the first knee to the last.

Every bone channel (location, rotation, scale) is sampled from the source action at the mapped source time, so the
hips' travel (root motion in Unity) is retimed with the pose. Export settings match unarmed_foot_step.py /
twohand_blade_line.py.
"""
import json
import sys

import bpy


def source_frame(time_map, out_frame):
    for (o0, s0), (o1, s1) in zip(time_map, time_map[1:]):
        if o0 <= out_frame <= o1:
            return s0 if o1 == o0 else s0 + (s1 - s0) * (out_frame - o0) / (o1 - o0)
    raise ValueError(f"output frame {out_frame} outside the time map")


def run(cfg):
    time_map = cfg["time_map"]
    if len(time_map) < 2 or any(b[0] <= a[0] for a, b in zip(time_map, time_map[1:])):
        raise ValueError("time_map needs at least two knees with increasing output frames")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=cfg["input_fbx"], automatic_bone_orientation=False, ignore_leaf_bones=False)
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    if len(arms) != 1:
        raise RuntimeError(f"expected one armature in {cfg['input_fbx']}, got {len(arms)}")
    arm = arms[0]
    scene = bpy.context.scene
    src_first, src_last = (int(round(v)) for v in arm.animation_data.action.frame_range)
    for _, s in time_map:
        if not src_first <= s <= src_last:
            raise ValueError(f"source frame {s} outside the action ({src_first}-{src_last})")

    # Sample the source pose of every bone at each mapped time (fractional frames are evaluated by the action). F-curves
    # blend quaternions per component: normalize, and report sign flips between source keys (a flip would make the
    # in-between pose collapse — fix the source if any is reported inside a stretched segment).
    flips = 0
    prev = {}
    for f in range(src_first, src_last + 1):
        scene.frame_set(f)
        for b in arm.pose.bones:
            if b.rotation_mode != 'QUATERNION':
                continue
            q = b.rotation_quaternion.copy()
            if b.name in prev and prev[b.name].dot(q) < 0.0:
                flips += 1
            prev[b.name] = q
    out_first, out_last = int(time_map[0][0]), int(time_map[-1][0])
    poses = []
    for f in range(out_first, out_last + 1):
        s = source_frame(time_map, f)
        scene.frame_set(int(s), subframe=s - int(s))
        poses.append({b.name: (b.location.copy(), b.rotation_quaternion.normalized(), b.rotation_euler.copy(), b.scale.copy())
                      for b in arm.pose.bones})

    # Replace the action with the retimed keys.
    old = arm.animation_data.action
    action = bpy.data.actions.new(old.name + "_retimed")
    arm.animation_data.action = action
    for i, f in enumerate(range(out_first, out_last + 1)):
        for b in arm.pose.bones:
            loc, quat, eul, scl = poses[i][b.name]
            b.location, b.scale = loc, scl
            if b.rotation_mode == 'QUATERNION':
                b.rotation_quaternion = quat
                b.keyframe_insert("rotation_quaternion", frame=f)
            else:
                b.rotation_euler = eul
                b.keyframe_insert("rotation_euler", frame=f)
            b.keyframe_insert("location", frame=f)
            b.keyframe_insert("scale", frame=f)
    scene.frame_start, scene.frame_end = out_first, out_last

    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=cfg["output_fbx"], use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        apply_unit_scale=True, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    print(f"RETIMED {cfg['input_fbx']} -> {cfg['output_fbx']}: frames {out_first}-{out_last} "
          f"(source {src_first}-{src_last}), quaternion sign flips between source keys: {flips}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        raise SystemExit("usage: blender -b --factory-startup -P mixamo_retime.py -- <config.json>")
    with open(argv[0]) as fh:
        run(json.load(fh))
