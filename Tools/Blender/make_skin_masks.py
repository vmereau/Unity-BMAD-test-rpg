"""Bake the skin masks used by SG_SkinLit (spec-npc-skin-tones). Run inside Blender (MCP).

    exec(open(r".../Tools/Blender/make_skin_masks.py").read())
    result = run(r".../Tools/Blender/configs/skin_masks.json")

A mask is an R8 PNG on the same UVs as an atlas: 1 = skin that takes the NPC's tone, 0 = untouched (hair, eyes,
cloth). Masks come from geometry, never from colour alone: the undyed linen trousers are about the colour of light skin.

  base   body + base head atlas (Meshy base hero). Body: skin below the hem band and above the waist band, trousers
         between; inside those two narrow bands saturation tells skin from linen (the waistline isn't level). Texels the
         head's inset neck stump shares with the body follow the body rule. Head-only texels: everything except the eyes
         and clearly dark scalp / brow hair (darkness relative to the forehead skin, inside the hair zones).
  armor  each entry: a fitted outfit FBX; skin = vertices weighted to the listed bones (hands / fingers).

Also reports the mean masked-skin luminance in linear space (`_SkinRefLuminance` on the material): the shader maps a
texel of that luminance exactly onto the tone colour. Coordinates: game metres, rest pose, +Z up, -Y = facing.
"""
import json
import runpy
from pathlib import Path

import bpy
import numpy as np

PROJECT = Path(r"D:/github/perso/Unity-BMAD-test-rpg")
HV = runpy.run_path(str(PROJECT / "Tools/Blender/make_head_variant.py"))  # _load, _coverage, _dilate, _smooth


def _srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _lum(c):
    return c[..., 0] * 0.299 + c[..., 1] * 0.587 + c[..., 2] * 0.114


