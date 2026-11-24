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
  preview_tone    "#RRGGBB"? the NPC's SkinToneSO colour: mockups show the face as SG_SkinLit will tone it.
  portrait        optional face source from a front + side portrait (see the portrait section).
  meshy           optional face source from a Meshy 3D head: {"source", "texture", "landmarks", ...} (see the meshy
                  section). One source per head: "portrait" or "meshy", never both.
Paint only ever touches texels of head faces whose layer mask is > 0; body texels are copied unchanged.

Skin mask (SG_SkinLit `_SkinMask`, spec-npc-faces-second-batch): every layer also updates a per-texel skin weight. It
starts from the UNBLURRED base head rule (_base_head_skin, configs/skin_masks.json), the eyes are excluded again after
the last layer, and the result is blurred once like the base mask. Export writes it as T_SkinMask_Head_<Name>.png;
texels outside the head are the shipped base mask (T_SkinMask_BaseHero.png) unchanged. See _paint_layer for each op's
skin value.
"""
import json
import math
import re
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
BASE_MASK = BASE_DIR / "T_SkinMask_BaseHero.png"
BASE_MATERIAL = BASE_DIR / "M_TestBaseHero.mat"  # its _SkinRefLuminance: variant heads reuse it (same tone transform)
CACHE = PROJECT / "Temp/HeadVariants/_posmap.npz"
SCENE = "HeadVariant"
FREEZE = {"seam_m": 1.585, "distance_m": 0.012, "ramp_m": 0.035}
# The ring is stored at z = 1.5850001: a strict "<= seam" test leaves it out of the frozen seed, and a large warp near the
# neck (portrait RBF) then moves it by its tiny ramp weight. Portrait faces seed everything within this tolerance of the
# seam (legacy configs keep the strict seed: see _freeze_weights).
SEAM_TOL_M = 1e-4


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

def _freeze_weights(pos, seam_tol=0.0):
    """seam_tol: SEAM_TOL_M for portrait faces. Legacy (painted) configs keep the strict seed, so their regenerated
    FBXs stay identical to the shipped ones (their deforms barely reach the neck, so the ring's tiny weight is moot)."""
    frozen = np.where(pos[:, 2] <= FREEZE["seam_m"] + seam_tol)[0]
    if len(frozen) == 0:
        raise RuntimeError(f"No head vertex at or below the seam ({FREEZE['seam_m']} m): wrong input or seam height")
    tree = KDTree(len(frozen))
    for i, idx in enumerate(frozen):
        tree.insert(Vector(pos[idx]), i)
    tree.balance()
    w = np.zeros(len(pos))
    for i, p in enumerate(pos):
        if pos[i, 2] <= FREEZE["seam_m"] + seam_tol:
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
    """Soft ellipsoid; optional `rot_y` (degrees) tilts it in the face (X-Z) plane: a diagonal scar."""
    d = p - np.array(e["center"])
    if e.get("rot_y"):
        a = math.radians(e["rot_y"])
        d = np.stack([d[:, 0] * math.cos(a) - d[:, 2] * math.sin(a), d[:, 1],
                      d[:, 0] * math.sin(a) + d[:, 2] * math.cos(a)], axis=1)
    d2 = ((d / _radius(e["radius"])) ** 2).sum(axis=1)
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


def _paint_layer(c, p, layer, skin, s):
    """Apply one layer to colors c (N,3, sRGB 0-1) at positions p; returns (colors, skin weights s). Ops:
    skin     replace hair with the sampled skin tone (bald / receding / shaved), loose hair threshold
    tint     recolour hair toward `color` (alias of grey; kept for readability)
    grey     recolour hair toward `color` (grey / white / other hair colour)
    fill     paint flat `color` (beard / scar / bruise)
    darken   multiply by `amount`
    iris     recolour the painted iris (eye colour)
    decal    project an image (luminance = ink coverage) along +-X: brand / tattoo, over the skin detail
    Skin weight: s = s * (1 - m) + k * m with the layer mask m. Default k per op: skin 1, grey / tint 1 - hair
    fraction, fill 0 (beard), iris 0, darken keeps s, decal 0 (weighted by the painted ink, opacity included). Override with
    `skin` (0-1): scars ~0.5, freckles ~0.8, bruises ~0.4 keep part of their hue once toned.
    Any op takes `detail` {freq, seed, amount}: brightness noise on the target (strands).
    Hair recolouring treats each texel as a skin/hair mix: its hair fraction is where its luminance falls between
    the forehead skin and the region's hair median, and only that fraction is shifted. This keeps hairline
    transitions free of halos (texels partly recoloured toward grey) and of leftover brown edges.
    """
    if layer.get("strength", 1.0) < 0:
        raise ValueError(f"layer {layer.get('op')}: strength must be >= 0, got {layer['strength']}")
    m = np.clip(_region_mask(p, layer), 0, 1)[:, None]
    op = layer["op"]
    k = {"skin": 1.0, "fill": 0.0, "iris": 0.0}.get(op)  # None = keep s (darken; decal handles its own)
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
            return c, s
        hair_ref = np.median(c[dark], axis=0)
        hair_lum = float(_lum(hair_ref[None])[0])
        # Eased hair fraction: near-skin transition texels stay original (no light band / cyan cast from
        # subtracting brown), clearly-hair texels take the full recolour.
        raw = (skin_lum - lum) / max(skin_lum - hair_lum, 1e-3)
        fraction = _smooth(raw, *layer.get("fraction_range", [0.2, 0.9]))
        if not layer.get("hair_only", True):
            fraction = np.ones_like(fraction)
            raw = np.ones_like(raw)
        strand = np.clip(lum / max(hair_lum, 1e-3), 0, 1.0) ** layer.get("contrast", 1.0)  # >1 = lighter than hair: halo
        target = np.clip(c * (1 - fraction) + col[None] * strand * fraction, 0, 1)
        # Mixed hair / skin edge texels. Dark hair: skin = all but clearly-hair texels (toning a mixed texel keeps it
        # dark; leaving it untoned shows its light skin share as a halo on dark tones, as with the base mask).
        # Light hair (blonde, grey, white): the reverse - a toned light texel turns tone-coloured (salmon band on dark
        # tones), so any noticeable hair share stays untoned.
        light = _lum(col[None])[0] > 0.6 * skin_lum
        k = 1.0 - _smooth(raw[:, 0], *layer.get("skin_hair_range", [0.35, 0.7] if light else [0.6, 0.95]))
    elif op == "iris":
        # Eye colour: recolour the painted iris (centres x +-0.0353, z 1.7197, r ~3 mm; use mirror_x),
        # keeping its luminance pattern. Bright sclera and lid skin are skipped.
        col = np.array(layer["color"])
        m = m * _smooth(skin_lum * 0.92 - lum, 0.0, 0.08)
        core = m[:, 0] > 0.5
        if core.sum() < 5:
            return c, s
        ref = float(np.median(lum[core]))
        target = np.clip(col[None] * np.clip(lum / max(ref, 1e-3), 0, 1.1) ** layer.get("contrast", 1.0), 0, 1)
    elif op == "fill":
        col = np.array(layer["color"])
        target = np.repeat(col[None], len(c), axis=0)
    elif op == "darken":
        target = c * layer.get("amount", 0.8)
    elif op == "decal":
        if layer.get("mirror_x"):
            raise ValueError("decal: mirror_x is not supported (one side per layer; add a second layer at -x)")
        cov = _decal_coverage(p, layer)
        ink = np.array(layer.get("color", [0.07, 0.08, 0.11]))
        detail = np.clip(lum / max(skin_lum, 1e-3), 0.6, 1.3)  # pores / shading show through the ink
        target = np.clip(ink[None] * detail, 0, 1)
        m = m * cov[:, None] * layer.get("opacity", 0.75)
        # Skin follows the painted ink (opacity included): the skin left under semi-opaque ink stays toneable, so no
        # light ghost of the base skin shows inside the brand on dark tones.
        s = s * (1 - m[:, 0]) + layer.get("skin", 0.0) * m[:, 0]
    else:
        raise ValueError(f"unknown paint op {op}")
    if "detail" in layer:  # strand-scale brightness noise (beards, brows): breaks up flat recolours
        d = layer["detail"]
        v = _value_noise(p, d["freq"], d.get("seed", 3))[:, None]
        target = np.clip(target * (1 - d.get("amount", 0.3) + 2 * d.get("amount", 0.3) * v), 0, 1)
    if op != "decal":
        k = layer.get("skin", k)
        if k is not None:
            s = s * (1 - m[:, 0]) + np.asarray(k) * m[:, 0]
    return c * (1 - m) + target * m, np.clip(s, 0, 1)


_DECAL_CACHE = {}


def _decal_image(path):
    """Ink coverage image of a decal source: luminance x alpha (transparent texels never ink), rows bottom-up as Blender
    stores them. Expected input: 8-bit sRGB PNG (pixels read raw). Cached per path + modification time."""
    full = PROJECT / path
    if not full.exists():
        raise FileNotFoundError(f"decal image {path} not found")
    key = (path, full.stat().st_mtime_ns)
    if key not in _DECAL_CACHE:
        img = bpy.data.images.load(str(full), check_existing=False)
        try:
            w, h = img.size
            px = np.empty(w * h * 4, dtype=np.float32)
            img.pixels.foreach_get(px)
            px = px.reshape(h * w, 4)
            _DECAL_CACHE[key] = (_lum(px[:, :3]) * px[:, 3]).reshape(h, w)
        finally:
            bpy.data.images.remove(img)
    return _DECAL_CACHE[key]


def _decal_coverage(p, layer):
    """Ink coverage of an image projected along X onto the side x * sign(center x) > 0.
    `center` [x,y,z]; `size` [w,h] metres (w along the neck toward the back, h along +Z); `crop` [u0,v0,u1,v1] of
    the image (v from the top); `threshold` [lo,hi] on its luminance; `depth` max |x - cx|. Reads unmirrored from
    that side."""
    img = _decal_image(layer["image"])
    h, w = img.shape
    cx, cy, cz = layer["center"]
    sw, sh = layer["size"]
    if abs(cx) < 1e-3:
        raise ValueError("decal: projected along X onto one side, so center x must be off the midline (|x| >= 1 mm)")
    if sw <= 0 or sh <= 0:
        raise ValueError(f"decal: size must be positive, got {layer['size']}")
    crop = layer.get("crop", [0, 0, 1, 1])
    if crop[2] <= crop[0] or crop[3] <= crop[1]:
        raise ValueError(f"decal: crop must be [u0, v0, u1, v1] with u1 > u0 and v1 > v0, got {crop}")
    sign = 1.0 if cx > 0 else -1.0
    u = (p[:, 1] - cy) * sign / sw + 0.5
    v = (p[:, 2] - cz) / sh + 0.5
    u0, v0, u1, v1 = layer.get("crop", [0, 0, 1, 1])
    iu = u0 + u * (u1 - u0)
    iv = 1 - (v0 + (1 - v) * (v1 - v0))  # image v from the top -> Blender rows from the bottom
    inside = ((u >= 0) & (u <= 1) & (v >= 0) & (v <= 1) & (p[:, 0] * sign > 0)
              & (np.abs(p[:, 0] - cx) < layer.get("depth", 0.03)))
    x = np.clip(iu * (w - 1), 0, w - 1)
    y = np.clip(iv * (h - 1), 0, h - 1)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    x1, y1 = np.minimum(x0 + 1, w - 1), np.minimum(y0 + 1, h - 1)
    fx, fy = x - x0, y - y0
    val = (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x1] * fx * (1 - fy)
           + img[y1, x0] * (1 - fx) * fy + img[y1, x1] * fx * fy)
    return np.where(inside, _smooth(val, *layer.get("threshold", [0.35, 0.65])), 0.0)


