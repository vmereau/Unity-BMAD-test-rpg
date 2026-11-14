"""Derive an NPC head variant from the canonical base-hero head. Run inside Blender (MCP).

    exec(open(r".../Tools/Blender/make_head_variant.py").read())
    result = run(r".../Tools/Blender/configs/heads/warden.json", export=False)   # mockup renders only
    result = run(r".../Tools/Blender/configs/heads/warden.json", export=True)    # + FBX and texture

Input: the neck-prepared head of `base hero split.fbx` (never modified). Everything near the neck ring is
frozen (module constant FREEZE, shared by every face): a vertex's deform weight is 0 within `distance_m` of any
vertex at or below `seam_m`, easing to 1 over `ramp_m`. Frozen vertices keep their position, UVs and weights, and
their custom normal within re-encoding noise (~0.02 deg), so the head still joins the canonical body and every
fitted outfit (see Humanoids/CLAUDE.md, neck interface). The overhanging chin is not frozen.

Coordinates are game metres in the character's rest pose: +X = character's left, -Y = facing direction, +Z = up.

Config (JSON):
  name            "Warden" -> mesh "Head_Warden", files "Head_Warden.fbx" / "T_Head_Warden_BaseColor.png"
  deforms         list, applied in order; each is a soft ellipsoid region:
                    {"center": [x,y,z], "radius": [rx,ry,rz], "scale": [sx,sy,sz], "pivot": [x,y,z]?,
                     "offset": [dx,dy,dz]?, "mirror_x": bool?}
                  scale is about pivot (default = center); weight = (1 - d^2)^2 inside the ellipsoid.
                  A mirrored op evaluates both sides on the same input positions (no double application).
  paint           list of texture layers (see _paint_layer), applied in order on a copy of the base texture.
  render          {"out_dir": "Temp/HeadVariants"} (relative to project root)
Paint only ever touches texels of head faces whose layer mask is > 0; body texels are copied unchanged.
"""
import json
import math
import os
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

PROJECT = Path(r"D:/github/perso/Unity-BMAD-test-rpg")
BASE_DIR = PROJECT / "Assets/_Game/Art/Characters/Humanoids/models/test base hero"
SPLIT_FBX = BASE_DIR / "base hero split.fbx"
BASE_TEX = BASE_DIR / "Meshy_AI_Neutral_Figure_T_Pose_1009121619_texture.png"
BASE_NRM = BASE_DIR / "Meshy_AI_Neutral_Figure_T_Pose_1009121619_texture_normal.png"
HEADS_DIR = PROJECT / "Assets/_Game/Art/Characters/Humanoids/models/heads"
CACHE = PROJECT / "Temp/HeadVariants/_posmap.npz"
SCENE = "HeadVariant"
FREEZE = {"seam_m": 1.585, "distance_m": 0.012, "ramp_m": 0.035}


# ---------------------------------------------------------------------------------------------- scene

def _load():
    """Fresh scene with the rig, the split head and the base body (preview context only, never exported).
    Returns (scene, rig, head, body, unit). Needs interactive Blender (bpy.context.window)."""
    if bpy.context.window is None:
        raise RuntimeError("make_head_variant needs interactive Blender (MCP add-on), not background mode")
    old = bpy.data.scenes.get(SCENE)
    scene = bpy.data.scenes.new(SCENE)  # created first: Blender refuses to remove a file's only scene
    bpy.context.window.scene = scene
    if old is not None:
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(old)
        scene.name = SCENE
        bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=False, do_recursive=True)  # previous run's meshes / rig
    bpy.ops.import_scene.fbx(filepath=str(SPLIT_FBX))
    rig = next((o for o in scene.objects if o.type == 'ARMATURE'), None)
    head = next((o for o in scene.objects if o.type == 'MESH' and '_head' in o.name), None)
    body = next((o for o in scene.objects if o.type == 'MESH' and '_body' in o.name), None)
    if rig is None or head is None or body is None or head.data.uv_layers.active is None:
        raise RuntimeError(f"{SPLIT_FBX.name}: expected an armature, a *_head mesh with UVs and a *_body mesh")
    if rig.animation_data:
        rig.animation_data.action = None
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    return scene, rig, head, body, rig.matrix_world.to_scale().x


def _positions(head, unit):
    mw = head.matrix_world
    return np.array([list((mw @ v.co) / unit) for v in head.data.vertices], dtype=np.float64)


