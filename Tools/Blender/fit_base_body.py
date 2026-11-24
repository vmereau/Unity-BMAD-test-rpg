"""Fit the Meshy base hero v2 body onto the canonical skeleton (spec-base-body-regen).

Run through the connected Blender (MCP add-on):
    ns = runpy.run_path(root + "Tools/Blender/fit_base_body.py"); ns["run"](ns["load_config"](cfg_path))

Inputs never change: the raw Meshy GLB (sources/base_hero_v2), `base hero split.fbx` (canonical armature, body seam
ring, neck-prepared head). Every non-finger bone keeps its canonical rest transform; the finger chains are rebuilt
(5 fingers x 3 bones + tip) at the fitted knuckles. Work happens in a temporary scene; the active scene is restored.

Units: Mixamo FBX import puts the armature at scale 0.01 with a +90 deg X rotation, so 1 m = 0.01 world units.
Everything below is measured in metres in "character space": x = character's left (+X), y = back (+Y, the
character faces -Y), z = up, feet at z = 0.
"""
from pathlib import Path
import json
import math
import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2] if "__file__" in globals() else Path(r"D:/github/perso/Unity-BMAD-test-rpg")
SIDES = ("Left", "Right")
FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")
PFX = "mixamorig:"


def load_config(path):
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    for k in ("source_glb", "canonical_split", "output_fbx", "texture_dir"):
        cfg[k] = str(ROOT / cfg[k])
    return cfg


# --------------------------------------------------------------------------------------------- scene helpers

def _new_scene(name):
    old = bpy.data.scenes.get(name)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.scenes.remove(old)
    sc = bpy.data.scenes.new(name)
    bpy.context.window.scene = sc
    return sc


def _import_canonical(path, sc):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == 'ARMATURE')
    meshes = {o.name.split(".")[0]: o for o in new if o.type == 'MESH'}
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    return arm, meshes