SKIN_MASKS_CONFIG = PROJECT / "Tools/Blender/configs/skin_masks.json"


def _ellipsoid_in(p, center, radius):
    d2 = (((p - np.array(center)) / np.array(radius)) ** 2).sum(axis=-1)
    return 1 - _smooth(d2, 0.6, 1.0)


def _base_head_skin(p, c, cfg):
    """Unblurred base skin rule for head texels (skin_masks.json "head"): 1 = face skin. Hair = texels clearly darker
    than the forehead inside the scalp / brow zones; eyes excluded. Shared by make_skin_masks.py."""
    lum = _lum(c)
    fore = (np.abs(p[:, 0]) < 0.025) & (p[:, 2] > 1.745) & (p[:, 2] < 1.76) & (p[:, 1] < -0.08)
    skin = np.median(lum[fore]) if fore.any() else 0.55
    dark = _smooth(skin - lum, cfg["hair_dark_lo"] * skin, cfg["hair_dark_hi"] * skin)
    zone = np.zeros(len(p))
    for e in cfg["hair_zones"]:
        zone = np.maximum(zone, _ellipsoid_in(p, e["center"], e["radius"]))
        if e.get("mirror_x"):
            zone = np.maximum(zone, _ellipsoid_in(p * np.array([-1, 1, 1]), e["center"], e["radius"]))
    m = 1 - dark * zone
    for e in cfg["eyes"]:
        for sx in (1, -1):
            m *= 1 - _ellipsoid_in(p * np.array([sx, 1, 1]), e["center"], e["radius"])
    return m


def _blur(mask, px):
    """Box blur of `px` texels (soft mask edges)."""
    out = mask.copy()
    for _ in range(px):
        acc = out.copy()
        acc[1:, :] += out[:-1, :]
        acc[:-1, :] += out[1:, :]
        acc[:, 1:] += out[:, :-1]
        acc[:, :-1] += out[:, 1:]
        out = acc / 5
    return out


def _paint(config, scene, head, body, unit, project=None, force_eyes=True):
    """project: optional callable (p, n, c, c0, s) -> (c, s, report) run after the paint layers (portrait faces).
    force_eyes: zero the skin weight inside the base eye ellipsoids after everything. Meshy heads pass False: their
    projection keeps the eyes (sclera, iris) untoned itself, and the Meshy lids that cover part of the base eyeball
    must stay toneable (forced to 0 they show as dark rings on Fair, pale rings on Deep)."""
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
    # Skin weight starts from the UNBLURRED base head rule and is blurred once at the end, like the base mask: tracking
    # from the blurred PNG leaves a half-masked (untoned) band wherever paint moves a hair edge (receding / shaved).
    sm_cfg = json.loads(SKIN_MASKS_CONFIG.read_text(encoding="utf-8-sig"))["base"]
    s = _base_head_skin(p, c, sm_cfg["head"])
    c0 = c.copy()
    for layer in config.get("paint", []):
        c, s = _paint_layer(c, p, layer, skin, s)
    proj_report = None
    if project is not None:
        c, s, proj_report = project(p, nrm[idx].astype(np.float64), c, c0, s)
    for e in (sm_cfg["head"]["eyes"] if force_eyes else []):  # eyes are never skin, whatever a later layer covered
        for sx in (1, -1):
            s *= 1 - _ellipsoid_in(p * np.array([sx, 1, 1]), e["center"], e["radius"])
    out = px.copy()
    out[idx[0], idx[1], :3] = np.clip(c, 0, 1)
    mask = _load_grey(BASE_MASK, size)  # body texels: the base mask as shipped
    blurred = mask.copy()
    blurred[idx] = np.clip(s, 0, 1)
    blurred = _blur(blurred, sm_cfg.get("blur_px", 2))
    mask[idx] = np.clip(blurred[idx], 0, 1)
    return out, size, mask, proj_report


def _load_grey(path, size):
    """R channel of a non-colour PNG (raw values), size-checked."""
    img = bpy.data.images.load(str(path), check_existing=False)
    try:
        if tuple(img.size) != (size, size):
            raise ValueError(f"{path.name}: expected {size}x{size}, got {tuple(img.size)}")
        px = np.empty(size * size * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return px.reshape(size, size, 4)[..., 0].copy()
    finally:
        bpy.data.images.remove(img)


def _srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _linear_to_srgb(c):
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.maximum(c, 0), 1 / 2.4) - 0.055)


def _base_ref_luminance():
    """M_TestBaseHero._SkinRefLuminance, read from the material YAML (never a copied constant: it moves when the base
    mask is regenerated, and the mockups must use the value the game uses)."""
    m = re.search(r"- _SkinRefLuminance: ([0-9.eE+-]+)", BASE_MATERIAL.read_text(encoding="utf-8"))
    if m is None:
        raise ValueError(f"{BASE_MATERIAL.name}: no _SkinRefLuminance")
    return float(m.group(1))


def _tone_rgb(tone_hex):
    h = str(tone_hex).lstrip("#")
    if not re.fullmatch(r"[0-9a-fA-F]{6}", h):
        raise ValueError(f"preview_tone must be #RRGGBB, got {tone_hex!r}")
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def _toned(pixels, mask, tone_hex):
    """SG_SkinLit's recolour in numpy (linear space): tone * lum / ref, clamped, blended by the mask."""
    tone = _srgb_to_linear(_tone_rgb(tone_hex))
    out = pixels.copy()
    lin = _srgb_to_linear(pixels[..., :3].astype(np.float64))
    lum = (lin * np.array([0.299, 0.587, 0.114])).sum(axis=-1, keepdims=True)
    toned = np.clip(tone * lum / _base_ref_luminance(), 0, 1)
    m = mask[..., None]
    out[..., :3] = _linear_to_srgb(lin * (1 - m) + toned * m)
    return out


# ---------------------------------------------------------------------------------------------- portrait
# spec-npc-portrait-faces: a front portrait + side profile (user-generated, refs/) drive the shape through MediaPipe
# landmarks and are projected onto the face as its texture. Landmarks come from Tools/FaceRef/face_landmarks.py
# (uv, Python 3.12), run on the portraits AND on flat ortho renders of the base head (render_base_refs), so no
# landmark is placed by hand.

REFS = PROJECT / "Tools/Blender/configs/heads/refs"
BASE_LANDMARKS = REFS / "_base_landmarks.json"
# Ortho cameras of the base head renders: centre (x or y, z) in game metres, ortho scale (m), square pixel size.
ORTHO = {"front": {"center": (0.0, 1.70), "scale": 0.36, "px": 1024},
         "side": {"center": (-0.01, 1.70), "scale": 0.36, "px": 1024}}
# MediaPipe Face Mesh indices.
MP_OVAL = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400, 377, 152, 148, 176,
           149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67, 109]
MP_EYE_R = [33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246]   # the man's right
MP_EYE_L = [263, 249, 390, 373, 374, 380, 381, 382, 362, 398, 384, 385, 386, 387, 388, 466]
MP_LIPS = [61, 146, 91, 181, 84, 17, 314, 405, 321, 375, 291, 409, 270, 269, 267, 0, 37, 39, 40, 185]
MP_BROWS = [70, 63, 105, 66, 107, 55, 65, 52, 53, 46, 300, 293, 334, 296, 336, 285, 295, 282, 283, 276]
MP_NOSE = [168, 6, 197, 195, 5, 4, 1, 19, 94, 2, 98, 327, 129, 358, 49, 279, 48, 278, 64, 294]
MP_CHEEKS = [50, 280, 205, 425, 123, 352, 116, 345, 187, 411]
MP_EYE_KEYS = [33, 133, 159, 145, 362, 263, 386, 374]
MP_MIDLINE = [10, 151, 9, 8, 168, 6, 197, 195, 5, 4, 1, 19, 94, 2, 164, 0, 11, 12, 13, 14, 15, 16, 17, 18, 200, 199,
              175, 152]
MP_UNDER_CHIN = [152, 175, 199, 200, 18]  # midline points on / under the chin: the profile there is the throat
MP_ALIGN = [33, 133, 362, 263, 168]  # eye corners + nasion: the face's own proportions survive the alignment
MP_CONTROLS = sorted(set(MP_OVAL + MP_EYE_KEYS + MP_LIPS + MP_BROWS + MP_NOSE + MP_CHEEKS + MP_MIDLINE))
PORTRAIT_DEFAULTS = {"shape_strength": 1.0, "depth_strength": 1.0, "max_depth_mm": 8.0, "max_mm": 25.0, "facing": [0.25, 0.55],
                     "erode_m": 0.002, "feather_m": 0.006, "feather_top_m": 0.02, "forehead_extend_m": 0.02, "delight": 0.3, "delight_radius_m": 0.03,
                     "skin_patch_m": 0.012, "contrast": 1.0,
                     # Side profile projection (cheeks, jaw): |normal.x| ramp, region in game metres (stops in front of
                     # the ear and below the eyes), overall strength. The profile shows the man's left; side_mirror
                     # also paints it on his right (turn off when one-sided marks fall in the region).
                     "side_facing": [0.3, 0.65], "side_strength": 1.0, "side_mirror": True,
                     "side_region": {"z_min_m": 1.59, "z_max_m": 1.715, "y_max_m": -0.005}}


def render_base_refs(out_dir=None):
    """Flat (emission) ortho renders of the base head, front and side (facing left), on a plain light grey background.
    Then run: uv run Tools/FaceRef/face_landmarks.py <front> <side> --out Tools/Blender/configs/heads/refs/_base_landmarks.json
    The ORTHO table above maps their pixels back to game metres."""
    out_dir = Path(out_dir or PROJECT / "Temp/FaceRef")
    out_dir.mkdir(parents=True, exist_ok=True)
    scene, rig, head, body, unit = _load()
    body.hide_render = True
    rig.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    img = bpy.data.images.load(str(BASE_TEX), check_existing=True)
    mat = bpy.data.materials.new("_HV_flat")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
    nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    saved = list(head.data.materials)
    head.data.materials.clear(); head.data.materials.append(mat)
    cd = bpy.data.cameras.new("_HV_Ortho"); cd.type = 'ORTHO'
    cam = bpy.data.objects.new("_HV_Ortho", cd); scene.collection.objects.link(cam)
    scene.camera = cam
    if scene.world is None:
        scene.world = bpy.data.worlds.new("_HV_World")
    scene.world.use_nodes = False
    scene.world.color = (0.6, 0.6, 0.6)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'Standard'
    files = []
    try:
        for view in ("front", "side"):
            o = ORTHO[view]
            cd.ortho_scale = o["scale"]
            scene.render.resolution_x = scene.render.resolution_y = o["px"]
            if view == "front":  # looking +Y: image right = +X (the man's left), up = +Z
                cam.location = (o["center"][0], -2.0, o["center"][1])
                cam.rotation_euler = (math.radians(90), 0, 0)
            else:  # looking -X: image right = +Y (back of the head), so the face points left
                cam.location = (2.0, o["center"][0], o["center"][1])
                cam.rotation_euler = (math.radians(90), 0, math.radians(90))
            scene.render.filepath = str(out_dir / f"base_{view}.png")
            bpy.ops.render.render(write_still=True)
            files.append(scene.render.filepath)
    finally:
        head.data.materials.clear()
        for m in saved:
            head.data.materials.append(m)
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.materials.remove(mat)
        rig.scale = (unit, unit, unit)
        body.hide_render = False
    return files


