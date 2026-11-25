"""Fit a Meshy text-to-motion clip that travels (SMPL-H skeleton) to a wanted forward distance, then export an FBX.
One script for every clip read by ActionRootMotion (unarmed jab, hook, uppercut; one-handed sword cut, backhand,
thrust): one config per clip in configs/unarmed/meshy_*.json and configs/sword/meshy_sword_*.json.

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

Optional, all off by default (the jab config uses none of them):
  - `lateral_scale` (1): scales the sideways part of every displacement. Meshy bodies swing off the travel line and
    come back (hook: 0.18 m); below 1 the path is straighter, planted feet stay planted.
  - `backtrack_scale` (1, within 0..1): scales how far the pelvis comes back from its furthest point forward so far. A
    punch leans in and settles back (hook: 7 cm); in game that is travel the player loses again, and how much depends
    on when the next attack is chained. Below 1 the body settles less, the feet are untouched. `travel_m` is set before
    this correction: the clip then ends further than `travel_m` (the report gives `net_travel_m` / `peak_travel_m`).
  - `facing_offset_deg` (0): turns the body (start stance, bone orientations) about the vertical without turning the
    travel, to line a clip's facing up with the clip it chains from.
  - `travel_yaw_deg` (0): turns the travel line, not the body, by this angle to the character's right. Unity moves the
    player by the centre of mass, which leans into a hook and only comes back at the end of the clip: a chain cuts that
    return, and a few degrees of travel yaw put the lean back on the line at the combo window.
  - `start_match` / `end_match`: `{"L": {"offset": [x, y, z], "fade": [f0, f1], "lift_m": 0.0}, "R": [{...}, {...}]}`.
    Meshy cannot start from a pose, so a foot is moved by `offset` (metres, fitted space: -Y forward, +X the character's
    left) at the first (start) or last (end) output frame, and the offset fades out (start) or in (end) across the
    output frames `fade` - pick frames where that foot is in the air, or add `lift_m` to turn the fade into a small
    step. A foot takes one spec or a list (e.g. a height that lands in 5 frames and a sideways part released later).
    The offset is the difference measured in Unity (meshy_motion_measure.cs) between the previous clip inside its combo
    window and this clip's first frame.
  - `foot_shift`: `{"R": [x, y, z]}` moves a foot's whole path by a constant (a narrower or wider stance all clip long).
  - `pelvis_drop_m` (0): lowers the pelvis by a constant, all clip long. A stance widened by `start_match` /
    `end_match` (the sword idle stands 0.9 m wide) is out of the legs' reach at the source's standing height: the report's
    `reach_clamp_max_m` says by how much a foot falls short (it then floats above its target).
  - `time_map`: `[[1, 1], [18, 18], [36, 27]]` (output frame, source frame; both increasing, first output frame 1)
    replaces `first_frame` / `last_frame` / `speed` (an error when both are given) with a piecewise-linear retime: here
    the strike plays at real time and the 9 source frames after it are spread over 18. The speed changes in one frame
    at each knee: put it where the body is nearly still. A stepping sword cut is over in 0.87 s, which leaves no room
    for a combo window before the Animator's exit-time transition; slowing only the follow-through keeps the hit time.
    Every other block (`start_match`, `blade_aim`, `hand_shift`...) counts in output frames.
  - `upper_body`: `{"first_frame": 27, "speed": 1.0}` takes the spine, head, both arms and the pelvis orientation from
    another stretch of the same source (same retime as the legs, started at its own `first_frame`) while the pelvis path and the legs
    still come from `first_frame` / `last_frame`. For a source whose step and strike do not happen together (sword
    rising cut: the step is at frames 5-25, the cut it was asked for at 27-45). `"keep_prefixes": ["L_"]` leaves the
    bones whose name starts with one of them on the legs' stretch (there: the left arm, which hangs at the side during
    the step but lifts a held shield flat in front of the chest, into the sword hand's path, during the cut).
  - `hand_shift`: `{"L": {"offset": [x, y, z], "frames": [f0, f1, f2, f3]}}` moves a wrist by `offset` (fitted space),
    fading in over output frames f0..f1 and out over f2..f3, with a 2-bone solve of that arm (elbow on the source's
    side, hand orientation kept). For an off-hand that comes too close to the weapon hand. f2 = f3 = the last frame
    keeps the offset to the end of the clip (sword cut: the next clip starts with the same offset and fades it out).
  - `blade_aim`: `{"keys": [{"frame": 1, "dir": [x, y, z]}, ...], "forearm_angle_deg": [60, 120]}` replaces the right
    wrist's orientation so a one-handed weapon points along `dir` (fitted space, output frames; directions are slerped
    between keys and held outside them). Meshy animates no weapon, so the source wrist is arbitrary
    (.claude/rules/meshy-mcp.md). A hammer grip holds the blade across the forearm (the Mixamo sword clips: 40-123
    degrees, mostly 70-110), so the asked direction is kept within `forearm_angle_deg` of the forearm line and the
    hand is rolled about the blade to stay on that line: the cutting edge is not a free choice, it faces along the
    forearm, which is the leading side when the arm swings in the plane of forearm and blade. Key the blade trailing
    the swing (pointing back where the hand comes from) and tilting forward at the hit. `blade_local` (R_Wrist bone
    space) defaults to the one-handed sword calibration below. The report gives, per frame, `r_arm` (shoulder ->
    wrist), `forearm`, `wrist_height`, the `blade` / `edge` the model predicts and `wrist_bend_deg` (hand line
    against the forearm), with or without the block.

Run (Blender MCP) - pass `__file__` so relative config paths resolve from the repo root:
    path = r"<repo>/Tools/Blender/meshy_motion_fit.py"
    ns = {"__file__": path, "__name__": "meshy_motion_fit"}; exec(open(path).read(), ns)
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
# One-handed sword in the Player's right hand (WeaponSocket + Archetype_OneHandedSword.drawnPose), expressed in the
# SMPL-H R_Wrist bone's space: least-squares fit of Unity's blade / edge directions (Tools/Blender/sword_blade_measure.cs)
# against the wrist's world rotation over `hook step` and `uppercut step` (mean error 8 degrees, worst 16: the Humanoid
# retarget is not exactly rigid). Re-derive when the drawnPose, the socket or the avatar changes.
SWORD_BLADE_LOCAL = (0.214, 0.662, 0.719)
SWORD_EDGE_LOCAL = (-0.798, -0.307, 0.519)
R_ARM = ("R_Shoulder", "R_Elbow", "R_Wrist", "R_Middle1")
ARMS = {"L": ("L_Shoulder", "L_Elbow", "L_Wrist"), "R": ("R_Shoulder", "R_Elbow", "R_Wrist")}
TOES = ("L_Foot", "R_Foot")  # SMPL-H names the toe bones "Foot": they follow the legs, never an `upper_body` stretch


def _no_duplicate_keys(pairs):
    seen = {}
    for key, value in pairs:
        if key in seen:
            raise ValueError("config key '%s' is given twice (JSON keeps only the last): for two specs on one foot, "
                             "use a list" % key)
        seen[key] = value
    return seen


def load_config(path):
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f, object_pairs_hook=_no_duplicate_keys)
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
    arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
    if arm is None:
        raise ValueError("no armature in %s" % path)
    if arm.animation_data is None or arm.animation_data.action is None:
        raise ValueError("no animation on the armature of %s" % path)
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


def _smooth(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3.0 - 2.0 * x)


def _foot_offsets(cfg, count):
    """Per output frame (1-based) and foot: foot_shift + start_match / end_match offsets, in fitted space."""
    offsets = {key: [Vector((0.0, 0.0, 0.0)) for _ in range(count + 1)] for key in LEGS}

    def check(block, key):
        if key not in LEGS:
            raise ValueError("%s: unknown foot '%s' (use L / R)" % (block, key))

    for key, shift in (cfg.get("foot_shift") or {}).items():
        check("foot_shift", key)
        for frame in range(1, count + 1):
            offsets[key][frame] = offsets[key][frame] + Vector(shift)
    for block, at_start in (("start_match", True), ("end_match", False)):
        for key, specs in (cfg.get(block) or {}).items():
            check(block, key)
            for spec in (specs if isinstance(specs, list) else [specs]):
                if "offset" not in spec or "fade" not in spec or len(spec["offset"]) != 3 or len(spec["fade"]) != 2:
                    raise ValueError("%s.%s: needs \"offset\": [x, y, z] and \"fade\": [f0, f1]" % (block, key))
                f0, f1 = float(spec["fade"][0]), float(spec["fade"][1])
                if not 1.0 <= f0 < f1 <= count:
                    raise ValueError("%s.%s: fade %s must be increasing output frames within 1..%d"
                                     % (block, key, spec["fade"], count))
                offset, lift = Vector(spec["offset"]), float(spec.get("lift_m", 0.0))
                for frame in range(1, count + 1):
                    s = _smooth((frame - f0) / (f1 - f0))
                    weight = 1.0 - s if at_start else s
                    offsets[key][frame] = (offsets[key][frame] + offset * weight
                                           + Vector((0.0, 0.0, lift * math.sin(math.pi * s))))
    return offsets


def _hand_shifts(cfg, count):
    """Per arm: [(offset, (f0, f1, f2, f3))] from `hand_shift`."""
    shifts = {}
    for key, specs in (cfg.get("hand_shift") or {}).items():
        if key not in ARMS:
            raise ValueError("hand_shift: unknown hand '%s' (use L / R)" % key)
        for spec in (specs if isinstance(specs, list) and specs and isinstance(specs[0], dict) else [specs]):
            if not isinstance(spec, dict):
                raise ValueError("hand_shift.%s: needs {\"offset\": [x, y, z], \"frames\": [f0, f1, f2, f3]} (got %r)"
                                 % (key, spec))
            frames = spec.get("frames", [])
            if len(spec.get("offset", [])) != 3 or len(frames) != 4 or not 1 <= frames[0] <= frames[1] <= frames[2] <= frames[3] <= count:
                raise ValueError("hand_shift.%s: needs \"offset\": [x, y, z] and \"frames\": [f0, f1, f2, f3] "
                                 "in order within 1..%d" % (key, count))
            shifts.setdefault(key, []).append((Vector(spec["offset"]), [float(f) for f in frames]))
    return shifts


def _shift_hand(arm, key, offset):
    """Move the wrist by `offset`: 2-bone solve of shoulder / elbow, elbow kept on its side, hand orientation kept."""
    upper, fore, wrist = ARMS[key]
    shoulder = _world(arm, upper).translation.copy()
    elbow = _world(arm, fore).translation.copy()
    hand = _world(arm, wrist).copy()
    l1, l2 = (elbow - shoulder).length, (hand.translation - elbow).length
    reach = hand.translation + offset - shoulder
    dist = min(max(reach.length, 1e-6), REACH * (l1 + l2))
    axis = reach.normalized()
    target = shoulder + axis * dist
    along = (l1 * l1 - l2 * l2 + dist * dist) / (2.0 * dist)
    height = math.sqrt(max(l1 * l1 - along * along, 0.0))
    hint = elbow - shoulder
    bend = hint - hint.dot(axis) * axis
    if bend.length < 1e-6:
        bend = Vector((0.0, 0.0, -1.0)) - Vector((0.0, 0.0, -1.0)).dot(axis) * axis
    _aim(arm, upper, fore, shoulder, shoulder + axis * along + bend.normalized() * height)
    _aim(arm, fore, wrist, _world(arm, fore).translation.copy(), target)
    now = _world(arm, wrist)
    _set_world(arm, wrist, Matrix.LocRotScale(now.translation, hand.to_quaternion(), now.to_scale()))


def _frame(primary, secondary):
    """Orthonormal basis (columns: primary, secondary made perpendicular, their cross product)."""
    x = Vector(primary).normalized()
    y = Vector(secondary)
    y = y - y.dot(x) * x
    if y.length < 1e-4:
        raise ValueError("blade_aim: %s is parallel to %s" % (list(secondary), list(primary)))
    y.normalize()
    z = x.cross(y)
    return Matrix((x, y, z)).transposed()


def _blade_aim(cfg, count):
    """Per output frame (1-based): the blade direction asked by `blade_aim` (key directions slerped, held outside the
    keys), or None without the block. Returns (directions, blade_local, (min, max) angle to the forearm)."""
    aim = cfg.get("blade_aim")
    blade_local = Vector((aim or {}).get("blade_local", SWORD_BLADE_LOCAL))
    if blade_local.length < 1e-6:
        raise ValueError("blade_aim.blade_local: needs a non-zero direction")
    blade_local.normalize()
    if not aim:
        return None, blade_local, None
    limits = aim.get("forearm_angle_deg", [60.0, 120.0])
    if len(limits) != 2 or not 5.0 <= limits[0] < limits[1] <= 175.0:
        raise ValueError("blade_aim.forearm_angle_deg: needs [min, max] within 5..175 (got %s)" % limits)
    keys, last = [], 0.0
    for key in aim.get("keys") or []:
        if "frame" not in key or len(key.get("dir", [])) != 3 or Vector(key["dir"]).length < 1e-6:
            raise ValueError("blade_aim key %s: needs \"frame\" and a non-zero \"dir\": [x, y, z]" % key)
        frame = float(key["frame"])
        if not max(last, 1.0 - 1e-9) < frame + (1e-9 if not keys else 0.0) <= count:
            raise ValueError("blade_aim: key frames must increase within 1..%d (got %s)" % (count, frame))
        direction = Vector(key["dir"]).normalized()
        if keys and keys[-1][1].dot(direction) < -0.999:
            raise ValueError("blade_aim: keys at frames %s and %s point opposite ways - add a key between them"
                             % (keys[-1][0], frame))
        last = frame
        keys.append((frame, direction))
    if not keys:
        raise ValueError("blade_aim: needs at least one key")
    out = [None] * (count + 1)
    for frame in range(1, count + 1):
        if frame <= keys[0][0]:
            out[frame] = keys[0][1].copy()
        elif frame >= keys[-1][0]:
            out[frame] = keys[-1][1].copy()
        else:
            for (f0, d0), (f1, d1) in zip(keys, keys[1:]):
                if f0 <= frame <= f1:
                    out[frame] = d0.slerp(d1, (frame - f0) / (f1 - f0))
                    break
    return out, blade_local, (math.radians(limits[0]), math.radians(limits[1]))


def _aim_blade(arm, wanted, blade_local, limits):
    """Turn the right wrist so the blade points along `wanted`, kept within `limits` of the forearm line (a hammer
    grip holds the blade across the forearm), with the roll about the blade that leaves the hand closest to the
    forearm's own line - the wrist then only bends by the blade's tilt. The cutting edge follows from the grip."""
    upper, fore, wrist, finger = R_ARM
    hand = _world(arm, wrist)
    forearm = (hand.translation - _world(arm, fore).translation).normalized()
    blade = wanted.normalized()
    angle = forearm.angle(blade, math.pi / 2.0)
    clamped = min(max(angle, limits[0]), limits[1])
    if abs(clamped - angle) > 1e-6:
        side = blade - blade.dot(forearm) * forearm
        if side.length < 1e-6:  # blade asked along the arm: no side to keep, lean it on the hand's current one
            side = hand.to_quaternion() @ blade_local
            side = side - side.dot(forearm) * forearm
        side.normalize()
        blade = forearm * math.cos(clamped) + side * math.sin(clamped)
    hand_local = (hand.inverted() @ _world(arm, finger).translation).normalized()  # wrist -> middle finger, bone space
    rotation = (_frame(blade, forearm) @ _frame(blade_local, hand_local).transposed()).to_quaternion()
    _set_world(arm, wrist, Matrix.LocRotScale(hand.translation, rotation, hand.to_scale()))


def run(cfg):
    time_map = cfg.get("time_map")
    if time_map is not None:
        if not isinstance(time_map, list) or any(not isinstance(pair, (list, tuple)) for pair in time_map):
            raise ValueError("time_map: needs a list of [output frame, source frame] pairs (got %r)" % (time_map,))
        clash = [key for key in ("first_frame", "last_frame", "speed") if key in cfg]
        if clash:
            raise ValueError("time_map replaces %s: remove them from the config" % ", ".join(clash))
        if len(time_map) < 2 or any(len(pair) != 2 for pair in time_map) or float(time_map[0][0]) != 1.0:
            raise ValueError("time_map: needs [[1, source], [output, source], ...] with at least two pairs")
        for (o0, s0), (o1, s1) in zip(time_map, time_map[1:]):
            if not (o0 < o1 and s0 < s1) or float(o1) != int(o1):
                raise ValueError("time_map: whole output frames and source frames must both increase (%s -> %s)"
                                 % ([o0, s0], [o1, s1]))
        source_frames = []
        for (o0, s0), (o1, s1) in zip(time_map, time_map[1:]):
            for out in range(int(o0), int(o1)):
                source_frames.append(s0 + (s1 - s0) * (out - o0) / (o1 - o0))
        source_frames.append(float(time_map[-1][1]))
        first, last = source_frames[0], source_frames[-1]
    else:
        first, last, speed = float(cfg["first_frame"]), float(cfg["last_frame"]), float(cfg["speed"])
        if speed <= 0.0:
            raise ValueError("speed must be > 0 (got %s)" % speed)
        if last <= first:
            raise ValueError("last_frame (%s) must be after first_frame (%s)" % (last, first))
        source_frames = [first + i * speed for i in range(int(math.floor((last - first) / speed + 1e-6)) + 1)]
    scene = bpy.context.scene
    arm = _import(cfg["source_fbx"])
    scene.render.fps = cfg.get("fps", 30)
    source_action = arm.animation_data.action
    # One frame of slack at the start: a stripped source's action starts at frame 2 and frame 1 holds its first pose
    # (the sword cut config starts there).
    if time_map and (first < source_action.frame_range[0] - 1 or last > source_action.frame_range[1]):
        raise ValueError("time_map: source frames %s..%s are outside the clip %s"
                         % (first, last, list(source_action.frame_range)))
    count = len(source_frames)
    foot_offsets = _foot_offsets(cfg, count)
    blade_aim, blade_local, blade_limits = _blade_aim(cfg, count)
    hand_shifts = _hand_shifts(cfg, count)
    facing = Vector(cfg.get("forward", [0.0, -1.0, 0.0]))
    facing.z = 0.0  # travel and its sideways part are measured on the ground plane
    if facing.length < 1e-6:
        raise ValueError("forward must have a horizontal part (got %s)" % cfg.get("forward"))
    facing.normalize()
    # travel line: `forward` turned to the character's right (clockwise seen from above) by travel_yaw_deg
    forward = Matrix.Rotation(-math.radians(cfg.get("travel_yaw_deg", 0.0)), 3, 'Z') @ facing
    tracked = [PELVIS] + [b for leg in LEGS.values() for b in leg]

    # 1. sample the source
    frames = []
    for i in range(count):
        sf = source_frames[i]
        scene.frame_set(int(math.floor(sf)), subframe=sf - math.floor(sf))
        bpy.context.view_layer.update()
        frames.append({
            "basis": {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones},
            "world": {b: _world(arm, b).copy() for b in tracked},
        })

    # upper body from another stretch of the source (upper_body): bases of every bone outside the legs + pelvis facing
    upper_cfg = cfg.get("upper_body")
    leg_bones = {b for leg in LEGS.values() for b in leg} | set(TOES)
    if upper_cfg:
        if "first_frame" not in upper_cfg:
            raise ValueError("upper_body: needs \"first_frame\"")
        u_first, u_speed = float(upper_cfg["first_frame"]), float(upper_cfg.get("speed", 1.0))
        upper_keep = upper_cfg.get("keep_prefixes", [])  # bones that stay on the legs' stretch
        if not isinstance(upper_keep, list) or any(not isinstance(p, str) or not p for p in upper_keep):
            raise ValueError("upper_body.keep_prefixes: needs a list of non-empty strings (got %r)" % (upper_keep,))
        upper_keep = tuple(upper_keep)
        if u_speed <= 0.0:
            raise ValueError("upper_body.speed must be > 0 (got %s)" % u_speed)
        u_last = u_first + (source_frames[-1] - first) * u_speed
        if u_first < source_action.frame_range[0] or u_last > source_action.frame_range[1]:
            raise ValueError("upper_body: source frames %s..%s are outside the clip %s"
                             % (u_first, u_last, list(source_action.frame_range)))
        for i, frame in enumerate(frames):
            sf = u_first + (source_frames[i] - first) * u_speed  # same retime as the legs
            scene.frame_set(int(math.floor(sf)), subframe=sf - math.floor(sf))
            bpy.context.view_layer.update()
            frame["upper_basis"] = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones
                                    if pb.name != PELVIS and pb.name not in leg_bones
                                    and not pb.name.startswith(upper_keep)}
            frame["upper_pelvis_rot"] = _world(arm, PELVIS).to_quaternion()

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
    phi = math.atan2(facing.y, facing.x) - math.atan2(travel.y, travel.x)
    turn3 = Matrix.Rotation(math.atan2(forward.y, forward.x) - math.atan2(travel.y, travel.x), 3, 'Z')  # every displacement
    body = Matrix.Rotation(phi + math.radians(cfg.get("facing_offset_deg", 0.0)), 4, 'Z')  # start stance, orientations
    body3 = body.to_3x3()
    k_side = cfg.get("lateral_scale", 1.0)
    kz_pelvis = cfg.get("pelvis_height_scale", 1.0)
    kz_foot = cfg.get("foot_lift_scale", 1.0)
    pelvis_drop = float(cfg.get("pelvis_drop_m", 0.0))

    # Heights scale about the pelvis start height (the dip shrinks) and about each ankle's lowest point (the lift
    # shrinks, a planted foot stays on the ground).
    height_ref = {PELVIS: start[PELVIS].z}
    for leg in LEGS.values():
        height_ref[leg[2]] = min(f["world"][leg[2]].translation.z for f in frames)

    def fit_point(name, pos, kz):
        base = start[name]
        # relative to the pelvis start, which lands on the world origin: Unity keeps a clip's own XZ offset as a root
        # offset ("Based Upon: Original"), and blending into the clip then drags the player sideways by that offset
        move = turn3 @ (xy(pos) - xy(base))
        along = forward * move.dot(forward)
        placed = body3 @ (xy(base) - origin) + k * (along + k_side * (move - along))
        placed.z = height_ref[name] + kz * (pos.z - height_ref[name])
        return placed

    def fit_matrix(name, world, kz):
        loc, rot, scale = world.decompose()
        return Matrix.LocRotScale(fit_point(name, loc, kz), body.to_quaternion() @ rot, scale)

    # Pelvis settle-back: per frame, how far to push the pelvis forward again (backtrack_scale < 1).
    backtrack = cfg.get("backtrack_scale", 1.0)
    if not 0.0 <= backtrack <= 1.0:
        raise ValueError("backtrack_scale must be within 0..1 (got %s)" % backtrack)
    settle = 1.0 - backtrack
    pelvis_push, furthest = [], -1e9
    for frame in frames:
        progress = fit_point(PELVIS, frame["world"][PELVIS].translation, kz_pelvis).dot(forward)
        furthest = max(furthest, progress)
        pelvis_push.append(forward * (settle * (furthest - progress)))

    # 2. rebuild every frame on a new action
    arm.animation_data.action = None
    bpy.data.actions.remove(source_action)
    report = {"frames": {}, "reach_clamped_frames": [], "reach_clamp_max_m": 0.0}  # how far a foot fell short of its target
    for i, frame in enumerate(frames):
        out_frame = i + 1
        for pb in arm.pose.bones:
            pb.matrix_basis = frame["basis"][pb.name]
        bpy.context.view_layer.update()
        for name, basis in frame.get("upper_basis", {}).items():
            arm.pose.bones[name].matrix_basis = basis
        pelvis = fit_matrix(PELVIS, frame["world"][PELVIS], kz_pelvis)
        pelvis.translation = pelvis.translation + pelvis_push[i] - Vector((0.0, 0.0, pelvis_drop))
        if "upper_pelvis_rot" in frame:
            pelvis = Matrix.LocRotScale(pelvis.translation, body.to_quaternion() @ frame["upper_pelvis_rot"],
                                        pelvis.to_scale())
        _set_world(arm, PELVIS, pelvis)

        row = {"pelvis": [round(c, 4) for c in _world(arm, PELVIS).translation]}
        for key, (thigh, shin, ankle) in LEGS.items():
            src_hip = frame["world"][thigh].translation
            src_knee = frame["world"][shin].translation
            src_ankle = frame["world"][ankle].translation
            l1, l2 = (src_knee - src_hip).length, (src_ankle - src_knee).length
            hip = _world(arm, thigh).translation.copy()
            target = fit_point(ankle, src_ankle, kz_foot) + foot_offsets[key][out_frame]
            reach = target - hip
            if reach.length > REACH * (l1 + l2):
                report["reach_clamp_max_m"] = round(max(report["reach_clamp_max_m"], reach.length - REACH * (l1 + l2)), 4)
                target = hip + reach.normalized() * REACH * (l1 + l2)
                reach = target - hip
                report["reach_clamped_frames"].append([out_frame, key])
            dist = max(reach.length, 1e-6)
            axis = reach / dist
            along = (l1 * l1 - l2 * l2 + dist * dist) / (2.0 * dist)
            height = math.sqrt(max(l1 * l1 - along * along, 0.0))
            hint = body3 @ (src_knee - src_hip)            # knee stays on the source's side
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

        for key, specs in hand_shifts.items():
            total = Vector((0.0, 0.0, 0.0))
            for offset, (f0, f1, f2, f3) in specs:
                rise = 1.0 if out_frame >= f1 else _smooth((out_frame - f0) / max(f1 - f0, 1e-6))
                fall = 1.0 if out_frame <= f2 else 1.0 - _smooth((out_frame - f2) / max(f3 - f2, 1e-6))
                total = total + offset * (rise * fall if f0 <= out_frame <= f3 else 0.0)
            if total.length > 1e-5:
                _shift_hand(arm, key, total)
        upper, fore, wrist, finger = R_ARM
        if blade_aim is not None:
            missing = [bone for bone in R_ARM if bone not in arm.pose.bones]
            if missing:
                raise ValueError("blade_aim: the rig has no %s" % ", ".join(missing))
            _aim_blade(arm, blade_aim[out_frame], blade_local, blade_limits)
        if all(bone in arm.pose.bones for bone in R_ARM):
            hand = _world(arm, wrist)
            hand_rot = hand.to_quaternion()
            forearm = hand.translation - _world(arm, fore).translation
            row["r_arm"] = [round(c, 3) for c in hand.translation - _world(arm, upper).translation]
            row["blade"] = [round(c, 3) for c in hand_rot @ blade_local]
            # the sword's calibrated edge: only meaningful with the default `blade_local`
            row["edge"] = [round(c, 3) for c in hand_rot @ Vector(SWORD_EDGE_LOCAL)]
            row["forearm"] = [round(c, 3) for c in forearm.normalized()]
            row["wrist_height"] = round(hand.translation.z, 3)
            row["wrist_bend_deg"] = round(math.degrees(
                forearm.angle(_world(arm, finger).translation - hand.translation, 0.0)), 1)

        for pb in arm.pose.bones:
            pb.keyframe_insert(data_path="rotation_quaternion", frame=out_frame)
            if pb.name == PELVIS:
                pb.keyframe_insert(data_path="location", frame=out_frame)
        report["frames"][out_frame] = row

    action = arm.animation_data.action
    action.name = cfg.get("action_name") or os.path.splitext(os.path.basename(cfg["output_fbx"]))[0]
    for fcurve in _fcurves(action):
        for point in fcurve.keyframe_points:
            point.interpolation = 'LINEAR'
    scene.frame_start, scene.frame_end = 1, count
    _export(arm, cfg["output_fbx"])
    progress = [Vector(report["frames"][f]["pelvis"]).dot(forward) for f in sorted(report["frames"])]
    report.update({"net_travel_m": round(progress[-1], 3), "peak_travel_m": round(max(progress), 3),
                   "last_source_frame": round(source_frames[-1], 2)})
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