def _import_source(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    mesh = next(o for o in new if o.type == 'MESH')
    for o in new:
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    mesh.parent = None
    mesh.data.transform(mesh.matrix_world)
    mesh.matrix_world = Matrix.Identity(4)
    # glTF splits vertices along UV seams: weld them (UVs stay per corner) so the surface is one closed skin
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.to_mesh(mesh.data)
    bm.free()
    return mesh


def _decimate(mesh, max_faces):
    if max_faces and len(mesh.data.polygons) > max_faces:
        mod = mesh.modifiers.new("dec", 'DECIMATE')
        mod.ratio = max_faces / len(mesh.data.polygons)
        mod.use_collapse_triangulate = True
        with bpy.context.temp_override(object=mesh, active_object=mesh):
            bpy.ops.object.modifier_apply(modifier=mod.name)


def _co(obj, world=True):
    n = len(obj.data.vertices)
    a = np.empty(n * 3)
    obj.data.vertices.foreach_get("co", a)
    a = a.reshape(-1, 3)
    if world:
        m = np.array(obj.matrix_world)
        a = a @ m[:3, :3].T + m[:3, 3]
    return a


def _bone_points(arm):
    """Armature rest heads / tails in character metres."""
    mw = arm.matrix_world
    k = 100.0  # 0.01 world unit = 1 m
    out = {}
    for b in arm.data.bones:
        out[b.name[len(PFX):] if b.name.startswith(PFX) else b.name] = (
            Vector(mw @ b.head_local) * k, Vector(mw @ b.tail_local) * k)
    return out


# --------------------------------------------------------------------------------------------- measurement

def _slice_stats(co, axis, lo, hi, mask=None):
    m = (co[:, axis] >= lo) & (co[:, axis] < hi)
    if mask is not None:
        m &= mask
    p = co[m]
    if len(p) < 8:
        return None
    other = [i for i in range(3) if i != axis]
    c = p.mean(0)
    ext = p[:, other].max(0) - p[:, other].min(0)
    return c, ext


def _profile(co, axis, start, stop, step, mask):
    """Centroid + extents of thin slices along an axis."""
    rows = []
    x = start
    while (x < stop) if step > 0 else (x > stop):
        s = _slice_stats(co, axis, min(x, x + step), max(x, x + step), mask)
        if s:
            rows.append((x + step / 2, s[0], s[1]))
        x += step
    return rows


def measure(co, bones, cfg):
    """Joint estimates on the aligned source mesh (metres). Arms: shoulder/elbow by canonical ratio, wrist at
    the thinnest forearm slice before the hand. Legs: knee/ankle by canonical height ratio, centred on slices."""
    out = {}
    for side, sgn in (("Left", 1), ("Right", -1)):
        sx = co[:, 0] * sgn
        arm_mask = (sx > 0.30) & (co[:, 2] > 1.2)
        prof = _profile(co * np.array([sgn, 1, 1]), 0, 0.50, 0.95, 0.005, arm_mask)
        # hand starts where the vertical extent (palm forward: hand width is along z) jumps after the wrist minimum
        thick = [(x, e[1] + e[0]) for x, c, e in prof]  # extents of (y, z)
        wr = min((t for t in thick if 0.55 < t[0] < 0.80), key=lambda t: t[1])
        wrist_x = wr[0]
        sl = _slice_stats(co * np.array([sgn, 1, 1]), 0, wrist_x - 0.005, wrist_x + 0.005, arm_mask)
        wrist = Vector((wrist_x * sgn, sl[0][1], sl[0][2]))
        cs = bones[f"{side}Arm"][0]
        cw = bones[f"{side}Hand"][0]
        ce = bones[f"{side}ForeArm"][0]
        r = (ce - cs).length / (cw - cs).length
        sh = Vector(cs)  # shoulder joint is inside the torso: keep canonical
        el_x = sh.x + (wrist.x - sh.x) * r
        es = _slice_stats(co * np.array([sgn, 1, 1]), 0, abs(el_x) - 0.01, abs(el_x) + 0.01, arm_mask)
        elbow = Vector((el_x, es[0][1], es[0][2]))
        hand_pts = co[(sx > wrist_x) & arm_mask]
        out[side] = {"shoulder": sh, "elbow": elbow, "wrist": wrist,
                     "tip_x": float(hand_pts[:, 0].max() if sgn > 0 else hand_pts[:, 0].min()),
                     "hand_count": int(len(hand_pts))}
        # legs
        leg_mask = (sx > 0.0) & (co[:, 2] < 0.9)
        for joint, bname in (("knee", f"{side}Leg"), ("ankle", f"{side}Foot")):
            z = bones[bname][0].z
            s = _slice_stats(co, 2, z - 0.01, z + 0.01, leg_mask & (np.abs(co[:, 1]) < 0.2))
            out[side][joint] = Vector((s[0][0], s[0][1], z)) if s else None
    return out


def align(src, canon_body, canon_head, cfg):
    """Uniform scale (crown height) + translation (feet on the floor, torso centred) of the source mesh."""
    sco = _co(src)
    cco = np.vstack([_co(canon_body), _co(canon_head)]) * 100.0
    s_h = sco[:, 2].max() - sco[:, 2].min()
    c_h = cco[:, 2].max() - cco[:, 2].min()
    k = c_h / s_h * cfg.get("scale_adjust", 1.0)
    sco = sco * k
    sco[:, 2] -= sco[:, 2].min() - cco[:, 2].min()
    band = lambda a: a[(a[:, 2] > 1.1) & (a[:, 2] < 1.4) & (np.abs(a[:, 0]) < 0.2)]
    d = band(cco).mean(0) - band(sco).mean(0)
    sco[:, 0] += d[0]
    sco[:, 1] += d[1]
    return sco, {"scale": k, "src_height": float(s_h * k), "canon_height": float(c_h), "shift": d[:2].tolist()}


# --------------------------------------------------------------------------------------------- deformation

def _smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _rot_between(a, b):
    a = np.asarray(a, float) / np.linalg.norm(a)
    b = np.asarray(b, float) / np.linalg.norm(b)
    return np.array(Vector(a).rotation_difference(Vector(b)).to_matrix())


def _axis_rot(axis, deg):
    return np.array(Matrix.Rotation(math.radians(deg), 3, Vector(axis).normalized()))


def _seg_map(a_s, b_s, a_t, b_t, scale_len=True, extra=None):
    """Affine map (3x3, translation) of a source bone segment onto its target: rotate the direction, stretch along
    the axis to the target length, move the head. extra: rotation applied in source space first (twist)."""
    a_s, b_s, a_t, b_t = (np.asarray(v, float) for v in (a_s, b_s, a_t, b_t))
    d_s = b_s - a_s
    ls = np.linalg.norm(d_s)
    u = d_s / ls
    k = (np.linalg.norm(b_t - a_t) / ls) if scale_len else 1.0
    S = np.eye(3) + (k - 1.0) * np.outer(u, u)
    R = _rot_between(d_s, b_t - a_t)
    A = R @ S
    if extra is not None:
        A = A @ extra
    return A, a_t - A @ a_s


def _param(co, a, b):
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = b - a
    return ((co - a) @ d) / (d @ d)


def deform(co, bones, j, cfg):
    """Twist the forearms palm-down, then map arm / leg segments onto the canonical joints with blend zones."""
    out = co.copy()
    blend = cfg.get("joint_blend_m", 0.04)
    for side, sgn in (("Left", 1), ("Right", -1)):
        J = j[side]
        sx = co[:, 0] * sgn
        # ---------------- arm
        arm = (sx > 0.12) & (co[:, 2] > 1.15)
        sh_t = np.asarray(bones[f"{side}Arm"][0]); el_t = np.asarray(bones[f"{side}ForeArm"][0])
        wr_t = np.asarray(bones[f"{side}Hand"][0]); hd_t = np.asarray(bones[f"{side}Hand"][1])
        sh_s, el_s, wr_s = (np.asarray(J[k]) for k in ("shoulder", "elbow", "wrist"))
        hand_pts = co[arm & (sx > abs(wr_s[0]) + 0.02)]
        tip_s = hand_pts.mean(0)
        # source hand axis: wrist -> hand centroid, target: canonical hand bone direction at the same distance
        hd_s = wr_s + (tip_s - wr_s) / np.linalg.norm(tip_s - wr_s) * np.linalg.norm(hd_t - wr_t)
        twist = cfg.get("hand_twist_deg", 90.0) * sgn
        fa_axis = wr_s - el_s
        # per-vertex twist about the source forearm axis, 0 at the elbow -> full at the wrist
        t_fa = _param(co, el_s, wr_s)
        ang = twist * _smooth((t_fa - 0.05) / 0.95)
        tw = np.zeros_like(co)
        for i in np.nonzero(arm & (t_fa > 0.0))[0]:
            R = _axis_rot(fa_axis, ang[i])
            tw[i] = R @ (co[i] - el_s) + el_s - co[i]
        src = co + tw
        # hand frame after the twist: rotate the hand point too
        Rh = _axis_rot(fa_axis, twist)
        hd_s2 = Rh @ (hd_s - el_s) + el_s
        maps = [
            (np.eye(3), np.zeros(3)),
            _seg_map(sh_s, el_s, sh_t, el_t),
            _seg_map(el_s, wr_s, el_t, wr_t),
            _seg_map(wr_s, hd_s2, wr_t, hd_t, scale_len=False),
        ]
        # weights along the arm: torso -> upper (shoulder fade), upper -> fore (elbow), fore -> hand (wrist)
        x = np.abs(src[:, 0])
        L_el, L_wr = abs(el_s[0]), abs(wr_s[0])
        w_up = _smooth((x - cfg.get("shoulder_fade", [0.14, 0.24])[0]) /
                       (cfg.get("shoulder_fade", [0.14, 0.24])[1] - cfg.get("shoulder_fade", [0.14, 0.24])[0]))
        w_fa = _smooth((x - (L_el - blend / 2)) / blend)
        w_hd = _smooth((x - (L_wr - blend / 4)) / (blend / 2))
        W = np.stack([1 - w_up, w_up * (1 - w_fa), w_up * w_fa * (1 - w_hd), w_up * w_fa * w_hd], 1)
        res = np.zeros_like(co)
        for k, (A, t) in enumerate(maps):
            res += W[:, k:k + 1] * (src @ A.T + t)
        out[arm] = res[arm]
        # ---------------- leg
        leg = ((sx > 0) if sgn > 0 else (sx >= 0)) & (co[:, 2] < 1.0) & (np.abs(co[:, 0]) < 0.4)
        hip_t = np.asarray(bones[f"{side}UpLeg"][0]); kn_t = np.asarray(bones[f"{side}Leg"][0])
        an_t = np.asarray(bones[f"{side}Foot"][0])
        kn_s, an_s = np.asarray(J["knee"]), np.asarray(J["ankle"])
        hip_s = hip_t.copy()
        toe_s = an_s + (np.asarray(bones[f"{side}Foot"][1]) - an_t)
        lm = [
            (np.eye(3), np.zeros(3)),
            _seg_map(hip_s, kn_s, hip_t, kn_t),
            _seg_map(kn_s, an_s, kn_t, an_t),
            _seg_map(an_s, toe_s, an_t, np.asarray(bones[f"{side}Foot"][1]), scale_len=False),
        ]
        z = co[:, 2]
        hz = cfg.get("hip_fade", [0.80, 0.92])
        w_th = 1 - _smooth((z - hz[0]) / (hz[1] - hz[0]))
        w_sh = 1 - _smooth((z - (kn_s[2] - blend / 2)) / blend)
        w_ft = 1 - _smooth((z - (an_s[2] - blend / 4)) / (blend / 2))
        W = np.stack([1 - w_th, w_th * (1 - w_sh), w_th * w_sh * (1 - w_ft), w_th * w_sh * w_ft], 1)
        res = np.zeros_like(co)
        for k, (A, t) in enumerate(lm):
            res += W[:, k:k + 1] * (co @ A.T + t)
        out[leg] = res[leg]
    return out


def _set_co(obj, co_m, arm):
    """Write character-metre coordinates back as world coordinates of the canonical import (x0.01)."""
    w = co_m / 100.0
    obj.data.vertices.foreach_set("co", w.ravel())
    obj.matrix_world = Matrix.Identity(4)
    obj.data.update()


def fitted_scene(cfg, keep=True):
    """Steps 1-2: canonical import + source aligned and deformed onto the canonical joints, in scene _BodyV2Fit."""
    sc = _new_scene("_BodyV2Fit")
    arm, meshes = _import_canonical(cfg["canonical_split"], sc)
    src = _import_source(cfg["source_glb"])
    src.name = "base_hero_v2_src"
    bones = _bone_points(arm)
    sco, info = align(src, meshes["base_hero_body"], meshes["base_hero_head"], cfg)
    j = measure(sco, bones, cfg)
    fco = deform(sco, bones, j, cfg)
    _set_co(src, fco, arm)
    return sc, arm, meshes, src, bones, info


def inspect(cfg):
    """Step 1 (read only): align the source and report joint estimates vs the canonical skeleton."""
    prev = bpy.context.window.scene
    sc = _new_scene("_BodyV2Fit")
    try:
        arm, meshes = _import_canonical(cfg["canonical_split"], sc)
        src = _import_source(cfg["source_glb"])
        bones = _bone_points(arm)
        sco, info = align(src, meshes["base_hero_body"], meshes["base_hero_head"], cfg)
        j = measure(sco, bones, cfg)
        rep = {"align": info}
        for side in SIDES:
            rep[side] = {}
            for key, bname in (("elbow", "ForeArm"), ("wrist", "Hand"), ("knee", "Leg"), ("ankle", "Foot")):
                c = bones[f"{side}{bname}"][0]
                m = j[side][key]
                rep[side][key] = {"canon": [round(v, 3) for v in c], "src": [round(v, 3) for v in m] if m else None,
                                  "delta_cm": round((m - c).length * 100, 1) if m else None}
            rep[side]["tip_x"] = round(j[side]["tip_x"], 3)
            rep[side]["canon_tip_x"] = round(bones[f"{side}HandMiddle4"][1].x, 3)
        return rep
    finally:
        bpy.context.window.scene = prev


# --------------------------------------------------------------------------------------------- neck seam

def _canon_ring(body):
    """Boundary ring of the canonical body at the exposed seam: per vertex world-metre position, normal, groups."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.verts.ensure_lookup_table()
    mw = body.matrix_world
    # walk the boundary in edge order (the zip pairs loops by arc length, not by angle)
    start = next(v for v in bm.verts if v.is_boundary)
    ring, prev, cur = [], None, start
    while True:
        ring.append(cur)
        nxt = next(e.other_vert(cur) for e in cur.link_edges if e.is_boundary and e.other_vert(cur) is not prev)
        prev, cur = cur, nxt
        if cur is start:
            break
    if len(ring) != sum(1 for v in bm.verts if v.is_boundary):
        raise RuntimeError("canonical body boundary is not a single loop")
    me = body.data
    # corner normals averaged per vertex (the seam normals are smooth there)
    nrm = {v.index: Vector() for v in ring}
    for lp in me.loops:
        if lp.vertex_index in nrm:
            nrm[lp.vertex_index] += me.corner_normals[lp.index].vector
    names = [g.name for g in body.vertex_groups]
    out = []
    for v in ring:
        mv = me.vertices[v.index]
        out.append({"co": np.array(mw @ v.co) * 100.0,
                    "n": (mw.to_3x3() @ nrm[v.index]).normalized(),
                    "w": {names[g.group]: g.weight for g in mv.groups if g.weight > 0}})
    bm.free()
    return out


def _bvh_of(obj, scale=100.0):
    mw = obj.matrix_world
    verts = [tuple(np.array(mw @ v.co) * scale) for v in obj.data.vertices]
    polys = [tuple(p.vertices) for p in obj.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def neck_blend(src, canon_body, cfg):
    """Pull the source neck onto the canonical neck surface below the seam: project along the vertex normal (nearest
    point folds vertices into the concave throat notch), smooth the zone, then push anything left inside back out
    (the head's inset neck stump lives just under the canonical surface)."""
    bvh = _bvh_of(canon_body)
    src.data.update()
    co = _co(src) * 100.0
    nrm = np.empty(len(co) * 3)
    src.data.vertices.foreach_get("normal", nrm)
    nrm = nrm.reshape(-1, 3)
    z0, z1 = cfg.get("neck_blend_z", [1.49, 1.55])
    axis = np.array(cfg.get("neck_axis_xy", [0.0, 0.03]))
    r_in, r_out = cfg.get("neck_blend_r", [0.085, 0.12])
    rad = np.linalg.norm(co[:, :2] - axis, axis=1)
    w = _smooth((co[:, 2] - z0) / (z1 - z0)) * (1 - _smooth((rad - r_in) / (r_out - r_in)))
    zone = np.nonzero(w > 0)[0]
    reach = cfg.get("neck_ray_m", 0.04)
    for i in zone:
        p0, n = Vector(co[i]), Vector(nrm[i])
        best = None
        for d in (n, -n):
            hit = bvh.ray_cast(p0, d, reach)
            if hit[0] is not None and (best is None or hit[3] < best[1]):
                best = (np.array(hit[0]), hit[3])
        if best is None:
            best = (np.array(bvh.find_nearest(p0)[0]), 0)
        co[i] = co[i] + (best[0] - co[i]) * w[i]
    me = src.data
    ev = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ev)
    ev = ev.reshape(-1, 2)

    def smooth(co, k, iters):
        for _ in range(iters):
            acc = np.zeros_like(co)
            cnt = np.zeros(len(co))
            np.add.at(acc, ev[:, 0], co[ev[:, 1]])
            np.add.at(acc, ev[:, 1], co[ev[:, 0]])
            np.add.at(cnt, ev[:, 0], 1)
            np.add.at(cnt, ev[:, 1], 1)
            avg = acc / np.maximum(cnt, 1)[:, None]
            co = co * (1 - k[:, None]) + avg * k[:, None]
        return co

    co = smooth(co, 0.5 * w, cfg.get("neck_smooth_iter", 6))
    # graded push out of the canonical surface (the head's inset stump lives just under it), then smooth again
    clear = cfg.get("neck_clearance_m", 0.0015)
    zp, rp = cfg.get("neck_push_z", [1.45, 1.49]), cfg.get("neck_push_r", [0.09, 0.12])
    rad = np.linalg.norm(co[:, :2] - axis, axis=1)
    g = _smooth((co[:, 2] - zp[0]) / (zp[1] - zp[0])) * (1 - _smooth((rad - rp[0]) / (rp[1] - rp[0])))
    pushed = 0
    for _ in range(cfg.get("neck_push_passes", 3)):
        for i in np.nonzero(g > 0)[0]:
            hit = bvh.find_nearest(Vector(co[i]))
            if hit[0] is None:
                continue
            p, n = np.array(hit[0]), np.array(hit[1])
            depth = (co[i] - p) @ n
            if depth < clear:
                co[i] = co[i] + n * (clear - depth) * g[i]
                pushed += 1
        co = smooth(co, 0.5 * g, 3)
    # relax right under the seam: the normal ray-cast can fold a row of vertices over its neighbours at the throat
    # (a slit under the zip). Alternate smoothing and snapping back onto the canonical surface (+ clearance): the
    # vertices are already on the surface, so the nearest point no longer jumps into the throat notch.
    ra, rb = cfg.get("neck_relax_z", [1.53, 1.60])
    rad = np.linalg.norm(co[:, :2] - axis, axis=1)
    rz = np.nonzero((co[:, 2] >= ra) & (co[:, 2] <= rb) & (rad < r_out))[0]
    kr = np.zeros(len(co))
    kr[rz] = 0.5
    # the clearance tapers toward the zip: the ring sits exactly on the canonical surface, a full 1.5 mm offset in the
    # last rows left a lit lip along the seam
    t0, t1 = cfg.get("neck_clear_taper_z", [1.55, cfg.get("zip_z", 1.578)])
    keep = cfg.get("neck_clear_min", 0.0)
    for _ in range(cfg.get("neck_relax_iter", 8)):
        co = smooth(co, kr, 2)
        for i in rz:
            hit = bvh.find_nearest(Vector(co[i]))
            if hit[0] is not None:
                c = clear * (1 - (1 - keep) * float(_smooth((co[i, 2] - t0) / (t1 - t0))))
                co[i] = np.array(hit[0]) + np.array(hit[1]) * c
    inside = 0
    for i in np.nonzero((co[:, 2] > zp[1]) & (rad < rp[0]))[0]:
        hit = bvh.find_nearest(Vector(co[i]))
        if hit[0] is not None and (co[i] - np.array(hit[0])) @ np.array(hit[1]) < 0:
            inside += 1
    pushed = (pushed, inside)
    _set_co(src, co, None)
    return int(len(zone)), pushed


def neck_zip(src, ring, cfg):
    """Cut the source at the zip height, then bridge its loop to the exact canonical seam ring (by angle)."""
    zc = cfg.get("zip_z", 1.56)
    bm = bmesh.new()
    bm.from_mesh(src.data)
    # uv-sampling tree before cutting
    uv_layer = bm.loops.layers.uv.active
    bm.faces.ensure_lookup_table()
    pre_bvh = BVHTree.FromBMesh(bm)
    face_uv = {f.index: [(Vector(l.vert.co), Vector(l[uv_layer].uv)) for l in f.loops] for f in bm.faces}
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, zc / 100.0), plane_no=(0, 0, 1), clear_outer=True)
    # drop islands that are not connected to the body (stray pieces above / floating)
    loops = _boundary_loops(bm, zc / 100.0)
    if len(loops) != 1:
        raise RuntimeError(f"expected one neck loop at z={zc}, got {[len(l) for l in loops]}")
    low = loops[0]  # boundary walk order
    axis = np.array(cfg.get("neck_axis_xy", [0.0, 0.03])) / 100.0
    # Pair the two loops by normalised arc length along their own edge order. Sorting by angle around a neck axis
    # folded the strip at the throat, where the ring is not star-shaped around any axis.
    ring_pts = [ring[i]["co"] / 100.0 for i in range(len(ring))]

    def area(pts):
        return sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(pts, pts[1:] + pts[:1]))

    ring_order = list(range(len(ring)))
    if (area([tuple(v.co) for v in low]) > 0) != (area([tuple(p) for p in ring_pts]) > 0):
        ring_order.reverse()
    first = min(ring_order, key=lambda i: (Vector(ring_pts[i]) - low[0].co).length)
    k0 = ring_order.index(first)
    ring_sorted = ring_order[k0:] + ring_order[:k0]
    top = [bm.verts.new(Vector(ring_pts[i])) for i in ring_sorted]

    def arc(pts):
        d = [0.0]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            d.append(d[-1] + (Vector(b) - Vector(a)).length)
        return [2 * math.pi * x / d[-1] for x in d[:-1]]

    new_faces = _zip(bm, low, arc([v.co for v in low]), top, arc([v.co for v in top]))
    bm.normal_update()
    # outward winding: compare with radial direction
    for f in new_faces:
        c = f.calc_center_median()
        radial = Vector((c.x - axis[0], c.y - axis[1], 0))
        if f.normal.dot(radial) < 0:
            f.normal_flip()
    # UVs for the strip: barycentric in one source face just below the cut, top verts pulled to the low anchor (stays
    # inside that face's island). The source neck's UVs change island every few degrees (Meshy atlas), so per-vertex
    # projection mixes islands; instead the zip sits 7 mm under the seam, which keeps this smear short.
    zs = zc / 100.0 - cfg.get("uv_sample_below_m", 0.004) / 100.0
    low_set = set(low)
    for f in new_faces:
        lows = [l.vert for l in f.loops if l.vert in low_set]
        anchor = sum((v.co for v in lows), Vector()) / len(lows)
        hit = pre_bvh.find_nearest(Vector((anchor.x, anchor.y, zs)))
        tri = face_uv[hit[2]]
        for l in f.loops:
            q = l.vert.co.copy() if l.vert in low_set else anchor.copy()
            q.z = zs
            l[uv_layer].uv = _bary_uv(q, tri)
    bm.verts.index_update()
    bm.to_mesh(src.data)
    src.data.update()
    ring_index = {}
    for k, i in enumerate(ring_sorted):
        ring_index[top[k].index] = i
    bm.free()
    return ring_index, len(new_faces)


