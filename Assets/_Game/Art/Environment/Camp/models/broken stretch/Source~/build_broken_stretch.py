"""Area 1 broken stretch: the collapsed 8 m of the north palisade and its repair works.

Run in Blender (5.x):  blender -b -P build_broken_stretch.py
or from a session:      runpy.run_path(r"...\\build_broken_stretch.py", run_name="__main__")

Writes  ../BrokenStretch.fbx   every piece's mesh, each standing at the origin on its own pivot
        ../BrokenStretch.json  where each piece goes, how it meets the ground and what it collides with
        and BrokenStretch.blend (the built objects only: this script is the source).

`Tools/World/Build Area 1 Camp Edge` (Editor/World/BrokenStretchBuilder) places the pieces. Look and layout rules:
BROKEN_STRETCH_CONCEPTS.md.

Everything is in Unity's frame, local to the stretch: the origin is the break point on the wall line, +x runs along
the wall (east), +z points out of the camp (toward the river), y is up from the ground under a piece.
`to_blender` converts each vertex, so the FBX (-Z forward, Y up, Apply Transform) arrives in Unity in that frame.

Placement kinds (the `fit` of a part):
  ground   pos is (x, height above the terrain, z); the piece keeps `forward` and `roll`.
  surface  the same, above whatever is highest there: the terrain or one of the camp's rocks.
  span     a log lying between two points a and b (x, z): each end rests `lift` above the ground (`on`: ground
           or surface) and the mesh's +z is turned from a to b.
  strut    a unit pole stretched from a to b, both (x, height above the terrain at that point, z).

Wood UVs are the camp's (Camp/CLAUDE.md -> One wood, by construction): T_ModularWood holds ten planks per tile,
2 m per tile; a round log is wrapped there and back over two planks, a board is mapped onto one.
"""
import json
import math
import os
import random

import bpy

TILE = 2.0
ROWS = 10
SEED = 9241
NAME = "BrokenStretch"
VERSION = 1

# Palisade sizes: the old and the new logs are wall logs. Copied from the first five LOGS of
# models/palisade/Source~/build_palisade.py (Palisade.json's radii); change them there and here together.
LOG_RADII = (0.150, 0.138, 0.162, 0.145, 0.156)
LOG_SIDES = 8
POINT = 0.31
POLE_SIDES = 6
IRON_UV = (0.04, 0.17, 0.42, 0.055)   # the one plain patch of the kit's iron atlas (build_warehouse.py)

# Material slots, by the name the builder maps to an M_Camp_* material.
OLD, NEW, POLE, ROPE, STONE, IRON, RAG, BOARD = "old", "new", "pole", "rope", "stone", "iron", "rag", "board"
MATERIALS = (OLD, NEW, POLE, ROPE, STONE, IRON, RAG, BOARD)