def _ortho_to_game(view, u, v):
    """Pixel (u right, v down) of a base ortho render -> (horizontal, z) game metres."""
    o = ORTHO[view]
    s, n = o["scale"], o["px"]
    return o["center"][0] + ((np.asarray(u) + 0.5) / n - 0.5) * s, o["center"][1] + (0.5 - (np.asarray(v) + 0.5) / n) * s


def _similarity(src, dst):
    """2D Umeyama: dst ~ s * R @ src + t."""
    mu_s, mu_d = src.mean(0), dst.mean(0)
    a, b = src - mu_s, dst - mu_d
    u, sig, vt = np.linalg.svd(b.T @ a / len(src))
    d = np.diag([1, np.sign(np.linalg.det(u @ vt))])
    rot = u @ d @ vt
    s = (sig * np.diag(d)).sum() / (a ** 2).sum(1).mean()
    return s, rot, mu_d - s * rot @ mu_s


def _poly_inside_dist(pts, poly):
    """Signed distance (positive inside) of pts (N,2) to a closed polygon (E,2)."""
    a, b = poly, np.roll(poly, -1, axis=0)
    inside = np.zeros(len(pts), dtype=bool)
    for (x1, y1), (x2, y2) in zip(a, b):
        cross = ((y1 > pts[:, 1]) != (y2 > pts[:, 1]))
        xint = x1 + (pts[:, 1] - y1) * (x2 - x1) / np.where(y2 == y1, 1e-12, y2 - y1)
        inside ^= cross & (pts[:, 0] < xint)
    ab = b - a
    t = np.clip(((pts[:, None, :] - a[None]) * ab[None]).sum(-1) / np.maximum((ab ** 2).sum(-1), 1e-12), 0, 1)
    d = np.linalg.norm(pts[:, None, :] - (a[None] + t[..., None] * ab[None]), axis=-1).min(1)
    return np.where(inside, d, -d)