def _bake_vertex_values(scene, obj, values, size):
    """Bake a per-vertex float3 (interpolated over faces) into a size x size map. Returns (map, coverage)."""
    attr = obj.data.attributes.new("_sm_val", 'FLOAT_VECTOR', 'POINT')
    for i, v in enumerate(values):
        attr.data[i].vector = v
    saved = list(obj.data.materials)
    mat = bpy.data.materials.new("_SM_Bake")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    at = nt.nodes.new("ShaderNodeAttribute")
    at.attribute_name = "_sm_val"
    tex = nt.nodes.new("ShaderNodeTexImage")
    nt.links.new(at.outputs["Vector"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    img = bpy.data.images.new("_SM_val", size, size, alpha=True, float_buffer=True)
    img.colorspace_settings.name = 'Non-Color'
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    try:
        img.pixels.foreach_set(np.zeros(size * size * 4, dtype=np.float32))
        tex.image = img
        nt.nodes.active = tex
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 1
        scene.render.bake.margin = 3
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.bake(type='EMIT')
        px = np.empty(size * size * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        vals = px.reshape(size, size, 4)[..., :3].copy()
    finally:
        obj.data.materials.clear()
        for m in saved:
            obj.data.materials.append(m)
        bpy.data.materials.remove(mat)
        bpy.data.images.remove(img)
        obj.data.attributes.remove(obj.data.attributes["_sm_val"])
    cov = HV["_dilate"](HV["_coverage"](scene, obj, size), 3)
    return vals, cov


def _rest_positions(obj, unit):
    mw = obj.matrix_world
    return np.array([list((mw @ v.co) / unit) for v in obj.data.vertices], dtype=np.float64)


def _load_texture(path):
    img = bpy.data.images.load(str(path), check_existing=False)
    try:
        size = img.size[0]
        px = np.empty(size * size * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return px.reshape(size, size, 4)[..., :3].astype(np.float64), size
    finally:
        bpy.data.images.remove(img)


def _ellipsoid_in(p, center, radius):
    d2 = (((p - np.array(center)) / np.array(radius)) ** 2).sum(axis=-1)
    return 1 - HV["_smooth"](d2, 0.6, 1.0)


def _saturation(c):
    return (c.max(axis=-1) - c.min(axis=-1)) / np.maximum(c.max(axis=-1), 1e-6)


def _body_mask(p, c, cfg):
    """1 = skin. Skin below the hem band and above the waist band; trousers between. Inside the two narrow
    transition bands (the waistline is not level), skin is told from linen by saturation (skin ~0.33, linen ~0.16)."""
    z = p[:, 2]
    hem_lo, hem_hi = cfg["hem_band"]
    waist_lo, waist_hi = cfg["waist_band"]
    by_colour = HV["_smooth"](_saturation(c), *cfg["skin_saturation"])
    m = np.zeros(len(p))
    m[z < hem_lo] = 1
    m[z > waist_hi] = 1
    band = ((z >= hem_lo) & (z <= hem_hi)) | ((z >= waist_lo) & (z <= waist_hi))
    m[band] = by_colour[band]
    return m


def _head_mask(p, c, cfg):
    """1 = face skin. Hair = texels darker than the forehead skin inside the scalp / brow zones; eyes excluded."""
    lum = _lum(c)
    fore = (np.abs(p[:, 0]) < 0.025) & (p[:, 2] > 1.745) & (p[:, 2] < 1.76) & (p[:, 1] < -0.08)
    skin = np.median(lum[fore]) if fore.any() else 0.55
    dark = HV["_smooth"](skin - lum, cfg["hair_dark_lo"] * skin, cfg["hair_dark_hi"] * skin)
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


def _save_mask(mask, path):
    size = mask.shape[0]
    img = bpy.data.images.new("_SM_out", size, size, alpha=False)
    try:
        img.colorspace_settings.name = 'Non-Color'  # before writing pixels: changing it resets a generated image
        rgba = np.zeros((size, size, 4), dtype=np.float32)
        rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = np.clip(mask, 0, 1)
        rgba[..., 3] = 1
        img.pixels.foreach_set(rgba.ravel())
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save()
    finally:
        bpy.data.images.remove(img)


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


def _ref_luminance(c, mask):
    w = mask.ravel()
    lum = _lum(_srgb_to_linear(c)).ravel()
    return float((lum * w).sum() / max(w.sum(), 1e-6))


def _base(cfg):
    scene, rig, head, body, unit = HV["_load"]()
    tex, size = _load_texture(PROJECT / cfg["texture"])
    mask = np.zeros((size, size))
    bp, bcov = _bake_vertex_values(scene, body, _rest_positions(body, unit), size)
    hp, hcov = _bake_vertex_values(scene, head, _rest_positions(head, unit), size)
    # The head's inset neck stump reuses the body's skin UVs: those shared texels follow the body rule (skin). Head-only
    # texels (face, scalp, ears) follow the head rule.
    hcov &= ~HV["_coverage"](scene, body, size)
    bcov &= ~hcov
    mask[bcov] = _body_mask(bp[bcov], tex[bcov], cfg["body"])
    idx = np.where(hcov)
    mask[idx] = _head_mask(hp[idx], tex[idx], cfg["head"])
    mask = _blur(mask, cfg.get("blur_px", 2)) * (bcov | hcov)
    _save_mask(mask, PROJECT / cfg["out"])
    return {"out": cfg["out"], "ref_luminance": _ref_luminance(tex, (mask > 0.9) & (bcov | hcov)), "coverage": int((mask > 0.5).sum())}


def _armor(cfg):
    scene = bpy.data.scenes.get("HeadVariant") or bpy.context.window.scene
    before = set(scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(PROJECT / cfg["fbx"]))
    new = [o for o in scene.objects if o not in before]
    try:
        mesh = next(o for o in new if o.type == 'MESH' and o.data.uv_layers.active is not None)
        tex, size = _load_texture(PROJECT / cfg["texture"])
        groups = {g.index for g in mesh.vertex_groups if any(b in g.name for b in cfg["skin_bones"])}
        w = np.array([sum(g.weight for g in v.groups if g.group in groups) for v in mesh.data.vertices])
        lo, hi = cfg["weight_range"]
        vals = np.repeat(HV["_smooth"](w, lo, hi)[:, None], 3, axis=1)
        m, cov = _bake_vertex_values(scene, mesh, vals, size)
        mask = _blur(m[..., 0] * cov, cfg.get("blur_px", 2)) * cov
        _save_mask(mask, PROJECT / cfg["out"])
        return {"out": cfg["out"], "ref_luminance": _ref_luminance(tex, mask > 0.9), "coverage": int((mask > 0.5).sum())}
    finally:
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)


def run(config_path):
    cfg = json.loads(Path(config_path).read_text(encoding="utf-8"))
    out = {"base": _base(cfg["base"])}
    for a in cfg.get("armor", []):
        out[a["name"]] = _armor(a)
    return out
