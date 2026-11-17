"""Give an in-place unarmed attack clip a forward foot step that matches the scripted PlayerAttackStep, then export a new FBX.

In game the Animator drops the clip's root XZ (not baked, applyRootMotion off) and PlayerAttackStep moves the capsule
forward at constant speed: D(t) = distance * clamp((t - start) / duration, 0, 1), t = seconds since state enter. A foot
is world-fixed when its position relative to the Player root is `W - D(t)` for a constant W. This script rewrites both
feet's paths (toe = contact point, player-local, measured in Unity on the Player rig): source until `step.start`, then
world-fixed except during the foot's lifted moves (`lead_moves` / `rear_moves`, see desired_paths), ending on the source
end stance one step ahead (the clip closes in its guard).
The difference to the source (+ per-frame `correction` from Unity re-measurements) moves each foot as a rigid
translation (foot orientation kept in armature space) through a 2-bone leg IK, targets clamped to the leg's reach;
thighs, shins and feet are baked only where a foot moves. Hips, spine, arms and head are untouched.

Run (Blender MCP) — pass `__file__` so relative config paths resolve from the repo root:
    path = r"<repo>/Tools/Blender/unarmed_foot_step.py"
    ns = {"__file__": path, "__name__": "unarmed_foot_step"}; exec(open(path).read(), ns)
    result = ns["run"](ns["load_config"](r"<repo>/Tools/Blender/configs/unarmed/lead_jab.json"))
Command line: blender -b -P Tools/Blender/unarmed_foot_step.py -- Tools/Blender/configs/unarmed/lead_jab.json

Correction pass (after sampling the exported clip with Tools/Blender/unarmed_foot_step_measure.cs):
    ns["update_correction"](r"<repo>/Tools/Blender/configs/unarmed/lead_jab.json", r"<measured>.json")

The foot height (Y) follows the source on purpose (heel raises, pivots); only the lifted moves add an arc. All times
are clip seconds; samples are at 30 fps (Unity frame index, the Hook's index 0 = source frame 10). Tested on Blender 5.2.
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

LEGS = {
    "Left": ("mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot", "mixamorig:LeftToeBase"),
    "Right": ("mixamorig:RightUpLeg", "mixamorig:RightLeg", "mixamorig:RightFoot", "mixamorig:RightToeBase"),
}
KEY = {"Left": "L", "Right": "R"}
EPS = 1e-4


def _repo_root():
    here = globals().get("__file__")
    if here:
        return os.path.abspath(os.path.join(os.path.dirname(here), "..", ".."))
    return os.getcwd()


def load_config(path):
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    root = cfg.get("repo_root") or _repo_root()
    for key in ("source_fbx", "output_fbx", "unity_source_file", "save_blend"):
        if cfg.get(key) and not os.path.isabs(cfg[key]):
            cfg[key] = os.path.join(root, cfg[key])
    with open(cfg["unity_source_file"], encoding="utf-8") as f:
        cfg["unity_source"] = json.load(f)
    return cfg


def _smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def _lerp(a, b, w):
    return [x + (y - x) * w for x, y in zip(a, b)]


def _sample(path, t):
    """Linear sample of a per-frame (30 fps) list of [x, y, z] at time t."""
    u = max(0.0, min(len(path) - 1.0, t * 30.0))
    i = min(int(u), len(path) - 2)
    return _lerp(path[i], path[i + 1], u - i)


def step_displacement(step, t):
    if step["distance"] <= 0 or step["duration"] <= 0:
        return 0.0
    return step["distance"] * max(0.0, min(1.0, (t - step["start"]) / step["duration"]))


def _plant(path, step, t):
    """World XZ (x, z) where the source has the foot at clip time t, plus the step done by then."""
    p = _sample(path, t)
    return [p[0], p[2] + step_displacement(step, t)]


def desired_paths(cfg):
    """Wanted toe positions (player-local metres) per Unity clip frame, both feet: {"L": [[x, y, z]], "R": ...}.

    Each foot follows the source until `step.start`, then is world-fixed except during its moves. A move
    [t0, t1, T] lifts the foot (smoothstep XZ + sine arc of `<foot>_lift_m`) from where it stands to the source's own
    position at time T plus the step done by T (T null = clip end, i.e. the final guard stance). The last move must end
    on the clip end so the clip closes on its source stance (relative == source at the last frame)."""
    src = cfg["unity_source"]
    step = cfg["step"]
    lead = KEY[cfg["lead"]]
    rear = "R" if lead == "L" else "L"
    for name in ("lead_moves", "rear_moves"):
        moves = cfg[name]
        if not moves or moves[-1][2] is not None:
            raise ValueError(f"{name}: needs at least one move and the last must target the clip end (null)")
        prev_end = step["start"]
        for t0, t1, _target in moves:
            if not prev_end <= t0 < t1:
                raise ValueError(f"{name}: moves must start after step.start / the previous move and have t1 > t0")
            prev_end = t1
        if prev_end > (len(src["L"]) - 1) / 30.0:
            raise ValueError(f"{name}: the last move must end before the clip end")
    out = {}
    for key, moves, lift in ((lead, cfg["lead_moves"], cfg.get("lead_lift_m", 0.0)),
                             (rear, cfg["rear_moves"], cfg.get("rear_lift_m", 0.0))):
        path = src[key]
        n = len(path)
        end_t = (n - 1) / 30.0
        # plant chain: start (source at step start, D = 0 there) then one target per move
        chain = [_plant(path, step, step["start"])]
        for _t0, _t1, target_t in moves:
            chain.append(_plant(path, step, end_t if target_t is None else target_t))
        res = []
        for u in range(n):
            t = u / 30.0
            if t <= step["start"]:
                res.append(list(path[u]))
                continue
            world, arc = chain[0], 0.0
            for i, (t0, t1, _target) in enumerate(moves):
                if t >= t1:
                    world = chain[i + 1]
                elif t > t0:
                    w = _smoothstep((t - t0) / (t1 - t0))
                    world = _lerp(chain[i], chain[i + 1], w)
                    arc = lift * math.sin(math.pi * w)
                    break
                else:
                    break
            res.append([world[0], path[u][1] + arc, world[1] - step_displacement(step, t)])
        out[key] = res
    return out


def _ctx_override(**extra):
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
    first, last = (int(round(v)) for v in arm.animation_data.action.frame_range)
    return arm, first, last


def _empty(name, parent):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.matrix_parent_inverse.identity()  # location == armature space
    return e


def _pole_position(o, offset):
    mid = (o["hip"] + o["ankle"]) * 0.5
    return o["knee"] + (o["knee"] - mid).normalized() * offset


def run(cfg):
    """Apply the step described by cfg and export. Returns a report dict (incl. the wanted Unity paths)."""
    scene = bpy.context.scene
    _clear_scene()
    arm, first, last = _import(cfg["source_fbx"], cfg.get("fps", 30))
    pb = arm.pose.bones
    trim = cfg.get("trim_first_frame", 0)              # Unity clip frame 0 = source frame `trim`
    # Mixamo exports differ: armature space is metres (lead jab, Hook) or centimetres (Uppercut, hips ~88)
    scene.frame_set(first)
    unit = 100.0 if pb["mixamorig:Hips"].head.y > 10.0 else 1.0
    k = cfg.get("unity_to_blender", 1.0) * unit        # Player-rig metres -> source armature units
    pole_offset = cfg.get("pole_offset", 0.4) * unit
    want = desired_paths(cfg)
    src = cfg["unity_source"]
    corr = cfg.get("correction", {})                   # {"L": {"u": [dx, dy, dz]}, ...} accumulated Unity-space fixes
    frames = list(range(first, last + 1))
    if first + trim + len(src["L"]) - 1 != last:
        raise ValueError(f"source sample has {len(src['L'])} frames, but Blender frames {first}-{last} with trim {trim} "
                         f"expect {last - first - trim + 1}: wrong trim_first_frame or unity_source_file?")

    # Unity-space delta per foot per Blender frame -> armature-space delta (+X left = -Unity x, +Y up, +Z forward)
    deltas = {side: {} for side in LEGS}
    for side in LEGS:
        key = KEY[side]
        for u, (w, s) in enumerate(zip(want[key], src[key])):
            c = corr.get(key, {}).get(str(u), [0.0, 0.0, 0.0])
            d = (w[0] - s[0] + c[0], w[1] - s[1] + c[1], w[2] - s[2] + c[2])
            if max(abs(v) for v in d) > EPS:
                deltas[side][first + trim + u] = Vector((-d[0], d[1], d[2])) * k

    # original pose (armature space) per frame
    orig = {}
    for f in frames:
        scene.frame_set(f)
        orig[f] = {side: {"hip": pb[b[0]].head.copy(), "knee": pb[b[1]].head.copy(), "ankle": pb[b[2]].head.copy(),
                          "foot": pb[b[2]].matrix.copy()} for side, b in LEGS.items()}

    report = {"frames": [first, last], "edited": {}, "ankle_miss": {}, "pole_angle_deg": {}}
    baked = {}
    for side, (up, leg, foot, _toe) in LEGS.items():
        edited = sorted(deltas[side])
        if not edited:
            continue
        bake = list(range(max(first, edited[0] - 1), min(last, edited[-1] + 1) + 1))
        target, pole = _empty(f"{side}_IK_Target", arm), _empty(f"{side}_IK_Pole", arm)
        for f in frames:
            pole.location = _pole_position(orig[f][side], pole_offset)
            pole.keyframe_insert("location", frame=f)
        ik = pb[leg].constraints.new('IK')
        ik.target, ik.pole_target = target, pole
        ik.chain_count, ik.use_tail, ik.use_stretch = 2, True, False

        # pole angle: the one that best reproduces the source knee with the (still un-keyed) target on the source ankle
        probe = frames[::4]
        def knee_err(angle):
            ik.pole_angle = math.radians(angle)
            err = 0.0
            for f in probe:
                scene.frame_set(f)
                target.location = orig[f][side]["ankle"]
                bpy.context.view_layer.update()
                err += (pb[leg].head - orig[f][side]["knee"]).length
            return err
        best = min(range(-180, 180, 10), key=knee_err)
        best = min((best + x * 0.5 for x in range(-20, 21)), key=knee_err)
        ik.pole_angle = math.radians(best)
        report["pole_angle_deg"][side] = best
        report.setdefault("pole_knee_err_m", {})[side] = round(knee_err(best) / len(probe) / unit, 4)
        # keep every target inside the leg's reach: an out-of-reach target makes the 2-bone IK spin the leg around
        # (the foot pops behind the other one); clamped, the foot just stops short at full extension
        reach = (pb[up].bone.length + pb[leg].bone.length) * cfg.get("max_leg_extension", 0.985)
        clamped = []
        for f in frames:
            goal = orig[f][side]["ankle"] + deltas[side].get(f, Vector())
            v = goal - orig[f][side]["hip"]
            if v.length > reach:
                goal = orig[f][side]["hip"] + v.normalized() * reach
                clamped.append(f)
            target.location = goal
            target.keyframe_insert("location", frame=f)
        report.setdefault("reach_clamped_frames", {})[side] = clamped

        misses = []
        for f in frames:
            ik.influence = 1.0 if f in bake else 0.0
            ik.keyframe_insert("influence", frame=f)
        for f in bake:
            scene.frame_set(f)
            misses.append((pb[foot].head - target.location).length)
            mats = {n: arm.convert_space(pose_bone=pb[n], matrix=pb[n].matrix.copy(), from_space='POSE', to_space='LOCAL')
                    for n in (up, leg)}
            m = orig[f][side]["foot"].copy()      # keep the foot's armature-space orientation, move it with the IK
            m.translation = pb[foot].head
            mats[foot] = arm.convert_space(pose_bone=pb[foot], matrix=m, from_space='POSE', to_space='LOCAL')
            baked.setdefault(f, {}).update(mats)
        pb[leg].constraints.remove(ik)
        bpy.data.objects.remove(target)
        bpy.data.objects.remove(pole)
        report["edited"][side] = [edited[0], edited[-1]]
        report["ankle_miss"][side] = round(max(misses) / unit, 4)  # metres

    # the IK of the second leg was evaluated with the first leg still un-keyed: key everything at the end
    for f, mats in sorted(baked.items()):
        for n, m in mats.items():
            pb[n].rotation_mode = 'QUATERNION'
            pb[n].rotation_quaternion = m.to_quaternion()
            pb[n].keyframe_insert("rotation_quaternion", frame=f)

    flips = 0
    for n in {n for mats in baked.values() for n in mats}:
        prev = None
        for f in frames:
            scene.frame_set(f)
            q = pb[n].rotation_quaternion.copy()
            if prev is not None and prev.dot(q) < 0:
                flips += 1
            prev = q
    report["quat_flips"] = flips

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
    report["output"] = cfg["output_fbx"]
    report["desired"] = want
    return report


def update_correction(config_path, measured_path, gain=0.8, clamp_m=0.08):
    """One measure -> correct pass: correction[foot][frame] += clamp(gain * (wanted - measured)), per axis, for frames
    after step.start. `measured_path` = unarmed_foot_step_measure.cs output for the exported clip. A damped, clamped step:
    the command -> result gain is well below 1 and cross-coupled (Unity's centre-of-mass root, leg muscle retarget), so a
    full or secant step overshoots into straight knees. Writes the config back; returns the max XZ error before the update."""
    cfg = load_config(config_path)
    want = desired_paths(cfg)
    with open(measured_path, encoding="utf-8") as f:
        got = json.load(f)
    with open(config_path, encoding="utf-8") as f:
        raw = json.load(f)                               # keep the stored (relative) paths
    corr = raw.setdefault("correction", {})
    worst = {}
    for key in ("L", "R"):
        if len(got[key]) != len(want[key]):
            raise ValueError(f"measured {key} has {len(got[key])} frames, wanted {len(want[key])}")
        per = corr.setdefault(key, {})
        for u, (w, o) in enumerate(zip(want[key], got[key])):
            worst[key] = max(worst.get(key, 0.0), math.hypot(w[0] - o[0], w[2] - o[2]))
            if u / 30.0 <= cfg["step"]["start"]:
                continue
            c = per.get(str(u), [0.0, 0.0, 0.0])
            per[str(u)] = [round(c[a] + max(-clamp_m, min(clamp_m, gain * (w[a] - o[a]))), 4) for a in range(3)]
    with open(config_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(raw, f, indent=2)
    return {k: round(v, 4) for k, v in worst.items()}


if __name__ == "__main__" and "--" in sys.argv:
    _args = sys.argv[sys.argv.index("--") + 1:]
    if not _args:
        raise SystemExit("usage: blender -b -P unarmed_foot_step.py -- <config.json>")
    rep = run(load_config(_args[0]))
    rep.pop("desired")
    print(json.dumps(rep, indent=2))