def _box_blur2d(a, r):
    """Separable box blur (3 passes ~ gaussian) of an (H, W) array with radius r pixels."""
    r = max(int(r), 1)
    out = a.astype(np.float64)
    for _ in range(3):
        for axis in (0, 1):
            pad = np.pad(out, [(r, r) if i == axis else (0, 0) for i in range(2)], mode='edge')
            cs = np.cumsum(pad, axis=axis)
            cs = np.insert(cs, 0, 0, axis=axis)
            n = out.shape[axis]
            hi = np.take(cs, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
            lo = np.take(cs, np.arange(0, n), axis=axis)
            out = (hi - lo) / (2 * r + 1)
    return out


def _bilinear(img, u, v):
    """Sample img (H, W, C) at pixel coordinates (u right, v down)."""
    h, w = img.shape[:2]
    u = np.clip(u - 0.5, 0, w - 1.001)
    v = np.clip(v - 0.5, 0, h - 1.001)
    u0, v0 = np.floor(u).astype(int), np.floor(v).astype(int)
    fu, fv = (u - u0)[:, None], (v - v0)[:, None]
    return (img[v0, u0] * (1 - fu) * (1 - fv) + img[v0, u0 + 1] * fu * (1 - fv)
            + img[v0 + 1, u0] * (1 - fu) * fv + img[v0 + 1, u0 + 1] * fu * fv)


def _load_rgb(path):
    """Image as (H, W, 3) top-down array of stored (sRGB-encoded) values. Transparent pixels are composited over the
    same light grey as face_landmarks.py, so both tools see one image."""
    img = bpy.data.images.load(str(path), check_existing=False)
    try:
        if img.is_float:
            raise ValueError(f"{Path(path).name}: 16-bit / float images are stored linear by Blender; save an 8-bit PNG")
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        a = px.reshape(h, w, 4)[::-1].astype(np.float64)
        return a[..., :3] * a[..., 3:] + (200 / 255) * (1 - a[..., 3:])
    finally:
        bpy.data.images.remove(img)


def _profile(side, to_game):
    """Side edge rows -> (z ascending, y) arrays in game metres."""
    e = np.array(side["edge"], dtype=np.float64)
    y, z = to_game(e[:, 1], e[:, 0])
    order = np.argsort(z)
    return z[order], y[order]


def _portrait_inputs(config):
    """Validate and load every portrait input BEFORE the scene is touched."""
    pc = config.get("portrait")
    if pc is None:
        return None
    if not isinstance(pc, dict) or "landmarks" not in pc:
        raise ValueError("portrait: expected {\"landmarks\": \".../refs/<name>_landmarks.json\", ...}")
    cfg = dict(PORTRAIT_DEFAULTS, **pc)
    cfg["side_region"] = dict(PORTRAIT_DEFAULTS["side_region"], **pc.get("side_region", {}))
    paths = {"landmarks": PROJECT / cfg["landmarks"], "base": BASE_LANDMARKS}
    for k, p in paths.items():
        if not p.is_file():
            raise FileNotFoundError(f"portrait {k}: {p} not found (run Tools/FaceRef/face_landmarks.py)")
    ref = json.loads(paths["landmarks"].read_text(encoding="utf-8"))
    base = json.loads(paths["base"].read_text(encoding="utf-8"))
    for which, data in (("portrait", ref), ("base", base)):
        try:
            ok = (len(data["front"]["points"]) == 478 and "crown_row" in data["front"] and len(data["side"]["edge"]) > 0
                  and all(f in data["side"]["features"] for f in ("nose_tip", "crown")))
        except (KeyError, TypeError):
            ok = False
        if not ok:
            raise ValueError(f"{which} landmarks: missing front points / crown_row or side edge / features "
                             "(re-run Tools/FaceRef/face_landmarks.py)")
    for view in ("front", "side"):
        if base[view]["size"] != [ORTHO[view]["px"]] * 2:
            raise ValueError(f"base landmarks: {view} size {base[view]['size']} does not match ORTHO (re-run render_base_refs)")
    positive = ("max_mm", "feather_m", "feather_top_m", "skin_patch_m", "delight_radius_m", "contrast")
    bad = [k for k in positive if not cfg[k] > 0]
    bad += [k for k in ("max_depth_mm", "erode_m", "forehead_extend_m", "delight") if not cfg[k] >= 0]
    bad += [k for k in ("shape_strength", "depth_strength", "side_strength") if not 0 <= cfg[k] <= 1]
    bad += [k for k in ("facing", "side_facing") if not (len(cfg[k]) == 2 and cfg[k][0] < cfg[k][1])]
    sr = cfg["side_region"]
    if not sr["z_min_m"] < sr["z_max_m"]:
        bad.append("side_region")
    if bad:
        raise ValueError(f"portrait: invalid {bad} (positive, >= 0, strengths 0-1, ramps [low, high])")
    cfg["ref"], cfg["base"] = ref, base
    for view, key in (("front", "image"), ("side", "side_image")):
        path = PROJECT / ref[view]["image"]
        if not path.is_file():
            raise FileNotFoundError(f"portrait {view} image {path} not found")
        cfg[key] = _load_rgb(path)
        if [cfg[key].shape[1], cfg[key].shape[0]] != ref[view]["size"]:
            raise ValueError(f"{path.name}: {cfg[key].shape[1]}x{cfg[key].shape[0]} but its landmarks were measured on "
                             f"{ref[view]['size']} (image replaced? re-run face_landmarks.py)")
    if cfg["side_mirror"] not in (True, False):
        raise ValueError("portrait side_mirror must be true or false")
    return cfg


def _portrait_fit(cfg, head, base_pos, freeze):
    """Landmark-fitted RBF warp. Returns (warp(points, freeze_w) -> displaced points, report, align)."""
    from mathutils.bvhtree import BVHTree
    ref, base = cfg["ref"], cfg["base"]
    bpts = np.array(base["front"]["points"])
    rpts = np.array(ref["front"]["points"])
    bx, bz = _ortho_to_game("front", bpts[:, 0], bpts[:, 1])
    base2d = np.stack([bx, bz], 1)
    s, rot, t = _similarity(np.stack([rpts[MP_ALIGN, 0], -rpts[MP_ALIGN, 1]], 1), base2d[MP_ALIGN])
    to_game = lambda u, v: (s * (rot @ np.stack([u, -v])).T + t)
    ref2d = to_game(rpts[:, 0], rpts[:, 1])
    # 3D anchors: ray-cast each base landmark onto the rest-pose head along +Y.
    polys = [list(p.vertices) for p in head.data.polygons]
    bvh = BVHTree.FromPolygons([Vector(p) for p in base_pos], polys)
    anchors, disp = [], []
    # Side profiles. The base is mapped by its ortho camera. The portrait's profile is scaled so crown -> nose tip
    # matches its own front image (front crown row -> landmark 1, once aligned): a long, unambiguous distance in both
    # photos (a nasion-based scale broke on the Warden's broken nose, ~1.7x off). The silhouette tip and MediaPipe's
    # landmark 1 are not the same point, so both the scale and the tip height are calibrated on the base head.
    # The profile is placed at the glabella (landmark 9), then depth deltas are re-centred on their median below.
    bz_prof, by_prof = _profile(base["side"], lambda u, v: _ortho_to_game("side", u, v))
    rf, bf = ref["side"]["features"], base["side"]["features"]
    _, b_tip_z = _ortho_to_game("side", bf["nose_tip"][1], bf["nose_tip"][0])
    _, b_crown_side_z = _ortho_to_game("side", 0, bf["crown"][0])
    _, b_crown_front_z = _ortho_to_game("front", 0, base["front"]["crown_row"])
    calib = (b_crown_side_z - b_tip_z) / (b_crown_front_z - base2d[1, 1])
    r_crown_z = to_game(np.array([rpts[9, 0]]), np.array([float(ref["front"]["crown_row"])]))[0, 1]
    if rf["nose_tip"][0] - rf["crown"][0] < 0.1 * ref["side"]["size"][1]:
        raise ValueError(f"side profile: crown row {rf['crown'][0]} -> nose tip row {rf['nose_tip'][0]} is too short "
                         "(check the _debug_side overlay)")
    k = (r_crown_z - ref2d[1, 1]) * calib / (rf["nose_tip"][0] - rf["crown"][0])
    tip_z = ref2d[1, 1] + (b_tip_z - base2d[1, 1])
    row_of = lambda z: rf["nose_tip"][0] - (z - tip_z) / k
    redge = np.array(ref["side"]["edge"], dtype=np.float64)
    g_row = row_of(ref2d[9, 1])
    g_col = float(np.interp(g_row, redge[:, 0], redge[:, 1]))
    g_y = float(np.interp(base2d[9, 1], bz_prof, by_prof))
    ref_side = lambda col, row: (g_y + (np.asarray(col) - g_col) * k, tip_z - (np.asarray(row) - rf["nose_tip"][0]) * k)
    rz_prof, ry_prof = _profile(ref["side"], ref_side)
    # Depth delta per midline landmark, re-centred on its median: generated profiles are not exact (slight pitch, a
    # brow level with the nose tip), so no single anchor point is trusted, only the shape. Landmarks under the chin are
    # skipped (the profile there is the throat line), and each correction is capped.
    depth_ids = [i for i in MP_MIDLINE if i not in MP_UNDER_CHIN]
    dys = {i: float(np.interp(ref2d[i, 1], rz_prof, ry_prof) - np.interp(base2d[i, 1], bz_prof, by_prof)) for i in depth_ids}
    dy0 = float(np.median(list(dys.values())))
    cap = cfg["max_depth_mm"] / 1000.0
    for i in MP_CONTROLS:
        hit = bvh.ray_cast(Vector((base2d[i, 0], -1.0, base2d[i, 1])), Vector((0, 1, 0)))
        if hit[0] is None:
            continue
        a = np.array(hit[0])
        d = np.array([ref2d[i, 0] - base2d[i, 0], 0.0, ref2d[i, 1] - base2d[i, 1]]) * cfg["shape_strength"]
        if i in dys:
            d[1] = float(np.clip(dys[i] - dy0, -cap, cap)) * cfg["depth_strength"]
        anchors.append(a); disp.append(d)
    if len(anchors) < 40:
        raise RuntimeError(f"portrait: only {len(anchors)} landmarks hit the base head (wrong base landmarks?)")
    # Zero-displacement pins: back of the skull and the crown keep the head's mass where the face doesn't reach.
    pin_idx = np.where(((base_pos[:, 1] > 0.04) | (base_pos[:, 2] > 1.84)) & (freeze > 0))[0][::25]
    ctrl = np.vstack([np.array(anchors), base_pos[pin_idx]])
    vals = np.vstack([np.array(disp), np.zeros((len(pin_idx), 3))])
    m = len(ctrl)
    phi = np.linalg.norm(ctrl[:, None] - ctrl[None], axis=-1)
    poly = np.hstack([np.ones((m, 1)), ctrl])
    A = np.zeros((m + 4, m + 4))
    A[:m, :m] = phi + 1e-5 * np.eye(m)
    A[:m, m:] = poly
    A[m:, :m] = poly.T
    rhs = np.vstack([vals, np.zeros((4, 3))])
    coef = np.linalg.solve(A, rhs)
    max_m = cfg["max_mm"] / 1000.0
    stats = {"clamped": 0}

    def warp(points, fw):
        out = np.empty_like(points)
        step = 10000  # (step, controls, 3) float64 temporaries: ~40 MB
        for a0 in range(0, len(points), step):
            p = points[a0:a0 + step]
            d = np.linalg.norm(p[:, None] - ctrl[None], axis=-1) @ coef[:m] + np.hstack([np.ones((len(p), 1)), p]) @ coef[m:]
            n = np.linalg.norm(d, axis=1)
            over = n > max_m
            stats["clamped"] += int((over & (fw[a0:a0 + step] > 0)).sum())
            d[over] *= (max_m / n[over])[:, None]
            out[a0:a0 + step] = p + d * fw[a0:a0 + step, None]
        return out

    align = {"to_game": to_game, "s": s, "rot": rot, "t": t, "ref_px": rpts[:, :2],
             "side": {"k": k, "col0": g_col, "row0": rf["nose_tip"][0], "y0": g_y - dy0, "z0": tip_z}}
    return warp, {"anchors": len(anchors), "pins": int(len(pin_idx)), "scale_m_per_px": float(s),
                  "side_scale_m_per_px": float(k), "depth_offset_mm": dy0 * 1000, "stats": stats}, align


def _freeze_at(points, base_pos):
    """FREEZE weight at arbitrary points (texels), from the head's seam vertices."""
    frozen = np.where(base_pos[:, 2] <= FREEZE["seam_m"] + SEAM_TOL_M)[0]
    tree = KDTree(len(frozen))
    for i, idx in enumerate(frozen):
        tree.insert(Vector(base_pos[idx]), i)
    tree.balance()
    w = np.empty(len(points))
    for i, p in enumerate(points):
        d = tree.find(Vector(p))[2] - FREEZE["distance_m"]
        t = min(max(d / FREEZE["ramp_m"], 0.0), 1.0)
        w[i] = 0.0 if p[2] <= FREEZE["seam_m"] + SEAM_TOL_M else t * t * (3 - 2 * t)
    return w


def _delit(img, mask, cfg, px_per_m):
    """Linear image with broad lighting gradients divided out (relative to the mean luminance under `mask`)."""
    lin = _srgb_to_linear(img)
    lum = (lin * np.array([0.299, 0.587, 0.114])).sum(-1)
    if not mask.any():
        raise ValueError("portrait: empty skin / silhouette mask (plain light background expected)")
    if cfg["delight"] > 0:
        ref = max(float(lum[mask].mean()), 1e-4)
        low = _box_blur2d(lum, cfg["delight_radius_m"] * px_per_m)
        lin = lin / (np.clip(low / ref, 0.5, 2.0) ** cfg["delight"])[..., None]
    return lin


def _silhouette(img, erode_px):
    """Soft foreground weight of a plain-background portrait, eroded so edge samples never pick up background."""
    border = np.concatenate([img[0], img[-1], img[:, 0], img[:, -1]])
    fg = (np.abs(img - np.median(border, axis=0)).max(-1) > 28 / 255).astype(np.float64)
    return _smooth(_box_blur2d(fg, erode_px), 0.92, 1.0)


def _project_portrait(cfg, align, warp, deforms, p, n, c, c0, s_weight, base_pos):
    """Project the portrait onto head texels: the front image where the face points forward, the side profile on the
    cheeks and jaw (the man's left, and his right too unless side_mirror is off).
    p / n: rest positions / normals, c: painted colours, c0: base colours, s_weight: skin weight so far.
    Returns (colours, skin weight, report)."""
    img = cfg["image"]
    rpx = align["ref_px"]
    near = (p[:, 1] < 0.01) & (p[:, 2] > 1.57) & (p[:, 2] < 1.82) & (np.abs(p[:, 0]) < 0.11)
    idx = np.where(near)[0]
    q = p[idx]
    fw = _freeze_at(q, base_pos)
    q = _deform(warp(q, fw), fw, deforms)
    lw = np.array([0.299, 0.587, 0.114])
    # ---- front: game (x, z) -> portrait pixel (inverse similarity; v is down).
    inv = align["rot"].T @ ((np.stack([q[:, 0], q[:, 2]], 1) - align["t"]).T) / align["s"]
    u, v = inv[0], -inv[1]
    uv = np.stack([u, v], 1)
    px_per_m = 1.0 / align["s"]
    # MediaPipe's oval stops mid-forehead: raise its upper points (above the glabella) toward the hairline so the
    # portrait's own forehead skin covers the band up to the painted scalp.
    oval = rpx[MP_OVAL].copy()
    lift = _smooth(rpx[9, 1] - oval[:, 1], 0, 0.04 * px_per_m)
    oval[:, 1] -= lift * cfg["forehead_extend_m"] * px_per_m
    region = _poly_inside_dist(uv, oval)
    # Tight edge at the jaw (the base beard must not show through), wide at the forehead: the base forehead between
    # the oval and the painted hairline differs in colour from the projected skin, so a short blend shows a band.
    above = _smooth(rpx[9, 1] - v, 0, 0.02 * px_per_m)  # 0 below the glabella, 1 well above it
    erode = cfg["erode_m"] * px_per_m
    feather = (cfg["feather_m"] + (cfg["feather_top_m"] - cfg["feather_m"]) * above) * px_per_m
    w_region = np.clip((region - erode) / feather, 0, 1)
    w_region = w_region * w_region * (3 - 2 * w_region)
    wf = w_region * _smooth(-n[idx, 1], cfg["facing"][0], cfg["facing"][1])
    wf *= (u >= 0) & (v >= 0) & (u < img.shape[1]) & (v < img.shape[0])
    # Patches of clean skin (cheeks, forehead) for colour statistics.
    patch_r = cfg["skin_patch_m"] * px_per_m
    hh, ww = np.mgrid[0:img.shape[0], 0:img.shape[1]]
    patch = np.zeros(img.shape[:2], dtype=bool)
    for lm in (50, 280, 151):
        patch |= (hh - rpx[lm, 1]) ** 2 + (ww - rpx[lm, 0]) ** 2 < patch_r ** 2
    lin = _delit(img, patch, cfg, px_per_m)
    ref_mu = lin[patch].mean(0)
    front = _bilinear(lin, u, v)
    # Base statistics: the base texture's texels that land in the same patches.
    tex_patch = np.zeros(len(idx), dtype=bool)
    for lm in (50, 280, 151):
        tex_patch |= ((uv - rpx[lm]) ** 2).sum(1) < patch_r ** 2
    tex_patch &= wf > 0.9
    if tex_patch.sum() < 50:
        raise RuntimeError(f"portrait: only {int(tex_patch.sum())} base texels in the skin patches (alignment off?)")
    b_mu = _srgb_to_linear(c0[idx][tex_patch]).mean(0)
    # Luminance: the skin mean moves to the base skin's, with a contrast gamma around it (config `contrast`; auto
    # matching the spread overshoots: the delighted, resampled portrait patches are smoother than the atlas).
    # Chroma: multiplicative white balance toward the base skin (a per-channel std gain tints dark hair teal).
    gamma = float(cfg["contrast"])
    lr, lb = (ref_mu * lw).sum(), (b_mu * lw).sum()
    wb = (b_mu / lb) / (ref_mu / lr)
    chroma_mu = ref_mu / max(ref_mu.sum(), 1e-4)

    def skin_map(sample):
        """(skin weight, colour moved to the base skin). Skin = near the portrait's skin colour and not much darker
        (hair / beard); freckles, bags and bruises stay mostly skin."""
        s_l = np.maximum((sample * lw).sum(1), 1e-5)
        scaled = np.clip(sample / s_l[:, None] * wb * (lb * (s_l / lr) ** gamma)[:, None], 0, 1)
        chroma = sample / np.maximum(sample.sum(1, keepdims=True), 1e-4)
        dist = 2 * np.linalg.norm(chroma - chroma_mu, axis=1) + np.maximum(0, 1 - s_l / max(lr, 1e-4))
        return 1 - _smooth(dist, 0.35, 0.7), scaled

    # Only skin moves to the base skin (the game tones it); hair, beard and eyes are never toned, so they keep the
    # portrait's own colour (scaling them with the skin blows out eye whites on a dark-skinned portrait).
    kf, scaled = skin_map(front)
    for eye in (MP_EYE_R, MP_EYE_L):
        kf *= 1 - _smooth(_poly_inside_dist(uv, rpx[eye]), -0.0015 * px_per_m, 0.0005 * px_per_m)
    kf = np.minimum(kf, 1 - 0.3 * _smooth(_poly_inside_dist(uv, rpx[MP_LIPS]), 0, 0.002 * px_per_m))
    mapped_f = scaled * kf[:, None] + front * (1 - kf[:, None])
    # ---- side: game (y, z) -> profile pixel (crown -> tip scale, glabella / median depth from _portrait_fit). The
    # projection ignores x, so the man's left profile also lands on his right unless side_mirror is off.
    sd = align["side"]
    simg = cfg["side_image"]
    col = sd["col0"] + (q[:, 1] - sd["y0"]) / sd["k"]
    row = sd["row0"] + (sd["z0"] - q[:, 2]) / sd["k"]
    spx_per_m = 1.0 / sd["k"]
    sil = _silhouette(simg, cfg["erode_m"] * spx_per_m * 2)
    sr = cfg["side_region"]
    w_sr = (_smooth(q[:, 2], sr["z_min_m"], sr["z_min_m"] + 0.01)
            * (1 - _smooth(q[:, 2], sr["z_max_m"] - 0.015, sr["z_max_m"]))
            * (1 - _smooth(q[:, 1], sr["y_max_m"] - 0.015, sr["y_max_m"])))
    inside = (col >= 0) & (row >= 0) & (col < simg.shape[1] - 1) & (row < simg.shape[0] - 1)
    ws_raw = w_sr * _smooth(np.abs(n[idx, 0]), cfg["side_facing"][0], cfg["side_facing"][1]) * inside
    ws_raw = ws_raw * _bilinear(sil[..., None], col, row)[:, 0]
    if not cfg["side_mirror"]:
        ws_raw = ws_raw * (p[idx, 0] > 0)
    slin = _delit(simg, sil > 0.5, cfg, spx_per_m)
    side = _bilinear(slin, col, row)
    # Exposure: the side image is a separate generation. Match skin to skin: the lighter half of what each image
    # projects (drops beard, brows and eyes). Matching where both overlap uses the front's shaded, grazing edge and
    # leaves the side too dark.
    def skin_mean(samples):
        l = (samples * lw).sum(1)
        return samples[l >= np.median(l)].mean(0)
    f_sel, s_sel = wf > 0.9, ws_raw > 0.7
    band = int(s_sel.sum())
    expo = np.ones(3)
    if f_sel.sum() >= 50 and band >= 50:
        expo = np.clip(skin_mean(front[f_sel]) / np.maximum(skin_mean(side[s_sel]), 1e-4), 0.33, 3.0)
    side = np.clip(side * expo, 0, 1)
    ks, scaled_s = skin_map(side)
    mapped_s = scaled_s * ks[:, None] + side * (1 - ks[:, None])
    ws = ws_raw * (1 - wf) * cfg["side_strength"]
    # ---- blend: front, then the side on what the front leaves, then the painted colour.
    c = c.copy(); s_weight = s_weight.copy()
    rest = 1 - wf - ws
    c[idx] = c[idx] * rest[:, None] + _linear_to_srgb(mapped_f) * wf[:, None] + _linear_to_srgb(mapped_s) * ws[:, None]
    s_weight[idx] = s_weight[idx] * rest + kf * wf + ks * ws
    return c, s_weight, {"projected_texels": int((wf > 0.5).sum()), "side_texels": int((ws > 0.5).sum()),
                         "side_exposure_texels": band, "side_exposure": expo.round(3).tolist(),
                         "white_balance_rgb_gamma": np.append(wb, gamma).round(3).tolist()}


def _portrait_sheet(name, out_dir, renders, cfg, suffix="portrait"):
    """Mockup renders beside the portrait (front | portrait front | side | portrait side) in one PNG.
    Meshy heads pass their flat source renders as the "portrait" (suffix "meshy")."""
    ref = cfg["ref"]
    imgs = []
    for path in (renders[0], PROJECT / ref["front"]["image"], renders[2], PROJECT / ref["side"]["image"]):
        a = _load_rgb(path)
        hgt = 520
        ys = (np.arange(hgt) * a.shape[0] / hgt).astype(int)
        wd = int(a.shape[1] * hgt / a.shape[0])
        xs = (np.arange(wd) * a.shape[1] / wd).astype(int)
        imgs.append(a[ys][:, xs])
    sheet = np.concatenate(imgs, axis=1)
    h, w = sheet.shape[:2]
    out = bpy.data.images.new("_HV_sheet", w, h, alpha=False)
    try:
        rgba = np.ones((h, w, 4), dtype=np.float32)
        rgba[..., :3] = sheet[::-1]
        out.pixels.foreach_set(rgba.ravel())
        out.filepath_raw = str(Path(out_dir) / f"{name}_{suffix}.png")
        out.file_format = 'PNG'
        out.save()
    finally:
        bpy.data.images.remove(out)
    return str(Path(out_dir) / f"{name}_{suffix}.png")


# ---------------------------------------------------------------------------------------------- meshy
# spec-npc-meshy-heads: a Meshy multi-image-to-3D head (generated from the NPC's refs/ portraits) is a SHAPE + TEXTURE
# source only. The canonical head is shrinkwrapped onto it (neck frozen) and its colours are baked into the canonical
# UVs, so the bake / skin mask / tone / verify contracts stay those of every other head. Steps:
#   1. meshy_trim_source(glb, out_dir, keep_above)  -> <stem>.obj + <stem>_BaseColor.png (Tools/Blender/sources/...)
#   2. render_meshy_refs(config)                     -> refs/<stem>_front.png / _side.png / _frame.json
#   3. uv run Tools/FaceRef/face_landmarks.py <front> --front-only --out refs/<stem>_landmarks.json (the side render
#      is for the sheet only: the profile detector is tuned for portraits, and the fit aligns on the front alone)
#   4. run(config)                                   -> mockups / export, as for a portrait face
# Config block: "meshy": {"source": ".obj", "texture": ".png", "landmarks": ".json", ...MESHY_DEFAULTS overrides}.

MESHY_DEFAULTS = {"max_mm": 25.0, "ray_mm": 30.0, "smooth_iters": 6, "max_texel_gap_mm": 12.0,
                  "seam_fade_m": [1.585, 1.61], "exclude": [], "contrast": 1.0, "head_ratio_range": [0.7, 1.4],
                  "eye_front_scale": 1.4, "skin_patches": None, "depth_band_m": [1.66, 1.78]}
# exclude: soft ellipsoids ({"center", "radius", "inner"?, "mirror_x"?}) that keep the base SHAPE (not shrinkwrapped).
# Ears: a Meshy ear rarely sits where the base ear does; exclude the base ears and move them with a config `deforms`
# offset onto the Meshy ears, or the ear's texture is printed on the skull. eye_front_scale: inside the base eyes
# (skin_masks.json eye ellipsoids x this) colours come from a straight front (+Y) projection, not the nearest point: the
# base eyeball sits behind the Meshy lids, and nearest-point stretches the iris into a dark blot. 0 = nearest everywhere.
# head_ratio_range: aligned Meshy (crown - eye line) / base (crown - eye line); outside = wrong landmarks or source.
# skin_patches: null = MESHY_SKIN_PATCHES; override when hair / beard / a mark covers the forehead or cheeks.
# depth_band_m: z band of the face (base head) whose median front gap sets the depth: above the mouth, so a sculpted
# beard does not push the source back.
MESHY_REF_PX = 1024
BASE_CROWN_Z, BASE_EYE_Z = 1.86, 1.7197  # canonical head landmarks (game metres)
# Clean skin patches for the colour statistics, on the REST base head (game metres): forehead centre and both cheeks.
MESHY_SKIN_PATCHES = [{"center": [0.0, -0.105, 1.752], "radius": [0.022, 0.03, 0.009]},
                      {"center": [0.045, -0.085, 1.655], "radius": [0.012, 0.03, 0.012], "mirror_x": True}]


_TRACKED_IDS = ("objects", "meshes", "materials", "images", "cameras", "worlds", "collections", "node_groups",
                "textures", "lights", "armatures", "actions")


def _id_snapshot():
    return {k: set(getattr(bpy.data, k)) for k in _TRACKED_IDS}


def _remove_new_ids(before):
    """Remove only the datablocks created since `before` (never a global orphan purge: the open file is the user's)."""
    for k in _TRACKED_IDS:
        coll = getattr(bpy.data, k)
        for i in [x for x in coll if x not in before[k]]:
            try:
                coll.remove(i)
            except (ReferenceError, RuntimeError):
                pass


def _base_color_image(obj, name):
    """The image linked (directly or through a few nodes) to a Principled BSDF Base Color: never a normal / ORM map."""
    for m in obj.data.materials:
        if m is None or m.node_tree is None:
            continue
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf is None or not bsdf.inputs["Base Color"].is_linked:
            continue
        node, steps = bsdf.inputs["Base Color"].links[0].from_node, 0
        while node is not None and node.type != 'TEX_IMAGE' and steps < 4:
            linked = [i for i in node.inputs if i.is_linked]
            node, steps = (linked[0].links[0].from_node if linked else None), steps + 1
        if node is not None and node.type == 'TEX_IMAGE' and node.image is not None:
            return node.image
    raise RuntimeError(f"{name}: no image texture linked to a Principled BSDF Base Color")


def _file_key(path):
    """Content hash: the frame records the source and its renders; a changed one refuses the refs downstream."""
    import hashlib
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def meshy_trim_source(glb_path, out_dir, keep_above, max_faces=200000, tex_px=2048, stem=None):
    """One-off: import a Meshy GLB (Blender Z-up, face toward -Y), delete everything below z = keep_above (source
    units: the shoulders), decimate to max_faces and write <stem>.obj (UVs, no materials) + <stem>_BaseColor.png
    (resized to tex_px) into out_dir. The raw GLB (~60 MB) is not kept in the repo."""
    import bmesh
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    stem = stem or Path(glb_path).stem
    if bpy.context.window is None:
        raise RuntimeError("meshy_trim_source needs interactive Blender (MCP add-on), not background mode")
    before = _id_snapshot()
    prev = bpy.context.window.scene
    scene = bpy.data.scenes.new("_MeshyTrim")
    bpy.context.window.scene = scene
    try:
        bpy.ops.import_scene.gltf(filepath=str(glb_path))
        meshes = [o for o in scene.objects if o.type == 'MESH']
        if len(meshes) != 1:
            raise RuntimeError(f"{Path(glb_path).name}: expected one mesh, got {len(meshes)}")
        obj = meshes[0]
        obj.data.transform(obj.matrix_world)
        obj.matrix_world.identity()
        img = _base_color_image(obj, Path(glb_path).name)
        if img.is_float:
            raise ValueError(f"{img.name}: float base colour (pixels read linear): convert it to an 8-bit image first")
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < keep_above], context='VERTS')
        bm.to_mesh(obj.data)
        bm.free()
        faces = len(obj.data.polygons)
        if faces == 0:
            raise ValueError(f"keep_above {keep_above} removed every face of {Path(glb_path).name}")
        if faces > max_faces:
            mod = obj.modifiers.new("_dec", 'DECIMATE')
            mod.ratio = max_faces / faces
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.data.materials.clear()
        obj_path = out_dir / f"{stem}.obj"
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.ops.wm.obj_export(filepath=str(obj_path), export_selected_objects=True, export_uv=True,
                              export_normals=False, export_materials=False, apply_modifiers=True)
        tex_path = out_dir / f"{stem}_BaseColor.png"
        img.scale(tex_px, tex_px)  # packed GLB image: copy into a fresh image to write it as a file
        px = np.empty(tex_px * tex_px * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        out = bpy.data.images.new("_MeshyTrim_tex", tex_px, tex_px, alpha=False)
        try:
            out.pixels.foreach_set(px)
            out.filepath_raw = str(tex_path)
            out.file_format = 'PNG'
            out.save()
        finally:
            bpy.data.images.remove(out)
        return {"obj": str(obj_path), "texture": str(tex_path), "faces": len(obj.data.polygons),
                "faces_before_decimate": faces}
    finally:
        bpy.context.window.scene = prev
        bpy.data.scenes.remove(scene)
        _remove_new_ids(before)


def _meshy_import(source):
    """Import the trimmed OBJ into the current scene. Returns (object, verts (N,3) source units)."""
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=str(source))
    new = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
    if len(new) != 1:
        raise RuntimeError(f"{Path(source).name}: expected one mesh, got {len(new)}")
    obj = new[0]
    if obj.data.uv_layers.active is None or len(obj.data.polygons) == 0:
        me = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(me)
        raise ValueError(f"{Path(source).name}: no faces or no UVs (re-run meshy_trim_source)")
    obj.data.transform(obj.matrix_world)
    obj.matrix_world.identity()
    co = np.empty(len(obj.data.vertices) * 3)
    obj.data.vertices.foreach_get("co", co)
    return obj, co.reshape(-1, 3)


