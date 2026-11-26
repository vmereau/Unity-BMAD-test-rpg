"""Area 1 palisade kit: log heads, posts, a rail and a rope lashing.

Run in Blender (5.x):  blender -b -P build_palisade.py
or from a session:      runpy.run_path(r"...\\build_palisade.py", run_name="__main__")

Writes  ../Palisade.fbx, ../Palisade.json (the sizes Editor/World/PalisadeKit reads)
        and Palisade.blend (the built objects only: this script is the source).

The kit is not a wall. `Tools/World/Build Area 1 Camp Edge` assembles it log by log into every palisade panel and
fits each log to the ground (Editor/World/PalisadeMesh). Look: PALISADE_CONCEPTS.md, artwork P6.

Everything is written in Unity's frame (x right, y up, z forward); `to_blender` converts each vertex, so the FBX
(-Z forward, Y up, Apply Transform) arrives in Unity in that same frame.

Contract with the assembler:
  Log_N, Post_N  a head standing on the y axis, its top near y 0, ending in an OPEN ring at exactly y -HEAD.
                 The assembler extends that ring down into the ground, so the ring is a clean circle on the axis.
                 U runs down the log at 1 / TILE per metre (u = -y / TILE on the shaft).
  Rail           a pole along +z from 0 to 1; the assembler stretches it (and its U) to the panel's length.
  Lashing        rope loops round a post at the origin and a rail at x = post radius + rail radius.

Wood UVs are the camp's (Camp/CLAUDE.md -> One wood, by construction): T_ModularWood holds ten planks per tile,
2 m per tile. A round log is wrapped onto two or three planks, going there and back round the trunk, so there
is no seam and half a 0.3 m trunk's girth covers about its true width of texture.
"""
import json
import math
import os
import random

import bpy

# --- Sizes (PALISADE_CONCEPTS.md -> Decisions taken from the artworks) ----------------------------
HEAD = 1.0                 # every head ends in its open ring this far under y 0
TILE = 2.0
ROWS = 10
LOG_SIDES = 8
POST_SIDES = 10
RAIL_SIDES = 6
RAIL_RADIUS = 0.065
ROPE_RADIUS = 0.013
ROPE_SIDES = 3
ROPE_SEGMENTS = 8
SEED = 4711

# name, shaft radius, point height, row of the wood texture the wrap starts on
LOGS = (("Log_1", 0.150, 0.30, 0), ("Log_2", 0.138, 0.34, 2), ("Log_3", 0.162, 0.27, 4),
        ("Log_4", 0.145, 0.31, 6), ("Log_5", 0.156, 0.36, 1), ("Log_6", 0.132, 0.28, 5))
POSTS = (("Post_1", 0.205, 0), ("Post_2", 0.195, 5))
MATERIALS = ("Pal_Wood", "Pal_Rope")
WOOD, ROPE = 0, 1


def to_blender(p):
    return (-p[0], -p[2], p[1])


def _normal(a, b, c):
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    return (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])


def wrap_v(s, sides, row, planks):
    """V round the trunk: up over `planks` planks on one half, back down on the other, so it closes without a seam."""
    t = s / sides
    there_and_back = 1.0 - abs(2.0 * t - 1.0)
    lo = (row + 0.06) / ROWS
    hi = (row + planks - 0.06) / ROWS
    return lo + (hi - lo) * there_and_back


class Mesh:
    def __init__(self):
        self.verts, self.faces, self.uvs, self.mats, self.smooth = [], [], [], [], []

    def face(self, idx, uv, mat, smooth, outward_from):
        p = [self.verts[i] for i in idx[:3]]
        n = _normal(p[0], p[1], p[2])
        c = to_blender(outward_from)
        if sum(n[k] * (p[0][k] - c[k]) for k in range(3)) < 0:
            idx = list(reversed(idx))
            uv = list(reversed(uv))
        self.faces.append(list(idx))
        self.uvs.append(list(uv))
        self.mats.append(mat)
        self.smooth.append(smooth)

    def triangles(self):
        return sum(len(f) - 2 for f in self.faces)

    def to_object(self, name, materials, collection):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.verts, [], self.faces)
        uv = mesh.uv_layers.new(name="UVMap")
        k = 0
        for face_uv in self.uvs:
            for c in face_uv:
                uv.data[k].uv = c
                k += 1
        for m in materials:
            mesh.materials.append(m)
        mesh.polygons.foreach_set("material_index", self.mats)
        mesh.polygons.foreach_set("use_smooth", self.smooth)
        mesh.update()
        mesh.validate()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        return obj