def _set_positions(head, unit, pos):
    inv = head.matrix_world.inverted()
    for v, p in zip(head.data.vertices, pos):
        v.co = inv @ (Vector(p) * unit)
    head.data.update()


# ---------------------------------------------------------------------------------------------- shape

def _freeze_weights(pos):
    frozen = np.where(pos[:, 2] <= FREEZE["seam_m"])[0]
    if len(frozen) == 0:
        raise RuntimeError(f"No head vertex at or below the seam ({FREEZE['seam_m']} m): wrong input or seam height")
    tree = KDTree(len(frozen))
    for i, idx in enumerate(frozen):
        tree.insert(Vector(pos[idx]), i)
    tree.balance()
    w = np.zeros(len(pos))
    for i, p in enumerate(pos):
        if pos[i, 2] <= FREEZE["seam_m"]:
            continue
        d = tree.find(Vector(p))[2] - FREEZE["distance_m"]
        t = min(max(d / FREEZE["ramp_m"], 0.0), 1.0)
        w[i] = t * t * (3 - 2 * t)
    return w


def _radius(radius):
    r = np.array(radius, dtype=np.float64)
    if r.shape != (3,) or not np.all(r > 0):
        raise ValueError(f"radius must be three positive numbers, got {radius}")
    return r


def _region_weight(pos, center, radius):
    d2 = (((pos - np.array(center)) / _radius(radius)) ** 2).sum(axis=1)
    return np.where(d2 < 1.0, (1.0 - d2) ** 2, 0.0)


def _deform(pos, freeze, deforms):
    out = pos.copy()
    for op in deforms:
        sides = [1, -1] if op.get("mirror_x") else [1]
        before = out.copy()  # both sides read the same positions: a midline vertex is never moved twice
        for side in sides:
            m = np.array([side, 1, 1])
            center = np.array(op["center"]) * m
            pivot = np.array(op.get("pivot", op["center"])) * m
            w = _region_weight(before, center, op["radius"]) * freeze
            scale = np.array(op.get("scale", [1, 1, 1]))
            offset = np.array(op.get("offset", [0, 0, 0])) * m
            target = pivot + (before - pivot) * scale + offset
            out = out + (target - before) * w[:, None]
    return out


def _fix_normals(head, freeze, original):
    """Frozen corners keep their exact custom normals; deformed corners blend to the new smooth normals.
    Smooth normals are read from a throwaway copy: zeroing the real mesh first re-bases its fans and
    shifts the frozen normals by ~1 degree when they are set back."""
    me = head.data
    tmp = me.copy()
    tmp.normals_split_custom_set([(0, 0, 0)] * len(tmp.loops))  # zero = auto (smooth) normal
    smooth = [Vector(c.vector) for c in tmp.corner_normals]
    bpy.data.meshes.remove(tmp)
    blended = []
    for loop, o, s in zip(me.loops, original, smooth):
        w = freeze[loop.vertex_index]
        blended.append(o if w <= 0.0 else o.lerp(s, w).normalized())
    me.normals_split_custom_set(blended)


# ---------------------------------------------------------------------------------------------- paint

