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
HV = runpy.run_path(str(PROJECT / "Tools/Blender/make_head_variant.py"))  # _load, _coverage, _dilate, _smooth,
#   _base_head_skin, _blur, _save_mask (shared with the per-head masks)


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
    mask[idx] = HV["_base_head_skin"](hp[idx], tex[idx], cfg["head"])
    mask = HV["_blur"](mask, cfg.get("blur_px", 2)) * (bcov | hcov)
    HV["_save_mask"](mask, PROJECT / cfg["out"])
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
        mask = HV["_blur"](m[..., 0] * cov, cfg.get("blur_px", 2)) * cov
        HV["_save_mask"](mask, PROJECT / cfg["out"])
        return {"out": cfg["out"], "ref_luminance": _ref_luminance(tex, mask > 0.9), "coverage": int((mask > 0.5).sum())}
    finally:
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)


def _body(cfg):
    """Stand-alone body FBX (base hero v2): same trouser-band rule as the base body, on its own atlas."""
    scene = bpy.data.scenes.get("HeadVariant") or bpy.context.window.scene
    bpy.context.window.scene = scene
    before = set(scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(PROJECT / cfg["fbx"]))
    new = [o for o in scene.objects if o not in before]
    try:
        rig = next(o for o in new if o.type == 'ARMATURE')
        mesh = next(o for o in new if o.type == 'MESH' and o.data.uv_layers.active is not None)
        rig.data.pose_position = 'REST'
        bpy.context.view_layer.update()
        unit = rig.matrix_world.to_scale().x
        # material slot 1 = the neck band on the head's old atlas (fit_base_body.canonical_neck_uvs): its UVs index
        # another texture, so it must not write into this mask
        if len(mesh.data.materials) > 1:
            import bmesh
            bm = bmesh.new()
            bm.from_mesh(mesh.data)
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index > 0], context='FACES_ONLY')
            bm.to_mesh(mesh.data)
            bm.free()
            mesh.data.update()
        tex, size = _load_texture(PROJECT / cfg["texture"])
        # a 4K bake comes back empty (Cycles); bake the mask at bake_px and box-downsample the texture to match
        bake = cfg.get("bake_px", 2048)
        if size > bake:
            k = size // bake
            tex = tex.reshape(bake, k, bake, k, 3).mean(axis=(1, 3))
            size = bake
        p, cov = _bake_vertex_values(scene, mesh, _rest_positions(mesh, unit), size)
        mask = np.zeros((size, size))
        mask[cov] = _body_mask(p[cov], tex[cov], cfg["body"])
        mask = HV["_blur"](mask, cfg.get("blur_px", 2)) * cov
        HV["_save_mask"](mask, PROJECT / cfg["out"])
        return {"out": cfg["out"], "ref_luminance": _ref_luminance(tex, (mask > 0.9) & cov), "coverage": int((mask > 0.5).sum())}
    finally:
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)


def run(config_path, only=None):
    """only: optional list of entry names ("base", a body or an armor name) to rebuild; default all."""
    cfg = json.loads(Path(config_path).read_text(encoding="utf-8"))
    out = {}
    if only is None or "base" in only:
        out["base"] = _base(cfg["base"])
    for b in cfg.get("bodies", []):
        if only is None or b["name"] in only:
            out[b["name"]] = _body(b)
    for a in cfg.get("armor", []):
        if only is None or a["name"] in only:
            out[a["name"]] = _armor(a)
    return out