def retract_sheets(obj, cfg):
    """Meshy builds the spread thumb's web as a thin double-sided sail; posed, it becomes blades through the fist.
    Find hand vertices whose inward ray hits the other side within `sheet_thickness_m` (fingers and palm are >= 14 mm
    thick), grow the set by two rings, and Laplacian-smooth it with its border fixed so the sheet retracts onto its
    base. Repeat until no thin vertex is left. Object coordinates are world = metres / 100 here."""
    thr = cfg.get("sheet_thickness_m", 0.006) / 100.0
    x_min = cfg.get("sheet_hand_x_m", 0.70) / 100.0
    me = obj.data
    ev = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ev)
    nb = [[] for _ in me.vertices]
    for a, b in ev.reshape(-1, 2):
        nb[a].append(b)
        nb[b].append(a)
    counts, moved = [], set()
    for _ in range(cfg.get("sheet_rounds", 6)):
        me.update()
        bvh = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get(), deform=False)
        thin = [v.index for v in me.vertices if abs(v.co.x) > x_min
                and bvh.ray_cast(v.co - v.normal * 1e-7, -v.normal, thr)[0] is not None]
        counts.append(len(thin))
        if not thin:
            break
        sel = set(thin)
        for _ in range(2):
            sel |= {n for i in list(sel) for n in nb[i]}
        moved |= sel
        sel = np.array(sorted(sel))
        co = np.empty(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        for _ in range(cfg.get("sheet_smooth_iter", 40)):
            avg = np.array([co[nb[i]].mean(0) for i in sel])
            co[sel] = co[sel] * 0.3 + avg * 0.7
        me.vertices.foreach_set("co", co.ravel())
    me.update()
    return {"thin_per_round": counts, "moved": len(moved)}


def _emit_material(name, image=None, attribute=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    if image is not None:
        src = nt.nodes.new("ShaderNodeTexImage")
        src.image = image
        nt.links.new(src.outputs["Color"], emit.inputs["Color"])
    else:
        src = nt.nodes.new("ShaderNodeAttribute")
        src.attribute_name = attribute
        nt.links.new(src.outputs["Vector"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def _bake_emit(sc, active, size, selected=None, cage_m=0.01, ray_m=0.03):
    """EMIT bake onto `active`'s UVs (optionally selected-to-active from `selected`). Returns float (size, size, 3)."""
    img = bpy.data.images.new("_bake", size, size, alpha=True, float_buffer=True)
    img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(np.zeros(size * size * 4, dtype=np.float32))
    for m in active.data.materials:
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = img
        m.node_tree.nodes.active = node
    try:
        sc.render.engine = 'CYCLES'
        sc.cycles.samples = 1
        sc.render.bake.margin = 4
        sc.render.bake.use_selected_to_active = selected is not None
        sc.render.bake.cage_extrusion = cage_m
        sc.render.bake.max_ray_distance = ray_m
        bpy.ops.object.select_all(action='DESELECT')
        if selected is not None:
            selected.select_set(True)
        active.select_set(True)
        bpy.context.view_layer.objects.active = active
        bpy.ops.object.bake(type='EMIT')
        px = np.empty(size * size * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return px.reshape(size, size, 4)[..., :3].copy()
    finally:
        bpy.data.images.remove(img)


def _srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def _load_px(path):
    img = bpy.data.images.load(str(path), check_existing=False)
    try:
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)  # byte PNG: raw (sRGB-encoded) values
        return px.reshape(h, w, 4)
    finally:
        bpy.data.images.remove(img)


def _save_px(px, path):
    h, w = px.shape[:2]
    img = bpy.data.images.new("_out", w, h, alpha=True)
    try:
        img.pixels.foreach_set(px.astype(np.float32).ravel())
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save()
    finally:
        bpy.data.images.remove(img)


def bake_neck_band(sc, canon, body, cfg):
    """The Meshy texture just under the seam is the underside of Meshy's own chin (shadow, stubble), while the head
    above the seam keeps the old atlas. Bake the canonical body's old texture onto the v2 body (selected to active:
    the v2 neck lies on the canonical neck surface) and blend it into the v2 base colour over `z` (metres), so the
    skin reaches the seam in the head's colours. The normal map fades to flat over the same band. Writes the two
    project textures from the immutable Meshy sources."""
    nb = cfg["neck_band"]
    size = nb.get("bake_px", 2048)  # a 4K Cycles bake comes back empty
    arm = body.parent
    saved_scale = arm.scale.copy()
    arm.scale = (1.0, 1.0, 1.0)  # world = metres while baking (ray distances)
    saved = {o.name: list(o.data.materials) for o in (canon, body)}
    old = bpy.data.images.load(str(ROOT / nb["old_texture"]), check_existing=False)
    pos = body.data.attributes.new("_nb_pos", 'FLOAT_VECTOR', 'POINT')
    mats = []
    prev = bpy.context.window.scene
    bpy.context.window.scene = sc
    try:
        bpy.context.view_layer.update()
        mw = body.matrix_world
        for i, v in enumerate(body.data.vertices):
            pos.data[i].vector = mw @ v.co
        for o, m in ((canon, _emit_material("_NB_Old", image=old)), (body, _emit_material("_NB_Pos", attribute="_nb_pos"))):
            mats.append(m)
            o.data.materials.clear()
            o.data.materials.append(m)
        p = _bake_emit(sc, body, size)
        cov = np.linalg.norm(p, axis=-1) > 1e-6
        body.data.materials.clear()
        body.data.materials.append(mats[0].copy())
        mats.append(body.data.materials[0])
        col = _bake_emit(sc, body, size, selected=canon, cage_m=nb.get("cage_m", 0.01), ray_m=nb.get("ray_m", 0.03))
    finally:
        for o in (canon, body):
            o.data.materials.clear()
            for m in saved[o.name]:
                o.data.materials.append(m)
        for m in mats:
            bpy.data.materials.remove(m)
        bpy.data.images.remove(old)
        body.data.attributes.remove(body.data.attributes["_nb_pos"])
        arm.scale = saved_scale
        bpy.context.view_layer.update()  # matrix_world is stale until the depsgraph runs
        bpy.context.window.scene = prev
    z0, z1 = nb.get("z", [1.50, 1.555])
    axis = np.array(cfg.get("neck_axis_xy", [0.0, 0.03]))
    rad = np.linalg.norm(p[..., :2] - axis, axis=-1)
    # texels the bake ray missed come back black: never blend those in, and fade in from their edge (a hard cut
    # shows the old atlas' soft skin against the Meshy pores)
    hit = col.max(axis=-1) > nb.get("min_hit", 0.004)
    soft = hit.astype(np.float64)
    rad_px = nb.get("hit_fade_px", 12)
    for axis_ in (0, 1):  # separable box blur, twice ~ a tent
        for _ in range(2):
            c = np.cumsum(np.pad(soft, [(rad_px + 1, rad_px) if a == axis_ else (0, 0) for a in (0, 1)]), axis=axis_)
            soft = (np.take(c, range(2 * rad_px + 1, c.shape[axis_]), axis=axis_) -
                    np.take(c, range(0, c.shape[axis_] - 2 * rad_px - 1), axis=axis_)) / (2 * rad_px + 1)
    hit = hit * _smooth(soft * 2 - 1)
    r0, r1 = nb.get("r", [0.085, 0.11])
    radial = 1 - _smooth((rad - r0) / (r1 - r0))
    # at z1 the faces above switch to the old atlas (canonical_neck_uvs): there the blend must be complete at every
    # radius, or the two textures meet as a sawtooth along the trapezius
    radial = radial + (1 - radial) * _smooth((p[..., 2] - (z1 - 0.015)) / 0.015)
    w = _smooth((p[..., 2] - z0) / (z1 - z0)) * radial
    w = w * cov * hit
    tex = Path(cfg["texture_dir"])
    base = _load_px(tex / "base_color.png")
    nrm = _load_px(tex / "normal.png")
    k = base.shape[0] // size
    up = lambda a: np.repeat(np.repeat(a, k, axis=0), k, axis=1)
    W = up(w)[..., None]
    base[..., :3] = base[..., :3] * (1 - W) + up(_srgb(col)) * W
    nrm[..., :3] = nrm[..., :3] * (1 - W) + np.array([0.5, 0.5, 1.0]) * W
    _save_px(base, ROOT / nb["out_base_color"])
    _save_px(nrm, ROOT / nb["out_normal"])
    return {"texels": int((w > 0.5).sum()), "size": size}


def canonical_neck_uvs(src, canon, cfg):
    """Faces above `neck_uv_z` (the zip strip included) move to material slot 1 (the head's old material in Unity)
    with UVs projected from the canonical body: the fitted neck lies on its surface, the ring vertices are its exact
    seam vertices, so the seam carries the head's own atlas and UVs. Both objects are in armature space here.
    A face whose three projections land on different canonical UV islands takes one canonical face (its centroid's)."""
    zc = cfg.get("neck_uv_z", 1.53)
    jump = cfg.get("neck_uv_jump", 0.02)
    max_off = cfg.get("neck_uv_max_offset_m", 0.003)  # object units are metres here
    mw = src.matrix_world  # world x 100 = metres
    cme = canon.data
    cuv = cme.uv_layers.active.data
    tris = {}
    for poly in cme.polygons:
        tris[poly.index] = [(cme.vertices[cme.loops[li].vertex_index].co.copy(), cuv[li].uv.copy())
                            for li in poly.loop_indices]
    bm = bmesh.new()
    bm.from_mesh(cme)
    bvh = BVHTree.FromBMesh(bm)
    bm.free()

    def uv_at(p):
        hit = bvh.find_nearest(p)
        poly = tris[hit[2]]
        return _bary_uv(hit[0], poly[:3]) if len(poly) == 3 else _bary_uv(hit[0], _tri_of(poly, hit[0]))

    # canonical vertices -> their loop UVs (several where a UV seam passes): the seam ring vertices coincide exactly
    from mathutils.kdtree import KDTree
    kd = KDTree(len(cme.vertices))
    for v in cme.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    # canonical vertex -> [(adjacent face centroid, that face's UV for the vertex)]
    corner_uvs = {}
    for cpoly in cme.polygons:
        cc = cpoly.center.copy()
        for li in cpoly.loop_indices:
            corner_uvs.setdefault(cme.loops[li].vertex_index, []).append((cc, cuv[li].uv.copy()))

    def exact_uv(co, face_center):
        """UV of the canonical vertex at `co` as seen from its adjacent face nearest `face_center` (picks the side
        of a canonical UV seam the face is on), or None if no canonical vertex is there."""
        _, idx, d = kd.find(co)
        if d >= 1e-5 or idx not in corner_uvs:
            return None
        return min(corner_uvs[idx], key=lambda cu: (cu[0] - face_center).length)[1]
    me = src.data
    uvl = me.uv_layers.active.data
    zw = lambda v: (mw @ v.co).z * 100.0
    moved = fallback = 0
    for poly in me.polygons:
        vs = [me.vertices[me.loops[li].vertex_index] for li in poly.loop_indices]
        if min(zw(v) for v in vs) < zc:
            continue
        # only skin lying on the canonical surface takes its UVs: off it (trapezius slopes) the nearest canonical
        # face is a guess, and those faces keep the v2 atlas (already baked to the old colours at this height)
        if max(bvh.find_nearest(v.co)[3] for v in vs) > max_off:
            continue
        uvs = [uv_at(v.co) for v in vs]
        if max((a - b).length for a in uvs for b in uvs) > jump:
            fallback += 1
            c = sum((v.co for v in vs), Vector()) / len(vs)
            hit = bvh.find_nearest(c)
            poly_c = tris[hit[2]]
            tri = poly_c[:3] if len(poly_c) == 3 else _tri_of(poly_c, hit[0])
            uvs = [_bary_uv(v.co, tri) for v in vs]
        # a vertex on a canonical vertex takes that vertex's own UV, from the side of a UV seam the face is on
        fc = sum((v.co for v in vs), Vector()) / len(vs)
        for j, v in enumerate(vs):
            uv = exact_uv(v.co, fc)
            if uv is not None:
                uvs[j] = uv
        for li, uv in zip(poly.loop_indices, uvs):
            uvl[li].uv = uv
        poly.material_index = 1
        moved += 1
    if len(me.materials) < 2:
        me.materials.append(bpy.data.materials.get("M_TestBaseHero") or bpy.data.materials.new("M_TestBaseHero"))
    me.update()
    return {"faces": moved, "uv_fallback": fallback}


def _tri_of(poly, p):
    """The fan triangle of an n-gon [(co, uv)] closest to p."""
    best, bd = None, float("inf")
    for i in range(1, len(poly) - 1):
        t = [poly[0], poly[i], poly[i + 1]]
        c = (t[0][0] + t[1][0] + t[2][0]) / 3
        d = (c - p).length
        if d < bd:
            best, bd = t, d
    return best


def _boundary_loops(bm, z, tol=1e-7):
    seen, loops = set(), []
    for v in bm.verts:
        if v in seen or not v.is_boundary or abs(v.co.z - z) > tol:
            continue
        loop, cur = [], v
        while cur and cur not in seen:
            seen.add(cur)
            loop.append(cur)
            nxt = None
            for e in cur.link_edges:
                if e.is_boundary:
                    o = e.other_vert(cur)
                    if o not in seen and abs(o.co.z - z) <= tol:
                        nxt = o
                        break
            cur = nxt
        loops.append(loop)
    return [l for l in loops if len(l) > 8]


def _zip(bm, low, a_low, top, a_top):
    """Triangle strip between two closed loops: walk both by angle from low[0], advance the smaller next angle."""
    t0 = a_low[0]
    two_pi = 2 * math.pi
    rl = [(a - t0) % two_pi for a in a_low]
    rt = [(a - t0) % two_pi for a in a_top]
    ol = sorted(range(len(low)), key=lambda k: rl[k])
    ot = sorted(range(len(top)), key=lambda k: rt[k])
    L = [low[k] for k in ol]
    T = [top[k] for k in ot]
    AL = [rl[k] for k in ol] + [two_pi + rl[ol[0]]]
    AT = [rt[k] for k in ot] + [two_pi + rt[ot[0]]]
    nl, nt = len(L), len(T)
    i = j = 0
    faces = []
    while i < nl or j < nt:
        if j >= nt or (i < nl and AL[i + 1] <= AT[j + 1]):
            faces.append(bm.faces.new((L[i], L[(i + 1) % nl], T[j % nt])))
            i += 1
        else:
            faces.append(bm.faces.new((L[i % nl], T[(j + 1) % nt], T[j])))
            j += 1
    return faces


def _bary_uv(p, tri):
    (a, ua), (b, ub), (c, uc) = tri[0], tri[1], tri[2]
    from mathutils.geometry import barycentric_transform
    q = barycentric_transform(p, a, b, c, Vector((ua.x, ua.y, 0)), Vector((ub.x, ub.y, 0)), Vector((uc.x, uc.y, 0)))
    return Vector((q.x, q.y))


# --------------------------------------------------------------------------------------------- preview renders

def render_views(sc, views, out_dir, size=800, clay=None):
    """views: [(name, target_m, direction, ortho_m)] in character metres. clay: objects to render untextured."""
    import os
    os.makedirs(out_dir, exist_ok=True)
    cam = sc.objects.get("_PrevCam")
    if cam is None:
        cd = bpy.data.cameras.new("_PrevCam")
        cam = bpy.data.objects.new("_PrevCam", cd)
        sc.collection.objects.link(cam)
        for nm, rot in (("_K1", (1.0, 0, 0.5)), ("_K2", (2.1, 0, 3.6))):
            ld = bpy.data.lights.new(nm, 'SUN')
            ld.energy = 3
            lo = bpy.data.objects.new(nm, ld)
            lo.rotation_euler = rot
            sc.collection.objects.link(lo)
    cd = cam.data
    cd.type, cd.clip_start, cd.clip_end = 'ORTHO', 0.0001, 100
    sc.camera = cam
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = sc.render.resolution_y = size
    if sc.world is None:
        sc.world = bpy.data.worlds.new("_PrevWorld")
        sc.world.color = (0.3, 0.33, 0.36)
    saved = {}
    if clay:
        mat = bpy.data.materials.get("_Clay") or bpy.data.materials.new("_Clay")
        for o in clay:
            saved[o.name] = list(o.data.materials)
            for k in range(len(o.data.materials)):
                o.data.materials[k] = mat
    prev = bpy.context.window.scene
    bpy.context.window.scene = sc
    paths = []
    for name, tgt, d, ortho in views:
        t = Vector(tgt) / 100.0
        d = Vector(d).normalized()
        cd.ortho_scale = ortho / 100.0
        cam.location = t - d
        up = Vector((0, 1, 0)) if abs(d.z) > 0.9 else Vector((0, 0, 1))
        z = -d
        x = up.cross(z).normalized()
        y = z.cross(x)
        cam.matrix_world = Matrix((x, y, z)).transposed().to_4x4()
        cam.location = t - d
        sc.render.filepath = str(Path(out_dir) / f"{name}.png")
        bpy.ops.render.render(write_still=True, scene=sc.name)
        paths.append(sc.render.filepath)
    bpy.context.window.scene = prev
    for o in clay or []:
        for k, m in enumerate(saved[o.name]):
            o.data.materials[k] = m
    return paths


# --------------------------------------------------------------------------------------------- fingers

def hand_frame(bones, side):
    """Canonical hand frame (metres): origin = wrist, u = along the hand, v = toward the thumb, n = palm normal
    (down, palms-down rest pose)."""
    h, t = bones[f"{side}Hand"]
    u = (t - h).normalized()
    n = Vector((0, 0, -1))
    n = (n - u * n.dot(u)).normalized()
    v = n.cross(u).normalized()
    th = bones[f"{side}HandThumb2"][0] - h
    if v.dot(th) < 0:
        v = -v
    return np.array(h), np.array(u), np.array(v), np.array(n)


def _clusters(vals, gap):
    order = np.argsort(vals)
    s = vals[order]
    groups, cur = [], [order[0]]
    for k in range(1, len(s)):
        if s[k] - s[k - 1] > gap:
            groups.append(cur)
            cur = []
        cur.append(order[k])
    groups.append(cur)
    return groups


def surface_samples(obj, mask_fn, density=2.0e6, seed=1):
    """Random points on the triangles whose centroid passes mask_fn (metres); density = points per m^2."""
    me = obj.data
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    tri = np.empty(n * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tri)
    tri = tri.reshape(-1, 3)
    co = _co(obj) * 100.0
    P = co[tri]
    cen = P.mean(1)
    keep = mask_fn(cen)
    P = P[keep]
    area = 0.5 * np.linalg.norm(np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]), axis=1)
    rng = np.random.default_rng(seed)
    cnt = rng.poisson(area * density)
    idx = np.repeat(np.arange(len(P)), cnt)
    r1, r2 = rng.random(len(idx)), rng.random(len(idx))
    s1 = np.sqrt(r1)
    w0, w1, w2 = 1 - s1, s1 * (1 - r2), s1 * r2
    return P[idx, 0] * w0[:, None] + P[idx, 1] * w1[:, None] + P[idx, 2] * w2[:, None]


def find_fingers(obj, bones, side, cfg):
    """Slice the hand across its axis (dense surface samples), cluster each slice along the thumb direction.
    Returns the hand frame and per-slice clusters (b centre, b width, c centre, count)."""
    o, u, v, n = hand_frame(bones, side)

    def mask(p):
        rel = p - o
        a = rel @ u
        return (a > -0.01) & (a < 0.30) & (np.abs(rel @ n) < 0.06) & (np.abs(rel @ v) < 0.14)
    pts = surface_samples(obj, mask, cfg.get("sample_density", 3.0e6))
    rel = pts - o
    a, b, c = rel @ u, rel @ v, rel @ n
    step = cfg.get("finger_slice_m", 0.002)
    gap = cfg.get("finger_gap_m", 0.0025)
    slices = []
    x = a.max()
    while x > 0.0:
        m = (a > x - step) & (a <= x)
        if m.sum() >= 3:
            idx = np.nonzero(m)[0]
            cl = _clusters(b[idx], gap)
            slices.append((x - step / 2, [(float(b[idx[g]].mean()), float(np.ptp(b[idx[g]])),
                                           float(c[idx[g]].mean()), len(g)) for g in cl if len(g) >= 4]))
        x -= step
    return {"frame": (o, u, v, n), "slices": slices, "a_max": float(a.max()), "pts": (a, b, c)}


def _tracks(slices, max_w, link):
    """Follow isolated (narrow) clusters from the fingertips toward the wrist. Returns tracks as lists of
    (a, b, c) from tip to web; a track ends when its cluster gets wider than max_w (merged with a neighbour)."""
    tracks = []
    for a, cl in slices:            # slices go from the tip (large a) to the wrist
        for (b, w, c, k) in cl:
            if w > max_w:
                continue
            best = None
            for t in tracks:
                if t["open"] and abs(t["pts"][-1][1] - b) < link and a < t["pts"][-1][0]:
                    if best is None or abs(t["pts"][-1][1] - b) < abs(best["pts"][-1][1] - b):
                        best = t
            if best is None:
                tracks.append({"pts": [(a, b, c)], "open": True, "seen": a})
            else:
                best["pts"].append((a, b, c))
                best["seen"] = a
        for t in tracks:            # close tracks not continued in this slice
            if t["open"] and t["seen"] > a + 1e-9:
                t["open"] = False
    return [t["pts"] for t in tracks if len(t["pts"]) >= 5]


def _line(pts):
    p = np.asarray(pts)
    c = p.mean(0)
    d = np.linalg.svd(p - c)[2][0]
    if d[0] < 0:
        d = -d
    return c, d / np.linalg.norm(d)


def finger_joints(r, cfg):
    """Joint positions (character metres) per finger: [base, j2, j3, tip]. Fingers: knuckle (MCP) = web - d_mcp
    along the centre line, phalanx ratios from cfg. Thumb: CMC from the hand frame, MCP back from the tip."""
    o, u, v, n = r["frame"]
    to_w = lambda a, b, c: o + u * a + v * b + n * c
    tr = _tracks(r["slices"], cfg.get("finger_max_w_m", 0.024), cfg.get("finger_link_m", 0.008))
    # fingers: the four longest narrow tracks, index (thumb side) -> pinky
    fingers = sorted(tr, key=lambda t: -len(t))[:4]
    fingers.sort(key=lambda t: -np.mean([p[1] for p in t]))
    # thumb: diagonal, so its slices are wide: every separate cluster beyond the index side
    b_index = max(p[1] for p in fingers[0]) + cfg.get("thumb_side_gap_m", 0.015)
    thumb = [(a, b, c) for a, cl in r["slices"] for (b, w, c, k) in cl if b > b_index and len(cl) > 1]
    if len(thumb) < 5:
        raise RuntimeError(f"thumb not found ({len(thumb)} slices)")
    if len(fingers) != 4:
        raise RuntimeError(f"found {len(fingers)} fingers")
    ratios = cfg.get("phalanx_ratios", [0.47, 0.30, 0.23])
    d_mcp = cfg.get("mcp_behind_web_m", {"Index": 0.018, "Middle": 0.018, "Ring": 0.018, "Pinky": 0.015})
    out, info = {}, {}
    for name, t in zip(("Index", "Middle", "Ring", "Pinky"), fingers):
        c, d = _line(t)
        tip_a, web_a = t[0][0], t[-1][0]
        at = lambda a: c + d * ((a - c[0]) / d[0])
        tip = at(tip_a + cfg.get("finger_slice_m", 0.002) / 2)
        web = at(web_a)
        mcp = web - d * d_mcp[name]
        L = np.linalg.norm(tip - mcp)
        j2 = mcp + d * L * ratios[0]
        j3 = j2 + d * L * ratios[1]
        out[name] = [to_w(*p) for p in (mcp, j2, j3, tip)]
        info[name] = {"tip_a": round(tip_a * 100, 1), "web_a": round(web_a * 100, 1), "len_cm": round(L * 100, 1)}
    # thumb: line through its isolated track, extended to the furthest sample near that line
    c, d = _line(thumb)
    if d[1] < 0:
        d = -d
    a, b, cc = r["pts"]
    P = np.stack([a, b, cc], 1)
    rel = P - c
    along = rel @ d
    perp = np.linalg.norm(rel - np.outer(along, d), axis=1)
    near = perp < cfg.get("thumb_radius_m", 0.012)
    tip = c + d * along[near].max()
    cmc = np.array(cfg.get("thumb_cmc_m", [0.025, 0.022, 0.0]))
    L2, L3 = cfg.get("thumb_phalanges_m", [0.032, 0.028])
    ip = tip - d * L3
    mcp = ip - d * L2
    out["Thumb"] = [to_w(*p) for p in (cmc, mcp, ip, tip)]
    info["Thumb"] = {"tip": [round(x * 100, 1) for x in tip], "track_n": len(thumb)}
    return out, info


# --------------------------------------------------------------------------------------------- rig + weights

def build_finger_bones(arm, joints, frames):
    """Rebuild the finger chains (Mixamo names, 1-3 + end bone 4) at the fitted joints. Other bones untouched."""
    inv = arm.matrix_world.inverted()
    loc = lambda p: inv @ (Vector(p) / 100.0)
    prev = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    made = []
    for side, J in joints.items():
        o, u, v, n = frames[side]
        dorsal = (inv.to_3x3() @ Vector(-n)).normalized()
        hand = eb[f"{PFX}{side}Hand"]
        for f in FINGERS:
            pts = [loc(p) for p in J[f]]
            parent = hand
            for k in range(4):
                name = f"{PFX}{side}Hand{f}{k + 1}"
                b = eb.get(name) or eb.new(name)
                b.use_connect = False
                b.parent = parent
                b.head = pts[k]
                b.tail = pts[k + 1] if k < 3 else pts[3] + (pts[3] - pts[2]) * 0.5
                b.align_roll(dorsal)
                parent = b
                made.append(name)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.objects.active = prev
    return made


def _weights_matrix(obj, names):
    idx = {n: k for k, n in enumerate(names)}
    W = np.zeros((len(obj.data.vertices), len(names)), dtype=np.float32)
    gname = {g.index: g.name for g in obj.vertex_groups}
    for v in obj.data.vertices:
        for g in v.groups:
            k = idx.get(gname[g.group])
            if k is not None:
                W[v.index, k] = g.weight
    return W


def transfer_weights(src, canon_body, bone_names):
    """Barycentric weight transfer from the canonical body (finger groups merged into the hand first)."""
    W = _weights_matrix(canon_body, bone_names)
    for side in SIDES:
        h = bone_names.index(f"{PFX}{side}Hand")
        for k, nme in enumerate(bone_names):
            if nme.startswith(f"{PFX}{side}Hand") and k != h:
                W[:, h] += W[:, k]
                W[:, k] = 0
    me = canon_body.data
    me.calc_loop_triangles()
    tris = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tris)
    tris = tris.reshape(-1, 3)
    cco = _co(canon_body) * 100.0
    bvh = BVHTree.FromPolygons([tuple(p) for p in cco], [tuple(t) for t in tris])
    sco = _co(src) * 100.0
    out = np.zeros((len(sco), len(bone_names)), dtype=np.float32)
    for i, p in enumerate(sco):
        hit = bvh.find_nearest(Vector(p))
        t = tris[hit[2]]
        a, b, c = (Vector(cco[x]) for x in t)
        from mathutils.geometry import barycentric_transform
        q = barycentric_transform(Vector(hit[0]), a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        bc = np.clip(np.array(q), 0, None)
        bc /= max(bc.sum(), 1e-9)
        out[i] = bc[0] * W[t[0]] + bc[1] * W[t[1]] + bc[2] * W[t[2]]
    return out


def _seg_dist(p, a, b):
    d = b - a
    t = np.clip(((p - a) @ d) / (d @ d), 0, 1)
    return np.linalg.norm(p - (a + np.outer(t, d)), axis=1), t


def _chain_share(f, d, k, t, s_signed, pts, p, cfg):
    """How much of a vertex finger `f` takes (0..1), and its split over the 3 chain bones."""
    bl = cfg.get("joint_blend_finger_m", 0.008)
    seg = [np.linalg.norm(pts[j + 1] - pts[j]) for j in range(3)]
    s = sum(seg[:k]) + t * seg[k]
    if f == "Thumb":
        if k == 0:
            # thumb 1 covers the thenar: weight by distance to the metacarpal segment
            lo, hi = cfg.get("thenar_r", [0.012, 0.03])
            into = float(1 - _smooth((d - lo) / (hi - lo)))
        else:
            into = 1.0
    else:
        # knuckle: hand -> finger over `knuckle_blend_m` around the MCP (s = 0; signed behind the knuckle)
        k0, k1 = cfg.get("knuckle_blend_m", [-0.015, 0.006])
        s_k = s_signed if (k == 0 and t <= 0) else s
        into = float(_smooth((s_k - k0) / (k1 - k0)))
    # radial falloff: a finger only owns the skin around its own axis (palm skin under the knuckles and the
    # thumb-index web stay on the hand instead of being dragged by whichever chain is nearest). Perpendicular to
    # the segment's line: the clamped distance behind the knuckle grows along the axis and stacked a second steep
    # drop on the knuckle ramp (torn knuckles in a fist).
    if not (f == "Thumb" and k == 0):
        a, b = pts[k], pts[k + 1]
        u = (b - a) / np.linalg.norm(b - a)
        r = p - a - ((p - a) @ u) * u
        perp = float(np.linalg.norm(r))
        lo, hi = cfg.get("thumb_r" if f == "Thumb" else "finger_r", [0.013, 0.022] if f == "Thumb" else [0.011, 0.018])
        into *= float(1 - _smooth((perp - lo) / (hi - lo)))
    b = np.zeros(3)
    cum = np.cumsum([0] + seg)
    for j in range(3):
        lo = _smooth((s - (cum[j] - bl / 2)) / bl) if j > 0 else 1.0
        hi = 1 - _smooth((s - (cum[j + 1] - bl / 2)) / bl) if j < 2 else 1.0
        b[j] = lo * hi
    b /= max(b.sum(), 1e-6)
    return into, b


def finger_weights(co, W, names, joints, cfg):
    """Split each hand's weight between the hand and its finger bones. Each vertex blends its two nearest finger
    chains (50/50 where equidistant, the nearest alone beyond `web_blend_m`), each with smooth blends across the
    knuckle and the finger joints and a radial falloff; thumb 1 takes the thenar by distance."""
    tau = cfg.get("web_blend_m", 0.006)
    for side, J in joints.items():
        h = names.index(f"{PFX}{side}Hand")
        hw = W[:, h].copy()
        idx = np.nonzero(hw > 0)[0]
        p = co[idx]
        per = {}
        dist = np.zeros((len(idx), len(FINGERS)))
        for fi, f in enumerate(FINGERS):
            pts = [np.asarray(x) for x in J[f]]
            ds, ts = zip(*[_seg_dist(p, pts[k], pts[k + 1]) for k in range(3)])
            ds = np.stack(ds, 1)
            ts = np.stack(ts, 1)
            k = ds.argmin(1)
            dd = pts[1] - pts[0]
            s_signed = ((p - pts[0]) @ dd) / np.linalg.norm(dd)
            per[f] = (ds[np.arange(len(idx)), k], k, ts[np.arange(len(idx)), k], s_signed, pts)
            dist[:, fi] = per[f][0]
        order = np.argsort(dist, axis=1)
        new = np.zeros((len(idx), len(names)), dtype=np.float32)
        hand_keep = np.ones(len(idx), dtype=np.float32)
        for i in range(len(idx)):
            f1, f2 = FINGERS[order[i, 0]], FINGERS[order[i, 1]]
            share2 = 0.5 * float(1 - _smooth((dist[i, order[i, 1]] - dist[i, order[i, 0]]) / tau))
            total = 0.0
            for f, share in ((f1, 1 - share2), (f2, share2)):
                if share <= 0:
                    continue
                d, k, t, s_signed, pts = per[f]
                into, b = _chain_share(f, d[i], k[i], t[i], s_signed[i], pts, p[i], cfg)
                if into <= 0:
                    continue
                for j in range(3):
                    new[i, names.index(f"{PFX}{side}Hand{f}{j + 1}")] += share * into * b[j]
                total += share * into
            hand_keep[i] = 1 - total
        W[idx] += new * hw[idx][:, None]
        W[idx, h] = hw[idx] * hand_keep
    return W


def thin_fingers(co, W, names, joints, cfg):
    """Meshy's fingers come out thick (a closed fist then pushes them through the palm). Pull each finger vertex
    toward its dominant finger bone's axis by `finger_thin` x its total finger weight, so the palm and the knuckle
    blend keep their shape. co: character metres. Returns the new co."""
    k = cfg.get("finger_thin", 0.15)
    if k <= 0:
        return co
    co = co.copy()
    for side, J in joints.items():
        cols = {}
        for f in FINGERS:
            for j in range(3):
                cols[names.index(f"{PFX}{side}Hand{f}{j + 1}")] = (f, j)
        ci = np.array(sorted(cols))
        wf = W[:, ci]
        total = wf.sum(1)
        for i in np.nonzero(total > 1e-3)[0]:
            f, j = cols[ci[int(wf[i].argmax())]]
            a, b = np.asarray(J[f][j]), np.asarray(J[f][j + 1])
            d = b - a
            t = np.clip(((co[i] - a) @ d) / (d @ d), 0, 1)
            axis_pt = a + t * d
            co[i] = axis_pt + (co[i] - axis_pt) * (1 - k * total[i])
    return co


def limit_normalize(W, max_inf=4):
    if W.shape[1] > max_inf:
        part = np.argpartition(-W, max_inf, axis=1)[:, max_inf:]
        np.put_along_axis(W, part, 0, axis=1)
    s = W.sum(1, keepdims=True)
    W /= np.maximum(s, 1e-9)
    return W


def write_weights(obj, W, names, eps=1e-4):
    obj.vertex_groups.clear()
    groups = [obj.vertex_groups.new(name=n) for n in names]
    for k, g in enumerate(groups):
        col = W[:, k]
        nz = np.nonzero(col > eps)[0]
        for i in nz:
            g.add([int(i)], float(col[i]), 'REPLACE')


# --------------------------------------------------------------------------------------------- run

def run(cfg, export=True):
    """Full fit: align + deform, neck blend + zip onto the canonical seam ring, finger bones, weights, normals,
    export (armature + `base_hero` body). Leaves scene _BodyV2Fit for inspection."""
    prev = bpy.context.window.scene
    sc, arm, meshes, src, bones, info = fitted_scene(cfg)
    canon = meshes["base_hero_body"]
    ring = _canon_ring(canon)
    rep = {"align": info, "neck": neck_blend(src, canon, cfg)}
    _decimate(src, cfg.get("max_faces"))
    ring_index, rep["strip_faces"] = neck_zip(src, ring, cfg)
    rep["sheets"] = retract_sheets(src, cfg)
    joints, frames, rep["fingers"] = {}, {}, {}
    for side in SIDES:
        r = find_fingers(src, bones, side, cfg)
        joints[side], rep["fingers"][side] = finger_joints(r, cfg)
        frames[side] = r["frame"]
    build_finger_bones(arm, joints, frames)
    names = [b.name for b in arm.data.bones]
    canon_names = [g.name for g in canon.vertex_groups]
    W = np.zeros((len(src.data.vertices), len(names)), dtype=np.float32)
    Wt = transfer_weights(src, canon, canon_names)
    for k, nme in enumerate(canon_names):
        W[:, names.index(nme)] = Wt[:, k]
    co = _co(src) * 100.0
    W = finger_weights(co, W, names, joints, cfg)
    W = limit_normalize(W, cfg.get("max_influences", 4))
    # seam ring: exact canonical weights
    for vi, ri in ring_index.items():
        W[vi] = 0
        for g, w in ring[ri]["w"].items():
            W[vi, names.index(g)] = w
    write_weights(src, W, names)
    _set_co(src, thin_fingers(co, W, names, joints, cfg), arm)
    # normals: smooth, with the exact canonical normals on the ring
    me = src.data
    vn = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("normal", vn)
    vn = vn.reshape(-1, 3)
    for vi, ri in ring_index.items():
        vn[vi] = np.array(ring[ri]["n"])
    # bring the mesh into armature space, parent it like the canonical body
    inv = arm.matrix_world.inverted()
    me.transform(inv)
    rot = np.array(inv.to_3x3().normalized())
    vn = vn @ rot.T
    vn /= np.linalg.norm(vn, axis=1, keepdims=True)
    me.normals_split_custom_set_from_vertices([tuple(x) for x in vn])
    src.parent = arm
    src.matrix_parent_inverse = Matrix.Identity(4)
    src.matrix_basis = Matrix.Identity(4)
    mod = src.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    src.name = "base_hero"
    me.name = "base_hero_v2"
    mat = me.materials[0] if me.materials else bpy.data.materials.new("M_BaseHeroV2Body")
    mat.name = "M_BaseHeroV2Body"
    rep["verts"], rep["tris"] = len(me.vertices), sum(len(p.vertices) - 2 for p in me.polygons)
    rep["bones"] = len(names)
    if "neck_band" in cfg:
        rep["neck_band"] = bake_neck_band(sc, canon, src, cfg)
    if max(abs(a - b) for ra, rb in zip(canon.matrix_world, src.matrix_world) for a, b in zip(ra, rb)) > 1e-6:
        raise RuntimeError("canonical body and fitted body are not in the same object space")
    # after the bake: the bake must not write through the reassigned (old-atlas) UVs
    rep["neck_uv"] = canonical_neck_uvs(src, canon, cfg)
    if export:
        out = Path(cfg["output_fbx"])
        out.parent.mkdir(parents=True, exist_ok=True)
        bpy.context.window.scene = sc
        bpy.ops.object.select_all(action='DESELECT')
        arm.select_set(True)
        src.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 add_leaf_bones=False, bake_anim=False, mesh_smooth_type='OFF',
                                 use_mesh_modifiers=False, path_mode='STRIP')
        rep["fbx"] = str(out)
    bpy.context.window.scene = prev
    return rep