def rings_to_faces(mesh, rings, sides, row, planks, flat_from=None):
    """`rings` are lists of (x, y, z), bottom first. Faces between ring r and r + 1 are flat-shaded from `flat_from` on."""
    base = len(mesh.verts)
    for ring in rings:
        mesh.verts.extend(to_blender(p) for p in ring)
    # U follows the surface down from the top, so the grain keeps its scale over the point as well
    run = [0.0] * len(rings)
    for r in range(len(rings) - 2, -1, -1):
        step = sum(math.dist(rings[r][s], rings[r + 1][s]) for s in range(sides)) / sides
        run[r] = run[r + 1] + step
    shaft_u = -rings[0][0][1] / TILE          # the open ring's U is exactly -y / TILE, as the assembler expects
    offset = shaft_u - run[0] / TILE
    for r in range(len(rings) - 1):
        axis = (0.0, (rings[r][0][1] + rings[r + 1][0][1]) * 0.5, 0.0)
        for s in range(sides):
            n = (s + 1) % sides
            idx = [base + r * sides + s, base + r * sides + n, base + (r + 1) * sides + n, base + (r + 1) * sides + s]
            u0, u1 = offset + run[r] / TILE, offset + run[r + 1] / TILE
            v0, v1 = wrap_v(s, sides, row, planks), wrap_v(s + 1, sides, row, planks)
            mesh.face(idx, [(u0, v0), (u0, v1), (u1, v1), (u1, v0)], WOOD,
                      flat_from is None or r < flat_from, axis)
    return base


def cap(mesh, ring_start, sides, centre, row, below):
    """End grain: a small patch of one plank."""
    idx = [ring_start + s for s in range(sides)]
    v_mid = (row + 0.5) / ROWS
    uv = [(0.5 + 0.03 * math.cos(2 * math.pi * s / sides), v_mid + 0.03 * math.sin(2 * math.pi * s / sides)) for s in range(sides)]
    mesh.face(idx, uv, WOOD, False, below)


def circle(y, radius, sides, centre=(0.0, 0.0), lift=None, squash=None):
    out = []
    for s in range(sides):
        a = 2.0 * math.pi * s / sides
        r = radius * (squash[s] if squash else 1.0)
        out.append((centre[0] + math.cos(a) * r, y + (lift[s] if lift else 0.0), centre[1] + math.sin(a) * r))
    return out