def to_blender(p):
    return (-p[0], -p[2], p[1])


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def mul(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def unit(a):
    l = math.sqrt(dot(a, a)) or 1.0
    return (a[0] / l, a[1] / l, a[2] / l)


def wrap_v(s, sides, row, planks):
    """V round a trunk: up over `planks` planks on one half, back down on the other, so it closes without a seam."""
    t = (s % sides) / sides if s != sides else 1.0
    there_and_back = 1.0 - abs(2.0 * t - 1.0)
    lo = (row + 0.06) / ROWS
    hi = (row + planks - 0.06) / ROWS
    return lo + (hi - lo) * there_and_back


class Mesh:
    def __init__(self):
        self.verts, self.faces, self.uvs, self.mats, self.smooth = [], [], [], [], []

    def vert(self, p):
        self.verts.append(p)          # kept in Unity's frame; converted on the way out
        return len(self.verts) - 1

    def face(self, idx, uv, mat, smooth, outward_from):
        p = [self.verts[i] for i in idx[:3]]
        n = cross(sub(p[1], p[0]), sub(p[2], p[0]))
        if dot(n, sub(p[0], outward_from)) < 0:
            idx = list(reversed(idx))
            uv = list(reversed(uv))
        self.faces.append(list(idx))
        self.uvs.append(list(uv))
        self.mats.append(mat)
        self.smooth.append(smooth)

    def triangles(self):
        return sum(len(f) - 2 for f in self.faces)

    def used(self):
        return [m for m in MATERIALS if m in self.mats]

    def to_object(self, name, materials, collection):
        used = self.used()
        mesh = bpy.data.meshes.new(name)
        # Blender's frame mirrors x, so the winding is reversed to keep the faces outward.
        mesh.from_pydata([to_blender(v) for v in self.verts], [], [list(reversed(f)) for f in self.faces])
        uv = mesh.uv_layers.new(name="UVMap")
        k = 0
        for face_uv in self.uvs:
            for c in reversed(face_uv):
                uv.data[k].uv = c
                k += 1
        for m in used:
            mesh.materials.append(materials[m])
        mesh.polygons.foreach_set("material_index", [used.index(m) for m in self.mats])
        mesh.polygons.foreach_set("use_smooth", self.smooth)
        mesh.update()
        mesh.validate()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        return obj


# --- Shapes ----------------------------------------------------------------------------------------

def frame(direction):
    """Two unit vectors across `direction`."""
    d = unit(direction)
    ref = (0.0, 1.0, 0.0) if abs(d[1]) < 0.9 else (1.0, 0.0, 0.0)
    side = unit(cross(ref, d))
    return side, cross(d, side)


def ring(mesh, centre, direction, radius, sides, lift=None, squash=None):
    side, up = frame(direction)
    d = unit(direction)
    out = []
    for s in range(sides):
        a = 2.0 * math.pi * s / sides
        r = radius * (squash[s] if squash else 1.0)
        p = add(centre, add(mul(side, math.cos(a) * r), mul(up, math.sin(a) * r)))
        if lift:
            p = add(p, mul(d, lift[s]))
        out.append(mesh.vert(p))
    return out


def skin(mesh, rings, sides, mat, uv_kind, row=0, planks=2, flat=(), u0=0.0):
    """Faces between consecutive rings. `flat` lists the ring gaps that are flat-shaded (cuts and breaks)."""
    run = [0.0]
    for r in range(1, len(rings)):
        step = sum(math.dist(mesh.verts[rings[r][s]], mesh.verts[rings[r - 1][s]]) for s in range(sides)) / sides
        run.append(run[-1] + step)
    for r in range(len(rings) - 1):
        mid = [0.0, 0.0, 0.0]
        for i in rings[r] + rings[r + 1]:
            mid = add(mid, mesh.verts[i])
        mid = mul(mid, 1.0 / (2 * sides))
        for s in range(sides):
            n = (s + 1) % sides
            idx = [rings[r][s], rings[r][n], rings[r + 1][n], rings[r + 1][s]]
            if uv_kind == "wood":
                a, b = u0 + run[r] / TILE, u0 + run[r + 1] / TILE
                v0, v1 = wrap_v(s, sides, row, planks), wrap_v(s + 1, sides, row, planks)
                uv = [(a, v0), (a, v1), (b, v1), (b, v0)]
            elif uv_kind == "rope":
                a, b = run[r] / 0.3, run[r + 1] / 0.3
                uv = [(a, s / sides), (a, (s + 1) / sides), (b, (s + 1) / sides), (b, s / sides)]
            else:
                uv = iron_uv(4)
            mesh.face(idx, uv, mat, r not in flat, mid)


def iron_uv(count):
    u, v, du, dv = IRON_UV
    corners = [(u, v), (u + du, v), (u + du, v + dv), (u, v + dv)]
    return corners[:count] if count <= 4 else [corners[i % 4] for i in range(count)]


def cap(mesh, loop, mat, uv_kind, row, inward_point, centre=None):
    """Closes a ring: flat when `centre` is None, else a fan to that point (a broken end's hollow)."""
    sides = len(loop)
    v_mid = (row + 0.5) / ROWS
    outward_from = inward_point
    if centre is None:
        if uv_kind == "wood":
            uv = [(0.5 + 0.03 * math.cos(2 * math.pi * s / sides), v_mid + 0.03 * math.sin(2 * math.pi * s / sides)) for s in range(sides)]
        else:
            uv = iron_uv(sides)
        mesh.face(list(loop), uv, mat, False, outward_from)
        return
    c = mesh.vert(centre)
    for s in range(sides):
        n = (s + 1) % sides
        uv = [(0.5, v_mid), (0.53, v_mid - 0.02), (0.53, v_mid + 0.02)] if uv_kind == "wood" else iron_uv(3)
        mesh.face([c, loop[s], loop[n]], uv, mat, False, outward_from)


def log(mesh, rng, length, radius, row, mat, start="butt", end="point", z0=0.0, bend=0.02):
    """A wall log along +z from z0 to z0 + length. Ends: butt (sawn flat), point (axe-cut) or snap (broken off)."""
    sides = LOG_SIDES
    axis = (0.0, 0.0, 1.0)
    lump = [rng.uniform(0.95, 1.05) for _ in range(sides)]
    rings, flat = [], []
    z1 = z0 + length

    def at(z, r, lift=None, off=(0.0, 0.0)):
        return ring(mesh, (off[0], off[1], z), axis, r, sides, lift, lump)

    if start == "snap":
        jag = [rng.uniform(0.0, 0.32) for _ in range(sides)]
        rings.append(at(z0, radius * 0.78, jag))
        flat.append(0)
        rings.append(at(z0 + 0.42, radius))
    else:
        rings.append(at(z0, radius))
    shaft_from = z0 + (0.42 if start == "snap" else 0.0)
    shaft_to = z1 - (POINT if end == "point" else 0.42 if end == "snap" else 0.0)
    steps = max(1, int((shaft_to - shaft_from) / 1.6))
    for k in range(1, steps):
        z = shaft_from + (shaft_to - shaft_from) * k / steps
        rings.append(at(z, radius * rng.uniform(0.97, 1.0), None, (rng.uniform(-bend, bend), rng.uniform(-bend, bend))))
    if end == "point":
        cuts = [rng.uniform(-0.06, 0.05) for _ in range(sides // 2)]
        rings.append(at(shaft_to, radius * 0.955, [cuts[s // 2] + rng.uniform(-0.012, 0.012) for s in range(sides)]))
        flat.append(len(rings) - 1)
        rings.append(at(z1 - 0.015, radius * 0.24, [rng.uniform(-0.012, 0.012) for _ in range(sides)]))
    elif end == "snap":
        rings.append(at(shaft_to, radius))
        flat.append(len(rings) - 1)
        rings.append(at(z1, radius * 0.78, [-rng.uniform(0.0, 0.32) for _ in range(sides)]))
    else:
        rings.append(at(z1, radius))
    skin(mesh, rings, sides, mat, "wood", row, 2, flat)
    mid = (0.0, 0.0, (z0 + z1) * 0.5)
    cap(mesh, rings[0], mat, "wood", row, mid, (0.0, 0.0, z0 + 0.26) if start == "snap" else None)
    cap(mesh, rings[-1], mat, "wood", row, mid, (0.0, 0.0, z1 - 0.26) if end == "snap" else None)


def pole(mesh, a, b, radius, mat, row=8, sides=POLE_SIDES, uv_kind="wood", caps=True):
    d = sub(b, a)
    rings = [ring(mesh, a, d, radius, sides), ring(mesh, b, d, radius, sides)]
    skin(mesh, rings, sides, mat, uv_kind, row, 1)
    if caps:
        mid = mul(add(a, b), 0.5)
        cap(mesh, rings[0], mat, "wood" if uv_kind == "wood" else "iron", row, mid)
        cap(mesh, rings[1], mat, "wood" if uv_kind == "wood" else "iron", row, mid)


def rope(mesh, points, radius=0.013, sides=4):
    rings = []
    for i, p in enumerate(points):
        d = sub(points[min(i + 1, len(points) - 1)], points[max(i - 1, 0)])
        rings.append(ring(mesh, p, d, radius, sides))
    skin(mesh, rings, sides, ROPE, "rope")


def lash(mesh, centre, direction, radius):
    """A rope binding round a pole: a short fat band."""
    d = unit(direction)
    rings = [ring(mesh, add(centre, mul(d, -0.05)), d, radius + 0.014, 6), ring(mesh, add(centre, mul(d, 0.05)), d, radius + 0.014, 6)]
    skin(mesh, rings, 6, ROPE, "rope")


def box(mesh, centre, size, mat, uv_kind, row=0, yaw=0.0, pitch=0.0, jitter=0.0, rng=None):
    """A board, a block or a strap. Wood is mapped onto one plank with the grain along the longest side."""
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))

    def turn(p):
        x, y, z = p
        y, z = y * cp - z * sp, y * sp + z * cp          # pitch about x
        x, z = x * cy + z * sy, -x * sy + z * cy          # yaw about y
        return (x, y, z)

    h = (size[0] * 0.5, size[1] * 0.5, size[2] * 0.5)
    corners = []
    for k in range(8):
        p = (h[0] if k & 1 else -h[0], h[1] if k & 2 else -h[1], h[2] if k & 4 else -h[2])
        if jitter and rng:
            p = (p[0] + rng.uniform(-jitter, jitter), p[1] + rng.uniform(-jitter, jitter), p[2] + rng.uniform(-jitter, jitter))
        corners.append(mesh.vert(add(centre, turn(p))))
    quads = ((0, 1, 3, 2, 2), (4, 6, 7, 5, 2), (0, 4, 5, 1, 1), (2, 3, 7, 6, 1), (0, 2, 6, 4, 0), (1, 5, 7, 3, 0))
    long_axis = max(range(3), key=lambda i: size[i])
    for a, b, c, d, normal_axis in quads:
        idx = [corners[a], corners[b], corners[c], corners[d]]
        local = []
        for k in (a, b, c, d):
            local.append((h[0] if k & 1 else -h[0], h[1] if k & 2 else -h[1], h[2] if k & 4 else -h[2]))
        axes = [i for i in range(3) if i != normal_axis]
        u_axis = long_axis if long_axis in axes else max(axes, key=lambda i: size[i])
        v_axis = [i for i in axes if i != u_axis][0]
        if uv_kind == "wood":
            lo, hi = (row + 0.08) / ROWS, (row + 0.92) / ROWS
            uv = [(p[u_axis] / TILE + 0.37 * row, lo + (hi - lo) * (p[v_axis] / size[v_axis] + 0.5)) for p in local]
        elif uv_kind == "stone":
            uv = [(p[u_axis] + centre[0] * 0.37, p[v_axis] + centre[2] * 0.53) for p in local]
        else:
            uv = iron_uv(4)
        mesh.face(idx, uv, mat, False, centre)


def stone(mesh, rng, size):
    """A rough broken stone: a block with its corners knocked about."""
    box(mesh, (0.0, size[1] * 0.5, 0.0), size, STONE, "stone", jitter=min(size) * 0.18, rng=rng)


# --- Pieces ----------------------------------------------------------------------------------------

def stump(rng, radius, height, row):
    """What is left of a wall log: it goes 0.9 m into the ground and ends in splinters."""
    mesh = Mesh()
    sides = LOG_SIDES
    lump = [rng.uniform(0.95, 1.05) for _ in range(sides)]
    up = (0.0, 1.0, 0.0)
    jag = [rng.uniform(-0.04, 0.28) if s % 2 else rng.uniform(-0.1, 0.1) for s in range(sides)]
    rings = [
        ring(mesh, (0.0, -0.9, 0.0), up, radius, sides),
        ring(mesh, (0.0, height * 0.45, 0.0), up, radius * 0.99, sides, None, lump),
        ring(mesh, (0.0, height - 0.12, 0.0), up, radius * 0.97, sides, None, lump),
        ring(mesh, (0.0, height, 0.0), up, radius * 0.7, sides, jag, lump),
    ]
    skin(mesh, rings, sides, OLD, "wood", row, 2, flat=(2,), u0=0.9 / TILE)
    cap(mesh, rings[-1], OLD, "wood", row, (0.0, 0.0, 0.0), (0.0, height - 0.16, 0.0))
    return mesh


def old_log(rng, length, radius, row, start, end, buried=0.0):
    mesh = Mesh()
    log(mesh, rng, length + buried, radius, row, OLD, start, end, z0=-buried)
    return mesh


def new_log(rng, length, radius, row):
    mesh = Mesh()
    log(mesh, rng, length, radius, row, NEW, "butt", "point", bend=0.012)
    return mesh


def unit_pole(radius):
    mesh = Mesh()
    pole(mesh, (0.0, 0.0, 0.0), (0.0, 0.0, 1.0), radius, POLE)
    return mesh


def bearer():
    mesh = Mesh()
    box(mesh, (0.0, 0.06, 0.0), (0.14, 0.12, 1.7), POLE, "wood", row=7)
    return mesh


SCAFFOLD_X, SCAFFOLD_Z = 1.0, 0.6      # half sizes between the uprights
SCAFFOLD_DECK = 2.14
SCAFFOLD_TOP = 4.5


def scaffold(rng):
    """Four lashed uprights, two levels of ledgers, one plank deck and a ladder on its east side."""
    mesh = Mesh()
    hx, hz = SCAFFOLD_X, SCAFFOLD_Z
    for x in (-hx, hx):
        for z in (-hz, hz):
            lean = (rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03))
            pole(mesh, (x, -1.6, z), (x + lean[0], SCAFFOLD_TOP + rng.uniform(-0.25, 0.2), z + lean[1]), 0.05, POLE, row=8)
    for y in (2.0, 3.9):
        for z in (-hz, hz):
            pole(mesh, (-hx - 0.3, y, z + 0.06 * (1 if z > 0 else -1)), (hx + 0.3, y + rng.uniform(-0.04, 0.04), z + 0.06 * (1 if z > 0 else -1)), 0.04, POLE, row=9)
            for x in (-hx, hx):
                lash(mesh, (x, y, z), (0.0, 1.0, 0.0), 0.05)
        for x in (-hx, hx):
            pole(mesh, (x + 0.06 * (1 if x > 0 else -1), y + 0.08, -hz - 0.25), (x + 0.06 * (1 if x > 0 else -1), y + 0.08, hz + 0.25), 0.04, POLE, row=9)
    pole(mesh, (-hx, 0.1, hz + 0.1), (hx, 2.0, hz + 0.1), 0.035, POLE, row=9)            # brace on the outer face
    for k in range(5):                                                                    # the deck
        z = -hz + 0.12 + k * 0.24
        box(mesh, (rng.uniform(-0.04, 0.04), SCAFFOLD_DECK, z), (2.0 * hx + 0.5, 0.035, 0.21), BOARD, "wood", row=k * 2 % ROWS, yaw=rng.uniform(-1.2, 1.2))
    foot, head = (hx + 0.85, 0.0), (hx + 0.12, SCAFFOLD_DECK + 0.25)                       # the ladder, in (x, y)
    for z in (-0.21, 0.21):
        pole(mesh, (foot[0], foot[1] - 0.1, z), (head[0], head[1], z), 0.03, POLE, row=6)
    for k in range(1, 8):
        t = k / 8.0
        x, y = foot[0] + (head[0] - foot[0]) * t, foot[1] + (head[1] - foot[1]) * t
        pole(mesh, (x, y, -0.25), (x, y, 0.25), 0.02, POLE, row=6)
    return mesh


SHEAR_FOOT = 1.35         # each foot from the middle, along the wall
SHEAR_APEX = (0.0, 5.4, -1.25)
SHEAR_STAKE = (1.3, 0.0, 1.05)      # behind it, between the dragged logs and the walk


def shear_legs():
    """Two crossed poles leaning over the wall line, a rope and block hanging from the crossing, a guy rope behind."""
    mesh = Mesh()
    top = SHEAR_APEX
    for side in (-1.0, 1.0):
        foot = (side * SHEAR_FOOT, -0.4, 0.0)
        over = add(top, mul(unit(sub(top, foot)), 0.45))
        pole(mesh, foot, add(over, (side * -0.02, 0.0, 0.0)), 0.07, POLE, row=8, sides=8)
    lash(mesh, top, (0.0, 1.0, 0.0), 0.11)
    hang = (top[0], 2.3, top[2] - 0.05)
    rope(mesh, [add(top, (0.0, -0.1, -0.05)), hang])
    box(mesh, add(hang, (0.0, -0.12, 0.0)), (0.1, 0.24, 0.16), POLE, "wood", row=3)       # the block
    box(mesh, add(hang, (0.0, -0.29, 0.0)), (0.03, 0.1, 0.03), IRON, "iron")              # its hook
    stake = SHEAR_STAKE
    rope(mesh, [add(top, (0.0, 0.05, 0.05)), add(mul(add(top, stake), 0.5), (0.0, -0.25, 0.0)), add(stake, (0.0, 0.3, 0.0))])
    pole(mesh, add(stake, (0.05, -0.5, 0.1)), add(stake, (-0.03, 0.4, -0.06)), 0.045, POLE, row=5)
    return mesh


ROPE_HALF = 2.9
ROPE_HEIGHT = 1.15


def rope_line(rng):
    """The makeshift closure: a sagging rope with rags between two stakes."""
    mesh = Mesh()
    for side in (-1.0, 1.0):
        pole(mesh, (side * ROPE_HALF, -1.0, 0.0), (side * ROPE_HALF + rng.uniform(-0.04, 0.04), ROPE_HEIGHT + 0.12, rng.uniform(-0.04, 0.04)), 0.045, POLE, row=5)
    points = []
    for k in range(9):
        t = k / 8.0
        points.append((-ROPE_HALF + 2.0 * ROPE_HALF * t, ROPE_HEIGHT - 0.32 * (1.0 - (2.0 * t - 1.0) ** 2), 0.0))
    rope(mesh, points)
    for k in range(7):
        t = (k + 0.6 + rng.uniform(-0.2, 0.2)) / 7.6
        x = -ROPE_HALF + 2.0 * ROPE_HALF * t
        y = ROPE_HEIGHT - 0.32 * (1.0 - (2.0 * t - 1.0) ** 2)
        w, l = rng.uniform(0.07, 0.12), rng.uniform(0.18, 0.36)
        tilt = rng.uniform(-0.05, 0.05)
        short = rng.uniform(0.75, 1.0)
        uv = [(0.1, 0.9), (0.3, 0.9), (0.3, 0.4), (0.1, 0.45)]
        for facing in (-1.0, 1.0):                                              # one face each way, on its own vertices
            z = 0.012 * facing
            quad = [mesh.vert((x - w * 0.5, y + 0.01, z)), mesh.vert((x + w * 0.5, y + 0.01, z)),
                    mesh.vert((x + w * 0.5 + tilt, y - l, z)), mesh.vert((x - w * 0.4 + tilt, y - l * short, z))]
            mesh.face(quad, uv, RAG, False, (x, y - l * 0.5, -facing))
    return mesh


def quarry_block(rng, size):
    mesh = Mesh()
    box(mesh, (0.0, size[1] * 0.5, 0.0), size, STONE, "stone", jitter=0.018, rng=rng)
    return mesh


def foot_stone(rng, size):
    mesh = Mesh()
    stone(mesh, rng, size)
    return mesh


def sledge():
    """A low stone sledge: two runners turned up at the front, cross bars and a deck."""
    mesh = Mesh()
    for x in (-0.33, 0.33):
        box(mesh, (x, 0.07, -0.05), (0.09, 0.14, 1.5), POLE, "wood", row=7)
        box(mesh, (x, 0.15, 0.78), (0.09, 0.14, 0.34), POLE, "wood", row=7, pitch=-32.0)
    for z in (-0.6, 0.0, 0.6):
        box(mesh, (0.0, 0.17, z), (0.86, 0.06, 0.09), POLE, "wood", row=4)
    for k in range(4):
        box(mesh, (-0.3 + k * 0.2, 0.215, -0.05), (0.19, 0.03, 1.45), BOARD, "wood", row=(k * 3) % ROWS)
    return mesh


SLEDGE_DECK = 0.23


def trestle():
    """A sawbuck: two crossed pairs joined by a stretcher. A log lies in the crotches, `TRESTLE_REST` above the ground."""
    mesh = Mesh()
    for z in (-0.28, 0.28):
        pole(mesh, (-0.42, -0.05, z), (0.3, 0.95, z), 0.04, POLE, row=6)
        pole(mesh, (0.42, -0.05, z), (-0.3, 0.95, z), 0.04, POLE, row=6)
        lash(mesh, (0.0, 0.53, z), (0.0, 0.0, 1.0), 0.06)
    pole(mesh, (0.2, 0.22, -0.34), (0.2, 0.22, 0.34), 0.03, POLE, row=6)
    return mesh


TRESTLE_REST = 0.62       # the height of a 0.15 m log's underside in the crotch

SHORE_AT = (-8.365, 10.331)     # where the first trestle stands, in the stretch's frame
SHORE_TURN = 169.46            # degrees: the shore group's +x, turned from the wall's +x toward +z


def stool():
    mesh = Mesh()
    up = (0.0, 1.0, 0.0)
    rings = [ring(mesh, (0.0, 0.40, 0.0), up, 0.17, 8), ring(mesh, (0.0, 0.44, 0.0), up, 0.17, 8)]
    skin(mesh, rings, 8, BOARD, "wood", 2, 1)
    cap(mesh, rings[0], BOARD, "wood", 2, (0.0, 0.42, 0.0))
    cap(mesh, rings[1], BOARD, "wood", 2, (0.0, 0.42, 0.0))
    for k in range(3):
        a = 2.0 * math.pi * k / 3.0 + 0.4
        pole(mesh, (math.cos(a) * 0.2, 0.0, math.sin(a) * 0.2), (math.cos(a) * 0.09, 0.41, math.sin(a) * 0.09), 0.022, POLE, row=6)
    return mesh


def fire_basket():
    """A cold iron fire basket on three legs."""
    mesh = Mesh()
    bars = 10
    for k in range(bars):
        a = 2.0 * math.pi * k / bars
        lo = (math.cos(a) * 0.16, 0.26, math.sin(a) * 0.16)
        hi = (math.cos(a) * 0.24, 0.62, math.sin(a) * 0.24)
        pole(mesh, lo, hi, 0.011, IRON, sides=4, uv_kind="iron", caps=False)
    for y, r in ((0.26, 0.16), (0.44, 0.2), (0.62, 0.24)):
        for k in range(bars):
            a, b = 2.0 * math.pi * k / bars, 2.0 * math.pi * (k + 1) / bars
            pole(mesh, (math.cos(a) * r, y, math.sin(a) * r), (math.cos(b) * r, y, math.sin(b) * r), 0.012, IRON, sides=4, uv_kind="iron", caps=False)
    for k in range(3):
        a = 2.0 * math.pi * k / 3.0
        pole(mesh, (math.cos(a) * 0.24, 0.0, math.sin(a) * 0.24), (math.cos(a) * 0.15, 0.27, math.sin(a) * 0.15), 0.014, IRON, sides=4, uv_kind="iron")
    box(mesh, (0.0, 0.255, 0.0), (0.26, 0.012, 0.26), IRON, "iron", yaw=20.0)
    return mesh


def mud_boards(rng):
    """Four loose boards laid on the mud to walk on."""
    mesh = Mesh()
    for k in range(4):
        box(mesh, (-0.36 + k * 0.24 + rng.uniform(-0.02, 0.02), 0.02, rng.uniform(-0.08, 0.08)), (0.21, 0.035, rng.uniform(1.25, 1.5)),
            BOARD, "wood", row=(k * 3 + 1) % ROWS, yaw=rng.uniform(-4.0, 4.0))
    return mesh


def chips(rng):
    """Axe chips round a log being pointed."""
    mesh = Mesh()
    for _ in range(16):
        a, r = rng.uniform(0.0, 2.0 * math.pi), rng.uniform(0.1, 0.8)
        box(mesh, (math.cos(a) * r, 0.012, math.sin(a) * r), (rng.uniform(0.06, 0.16), 0.012, rng.uniform(0.03, 0.06)), NEW, "wood",
            row=rng.randrange(ROWS), yaw=rng.uniform(0.0, 180.0))
    return mesh


# --- Layout ----------------------------------------------------------------------------------------

def cap_col(a, b, r):
    return {"t": "capsule", "a": list(a), "b": list(b), "r": r}


def box_col(c, s, yaw=0.0):
    return {"t": "box", "c": list(c), "s": list(s), "yaw": yaw}


def build(rng):
    meshes, parts = {}, []

    def place(name, mesh, **how):
        how.setdefault("fit", "ground")
        how.setdefault("pos", [0.0, 0.0, 0.0])
        how.setdefault("forward", [0.0, 0.0, 1.0])
        how.setdefault("roll", 0.0)
        how.setdefault("col", [])
        parts.append(dict(name=name, mesh=mesh, **how))

    # Stumps on the old line. The middle is torn out: that is where people step through.
    stump_at = (-2.54, -2.2, -1.84, -1.48, -1.14, 2.26, 2.52, 2.78)
    for i, x in enumerate(stump_at):
        r = LOG_RADII[i % len(LOG_RADII)]
        h = rng.uniform(0.3, 0.85)
        meshes["Stump_%d" % (i + 1)] = stump(rng, r, h, (i * 3) % ROWS)
        yaw = math.radians(rng.uniform(0.0, 360.0))
        place("Stump_%d" % (i + 1), "Stump_%d" % (i + 1), fit="surface", pos=[x, 0.0, rng.uniform(-0.04, 0.04)],
              forward=[math.sin(yaw), 0.0, math.cos(yaw)], col=[cap_col((0, -0.2, 0), (0, max(h - 0.1, 0.2), 0), r)])

    # The last logs of each end, out of line. They stand 0.9 m in the ground.
    leaning = (("Lean_W1", -3.42, (0.0, 0.992, 0.125), 5.9, 0), ("Lean_W2", -3.08, (0.03, 0.99, 0.135), 5.5, 1),
               ("Lean_W3", -2.74, (0.33, 0.94, 0.06), 5.2, 2), ("Lean_E1", 3.12, (-0.03, 0.975, 0.22), 5.4, 3),
               ("Lean_E2", 3.46, (0.0, 0.985, 0.17), 5.8, 4))
    for name, x, direction, length, k in leaning:
        r = LOG_RADII[k]
        meshes[name] = old_log(rng, length, r, (k * 2) % ROWS, "butt", "point", buried=0.9)
        place(name, name, fit="surface", pos=[x, 0.0, 0.0], forward=list(unit(direction)), roll=rng.uniform(0.0, 360.0),
              col=[cap_col((0, 0, -0.5), (0, 0, length - 0.3), r)])

    meshes["Pole"] = unit_pole(0.06)
    pole_col = [cap_col((0, 0, 0), (0, 0, 1), 0.06)]
    # A rail still tied to the west end, its other end on the ground; props under the logs that lean most.
    place("HangingRail", "Pole", fit="strut", on="surface", a=[-3.3, 3.5, -0.2], b=[-1.25, 0.2, -0.55], col=pole_col)
    place("Prop_W", "Pole", fit="strut", a=[-1.05, -0.2, 0.75], b=[-1.72, 3.0, 0.12], col=pole_col)
    place("Prop_E1", "Pole", fit="strut", a=[2.95, -0.2, 2.5], b=[3.06, 3.3, 0.95], col=pole_col)
    place("Prop_E2", "Pole", fit="strut", a=[3.9, -0.2, 2.35], b=[3.46, 3.1, 0.75], col=pole_col)

    # The closure, on the camp side of the stumps.
    meshes["RopeLine"] = rope_line(rng)
    place("RopeLine", "RopeLine", fit="surface", pos=[0.0, 0.0, -0.45],
          col=[cap_col((-ROPE_HALF, -0.2, 0), (-ROPE_HALF, ROPE_HEIGHT, 0), 0.045), cap_col((ROPE_HALF, -0.2, 0), (ROPE_HALF, ROPE_HEIGHT, 0), 0.045)])

    # Fallen into the camp over the rocks: two broken lengths and the footing stones.
    meshes["OldHalf_A"] = old_log(rng, 3.2, LOG_RADII[0], 1, "snap", "point")
    meshes["OldHalf_B"] = old_log(rng, 2.8, LOG_RADII[3], 5, "snap", "butt")
    meshes["OldHalf_C"] = old_log(rng, 3.4, LOG_RADII[2], 7, "butt", "snap")
    half_col = lambda length, r: [cap_col((0, 0, 0.1), (0, 0, length - 0.1), r)]
    place("Fallen_In1", "OldHalf_A", fit="span", on="surface", a=[-0.8, -0.3], b=[-1.2, -3.2], lift=0.13, roll=40.0, col=half_col(3.2, LOG_RADII[0]))
    place("Fallen_In2", "OldHalf_B", fit="span", on="surface", a=[0.5, -0.5], b=[1.3, -2.9], lift=0.13, roll=200.0, col=half_col(2.8, LOG_RADII[3]))
    stone_sizes = ((0.55, 0.38, 0.45), (0.8, 0.5, 0.6), (0.4, 0.3, 0.36), (0.65, 0.42, 0.7))
    for i, size in enumerate(stone_sizes):
        meshes["FootStone_%d" % (i + 1)] = foot_stone(rng, size)
    spilled = ((-1.9, -1.6), (-0.9, -2.4), (-0.2, -1.3), (0.6, -2.2), (1.5, -1.5), (2.1, -2.7), (-1.4, -3.3), (1.0, -3.4),
               (-2.6, 0.55), (2.75, 0.4), (2.45, 0.95))
    for i, (x, z) in enumerate(spilled):
        k = i % len(stone_sizes)
        yaw = rng.uniform(0.0, 360.0)
        size = stone_sizes[k]
        place("Spilled_%02d" % (i + 1), "FootStone_%d" % (k + 1), fit="surface", pos=[x, -0.08, z],
              forward=[math.sin(math.radians(yaw)), rng.uniform(-0.12, 0.12), math.cos(math.radians(yaw))], roll=rng.uniform(-10.0, 10.0),
              col=[box_col((0, size[1] * 0.5, 0), size)])

    # The work on the shelf outside. Kept west and east of the walk from the gap to the warehouse ramp
    # (2.5 m wide: x -0.45 .. 2.05 at z 0.6 .. 4.8) and south of the walk from the ramp to the dock (z above 4.8).
    # Tests/EditMode/BrokenStretchTests holds the same rectangles and fails when a solid part reaches into one.
    meshes["Scaffold"] = scaffold(rng)
    hx, hz = SCAFFOLD_X, SCAFFOLD_Z
    place("Scaffold", "Scaffold", pos=[-3.6, 0.0, 1.2],
          col=[box_col((0, SCAFFOLD_DECK, 0), (2.0 * hx + 0.5, 0.06, 2.0 * hz)),
               cap_col((-hx, -0.3, -hz), (-hx, SCAFFOLD_TOP - 0.3, -hz), 0.05), cap_col((hx, -0.3, -hz), (hx, SCAFFOLD_TOP - 0.3, -hz), 0.05),
               cap_col((-hx, -0.3, hz), (-hx, SCAFFOLD_TOP - 0.3, hz), 0.05), cap_col((hx, -0.3, hz), (hx, SCAFFOLD_TOP - 0.3, hz), 0.05)])
    meshes["ShearLegs"] = shear_legs()
    place("ShearLegs", "ShearLegs", pos=[-1.9, 0.0, 2.0],   # west of the walk: its legs lean, and would narrow it at head height
          col=[cap_col((-SHEAR_FOOT, 0, 0), (SHEAR_APEX[0] - 0.05, SHEAR_APEX[1], SHEAR_APEX[2]), 0.07),
               cap_col((SHEAR_FOOT, 0, 0), (SHEAR_APEX[0] + 0.05, SHEAR_APEX[1], SHEAR_APEX[2]), 0.07)])
    # Old lengths dragged aside, and the stone for the new footing with the sledge that brought it.
    place("Dragged_1", "OldHalf_C", fit="span", on="ground", a=[-4.2, 2.62], b=[-0.85, 2.78], lift=0.14, roll=80.0, col=half_col(3.4, LOG_RADII[2]))
    place("Dragged_2", "OldHalf_B", fit="span", on="ground", a=[-0.9, 3.08], b=[-3.65, 2.98], lift=0.14, roll=300.0, col=half_col(2.8, LOG_RADII[3]))
    block = (0.5, 0.36, 0.42)
    for i in range(3):
        meshes["QuarryBlock_%d" % (i + 1)] = quarry_block(rng, (block[0] + rng.uniform(-0.05, 0.05), block[1], block[2] + rng.uniform(-0.04, 0.04)))
    pile = ((-3.3, 0.0, 3.72), (-2.74, 0.0, 3.7), (-2.18, 0.0, 3.74), (-3.0, 0.36, 3.72), (-2.44, 0.36, 3.7), (-3.25, 0.0, 4.2), (-2.6, 0.0, 4.18))
    for i, (x, y, z) in enumerate(pile):
        yaw = rng.uniform(-7.0, 7.0)
        place("Quarry_%02d" % (i + 1), "QuarryBlock_%d" % (i % 3 + 1), pos=[x, y, z], forward=[math.sin(math.radians(yaw)), 0.0, math.cos(math.radians(yaw))],
              col=[box_col((0, block[1] * 0.5, 0), block)])
    meshes["Sledge"] = sledge()
    sledge_yaw = math.radians(72.0)
    sledge_forward = [math.sin(sledge_yaw), 0.0, math.cos(sledge_yaw)]
    place("Sledge", "Sledge", pos=[-1.55, 0.0, 3.9], forward=sledge_forward, col=[box_col((0, 0.13, 0), (0.86, 0.26, 1.7))])
    place("Quarry_OnSledge", "QuarryBlock_2", pos=[-1.6, SLEDGE_DECK, 3.88], forward=sledge_forward, col=[box_col((0, block[1] * 0.5, 0), block)])
    # The watch: a stool and a cold fire basket by the east end.
    meshes["Stool"] = stool()
    place("Stool", "Stool", pos=[3.05, 0.0, 1.05], col=[box_col((0, 0.22, 0), (0.34, 0.44, 0.34))])
    meshes["FireBasket"] = fire_basket()
    place("FireBasket", "FireBasket", pos=[2.65, 0.0, 1.6], col=[cap_col((0, 0.1, 0), (0, 0.6, 0), 0.24)])
    # Boards over the mud along the walk from the gap to the ramp.
    meshes["MudBoards"] = mud_boards(rng)
    for i, (x, z, yaw) in enumerate(((0.75, 0.95, 4.0), (0.85, 2.3, -6.0), (0.95, 3.65, 3.0))):
        place("MudBoards_%d" % (i + 1), "MudBoards", pos=[x, 0.0, z], forward=[math.sin(math.radians(yaw)), 0.0, math.cos(math.radians(yaw))])

    # Down on the shore: the new logs as they came off the river, and the one being pointed. Laid out along +x from
    # the first trestle, then turned and moved as one group to where Valentin put it by hand on 2026-11-26: the west
    # shore, clear of the dock's other entrance.
    turn = math.radians(SHORE_TURN)

    def shore(x, z):
        dx, dz = x - 1.7, z - 10.55
        return [SHORE_AT[0] + math.cos(turn) * dx - math.sin(turn) * dz, SHORE_AT[1] + math.sin(turn) * dx + math.cos(turn) * dz]

    def shore_pos(x, y, z):
        at = shore(x, z)
        return [at[0], y, at[1]]

    def shore_dir(x, z):
        return [math.cos(turn) * x - math.sin(turn) * z, 0.0, math.sin(turn) * x + math.cos(turn) * z]

    meshes["Bearer"] = bearer()
    for i, x in enumerate((1.5, 5.3)):
        place("Bearer_%d" % (i + 1), "Bearer", pos=shore_pos(x, 0.0, 8.9), forward=shore_dir(0.0, 1.0))
    new_len = 6.2
    for i in range(3):
        meshes["NewLog_%d" % (i + 1)] = new_log(rng, new_len, LOG_RADII[(i * 2) % len(LOG_RADII)], (i * 4) % ROWS)
    stack = ((8.45, 0.27, 0, False), (8.8, 0.27, 1, True), (9.15, 0.27, 2, False), (8.62, 0.54, 2, True), (8.98, 0.54, 0, False))
    for i, (z, lift, k, flip) in enumerate(stack):
        ends = (shore(0.25 + i * 0.07, z), shore(0.25 + i * 0.07 + new_len, z + 0.04))
        a, b = (ends[1], ends[0]) if flip else ends
        place("NewLog_Stack_%d" % (i + 1), "NewLog_%d" % (k + 1), fit="span", on="ground", a=a, b=b, lift=lift, roll=rng.uniform(0.0, 360.0),
              col=half_col(new_len, 0.15))
    meshes["Trestle"] = trestle()
    for i, x in enumerate((1.7, 5.1)):
        place("Trestle_%d" % (i + 1), "Trestle", pos=shore_pos(x, 0.0, 10.55), forward=shore_dir(1.0, 0.0),
              col=[box_col((0, 0.45, 0), (0.9, 0.9, 0.7))])
    place("NewLog_OnTrestles", "NewLog_2", fit="span", on="ground", a=shore(0.3, 10.55), b=shore(0.3 + new_len, 10.6), lift=TRESTLE_REST + 0.15, roll=15.0,
          col=half_col(new_len, 0.15))
    meshes["Chips"] = chips(rng)
    place("Chips", "Chips", pos=shore_pos(6.3, 0.0, 10.7), forward=shore_dir(0.0, 1.0))
    return meshes, parts


def materials():
    out = {}
    assets = _find_up(os.path.dirname(os.path.abspath(__file__)), "Assets")
    wood = os.path.join(assets, "Hivemind", "HDRP(Default)", "MedievalFantasyVillage", "Art", "Textures", "T_ModularWood_Basecolor.png") if assets else ""
    look = {OLD: (wood, (0.95, 0.95, 0.95, 1.0)), NEW: (wood, (2.6, 2.2, 1.5, 1.0)), POLE: (wood, (1.3, 1.2, 1.05, 1.0)), BOARD: (wood, (1.2, 1.15, 1.05, 1.0)),
            ROPE: ("", (0.3, 0.25, 0.18, 1.0)), STONE: ("", (0.42, 0.42, 0.4, 1.0)), IRON: ("", (0.08, 0.08, 0.08, 1.0)), RAG: ("", (0.3, 0.09, 0.07, 1.0))}
    for name in MATERIALS:
        path, tint = look[name]
        m = bpy.data.materials.get("BS_" + name) or bpy.data.materials.new("BS_" + name)
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
        out[name] = m
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
    meshes, parts = build(rng)

    old = bpy.data.collections.get(NAME)
    kept = set(old.objects) if old else set()
    for part in meshes:   # checked before anything is deleted
        clash = bpy.data.objects.get(part)
        if clash and clash not in kept:
            raise RuntimeError("An object called '%s' already exists in this Blender session; rename it first" % part)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    for stale in list(bpy.data.meshes):
        if stale.name.split(".")[0] in meshes and not any(o.data == stale for o in bpy.data.objects):
            bpy.data.meshes.remove(stale)
    col = bpy.data.collections.new(NAME)
    bpy.context.scene.collection.children.link(col)
    mats = materials()
    objects = [mesh.to_object(name, mats, col) for name, mesh in meshes.items()]

    per_mesh = {name: mesh.triangles() for name, mesh in meshes.items()}
    placed = sum(per_mesh[p["mesh"]] for p in parts)
    facts = {
        "version": VERSION,
        "meshes": {name: {"materials": mesh.used(), "triangles": per_mesh[name]} for name, mesh in meshes.items()},
        "parts": parts,
        # Across the gap, where no agent may path: the strip from the rocks' edge to just outside the old line.
        "notWalkable": {"centre": [0.0, -0.75], "size": [7.8, 2.9]},
        "triangles": placed,
    }
    # the parts file and the FBX go out together, before the .blend: the two must never be a version apart
    with open(os.path.join(out, NAME + ".json"), "w") as f:
        json.dump(facts, f, indent=1)
    export(objects, os.path.join(out, NAME + ".fbx"))
    for o in objects:
        o.name = NAME + "_" + o.name
    bpy.data.libraries.write(os.path.join(source, NAME + ".blend"), set(objects) | set(mats.values()), fake_user=True)
    return {"meshes": len(meshes), "parts": len(parts), "triangles": placed}


if __name__ == "__main__":
    result = main()
    print("broken stretch:", result)
