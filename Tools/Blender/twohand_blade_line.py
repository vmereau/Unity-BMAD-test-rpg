"""Re-aim the blade line of a Mixamo two-handed attack clip by moving only the left arm (IK), then export a new FBX.

In game the 2H weapon is aimed along the left -> right hand line (TwoHandGripConstraint), so changing where the left hand
sits relative to the right hand changes the blade direction. This script pulls that line toward a target pitch inside an
edit window, keeps the body / right arm / root untouched, and bakes the result on LeftArm + LeftForeArm only.

Inputs come from Unity measurements on the Player rig (PlayableGraph sampling of WeaponSocket / LeftWeaponSocket), because
the Blender hand bones are only a proxy for the in-game sockets (retarget + palm offsets). See README.md for the loop.

Run it either way:
  * Blender MCP / Blender text editor:
        exec(open(r"<repo>/Tools/Blender/twohand_blade_line.py").read())
        run(load_config(r"<repo>/Tools/Blender/configs/2hand_attack_1.json"))
  * Command line (background):
        blender -b -P Tools/Blender/twohand_blade_line.py -- Tools/Blender/configs/2hand_attack_1.json

Relative paths in the config resolve from the repo root (two folders above this file). Tested on Blender 5.2.
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

BONE_SHOULDER = "mixamorig:LeftArm"
BONE_ELBOW = "mixamorig:LeftForeArm"
BONE_WRIST = "mixamorig:LeftHand"
BONE_LEFT_KNUCKLE = "mixamorig:LeftHandMiddle1"
BONE_RIGHT_KNUCKLE = "mixamorig:RightHandMiddle1"
BAKED_BONES = (BONE_SHOULDER, BONE_ELBOW)
UP = Vector((0.0, 1.0, 0.0))  # armature space of a Mixamo import: +Y up, +Z forward, -X right


def _repo_root():
    here = globals().get("__file__")
    if here:
        return os.path.abspath(os.path.join(os.path.dirname(here), "..", ".."))
    return os.getcwd()


def load_config(path):
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    root = cfg.get("repo_root") or _repo_root()
    for key in ("source_fbx", "output_fbx", "save_blend"):
        if cfg.get(key) and not os.path.isabs(cfg[key]):
            cfg[key] = os.path.join(root, cfg[key])
    return cfg


def _smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def _weight(n, window):
    """0 outside [start, end], smoothstep up to full_start, 1 until full_end, smoothstep down to end."""
    start, full_start, full_end, end = window
    if n <= start or n >= end:
        return 0.0
    if n < full_start:
        return _smoothstep((n - start) / (full_start - start))
    if n <= full_end:
        return 1.0
    return _smoothstep((end - n) / (end - full_end))


def _ctx_override(**extra):
    """Window context when Blender has a UI (needed after resets: no active object), plain context in background."""
    scene = bpy.context.scene
    wm = bpy.context.window_manager
    kwargs = dict(scene=scene, view_layer=scene.view_layers[0], **extra)
    if wm.windows:
        kwargs["window"] = wm.windows[0]
    return bpy.context.temp_override(**kwargs)


def _clear_scene():
    for coll in (bpy.data.objects, bpy.data.armatures, bpy.data.actions):
        for block in list(coll):
            coll.remove(block)


def _import(path, fps):
    scene = bpy.context.scene
    scene.render.fps = fps
    before = set(bpy.data.objects.keys())
    with _ctx_override():
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arms = [bpy.data.objects[n] for n in bpy.data.objects.keys() if n not in before and bpy.data.objects[n].type == 'ARMATURE']
    if len(arms) != 1:
        raise RuntimeError(f"expected one armature in {path}, got {len(arms)}")
    arm = arms[0]
    action = arm.animation_data.action
    first, last = (int(round(v)) for v in action.frame_range)
    return arm, first, last


def _empty(name, parent):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.matrix_parent_inverse.identity()  # location == armature space
    return e


def run(cfg):
    """Apply the edit described by cfg and export. Returns a report dict."""
    scene = bpy.context.scene
    _clear_scene()
    arm, first, last = _import(cfg["source_fbx"], cfg.get("fps", 30))
    pb = arm.pose.bones
    frames = list(range(first, last + 1))

    pitch_unity = cfg["blade_pitch_unity"]  # index i = source frame i = Blender frame first + i
    if len(pitch_unity) != len(frames):
        raise ValueError(f"blade_pitch_unity has {len(pitch_unity)} values, clip has {len(frames)} frames")
    dist_scale = {int(k): v for k, v in cfg.get("distance_scale", {}).items()}  # keyed by Blender frame
    target_pitch = cfg["target_pitch_deg"]
    window = cfg["window"]
    span = float(last - first)

    # 1. original positions (armature space)
    orig = {}
    for f in frames:
        scene.frame_set(f)
        orig[f] = {
            "sh": pb[BONE_SHOULDER].head.copy(), "el": pb[BONE_ELBOW].head.copy(), "wr": pb[BONE_WRIST].head.copy(),
            "lk": pb[BONE_LEFT_KNUCKLE].head.copy(), "rk": pb[BONE_RIGHT_KNUCKLE].head.copy(),
        }

    # 2. keyed IK target + pole
    target = _empty("LH_IK_Target", arm)
    pole = _empty("LH_IK_Pole", arm)
    weights = {}
    for i, f in enumerate(frames):
        o = orig[f]
        w = _weight((f - first) / span, window)
        weights[f] = w
        line = o["rk"] - o["lk"]
        dist = line.length * dist_scale.get(f, 1.0)
        line.normalize()
        pitch_blender = math.degrees(math.asin(max(-1.0, min(1.0, line.y))))
        delta = max(0.0, pitch_unity[i] - target_pitch) if w > 0 else 0.0  # only ever pulls the blade down
        new_pitch = math.radians(pitch_blender - delta)
        horizontal = Vector((line.x, 0.0, line.z))
        horizontal = horizontal.normalized() if horizontal.length > 1e-4 else Vector((0.0, 0.0, 1.0))
        new_line = horizontal * math.cos(new_pitch) + UP * math.sin(new_pitch)
        target.location = (o["rk"] - new_line * dist + (o["wr"] - o["lk"])) if w > 0 else o["wr"]
        target.keyframe_insert("location", frame=f)
        pole.location = o["el"] + (o["el"] - (o["sh"] + o["wr"]) * 0.5).normalized() * cfg.get("pole_offset_m", 0.4)
        pole.keyframe_insert("location", frame=f)

    # 3. IK with keyed influence (the ramp lives in the influence, the target is fully corrected)
    forearm = pb[BONE_ELBOW]
    ik = forearm.constraints.new('IK')
    ik.target, ik.pole_target = target, pole
    ik.chain_count, ik.use_tail, ik.use_stretch = 2, True, False
    ik.pole_angle = math.radians(cfg.get("pole_angle_deg", 1.5))
    for f in frames:
        ik.influence = weights[f]
        ik.keyframe_insert("influence", frame=f)

    # 4. bake LOCAL rotations only where the IK is active (+1 frame each side), then clean up
    edited = [f for f in frames if weights[f] > 0]
    bake = list(range(max(first, edited[0] - 1), min(last, edited[-1] + 1) + 1)) if edited else []
    baked, wrist_miss, elbow_shift = {}, [], []
    for f in bake:
        scene.frame_set(f)
        if weights[f] == 1.0:
            wrist_miss.append(round((pb[BONE_WRIST].head - target.location).length, 4))
        elbow_shift.append(round((pb[BONE_ELBOW].head - orig[f]["el"]).length, 4))
        baked[f] = {n: arm.convert_space(pose_bone=pb[n], matrix=pb[n].matrix.copy(), from_space='POSE', to_space='LOCAL')
                    for n in BAKED_BONES}
    forearm.constraints.remove(ik)
    for f, mats in baked.items():
        for n, m in mats.items():
            pb[n].rotation_mode = 'QUATERNION'
            pb[n].rotation_quaternion = m.to_quaternion()
            pb[n].keyframe_insert("rotation_quaternion", frame=f)
    bpy.data.objects.remove(target)
    bpy.data.objects.remove(pole)

    # 5. quaternion continuity check (a sign flip interpolates the long way round)
    flips = 0
    for n in BAKED_BONES:
        prev = None
        for f in range(max(first, bake[0] - 2), min(last, bake[-1] + 2) + 1) if bake else []:
            scene.frame_set(f)
            q = pb[n].rotation_quaternion.copy()
            if prev is not None and prev.dot(q) < 0:
                flips += 1
            prev = q

    # 6. export
    scene.frame_start, scene.frame_end = first, last
    for ob in bpy.data.objects:
        ob.select_set(False)
    arm.select_set(True)
    with _ctx_override(active_object=arm, selected_objects=[arm]):
        bpy.ops.export_scene.fbx(
            filepath=cfg["output_fbx"], use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
            bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
            bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
            apply_unit_scale=True, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    if cfg.get("save_blend"):
        bpy.ops.wm.save_as_mainfile(filepath=cfg["save_blend"], copy=True)

    return {"frames": [first, last], "baked_frames": [bake[0], bake[-1]] if bake else [], "wrist_miss_m": wrist_miss,
            "max_elbow_shift_m": max(elbow_shift) if elbow_shift else 0.0, "quat_flips": flips,
            "output": cfg["output_fbx"]}


if __name__ == "__main__" and "--" in sys.argv:
    _args = sys.argv[sys.argv.index("--") + 1:]
    if not _args:
        raise SystemExit("usage: blender -b -P twohand_blade_line.py -- <config.json>")
    print(json.dumps(run(load_config(_args[0])), indent=2))