def _meshy_paths(mc):
    src = PROJECT / mc["source"]
    stem = src.stem
    return {"source": src, "texture": PROJECT / mc["texture"], "landmarks": PROJECT / mc["landmarks"],
            "frame": REFS / f"{stem}_frame.json", "front": REFS / f"{stem}_front.png", "side": REFS / f"{stem}_side.png"}


def render_meshy_refs(config_path):
    """Flat (emission) ortho front / side renders of the trimmed Meshy source on plain light grey, framed on its own
    bounding box (any scale), + <stem>_frame.json (pixel -> source mapping, source hash). Then run face_landmarks.py."""
    config = json.loads(Path(config_path).read_text(encoding="utf-8-sig"))
    mc = config.get("meshy")
    if not isinstance(mc, dict) or not all(k in mc for k in ("source", "texture", "landmarks")):
        raise ValueError("meshy: expected {\"source\": .obj, \"texture\": .png, \"landmarks\": .json}")
    paths = _meshy_paths(mc)
    for k in ("source", "texture"):
        if not paths[k].is_file():
            raise FileNotFoundError(f"meshy {k}: {paths[k]} not found (run meshy_trim_source)")
    if bpy.context.window is None:
        raise RuntimeError("render_meshy_refs needs interactive Blender (MCP add-on), not background mode")
    # New renders invalidate any landmarks measured on the old ones: delete them so run() asks for face_landmarks.py.
    lm = paths["landmarks"]
    for old in (lm, lm.with_name(lm.stem + "_debug_front.png"), lm.with_name(lm.stem + "_debug_side.png")):
        if old.is_file():
            old.unlink()
    before = _id_snapshot()
    prev = bpy.context.window.scene
    scene = bpy.data.scenes.new("_MeshyRefs")
    bpy.context.window.scene = scene
    try:
        obj, co = _meshy_import(paths["source"])
        img = bpy.data.images.load(str(paths["texture"]), check_existing=False)
        mat = bpy.data.materials.new("_MR_flat")
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        emit = nt.nodes.new("ShaderNodeEmission")
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
        nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
        nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
        obj.data.materials.clear(); obj.data.materials.append(mat)
        world = bpy.data.worlds.new("_MR_World")
        scene.world = world
        world.use_nodes = True  # Blender 5: world.color alone is ignored, set the Background node
        bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
        bg.inputs["Color"].default_value = (0.6, 0.6, 0.6, 1.0)
        bg.inputs["Strength"].default_value = 1.0
        cd = bpy.data.cameras.new("_MR_Ortho"); cd.type = 'ORTHO'
        cam = bpy.data.objects.new("_MR_Ortho", cd); scene.collection.objects.link(cam)
        scene.camera = cam
        scene.render.engine = 'BLENDER_EEVEE'
        scene.render.film_transparent = False
        scene.view_settings.view_transform = 'Standard'
        scene.render.resolution_x = scene.render.resolution_y = MESHY_REF_PX
        lo, hi = co.min(0), co.max(0)
        span = float((hi - lo).max()) * 1.15
        frame = {"source_sha1": _file_key(paths["source"]),
                 "front": {"center": [float(lo[0] + hi[0]) / 2, float(lo[2] + hi[2]) / 2], "scale": span, "px": MESHY_REF_PX},
                 "side": {"center": [float(lo[1] + hi[1]) / 2, float(lo[2] + hi[2]) / 2], "scale": span, "px": MESHY_REF_PX}}
        for view in ("front", "side"):
            f = frame[view]
            cd.ortho_scale = f["scale"]
            if view == "front":  # looking +Y: image right = +X, up = +Z
                cam.location = (f["center"][0], lo[1] - 2.0, f["center"][1])
                cam.rotation_euler = (math.radians(90), 0, 0)
            else:  # looking -X: image right = +Y, so the face points left
                cam.location = (hi[0] + 2.0, f["center"][0], f["center"][1])
                cam.rotation_euler = (math.radians(90), 0, math.radians(90))
            cd.clip_end = 10.0
            scene.render.filepath = str(paths[view])
            bpy.ops.render.render(write_still=True)
            frame[view]["sha1"] = _file_key(paths[view])
        paths["frame"].write_text(json.dumps(frame, indent=2), encoding="utf-8")
        return {"front": str(paths["front"]), "side": str(paths["side"]), "frame": str(paths["frame"]),
                "next": f"uv run Tools/FaceRef/face_landmarks.py {paths['front'].relative_to(PROJECT).as_posix()} "
                        f"--front-only --out {Path(mc['landmarks']).as_posix()}"}
    finally:
        bpy.context.window.scene = prev
        bpy.data.scenes.remove(scene)
        _remove_new_ids(before)