def log_head(rng, radius, point, row):
    """A trunk's top metre: shaft, an uneven shoulder where the axe cuts start, and a short blunt point."""
    mesh = Mesh()
    sides = LOG_SIDES
    lump = [rng.uniform(0.95, 1.05) for _ in range(sides)]
    # axe cuts come in a few strokes: the shoulder dips and rises in pairs of sides
    cuts = [rng.uniform(-0.06, 0.05) for _ in range(sides // 2)]
    shoulder = [cuts[s // 2] + rng.uniform(-0.012, 0.012) for s in range(sides)]
    tip = (rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03))
    rings = [
        circle(-HEAD, radius, sides),
        circle(-HEAD * 0.55, radius * 0.985, sides, (rng.uniform(-0.008, 0.008), rng.uniform(-0.008, 0.008)), squash=lump),
        circle(-point, radius * 0.955, sides, (rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01)), lift=shoulder, squash=lump),
        circle(-0.015, radius * 0.24, sides, tip, lift=[rng.uniform(-0.012, 0.012) for _ in range(sides)]),
    ]
    base = rings_to_faces(mesh, rings, sides, row, 2, flat_from=2)
    cap(mesh, base + 3 * sides, sides, tip, row, (0.0, -0.5, 0.0))
    top = max(p[1] for p in rings[3])
    mesh.shoulder = top - min(p[1] for p in rings[2])   # how far under its top the point starts, at its lowest cut
    return mesh


def post_head(rng, radius, row):
    """A heavier trunk sawn off flat, the cut a little out of level."""
    mesh = Mesh()
    sides = POST_SIDES
    lump = [rng.uniform(0.96, 1.04) for _ in range(sides)]
    tilt_a, tilt = rng.uniform(0.0, 2.0 * math.pi), rng.uniform(0.04, 0.09)
    slant = [math.cos(2.0 * math.pi * s / sides - tilt_a) * tilt * radius for s in range(sides)]
    rings = [
        circle(-HEAD, radius, sides),
        circle(-HEAD * 0.5, radius * 0.99, sides, (rng.uniform(-0.008, 0.008), rng.uniform(-0.008, 0.008)), squash=lump),
        circle(-0.05, radius * 0.975, sides, lift=slant, squash=lump),
        circle(-0.015, radius * 0.93, sides, lift=slant, squash=lump),
    ]
    base = rings_to_faces(mesh, rings, sides, row, 3, flat_from=2)
    cap(mesh, base + 3 * sides, sides, (0.0, 0.0), row, (0.0, -0.5, 0.0))
    return mesh


def rail():
    """A unit pole along +z. U is z / TILE, so stretching z and U together keeps the grain's scale."""
    mesh = Mesh()
    sides = RAIL_SIDES
    base = len(mesh.verts)
    for z in (0.0, 1.0):
        for s in range(sides):
            a = 2.0 * math.pi * s / sides
            mesh.verts.append(to_blender((math.cos(a) * RAIL_RADIUS, math.sin(a) * RAIL_RADIUS, z)))
    for s in range(sides):
        n = (s + 1) % sides
        v0, v1 = wrap_v(s, sides, 8, 1), wrap_v(s + 1, sides, 8, 1)
        mesh.face([base + s, base + n, base + sides + n, base + sides + s],
                  [(0.0, v0), (0.0, v1), (1.0 / TILE, v1), (1.0 / TILE, v0)], WOOD, True, (0.0, 0.0, 0.5))
    for end in (0, 1):
        idx = [base + end * sides + s for s in range(sides)]
        uv = [(0.5 + 0.02 * math.cos(2 * math.pi * s / sides), 0.85 + 0.02 * math.sin(2 * math.pi * s / sides)) for s in range(sides)]
        mesh.face(idx, uv, WOOD, False, (0.0, 0.0, 0.5))
    return mesh


def lashing(rng, post_radius):
    """Two crossed rope loops round the post (origin) and the rail (x = post radius + rail radius, along z)."""
    mesh = Mesh()
    rail_x = post_radius + RAIL_RADIUS
    far = rail_x + RAIL_RADIUS                      # the rail's far face
    cx = (far - post_radius) * 0.5
    ax = (far + post_radius) * 0.5 + ROPE_RADIUS
    az = post_radius * 1.12 + ROPE_RADIUS           # wide enough that a tilted loop still clears the post
    for tilt in (math.radians(22.0), math.radians(-19.0)):
        base = len(mesh.verts)
        wobble = [rng.uniform(-0.004, 0.004) for _ in range(ROPE_SEGMENTS)]
        centres, tangents = [], []
        for k in range(ROPE_SEGMENTS):
            a = 2.0 * math.pi * k / ROPE_SEGMENTS
            x, z = cx + math.cos(a) * ax, math.sin(a) * az
            y = z * math.tan(tilt) + wobble[k]
            centres.append((x, y, z))
        for k in range(ROPE_SEGMENTS):
            a, b = centres[k - 1], centres[(k + 1) % ROPE_SEGMENTS]
            t = [b[i] - a[i] for i in range(3)]
            l = math.sqrt(sum(c * c for c in t))
            tangents.append([c / l for c in t])
        length = 0.0
        for k in range(ROPE_SEGMENTS):
            t = tangents[k]
            side = [t[2], 0.0, -t[0]]
            l = math.sqrt(sum(c * c for c in side)) or 1.0
            side = [c / l for c in side]
            up = [t[1] * side[2] - t[2] * side[1], t[2] * side[0] - t[0] * side[2], t[0] * side[1] - t[1] * side[0]]
            for s in range(ROPE_SIDES):
                ang = 2.0 * math.pi * s / ROPE_SIDES
                mesh.verts.append(to_blender([centres[k][i] + (side[i] * math.cos(ang) + up[i] * math.sin(ang)) * ROPE_RADIUS
                                              for i in range(3)]))
        for k in range(ROPE_SEGMENTS):
            n = (k + 1) % ROPE_SEGMENTS
            step = math.dist(centres[k], centres[n])
            mid = [(centres[k][i] + centres[n][i]) * 0.5 for i in range(3)]
            for s in range(ROPE_SIDES):
                sn = (s + 1) % ROPE_SIDES
                idx = [base + k * ROPE_SIDES + s, base + k * ROPE_SIDES + sn, base + n * ROPE_SIDES + sn, base + n * ROPE_SIDES + s]
                u0, u1 = length / 0.3, (length + step) / 0.3
                uv = [(u0, s / ROPE_SIDES), (u0, (s + 1) / ROPE_SIDES), (u1, (s + 1) / ROPE_SIDES), (u1, s / ROPE_SIDES)]
                mesh.face(idx, uv, ROPE, True, mid)
            length += step
    return mesh


def materials():
    out = []
    assets = _find_up(os.path.dirname(os.path.abspath(__file__)), "Assets")
    wood = os.path.join(assets, "Hivemind", "HDRP(Default)", "MedievalFantasyVillage", "Art", "Textures", "T_ModularWood_Basecolor.png") if assets else ""
    look = {"Pal_Wood": (wood, (0.95, 0.95, 0.95, 1.0)), "Pal_Rope": ("", (0.3, 0.25, 0.18, 1.0))}
    for name in MATERIALS:
        path, tint = look[name]
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Roughness"].default_value = 0.9
            if path and os.path.exists(path) and not any(n.type == 'TEX_IMAGE' for n in m.node_tree.nodes):
                tex = m.node_tree.nodes.new("ShaderNodeTexImage")
                tex.image = bpy.data.images.load(path, check_existing=True)
                mix = m.node_tree.nodes.new("ShaderNodeMix")
                mix.data_type = 'RGBA'
                mix.blend_type = 'MULTIPLY'
                mix.inputs[0].default_value = 1.0
                m.node_tree.links.new(tex.outputs["Color"], mix.inputs[6])
                mix.inputs[7].default_value = tint
                m.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            elif not path:
                bsdf.inputs["Base Color"].default_value = tint
        out.append(m)
    return out


def _find_up(start, name):
    path = start
    while True:
        if os.path.basename(path) == name:
            return path
        parent = os.path.dirname(path)
        if parent == path:
            return None
        path = parent


def export(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='OFF',
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, use_tspace=False)


def main():
    source = os.path.dirname(os.path.abspath(__file__))
    out = os.path.dirname(source)
    rng = random.Random(SEED)

    parts = {}
    for name, radius, point, row in LOGS:
        parts[name] = log_head(rng, radius, point, row)
    for name, radius, row in POSTS:
        parts[name] = post_head(rng, radius, row)
    parts["Rail"] = rail()
    post_radius = max(r for _, r, _ in POSTS)
    parts["Lashing"] = lashing(rng, post_radius)

    name = "Palisade"
    old = bpy.data.collections.get(name)
    kept = set(old.objects) if old else set()
    for part in parts:   # checked before anything is deleted
        clash = bpy.data.objects.get(part)
        if clash and clash not in kept:
            raise RuntimeError("An object called '%s' already exists in this Blender session; rename it first" % part)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    # Mesh data outlives its object (the .blend is written with fake users), and a leftover 'Log_1' would push the
    # new one to 'Log_1.001'. Drop every mesh of a part's name that no object uses.
    for stale in list(bpy.data.meshes):
        if stale.name.split(".")[0] in parts and not any(o.data == stale for o in bpy.data.objects):
            bpy.data.meshes.remove(stale)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    mats = materials()
    objects = [mesh.to_object(part, mats, col) for part, mesh in parts.items()]

    triangles = {part: mesh.triangles() for part, mesh in parts.items()}
    max_log = max(r for _, r, _, _ in LOGS)
    # the sizes file and the FBX go out together, before the .blend: the two must never be a version apart
    facts = {
        "head": HEAD, "tile": TILE,
        "logs": [{"name": n, "radius": r, "sides": LOG_SIDES, "shoulder": round(parts[n].shoulder, 4)} for n, r, _, _ in LOGS],
        "posts": [{"name": n, "radius": r, "sides": POST_SIDES} for n, r, _ in POSTS],
        "rail": {"name": "Rail", "radius": RAIL_RADIUS},
        "lashing": {"name": "Lashing"},
        # across the wall, from the line the logs stand on, toward the camp side:
        "railOffset": max_log + RAIL_RADIUS,                 # the rail lies against the thickest log
        "postOffset": max_log - post_radius,                 # the post's camp face is flush with it
        "triangles": triangles,
    }
    with open(os.path.join(out, name + ".json"), "w") as f:
        json.dump(facts, f, indent=1)
    export(objects, os.path.join(out, name + ".fbx"))
    for o in objects:
        o.name = name + "_" + o.name
    bpy.data.libraries.write(os.path.join(source, name + ".blend"), set(objects) | set(mats), fake_user=True)
    return {"triangles": triangles}


if __name__ == "__main__":
    result = main()
    print("palisade:", result)