def _coverage(scene, obj, size):
    """Texels covered by obj's faces (bake margin 0)."""
    mat = bpy.data.materials.new("_HV_Cov")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    tex = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.new("_HV_cov", size, size, alpha=True, float_buffer=True)
    img.pixels.foreach_set(np.zeros(size * size * 4, dtype=np.float32))
    tex.image = img
    nt.nodes.active = tex
    saved = list(obj.data.materials)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    margin = scene.render.bake.margin
    try:
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 1
        scene.render.bake.margin = 0
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.bake(type='EMIT')
        px = np.empty(size * size * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return px.reshape(size, size, 4)[..., 0] > 0.5  # alpha is filled everywhere at margin 0; colour is not
    finally:
        scene.render.bake.margin = margin
        obj.data.materials.clear()
        for m in saved:
            obj.data.materials.append(m)
        bpy.data.materials.remove(mat)
        bpy.data.images.remove(img)


def _dilate(mask, steps):
    out = mask.copy()
    for _ in range(steps):
        grown = out.copy()
        grown[1:, :] |= out[:-1, :]
        grown[:-1, :] |= out[1:, :]
        grown[:, 1:] |= out[:, :-1]
        grown[:, :-1] |= out[:, 1:]
        out = grown
    return out


def _cache_key(size):
    """Invalidates the texel map when the source FBX or the base texture (or its size) changes."""
    parts = [f"{f.name}:{f.stat().st_size}:{int(f.stat().st_mtime)}" for f in (SPLIT_FBX, BASE_TEX)]
    return "|".join(parts + [str(size)])


def _position_map(scene, head, body, unit, size):
    """Bake (once) the rest-pose game-metre position and normal of every head texel. Cached to Temp."""
    key = _cache_key(size)
    if CACHE.exists():
        with np.load(CACHE) as data:  # context manager: an open npz locks the file on Windows
            if "key" in data and str(data["key"]) == key:
                return data["pos"], data["nrm"], data["cov"]
    attr = head.data.attributes.new("_rest_m", 'FLOAT_VECTOR', 'POINT')
    nattr = head.data.attributes.new("_rest_n", 'FLOAT_VECTOR', 'POINT')
    pos = _positions(head, unit)
    mw3 = head.matrix_world.to_3x3()
    for i, v in enumerate(head.data.vertices):
        attr.data[i].vector = pos[i]
        nattr.data[i].vector = (mw3 @ v.normal).normalized()
    results = {}
    saved = list(head.data.materials)
    mat = bpy.data.materials.new("_HV_Bake")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    at = nt.nodes.new("ShaderNodeAttribute")
    tex = nt.nodes.new("ShaderNodeTexImage")
    nt.links.new(at.outputs["Vector"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    head.data.materials.clear()
    head.data.materials.append(mat)
    try:
        nt.nodes.active = tex
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 1
        scene.render.bake.margin = 3
        bpy.ops.object.select_all(action='DESELECT')
        head.select_set(True)
        bpy.context.view_layer.objects.active = head
        for bake_key, name in (("pos", "_rest_m"), ("nrm", "_rest_n")):
            img = bpy.data.images.new("_HV_" + bake_key, size, size, alpha=True, float_buffer=True)
            try:
                img.colorspace_settings.name = 'Non-Color'
                img.pixels.foreach_set(np.zeros(size * size * 4, dtype=np.float32))
                tex.image = img
                at.attribute_name = name
                bpy.ops.object.bake(type='EMIT')
                px = np.empty(size * size * 4, dtype=np.float32)
                img.pixels.foreach_get(px)
                results[bake_key] = px.reshape(size, size, 4)
            finally:
                bpy.data.images.remove(img)
    finally:
        head.data.materials.clear()
        for m in saved:
            head.data.materials.append(m)
        bpy.data.materials.remove(mat)
        for name in ("_rest_m", "_rest_n"):
            if name in head.data.attributes:
                head.data.attributes.remove(head.data.attributes[name])
    # Bake alpha is filled over the whole image, so coverage comes from a margin-0 colour bake of the head,
    # grown by the 3 px bake margin (seam padding), minus every texel a body face covers.
    cov = _dilate(_coverage(scene, head, size), 3) & ~_coverage(scene, body, size)
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(CACHE, pos=results["pos"][..., :3], nrm=results["nrm"][..., :3], cov=cov, key=np.array(key))
    return results["pos"][..., :3], results["nrm"][..., :3], cov


def _value_noise(p, freq, seed):
    """Smooth 3D value noise in [0,1] at points p (N,3), frequency in cycles per metre."""
    seed = int(seed)
    q = p * freq + seed * 17.13
    i = np.floor(q).astype(np.int64)
    f = q - i
    f = f * f * (3 - 2 * f)

    def h(ix, iy, iz):
        n = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
        n = (n ^ (n >> 13)) * 1274126177
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0

    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                wx = f[:, 0] if dx else 1 - f[:, 0]
                wy = f[:, 1] if dy else 1 - f[:, 1]
                wz = f[:, 2] if dz else 1 - f[:, 2]
                out = out + wx * wy * wz * h(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz)
    return out


def _smooth(x, a, b):
    if b == a:  # degenerate range = hard step (avoids inf / NaN texels)
        return (np.asarray(x) >= a).astype(np.float64)
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def _ellipsoid(p, e):
    d2 = (((p - np.array(e["center"])) / _radius(e["radius"])) ** 2).sum(axis=1)
    return 1 - _smooth(d2, e.get("inner", 0.6), 1.0)


def _region_mask(p, layer):
    """Mask from a layer's soft region spec. `mirror_x` evaluates it on both sides of the face."""
    m = _region_mask_side(p, layer)
    if layer.get("mirror_x"):
        m = np.maximum(m, _region_mask_side(p * np.array([-1, 1, 1]), layer))
    if "noise" in layer:
        n = layer["noise"]
        v = _value_noise(p, n["freq"], n.get("seed", 1))
        m *= _smooth(v, n.get("lo", 0.4), n.get("hi", 0.6))
    return m * layer.get("strength", 1.0)


def _region_mask_side(p, layer):
    m = np.ones(len(p))
    if "ellipsoid" in layer:
        m *= _ellipsoid(p, layer["ellipsoid"])
    for e in layer.get("exclude", []):
        m *= 1 - _ellipsoid(p, e)
    if "z_above" in layer:          # [z0, z1]: 0 below z0, 1 above z1
        m *= _smooth(p[:, 2], *layer["z_above"])
    if "z_below" in layer:          # [z0, z1]: 1 below z0, 0 above z1
        m *= 1 - _smooth(p[:, 2], *layer["z_below"])
    if "front_y_below" in layer:    # [y0, y1]: 1 for y < y0 (front), 0 for y > y1
        m *= 1 - _smooth(p[:, 1], *layer["front_y_below"])
    if "hairline" in layer:         # scalp above a curved hairline: z_front at the forehead centre, z_temple at |x| = 0.06
        h = layer["hairline"]
        zx = h["z_front"] + (h["z_temple"] - h["z_front"]) * np.clip(np.abs(p[:, 0]) / 0.06, 0, 1) ** 2
        back = np.clip((p[:, 1] - h.get("y_back_start", -0.04)) / 0.08, 0, 1)
        zl = zx * (1 - back) + h.get("z_back", 1.62) * back
        m *= _smooth(p[:, 2] - zl, -h.get("soft", 0.006), h.get("soft", 0.006))
    return m


def _lum(c):
    return c[:, 0] * 0.299 + c[:, 1] * 0.587 + c[:, 2] * 0.114


def _paint_layer(c, p, layer, skin):
    """Apply one layer to colors c (N,3, sRGB 0-1) at positions p. Ops:
    skin     replace hair with the sampled skin tone (bald / receding / shaved), loose hair threshold
    tint     recolour hair toward `color` (alias of grey; kept for readability)
    grey     recolour hair toward `color` (grey / white / other hair colour)
    fill     paint flat `color` (beard / scar / bruise)
    darken   multiply by `amount`
    iris     recolour the painted iris (eye colour)
    Any op takes `detail` {freq, seed, amount}: brightness noise on the target (strands).
    Hair recolouring treats each texel as a skin/hair mix: its hair fraction is where its luminance falls between
    the forehead skin and the region's hair median, and only that fraction is shifted. This keeps hairline
    transitions free of halos (texels partly recoloured toward grey) and of leftover brown edges.
    """
    m = _region_mask(p, layer)[:, None]
    op = layer["op"]
    lum = _lum(c)[:, None]
    skin_lum = _lum(skin[None])[0]
    if op == "skin":
        detail = (lum / max(skin_lum, 1e-3)) ** 0.25
        target = np.clip(skin[None] * np.clip(detail, 0.9, 1.08), 0, 1)
        if layer.get("hair_only", False):
            lo, hi = layer.get("hair_range", [0.03, 0.15])  # loose: also clears hairline transition texels
            m = m * _smooth(skin_lum - lum, lo, hi)
    elif op in ("grey", "tint"):
        col = np.array(layer["color"])
        in_region = m[:, 0] > 0.3
        dark = in_region & (lum[:, 0] < skin_lum - 0.2)  # unambiguous hair texels
        if dark.sum() < 20:
            return c
        hair_ref = np.median(c[dark], axis=0)
        hair_lum = float(_lum(hair_ref[None])[0])
        # Eased hair fraction: near-skin transition texels stay original (no light band / cyan cast from
        # subtracting brown), clearly-hair texels take the full recolour.
        fraction = _smooth((skin_lum - lum) / max(skin_lum - hair_lum, 1e-3), *layer.get("fraction_range", [0.2, 0.9]))
        if not layer.get("hair_only", True):
            fraction = np.ones_like(fraction)
        strand = np.clip(lum / max(hair_lum, 1e-3), 0, 1.0) ** layer.get("contrast", 1.0)  # >1 = lighter than hair: halo
        target = np.clip(c * (1 - fraction) + col[None] * strand * fraction, 0, 1)
    elif op == "iris":
        # Eye colour: recolour the painted iris (centres x +-0.0353, z 1.7197, r ~3 mm; use mirror_x),
        # keeping its luminance pattern. Bright sclera and lid skin are skipped.
        col = np.array(layer["color"])
        m = m * _smooth(skin_lum * 0.92 - lum, 0.0, 0.08)
        core = m[:, 0] > 0.5
        if core.sum() < 5:
            return c
        ref = float(np.median(lum[core]))
        target = np.clip(col[None] * np.clip(lum / max(ref, 1e-3), 0, 1.1) ** layer.get("contrast", 1.0), 0, 1)
    elif op == "fill":
        col = np.array(layer["color"])
        target = np.repeat(col[None], len(c), axis=0)
    elif op == "darken":
        target = c * layer.get("amount", 0.8)
    else:
        raise ValueError(f"unknown paint op {op}")
    if "detail" in layer:  # strand-scale brightness noise (beards, brows): breaks up flat recolours
        d = layer["detail"]
        v = _value_noise(p, d["freq"], d.get("seed", 3))[:, None]
        target = np.clip(target * (1 - d.get("amount", 0.3) + 2 * d.get("amount", 0.3) * v), 0, 1)
    return c * (1 - m) + target * m


def _paint(config, scene, head, body, unit):
    img = bpy.data.images.load(str(BASE_TEX), check_existing=False)
    size = img.size[0]
    if img.size[0] != img.size[1]:
        bpy.data.images.remove(img)
        raise ValueError(f"{BASE_TEX.name}: expected a square texture, got {tuple(img.size)}")
    px = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(size, size, 4)
    bpy.data.images.remove(img)
    pos, nrm, cov = _position_map(scene, head, body, unit, size)
    idx = np.where(cov)
    p = pos[idx].astype(np.float64)
    c = px[idx][:, :3].astype(np.float64)
    # Reference skin = median forehead colour (centre, between brows and hairline).
    fore = (np.abs(p[:, 0]) < 0.025) & (p[:, 2] > 1.745) & (p[:, 2] < 1.76) & (p[:, 1] < -0.08)
    skin = np.median(c[fore], axis=0) if fore.any() else np.array([0.75, 0.62, 0.55])
    for layer in config.get("paint", []):
        c = _paint_layer(c, p, layer, skin)
    out = px.copy()
    out[idx[0], idx[1], :3] = np.clip(c, 0, 1)
    return out, size


# ---------------------------------------------------------------------------------------------- render / export

def _preview_material(name, pixels, size):
    img = bpy.data.images.new("_HV_tex_" + name, size, size, alpha=True)
    img.pixels.foreach_set(pixels.ravel())
    nrm = bpy.data.images.load(str(BASE_NRM), check_existing=True)
    nrm.colorspace_settings.name = 'Non-Color'
    mat = bpy.data.materials.new("_HV_mat_" + name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.7
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = nrm
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return img, mat


def _render(scene, rig, head, name, out_dir):
    rig.scale = (1, 1, 1)  # preview in game metres; restored by the caller
    bpy.context.view_layer.update()
    target = Vector((0, -0.02, 1.68))
    added = []

    def light(lname, loc, energy):
        ld = bpy.data.lights.new(lname, 'AREA'); ld.energy = energy; ld.size = 0.6
        lo = bpy.data.objects.new(lname, ld); lo.location = loc
        lo.rotation_euler = (target - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.collection.objects.link(lo); added.append(lo)

    light("_HV_Key", (0.8, -1.2, 2.1), 60)
    light("_HV_Fill", (-1.0, -0.9, 1.8), 25)
    light("_HV_Rim", (0.0, 1.2, 2.2), 20)
    cd = bpy.data.cameras.new("_HV_Cam"); cd.lens = 85
    cam = bpy.data.objects.new("_HV_Cam", cd); scene.collection.objects.link(cam); added.append(cam)
    scene.camera = cam
    if scene.world is None:
        scene.world = bpy.data.worlds.new("_HV_World")
    scene.world.color = (0.25, 0.25, 0.25)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 420, 520
    scene.view_settings.view_transform = 'Standard'  # closer to the game's untonemapped albedo than AgX
    files = []
    try:
        for label, deg in (("front", 0), ("34", 40), ("side", 90)):
            a = math.radians(deg)
            cam.location = (0.95 * math.sin(a), target.y - 0.95 * math.cos(a), target.z + 0.02)
            cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = str(Path(out_dir) / f"{name}_{label}.png")
            bpy.ops.render.render(write_still=True)
            files.append(scene.render.filepath)
        # Eye / brow close-up (iris colour, brows) - same lights, long lens.
        cd.lens = 200
        cam.location = (0, -0.6, 1.724)
        cam.rotation_euler = (Vector((0, 0, 1.724)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 600, 200
        scene.render.filepath = str(Path(out_dir) / f"{name}_eyes.png")
        bpy.ops.render.render(write_still=True)
        files.append(scene.render.filepath)
    finally:
        for obj in added:
            bpy.data.objects.remove(obj, do_unlink=True)
    return files


def _export(rig, head, name, pixels, size):
    folder = HEADS_DIR / name
    folder.mkdir(parents=True, exist_ok=True)
    tex_path = folder / f"T_Head_{name}_BaseColor.png"
    img = bpy.data.images.new("_HV_export_" + name, size, size, alpha=True)
    img.pixels.foreach_set(pixels.ravel())
    img.filepath_raw = str(tex_path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    bpy.ops.object.select_all(action='DESELECT')
    head.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx_path = folder / f"Head_{name}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                             object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=False, mesh_smooth_type='OFF', path_mode='STRIP')
    return [str(fbx_path), str(tex_path)]


# ---------------------------------------------------------------------------------------------- entry

def run(config_path, export=False, render=True):
    config = json.loads(Path(config_path).read_text(encoding="utf-8-sig"))
    name = config["name"]
    scene, rig, head, body, unit = _load()
    base_pos = _positions(head, unit)
    base_normals = [Vector(c.vector) for c in head.data.corner_normals]
    base_uv = [Vector(l.uv) for l in head.data.uv_layers.active.data]
    freeze = _freeze_weights(base_pos)
    pixels, size = _paint(config, scene, head, body, unit)  # masks use the rest (undeformed) positions
    pos = _deform(base_pos, freeze, config.get("deforms", []))
    _set_positions(head, unit, pos)
    _fix_normals(head, freeze, base_normals)
    # Frozen interface check: positions, corner normals and UVs of every frozen vertex are unchanged.
    after = _positions(head, unit)
    frozen = freeze <= 0.0
    max_shift = float(np.abs(after[frozen] - base_pos[frozen]).max())
    normals = [Vector(c.vector) for c in head.data.corner_normals]
    max_normal = max((normals[i] - base_normals[i]).length for i, l in enumerate(head.data.loops)
                     if frozen[l.vertex_index])
    uv_same = all((Vector(l.uv) - base_uv[i]).length < 1e-9 for i, l in enumerate(head.data.uv_layers.active.data))
    # Custom-normal re-encoding leaves ~3e-4 (0.02 deg) noise; Unity's seam check allows 1 deg.
    if max_shift > 1e-6 or max_normal > 1e-3 or not uv_same:
        raise RuntimeError(f"{name}: frozen neck changed (shift {max_shift}, normal {max_normal}, uv {uv_same})")
    head.name = head.data.name = f"Head_{name}"
    report = {"name": name, "frozen_vertices": int(frozen.sum()), "max_frozen_shift_m": max_shift,
              "max_frozen_normal_delta": max_normal,
              "max_deform_m": float(np.linalg.norm(after - base_pos, axis=1).max())}
    img, mat = _preview_material(name, pixels, size)
    saved = {o: list(o.data.materials) for o in (head, body)}
    for o in saved:
        o.data.materials.clear()
        o.data.materials.append(mat)  # body texels are unchanged in the painted copy
    try:
        if render:
            out_dir = PROJECT / config.get("render", {}).get("out_dir", "Temp/HeadVariants")
            out_dir.mkdir(parents=True, exist_ok=True)
            report["renders"] = _render(scene, rig, head, name, out_dir)
    finally:
        rig.scale = (unit, unit, unit)
        bpy.context.view_layer.update()
        for o, mats in saved.items():
            o.data.materials.clear()
            for m in mats:
                o.data.materials.append(m)
        bpy.data.materials.remove(mat)
        bpy.data.images.remove(img)
    if export:
        report["exported"] = _export(rig, head, name, pixels, size)
    return report