def _frame_px(f, u, v):
    """Pixel (u right, v down) of a Meshy ref render -> (horizontal, z) source units."""
    s, n = f["scale"], f["px"]
    return f["center"][0] + ((np.asarray(u) + 0.5) / n - 0.5) * s, f["center"][1] + (0.5 - (np.asarray(v) + 0.5) / n) * s


def _meshy_inputs(config):
    """Validate every Meshy input BEFORE the scene is touched (files, landmarks vs renders vs source)."""
    mc = config.get("meshy")
    if mc is None:
        return None
    if not isinstance(mc, dict) or not all(k in mc for k in ("source", "texture", "landmarks")):
        raise ValueError("meshy: expected {\"source\": .obj, \"texture\": .png, \"landmarks\": .json, ...}")
    cfg = dict(MESHY_DEFAULTS, **mc)
    paths = _meshy_paths(mc)
    fixes = {"source": "run meshy_trim_source", "texture": "run meshy_trim_source", "frame": "run render_meshy_refs",
             "front": "run render_meshy_refs", "side": "run render_meshy_refs",
             "landmarks": "run Tools/FaceRef/face_landmarks.py on the render_meshy_refs images"}
    for k, fix in fixes.items():
        if not paths[k].is_file():
            raise FileNotFoundError(f"meshy {k}: {paths[k]} not found ({fix})")
    frame = json.loads(paths["frame"].read_text(encoding="utf-8-sig"))
    ref = json.loads(paths["landmarks"].read_text(encoding="utf-8-sig"))
    base = json.loads(BASE_LANDMARKS.read_text(encoding="utf-8-sig"))
    try:
        ok = (len(ref["front"]["points"]) == 478 and len(base["front"]["points"]) == 478
              and len(ref["front"]["size"]) == 2 and ref["front"]["image"]
              and all(frame[v]["px"] > 0 and frame[v]["sha1"] and len(frame[v]["center"]) == 2 and frame[v]["scale"] > 0
                      for v in ("front", "side")))
    except (KeyError, TypeError):
        ok = False
    if not ok:
        raise ValueError("meshy: frame or landmarks incomplete (re-run render_meshy_refs, then face_landmarks.py)")
    if frame.get("source_sha1") != _file_key(paths["source"]):
        raise ValueError(f"meshy: {paths['source'].name} changed since render_meshy_refs (re-run it, then face_landmarks.py)")
    for view in ("front", "side"):
        if frame[view]["sha1"] != _file_key(paths[view]):
            raise ValueError(f"meshy: {paths[view].name} is not the render_meshy_refs output (re-run it, then face_landmarks.py)")
    if ref["front"]["size"] != [frame["front"]["px"]] * 2:  # only the front landmarks are used (side: sheet only)
        raise ValueError(f"meshy landmarks: front size {ref['front']['size']} was not measured on the current "
                         f"render_meshy_refs images ({frame['front']['px']} px): re-run face_landmarks.py")
    if Path(ref["front"]["image"]).name != paths["front"].name:
        raise ValueError(f"meshy landmarks: front image {ref['front']['image']} is not {paths['front'].name}")
    if base["front"]["size"] != [ORTHO["front"]["px"]] * 2:
        raise ValueError("base landmarks: front size does not match ORTHO (re-run render_base_refs)")
    num = lambda v: isinstance(v, (int, float)) and not isinstance(v, bool)
    ramp = lambda v: isinstance(v, (list, tuple)) and len(v) == 2 and all(num(x) for x in v) and v[0] < v[1]
    bad = [k for k in ("max_mm", "ray_mm", "max_texel_gap_mm", "contrast") if not (num(cfg[k]) and cfg[k] > 0)]
    bad += [k for k in ("eye_front_scale",) if not (num(cfg[k]) and cfg[k] >= 0)]
    bad += [k for k in ("seam_fade_m", "head_ratio_range", "depth_band_m") if not ramp(cfg[k])]
    if not (isinstance(cfg["smooth_iters"], int) and cfg["smooth_iters"] >= 0):
        bad.append("smooth_iters")
    if bad:
        raise ValueError(f"meshy: invalid {bad} (positive numbers, ramps [low, high], smooth_iters int >= 0)")
    if cfg["skin_patches"] is None:
        cfg["skin_patches"] = MESHY_SKIN_PATCHES
    for key in ("exclude", "skin_patches"):
        if not isinstance(cfg[key], list) or (key == "skin_patches" and not cfg[key]):
            raise ValueError(f"meshy {key}: expected a list of ellipsoids")
        for e in cfg[key]:
            if not (isinstance(e, dict) and isinstance(e.get("center"), list) and len(e["center"]) == 3
                    and all(num(x) for x in e["center"]) and 0 <= e.get("inner", 0.5) < 1):
                raise ValueError(f"meshy {key}: each entry needs center [x,y,z], radius [rx,ry,rz], inner in [0, 1)")
            _radius(e.get("radius"))
    cfg.update(paths=paths, frame=frame, ref=ref, base=base)
    # _portrait_sheet reads ref[view]["image"] relative to the project: point it at the Meshy renders.
    cfg["sheet_ref"] = {v: {"image": paths[v].relative_to(PROJECT).as_posix()} for v in ("front", "side")}
    return cfg


