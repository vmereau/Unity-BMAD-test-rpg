"""Fit a Meshy text-to-motion clip that travels (SMPL-H skeleton) to a wanted forward distance, then export an FBX.

Meshy ignores distance and direction words (.claude/rules/meshy-mcp.md): the stepping jab travels 1.22 m, 24 degrees off
to the left. In game the clip's root displacement moves the player (ActionRootMotion), so the travel must be right in
the clip itself. This script:
  1. trims and retimes the source (`first_frame`, `last_frame`, `speed`: output frame i = source first + i * speed);
  2. turns the whole motion about the vertical so the net pelvis travel points straight forward (rigid: body
     orientation and travel turn together, nothing shears) and moves the pelvis start onto the world origin;
  3. scales every displacement from the start pose by k = `travel_m` / source travel: pelvis path and both ankle paths.
     A foot that was planted in the source stays planted, and the start stance is unchanged;
  4. scales the pelvis dip and the foot lift (`pelvis_height_scale`, `foot_lift_scale`) - a short step needs neither
     the deep lunge nor the high knee of the long one;
  5. re-solves both legs (analytic 2-bone IK toward the scaled ankle, knee on the source's side, ankle orientation
     kept), keys every bone on every frame and exports an armature-only FBX.
Spine, arms and head keep their source local rotations.

Run (Blender MCP) - pass `__file__` so relative config paths resolve from the repo root:
    path = r"<repo>/Tools/Blender/meshy_jab_fit.py"
    ns = {"__file__": path, "__name__": "meshy_jab_fit"}; exec(open(path).read(), ns)
    result = ns["run"](ns["load_config"](r"<repo>/Tools/Blender/configs/unarmed/meshy_jab.json"))
`strip_source(raw_fbx, out_fbx)` turns a 16 MB Meshy download (mannequin meshes) into the small armature-only source.
`export_rig(source_fbx, out_fbx)` exports the T-pose skeleton Unity builds the SMPL-H avatar from (once per skeleton).

The script removes every object, armature and action of the open Blender file before importing. Tested on Blender 5.2.
"""

import json
import math
import os

import bpy
from mathutils import Matrix, Vector

PELVIS = "Pelvis"
LEGS = {"L": ("L_Hip", "L_Knee", "L_Ankle"), "R": ("R_Hip", "R_Knee", "R_Ankle")}
REACH = 0.985  # of thigh + shin: beyond it the 2-bone solve has no knee bend left


def load_config(path):
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    for key in ("source_fbx", "output_fbx"):
        if not os.path.isabs(cfg[key]):
            cfg[key] = os.path.join(root, cfg[key])
    return cfg


def _clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.armatures, bpy.data.meshes, bpy.data.actions):
        for item in list(block):
            block.remove(item)


def _import(path):
    _clear()
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return arm


def _export(arm, path):
    for obj in bpy.context.scene.objects:
        obj.select_set(obj is arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        apply_unit_scale=True, global_scale=1.0, axis_forward='-Z', axis_up='Y')


def strip_source(raw_fbx, out_fbx):
    """Armature + animation only (the Meshy download carries mannequin meshes)."""
    arm = _import(raw_fbx)
    action = arm.animation_data.action
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = int(action.frame_range[0]), int(action.frame_range[1])
    _export(arm, out_fbx)
    return {"output": out_fbx, "frames": [scene.frame_start, scene.frame_end]}