def _ellipsoids_weight(p, ellipsoids):
    """max over soft ellipsoids (inner fraction full weight), mirrored on X when asked."""
    w = np.zeros(len(p))
    for e in ellipsoids:
        inner = e.get("inner", 0.5)
        for sx in ((1, -1) if e.get("mirror_x") else (1,)):
            d = np.sqrt(((((p * np.array([sx, 1, 1])) - np.array(e["center"])) / _radius(e["radius"])) ** 2).sum(1))
            w = np.maximum(w, 1 - _smooth(d, inner, 1.0))
    return w


def _meshy_fit(cfg, head, base_pos, freeze, unit):
    """Align the Meshy source on the base head and shrinkwrap the head onto it.
    Returns (warp(points, freeze_w) -> displaced points, report, surface) where surface = (bvh, verts, tris, tri_uv)."""
    from mathutils.bvhtree import BVHTree
    obj, src = _meshy_import(cfg["paths"]["source"])
    me = obj.data
    try:
        me.calc_loop_triangles()
        tris = np.array([t.vertices[:] for t in me.loop_triangles], dtype=np.int64)
        loops = np.array([t.loops[:] for t in me.loop_triangles], dtype=np.int64)
        uv = np.empty(len(me.loops) * 2)
        me.uv_layers.active.data.foreach_get("uv", uv)
        tri_uv = uv.reshape(-1, 2)[loops]
    finally:
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(me)
    # ---- alignment: 2D similarity (eye corners + nasion, as portraits) in the front plane, scale applied to depth.
    ref, base = cfg["ref"], cfg["base"]
    rpts = np.array(ref["front"]["points"])
    bpts = np.array(base["front"]["points"])
    sx, sz = _frame_px(cfg["frame"]["front"], rpts[:, 0], rpts[:, 1])
    bx, bz = _ortho_to_game("front", bpts[:, 0], bpts[:, 1])
    s, rot, t = _similarity(np.stack([sx, sz], 1)[MP_ALIGN], np.stack([bx, bz], 1)[MP_ALIGN])
    xz = (s * (rot @ src[:, [0, 2]].T)).T + t
    g = np.stack([xz[:, 0], src[:, 1] * s, xz[:, 1]], 1)
    # Head size check (the source is in arbitrary units, so the raw scale says nothing): crown above the eye line.
    ratio = float((g[:, 2].max() - BASE_EYE_Z) / (BASE_CROWN_Z - BASE_EYE_Z))
    lo, hi = cfg["head_ratio_range"]
    if not lo <= ratio <= hi:
        raise RuntimeError(f"meshy: aligned head ratio {ratio:.2f} (crown above the eyes vs the base) outside "
                           f"{cfg['head_ratio_range']} (wrong landmarks or source?)")
    tri_list = tris.tolist()
    bvh = BVHTree.FromPolygons([Vector(p) for p in g], tri_list)
    # Depth: the median front-plane offset over the upper face (nose to forehead) puts the Meshy face on the base face.
    zlo, zhi = cfg["depth_band_m"]
    face = np.where((np.abs(base_pos[:, 0]) < 0.05) & (base_pos[:, 2] > zlo) & (base_pos[:, 2] < zhi)
                    & (base_pos[:, 1] < -0.05))[0]
    dys = []
    for i in face:
        hit = bvh.ray_cast(Vector((base_pos[i, 0], -1.0, base_pos[i, 2])), Vector((0, 1, 0)))
        if hit[0] is not None:
            dys.append(hit[0].y - base_pos[i, 1])
    if len(dys) < 40:
        raise RuntimeError(f"meshy: only {len(dys)} face vertices hit the aligned source (alignment off?)")
    dy = float(np.median(dys))
    g[:, 1] -= dy
    bvh = BVHTree.FromPolygons([Vector(p) for p in g], tri_list)
    # ---- shrinkwrap: each moving vertex goes to the closest surface hit along its normal (either way), else the
    # nearest point; excluded regions (config) keep the base shape; then the field is smoothed over the mesh.
    mw3 = head.matrix_world.to_3x3()
    nrm = np.array([list((mw3 @ v.normal).normalized()) for v in head.data.vertices])
    ray = cfg["ray_mm"] / 1000.0
    disp = np.zeros_like(base_pos)
    known = np.zeros(len(base_pos), dtype=bool)
    for i in np.where(freeze > 0)[0]:
        p, n = Vector(base_pos[i]), Vector(nrm[i])
        best = None
        for d in (n, -n):
            hit = bvh.ray_cast(p, d, ray)
            if hit[0] is not None and (best is None or hit[3] < best[1]):
                best = (hit[0], hit[3])
        if best is None:
            loc = bvh.find_nearest(p, ray)[0]
            if loc is None:
                continue
            best = (loc, 0.0)
        disp[i] = np.array(best[0]) - base_pos[i]
        known[i] = True
    region = 1 - _ellipsoids_weight(base_pos, cfg["exclude"]) if cfg["exclude"] else np.ones(len(base_pos))
    disp *= region[:, None]
    edges = np.array([e.vertices[:] for e in head.data.edges], dtype=np.int64)
    deg = np.bincount(edges.ravel(), minlength=len(base_pos)).astype(np.float64)
    for _ in range(cfg["smooth_iters"]):
        acc = np.zeros_like(disp)
        np.add.at(acc, edges[:, 0], disp[edges[:, 1]])
        np.add.at(acc, edges[:, 1], disp[edges[:, 0]])
        avg = acc / np.maximum(deg, 1)[:, None]
        disp = np.where((freeze > 0)[:, None], 0.5 * disp + 0.5 * avg, 0.0)
    disp *= region[:, None]  # smoothing must not leak shape back into an excluded region
    max_m = cfg["max_mm"] / 1000.0
    norm = np.linalg.norm(disp, axis=1)
    over = norm > max_m
    disp[over] *= (max_m / norm[over])[:, None]
    tree = KDTree(len(base_pos))
    for i, p in enumerate(base_pos):
        tree.insert(Vector(p), i)
    tree.balance()

    def warp(points, fw):
        """Vertex displacement field at arbitrary points (inverse-distance over the 4 nearest vertices) x freeze."""
        out = np.empty_like(points)
        for k, p in enumerate(points):
            near = tree.find_n(Vector(p), 4)
            if near[0][2] < 1e-9:
                d = disp[near[0][1]]
            else:
                w = np.array([1.0 / dist for _, _, dist in near])
                d = (w[:, None] * disp[[i for _, i, _ in near]]).sum(0) / w.sum()
            out[k] = p + d * fw[k]
        return out

    report = {"scale": float(s), "head_ratio": ratio, "max_shape_mm": float(norm[freeze > 0].max() * 1000), "roll_deg": float(np.degrees(np.arctan2(rot[1, 0], rot[0, 0]))),
              "depth_offset_mm": dy * 1000, "missed_vertices": int(((freeze > 0) & ~known).sum()),
              "clamped_vertices": int((over & (freeze > 0)).sum()),
              "mean_shape_mm": float(norm[freeze > 0].mean() * 1000), "source_faces": len(tris)}
    return warp, report, (bvh, g, tris, tri_uv)