def export_rig(source_fbx, out_fbx):
    """Rest-pose (T-pose) armature without animation: Unity builds the SMPL-H Humanoid avatar from it, and the clips
    copy that avatar. A Blender-exported clip carries its first animated pose as the model pose, which is no T-pose."""
    arm = _import(source_fbx)
    action = arm.animation_data.action
    arm.animation_data_clear()
    bpy.data.actions.remove(action)
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for obj in bpy.context.scene.objects:
        obj.select_set(obj is arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=out_fbx, use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False, bake_anim=False,
        apply_unit_scale=True, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    return {"output": out_fbx, "bones": len(arm.pose.bones)}


def _world(arm, name):
    return arm.matrix_world @ arm.pose.bones[name].matrix


def _set_world(arm, name, world):
    arm.pose.bones[name].matrix = arm.matrix_world.inverted() @ world
    bpy.context.view_layer.update()


def _aim(arm, name, child, head, target):
    """Swing the bone (minimal rotation, twist kept) so its `child` joint lands on the head -> target line, head pinned
    at `head`. The direction is read from the child joint: imported bones do not point along their own Y axis."""
    world = _world(arm, name)
    loc, rot, scale = world.decompose()
    current = (_world(arm, child).translation - loc).normalized()
    swing = current.rotation_difference((target - head).normalized())
    new = Matrix.LocRotScale(head, swing @ rot, scale)
    _set_world(arm, name, new)


def run(cfg):
    scene = bpy.context.scene
    arm = _import(cfg["source_fbx"])
    scene.render.fps = cfg.get("fps", 30)
    source_action = arm.animation_data.action
    first, last, speed = float(cfg["first_frame"]), float(cfg["last_frame"]), float(cfg["speed"])
    count = int(math.floor((last - first) / speed + 1e-6)) + 1
    forward = Vector(cfg.get("forward", [0.0, -1.0, 0.0])).normalized()
    tracked = [PELVIS] + [b for leg in LEGS.values() for b in leg]

    # 1. sample the source
    frames = []
    for i in range(count):
        sf = first + i * speed
        scene.frame_set(int(math.floor(sf)), subframe=sf - math.floor(sf))
        bpy.context.view_layer.update()
        frames.append({
            "basis": {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones},
            "world": {b: _world(arm, b).copy() for b in tracked},
        })

    def xy(v):
        return Vector((v.x, v.y, 0.0))

    start = {b: frames[0]["world"][b].translation.copy() for b in tracked}
    origin = xy(start[PELVIS])
    travel = xy(frames[-1]["world"][PELVIS].translation) - origin
    source_travel = travel.length
    if source_travel < 0.05:
        raise ValueError("source clip has no net travel (%.3f m): nothing to straighten or scale" % source_travel)
    k = cfg["travel_m"] / source_travel
    # signed angle about +Z turning the source travel onto `forward`
    phi = math.atan2(forward.y, forward.x) - math.atan2(travel.y, travel.x)
    turn = Matrix.Rotation(phi, 4, 'Z')
    turn3 = turn.to_3x3()
    kz_pelvis = cfg.get("pelvis_height_scale", 1.0)
    kz_foot = cfg.get("foot_lift_scale", 1.0)

    # Heights scale about the pelvis start height (the dip shrinks) and about each ankle's lowest point (the lift
    # shrinks, a planted foot stays on the ground).
    height_ref = {PELVIS: start[PELVIS].z}
    for leg in LEGS.values():
        height_ref[leg[2]] = min(f["world"][leg[2]].translation.z for f in frames)

    def fit_point(name, pos, kz):
        base = start[name]
        # relative to the pelvis start, which lands on the world origin: Unity keeps a clip's own XZ offset as a root
        # offset ("Based Upon: Original"), and blending into the clip then drags the player sideways by that offset
        placed = turn3 @ (xy(base) - origin) + k * (turn3 @ (xy(pos) - xy(base)))
        placed.z = height_ref[name] + kz * (pos.z - height_ref[name])
        return placed

    def fit_matrix(name, world, kz):
        loc, rot, scale = world.decompose()
        return Matrix.LocRotScale(fit_point(name, loc, kz), turn.to_quaternion() @ rot, scale)

    # 2. rebuild every frame on a new action
    arm.animation_data.action = None
    bpy.data.actions.remove(source_action)
    report = {"frames": {}, "reach_clamped_frames": []}
    for i, frame in enumerate(frames):
        out_frame = i + 1
        for pb in arm.pose.bones:
            pb.matrix_basis = frame["basis"][pb.name]
        bpy.context.view_layer.update()
        _set_world(arm, PELVIS, fit_matrix(PELVIS, frame["world"][PELVIS], kz_pelvis))

        row = {"pelvis": [round(c, 4) for c in _world(arm, PELVIS).translation]}
        for key, (thigh, shin, ankle) in LEGS.items():
            src_hip = frame["world"][thigh].translation
            src_knee = frame["world"][shin].translation
            src_ankle = frame["world"][ankle].translation
            l1, l2 = (src_knee - src_hip).length, (src_ankle - src_knee).length
            hip = _world(arm, thigh).translation.copy()
            target = fit_point(ankle, src_ankle, kz_foot)
            reach = target - hip
            if reach.length > REACH * (l1 + l2):
                target = hip + reach.normalized() * REACH * (l1 + l2)
                reach = target - hip
                report["reach_clamped_frames"].append([out_frame, key])
            dist = max(reach.length, 1e-6)
            axis = reach / dist
            along = (l1 * l1 - l2 * l2 + dist * dist) / (2.0 * dist)
            height = math.sqrt(max(l1 * l1 - along * along, 0.0))
            hint = turn3 @ (src_knee - src_hip)            # knee stays on the source's side
            bend = hint - hint.dot(axis) * axis
            if bend.length < 1e-6:                          # straight source leg: bend toward the travel direction
                bend = forward - forward.dot(axis) * axis
            knee = hip + axis * along + bend.normalized() * height
            _aim(arm, thigh, shin, hip, knee)
            _aim(arm, shin, ankle, _world(arm, shin).translation.copy(), target)
            ankle_world = fit_matrix(ankle, frame["world"][ankle], kz_foot)
            ankle_now = _world(arm, ankle)
            _set_world(arm, ankle, Matrix.LocRotScale(ankle_now.translation, ankle_world.to_quaternion(),
                                                      ankle_now.to_scale()))
            row[key] = [round(c, 4) for c in _world(arm, ankle).translation]
            row[key + "_err"] = round((_world(arm, ankle).translation - target).length, 4)

        for pb in arm.pose.bones:
            pb.keyframe_insert(data_path="rotation_quaternion", frame=out_frame)
            if pb.name == PELVIS:
                pb.keyframe_insert(data_path="location", frame=out_frame)
        report["frames"][out_frame] = row

    action = arm.animation_data.action
    action.name = cfg.get("action_name", "lead jab step")
    for fcurve in _fcurves(action):
        for point in fcurve.keyframe_points:
            point.interpolation = 'LINEAR'
    scene.frame_start, scene.frame_end = 1, count
    _export(arm, cfg["output_fbx"])
    report.update({"output": cfg["output_fbx"], "frame_count": count, "seconds": round((count - 1) / scene.render.fps, 3),
                   "source_travel_m": round(source_travel, 3), "scale_k": round(k, 4),
                   "turn_deg": round(math.degrees(phi), 2)})
    return report


def _fcurves(action):
    """Blender 5 layered actions keep fcurves in channelbags; older ones on the action."""
    if hasattr(action, "layers") and len(action.layers):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    yield from bag.fcurves
    elif hasattr(action, "fcurves"):
        yield from action.fcurves