def _project_meshy(cfg, warp, surface, deforms, p, n, c, c0, s_weight, base_pos):
    """Bake the Meshy colours into the canonical UVs: each (warped) texel takes the colour at its nearest point on the
    aligned source. Skin moves to the base skin colour (white balance + luminance, as portraits); hair, beard and eyes
    keep their colour. Fades to the painted base just above the neck seam (z seam_fade_m)."""
    bvh, g, tris, tri_uv = surface
    tex = _load_rgb(cfg["paths"]["texture"]).astype(np.float32)
    lw = np.array([0.299, 0.587, 0.114])
    fw = _freeze_at(p, base_pos)
    q = _deform(warp(p, fw), fw, deforms)
    gap = cfg["max_texel_gap_mm"] / 1000.0
    fi = np.full(len(q), -1, dtype=np.int64)
    loc = np.zeros_like(q)
    for k, x in enumerate(q):
        hit = bvh.find_nearest(Vector(x), gap)
        if hit[0] is not None:
            loc[k] = hit[0]
            fi[k] = hit[2]
    eyes = json.loads(SKIN_MASKS_CONFIG.read_text(encoding="utf-8-sig"))["base"]["head"]["eyes"]
    if cfg["eye_front_scale"] > 0:
        eye_w = _ellipsoids_weight(p, [{"center": e["center"], "radius": list(np.array(e["radius"]) * cfg["eye_front_scale"]),
                                        "inner": 0.99, "mirror_x": True} for e in eyes])
        for k in np.where(eye_w > 0)[0]:
            hit = bvh.ray_cast(Vector((q[k, 0], q[k, 1] - 0.05, q[k, 2])), Vector((0, 1, 0)), 0.1)
            if hit[0] is not None:
                loc[k] = hit[0]
                fi[k] = hit[2]
    ok = fi >= 0
    a, b, cc = (g[tris[fi[ok], j]] for j in range(3))
    v0, v1, v2 = b - a, cc - a, loc[ok] - a
    d00, d01, d11 = (v0 * v0).sum(1), (v0 * v1).sum(1), (v1 * v1).sum(1)
    d20, d21 = (v2 * v0).sum(1), (v2 * v1).sum(1)
    den_raw = d00 * d11 - d01 * d01
    degen = np.abs(den_raw) < 1e-24  # zero-area triangle: take its first corner's UV
    den = np.where(degen, 1.0, den_raw)
    wb_ = np.where(degen, 0.0, np.clip((d11 * d20 - d01 * d21) / den, 0, 1))
    wc_ = np.where(degen, 0.0, np.clip((d00 * d21 - d01 * d20) / den, 0, 1))
    wa_ = np.clip(1 - wb_ - wc_, 0, 1)
    tot = wa_ + wb_ + wc_  # clipped weights renormalised: the UV stays inside the triangle
    wa_, wb_, wc_ = wa_ / tot, wb_ / tot, wc_ / tot
    tuv = tri_uv[fi[ok]]
    uv = wa_[:, None] * tuv[:, 0] + wb_[:, None] * tuv[:, 1] + wc_[:, None] * tuv[:, 2]
    h, w_ = tex.shape[:2]
    sample = np.zeros((len(q), 3))
    sample[ok] = _bilinear(tex, uv[:, 0] * w_, (1 - uv[:, 1]) * h)
    lin = _srgb_to_linear(sample)
    # Colour statistics: the same clean skin patches on the rest head, in the Meshy samples and the base texture.
    patch = (_ellipsoids_weight(p, [dict(e, inner=0.99) for e in cfg["skin_patches"]]) > 0.5) & ok
    if patch.sum() < 50:
        raise RuntimeError(f"meshy: only {int(patch.sum())} texels in the skin patches")
    ref_mu = np.maximum(lin[patch].mean(0), 1e-4)
    b_mu = np.maximum(_srgb_to_linear(c0[patch]).mean(0), 1e-4)
    gamma = float(cfg["contrast"])
    lr, lb = max((ref_mu * lw).sum(), 1e-4), max((b_mu * lw).sum(), 1e-4)
    wbal = (b_mu / lb) / (ref_mu / lr)
    chroma_mu = ref_mu / max(ref_mu.sum(), 1e-4)
    s_l = np.maximum((lin * lw).sum(1), 1e-5)
    scaled = np.clip(lin / s_l[:, None] * wbal * (lb * (s_l / lr) ** gamma)[:, None], 0, 1)
    chroma = lin / np.maximum(lin.sum(1, keepdims=True), 1e-4)
    dist = 2 * np.linalg.norm(chroma - chroma_mu, axis=1) + np.maximum(0, 1 - s_l / max(lr, 1e-4))
    # Much lighter than the skin AND neutral (grey) = sclera, grey / white hair: never skin (a neutral grey is close to
    # skin in chroma). Skin highlights keep the skin's warm hue and stay skin, or they show untoned on light tones.
    neutral = 1 - _smooth(np.linalg.norm(chroma - 1 / 3, axis=1), 0.03, 0.08)
    k_skin = (1 - _smooth(dist, 0.35, 0.7)) * (1 - _smooth(s_l / max(lr, 1e-4), 1.5, 2.0) * neutral)
    mapped = scaled * k_skin[:, None] + lin * (1 - k_skin[:, None])
    lo, hi = cfg["seam_fade_m"]
    wt = _smooth(p[:, 2], lo, hi) * ok
    c = c * (1 - wt[:, None]) + _linear_to_srgb(mapped) * wt[:, None]
    s_weight = s_weight * (1 - wt) + k_skin * wt
    return c, s_weight, {"baked_texels": int(ok.sum()), "missed_texels": int((~ok).sum()), "patch_texels": int(patch.sum()),
                         "white_balance_rgb_gamma": np.append(wbal, gamma).round(3).tolist()}


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
        # Left neck close-up (Company brand), from the character's left, slightly in front.
        cd.lens = 135
        neck = Vector((0.045, 0.0, 1.62))
        cam.location = (0.62, -0.12, 1.64)
        cam.rotation_euler = (neck - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 420, 420
        scene.render.filepath = str(Path(out_dir) / f"{name}_neck.png")
        bpy.ops.render.render(write_still=True)
        files.append(scene.render.filepath)
    finally:
        for obj in added:
            bpy.data.objects.remove(obj, do_unlink=True)
    return files


def _save_mask(mask, path):
    """Grey PNG of raw values. Colour space set before writing: changing it resets a generated image."""
    size = mask.shape[0]
    img = bpy.data.images.new("_HV_mask_out", size, size, alpha=False)
    try:
        img.colorspace_settings.name = 'Non-Color'
        rgba = np.ones((size, size, 4), dtype=np.float32)
        rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = np.clip(mask, 0, 1)
        img.pixels.foreach_set(rgba.ravel())
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save()
    finally:
        bpy.data.images.remove(img)


EXPORT_PARTS = ("fbx", "texture", "mask")


def _export(rig, head, name, pixels, size, mask, parts=EXPORT_PARTS):
    folder = HEADS_DIR / name
    folder.mkdir(parents=True, exist_ok=True)
    written = []
    if "mask" in parts:
        mask_path = folder / f"T_SkinMask_Head_{name}.png"
        _save_mask(mask, mask_path)
        written.append(str(mask_path))
    if "texture" in parts:
        tex_path = folder / f"T_Head_{name}_BaseColor.png"
        img = bpy.data.images.new("_HV_export_" + name, size, size, alpha=True)
        try:
            img.pixels.foreach_set(pixels.ravel())
            img.filepath_raw = str(tex_path)
            img.file_format = 'PNG'
            img.save()
        finally:
            bpy.data.images.remove(img)
        written.append(str(tex_path))
    if "fbx" in parts:
        bpy.ops.object.select_all(action='DESELECT')
        head.select_set(True)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        fbx_path = folder / f"Head_{name}.fbx"
        bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                                 object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                                 use_mesh_modifiers=False, mesh_smooth_type='OFF', path_mode='STRIP')
        written.append(str(fbx_path))
    return written


# ---------------------------------------------------------------------------------------------- entry

def run(config_path, export=False, render=True, preview_tone=None):
    """export: False, True (FBX + texture + mask) or a subset of EXPORT_PARTS, e.g. ["mask"] to add a mask to a
    shipped head without rewriting its FBX. preview_tone overrides the config's ("" renders untoned)."""
    if isinstance(export, str):
        export = [export]
    if not isinstance(export, bool):
        if not export or not set(export) <= set(EXPORT_PARTS):
            raise ValueError(f"export must be True / False or a non-empty subset of {EXPORT_PARTS}, got {export!r}")
    config = json.loads(Path(config_path).read_text(encoding="utf-8-sig"))
    if preview_tone is not None:
        config["preview_tone"] = preview_tone
    if config.get("preview_tone"):
        _tone_rgb(config["preview_tone"])  # fail before the paint pass, not after
    name = config["name"]
    if config.get("portrait") is not None and config.get("meshy") is not None:
        raise ValueError(f"{name}: a head has one face source, 'portrait' or 'meshy', not both")
    portrait = _portrait_inputs(config)  # every portrait / meshy input is checked before the scene is touched
    meshy = _meshy_inputs(config)
    scene, rig, head, body, unit = _load()
    base_pos = _positions(head, unit)
    base_normals = [Vector(c.vector) for c in head.data.corner_normals]
    base_uv = [Vector(l.uv) for l in head.data.uv_layers.active.data]
    freeze = _freeze_weights(base_pos, SEAM_TOL_M if (portrait is not None or meshy is not None) else 0.0)
    deforms = config.get("deforms", [])
    project, fit_report = None, None
    start = base_pos
    if portrait is not None:
        warp, fit_report, align = _portrait_fit(portrait, head, base_pos, freeze)
        start = warp(base_pos, freeze)  # landmark warp first, then the config's own deforms
        fit_report["clamped_vertices"] = fit_report.pop("stats")["clamped"]
        project = lambda p, n, c, c0, s: _project_portrait(portrait, align, warp, deforms, p, n, c, c0, s, base_pos)
    elif meshy is not None:
        warp, fit_report, surface = _meshy_fit(meshy, head, base_pos, freeze, unit)
        start = warp(base_pos, freeze)
        project = lambda p, n, c, c0, s: _project_meshy(meshy, warp, surface, deforms, p, n, c, c0, s, base_pos)
    # Paint / masks use the rest (undeformed) positions; the projection maps them through the same warp.
    pixels, size, mask, proj_report = _paint(config, scene, head, body, unit, project, force_eyes=meshy is None)
    pos = _deform(start, freeze, deforms)
    _set_positions(head, unit, pos)
    _fix_normals(head, freeze, base_normals)
    # Frozen interface check: positions, corner normals and UVs of every frozen vertex are unchanged.
    after = _positions(head, unit)
    frozen = freeze <= 0.0
    # The exposed ring itself (z within SEAM_TOL_M of the seam) must not move, whatever its weight: a moved ring fails
    # the bake's exact join ("No compatible opposite vertex").
    ring = base_pos[:, 2] <= FREEZE["seam_m"] + SEAM_TOL_M
    ring_shift = float(np.abs(after[ring] - base_pos[ring]).max())
    if ring_shift > 1e-6:
        raise RuntimeError(f"{name}: neck ring moved {ring_shift * 1000:.4f} mm (bake would fail the exact join)")
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
    if portrait is not None:
        report["portrait"] = dict(fit_report, **(proj_report or {}))
        if fit_report["clamped_vertices"]:
            report.setdefault("warnings", []).append(
                f"{fit_report['clamped_vertices']} vertices clamped to max_mm {portrait['max_mm']}")
        if report["max_deform_m"] * 1000 > portrait["max_mm"] + 0.01:  # config deforms on top of the warp
            report.setdefault("warnings", []).append(
                f"max deform {report['max_deform_m'] * 1000:.1f} mm exceeds max_mm {portrait['max_mm']} (config deforms)")
    if meshy is not None:
        report["meshy"] = dict(fit_report, **(proj_report or {}))
        if fit_report["clamped_vertices"]:
            report.setdefault("warnings", []).append(
                f"{fit_report['clamped_vertices']} vertices clamped to max_mm {meshy['max_mm']}")
        if fit_report["missed_vertices"]:
            report.setdefault("warnings", []).append(
                f"{fit_report['missed_vertices']} vertices found no Meshy surface within ray_mm {meshy['ray_mm']}")
        missed = report["meshy"].get("missed_texels", 0)
        if missed > 0.03 * (missed + report["meshy"].get("baked_texels", 0)):
            report.setdefault("warnings", []).append(
                f"{missed} head texels found no Meshy surface within max_texel_gap_mm {meshy['max_texel_gap_mm']} "
                "(they keep the painted base colour)")
        if report["max_deform_m"] * 1000 > meshy["max_mm"] + 0.01:  # config deforms (ears) on top of the shrinkwrap
            report.setdefault("warnings", []).append(
                f"max deform {report['max_deform_m'] * 1000:.1f} mm exceeds max_mm {meshy['max_mm']} (config deforms)")
    preview = _toned(pixels, mask, config["preview_tone"]) if config.get("preview_tone") else pixels
    img, mat = _preview_material(name, preview, size)
    saved = {o: list(o.data.materials) for o in (head, body)}
    for o in saved:
        o.data.materials.clear()
        o.data.materials.append(mat)  # body texels are unchanged in the painted copy
    try:
        if render:
            out_dir = PROJECT / config.get("render", {}).get("out_dir", "Temp/HeadVariants")
            out_dir.mkdir(parents=True, exist_ok=True)
            report["renders"] = _render(scene, rig, head, name, out_dir)
            if portrait is not None:
                report["renders"].append(_portrait_sheet(name, out_dir, report["renders"], portrait))
            if meshy is not None:
                report["renders"].append(_portrait_sheet(name, out_dir, report["renders"], {"ref": meshy["sheet_ref"]},
                                                         suffix="meshy"))
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
        report["exported"] = _export(rig, head, name, pixels, size, mask,
                                     EXPORT_PARTS if export is True else tuple(export))
    return report
