"""Home yard belongings: the owned huts' small things the kit lacks (spec-area-1-home-yard-huts-and-yard).

Run headless:  blender -b -P build_belongings.py
or in a session: runpy.run_path(path, run_name="__main__")

Writes, beside Source~:
  Flail.fbx        Gerrit's threshing flail on two wall pegs. Origin at the wall face between the pegs; the staff runs
                   along X, the pegs go into the wall along -Y (Blender), the swingle hangs down from the +X end.
  TallyBoard.fbx   Bren's notched tally board on a cord loop. Origin at the peg (top of the loop); hangs down -Z.
  RolledBlanket.fbx  Odo's blanket, rolled and tied with two cords. Origin at the bottom of the roll, roll along X.
  StoneBlock.fbx   A half-shaped quarry block with a chisel on top and chips around it. Origin at the block's bottom
                   centre; chips lie on z = 0.
  Rag.fbx          A worn rag folded over a peg. Origin at the peg (top of the fold); hangs down -Z, flat in X-Z.
  Plank.fbx        A loose split plank, 1.2 m along X. Origin at its bottom middle.
  Boards.fbx       Four split planks on two battens, 1.1 × 0.68 m, laid over mud. Origin at the ground, middle.
  PegRail.fbx      A 0.7 m board with three pegs, on a wall. Origin at the wall face; pegs out along -Y.
  (The last three replace kit planks of about 3,300-3,900 triangles each.)
and Source~/Belongings.blend (result only).

Materials (remapped in Unity to the M_Camp_* of the same role): Belong_Wood -> M_Camp_RoughWood,
Belong_Rope -> M_Camp_Rope, Belong_Wool -> M_Camp_WoolGrey, Belong_Rubble -> M_Camp_Rubble,
Belong_Iron -> M_Camp_Iron, Belong_Rag -> M_Camp_Rag.
UVs: box-projected at 1 m per tile (Camp/CLAUDE.md -> Custom pieces); iron faces are squeezed into the iron atlas's
plain patch (IRON_UV, the same patch build_warehouse.py uses).
"""
import math
import os
import random

import bmesh
import bpy

IRON_UV = (0.04, 0.17, 0.42, 0.055)  # u, v, width, height of T_Iron's plain patch (build_warehouse.py)
MATERIALS = ["Belong_Wood", "Belong_Rope", "Belong_Wool", "Belong_Rubble", "Belong_Iron", "Belong_Rag"]
WOOD, ROPE, WOOL, RUBBLE, IRON, RAG = range(6)
PIECES = ["Flail", "TallyBoard", "RolledBlanket", "StoneBlock", "Rag", "Plank", "Boards", "PegRail"]


# ---------------------------------------------------------------- mesh helpers


class Mesh:
    """Triangles and quads with a material index each, built in metres."""

    def __init__(self):
        self.verts = []
        self.faces = []  # (indices, material)

    def v(self, p):
        self.verts.append(tuple(p))
        return len(self.verts) - 1

    def face(self, ids, mat):
        self.faces.append((list(ids), mat))

    def triangles(self):
        return sum(len(ids) - 2 for ids, _ in self.faces)

    def cylinder(self, a, b, r, mat, sides=8, caps=True, r_b=None):
        """A cylinder from point a to point b."""
        r_b = r if r_b is None else r_b
        ax = [b[i] - a[i] for i in range(3)]
        length = math.sqrt(sum(c * c for c in ax))
        d = [c / length for c in ax]
        helper = (0, 0, 1) if abs(d[2]) < 0.9 else (1, 0, 0)
        u = _norm(_cross(d, helper))
        w = _cross(d, u)
        ring_a, ring_b = [], []
        for i in range(sides):
            t = 2 * math.pi * i / sides
            off = [math.cos(t) * u[k] + math.sin(t) * w[k] for k in range(3)]
            ring_a.append(self.v([a[k] + off[k] * r for k in range(3)]))
            ring_b.append(self.v([b[k] + off[k] * r_b for k in range(3)]))
        for i in range(sides):
            j = (i + 1) % sides
            self.face([ring_a[i], ring_a[j], ring_b[j], ring_b[i]], mat)
        if caps:
            self.face(list(reversed(ring_a)), mat)
            self.face(ring_b, mat)

    def box(self, centre, size, mat, jitter=0.0, rng=None):
        cx, cy, cz = centre
        sx, sy, sz = (s / 2 for s in size)
        ids = []
        for z in (-sz, sz):
            for y in (-sy, sy):
                for x in (-sx, sx):
                    j = [(rng.uniform(-jitter, jitter) if rng else 0.0) for _ in range(3)]
                    ids.append(self.v((cx + x + j[0], cy + y + j[1], cz + z + j[2])))
        q = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
        for f in q:
            self.face([ids[i] for i in f], mat)

    def to_object(self, name, mats, collection):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.verts, [], [ids for ids, _ in self.faces])
        for m in mats:
            me.materials.append(m)
        for poly, (_, mat) in zip(me.polygons, self.faces):
            poly.material_index = mat
        me.validate()
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            axis = max(range(3), key=lambda k: abs(n[k]))
            for loop in f.loops:
                p = loop.vert.co
                if axis == 0:
                    coord = (p.y, p.z)
                elif axis == 1:
                    coord = (p.x, p.z)
                else:
                    coord = (p.x, p.y)
                if f.material_index == IRON:  # squeeze into the atlas's plain patch
                    coord = (IRON_UV[0] + (coord[0] % 1.0) * IRON_UV[2], IRON_UV[1] + (coord[1] % 1.0) * IRON_UV[3])
                loop[uv].uv = coord
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new(name, me)
        collection.objects.link(obj)
        return obj


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _norm(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v)


# ---------------------------------------------------------------- pieces


def flail():
    m = Mesh()
    staff_len, staff_r = 1.45, 0.017
    y = -0.07  # staff in front of the wall face
    # pegs into the wall (-Y is out of the wall here: the pegs run from the wall face y = 0 to the front)
    for x in (-0.45, 0.45):
        m.cylinder((x, 0.0, 0.0), (x, y - 0.04, 0.01), 0.012, WOOD, sides=6)
    m.cylinder((-staff_len / 2, y, 0.025), (staff_len / 2, y, 0.025), staff_r, WOOD, sides=8)
    # the swingle hangs from a leather thong at the +X end, slightly out from the wall
    top = (staff_len / 2 - 0.02, y - 0.01, -0.02)
    m.cylinder((staff_len / 2 - 0.01, y, 0.025), top, 0.006, ROPE, sides=5)
    m.cylinder(top, (staff_len / 2 + 0.02, y - 0.06, -0.58), 0.022, WOOD, sides=8, r_b=0.025)
    return m


def tally_board():
    m = Mesh()
    width, height, thick = 0.075, 0.5, 0.018
    top = -0.07  # board top below the peg; the cord loop runs up to the peg (origin)
    notch_every, depth = 0.03, 0.012
    rng = random.Random(7)
    # outline: left edge down with notches, right edge up with notches (counter-clockwise seen from the front, -Y)
    pts = []
    z = top
    left = []
    while z > top - height + 0.04:
        left.append((-width / 2, z))
        if z < top - 0.05 and rng.random() < 0.8:
            left.append((-width / 2 + depth, z - notch_every * 0.4))
        z -= notch_every
    left.append((-width / 2, top - height))
    right = []
    z = top - height
    while z < top - 0.04:
        right.append((width / 2, z))
        if z > top - height + 0.05 and rng.random() < 0.6:
            right.append((width / 2 - depth, z + notch_every * 0.4))
        z += notch_every
    right.append((width / 2, top))
    pts = left + right
    front = [m.v((x, -thick / 2, z)) for x, z in pts]
    back = [m.v((x, thick / 2, z)) for x, z in pts]
    m.face(front, WOOD)
    m.face(list(reversed(back)), WOOD)
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        m.face([front[i], back[i], back[j], front[j]], WOOD)
    # cord loop: two strands from the board's top corners up to the peg
    m.cylinder((-0.02, 0.0, top), (0.0, 0.0, 0.0), 0.004, ROPE, sides=4)
    m.cylinder((0.02, 0.0, top), (0.0, 0.0, 0.0), 0.004, ROPE, sides=4)
    return m


def rolled_blanket():
    m = Mesh()
    r, half = 0.1, 0.36
    sides = 14
    m.cylinder((-half, 0, r), (half, 0, r), r, WOOL, sides=sides)
    # the loose end of the roll lies along the bottom, a flat lip
    m.box((0.0, r * 0.75, 0.012), (half * 2 - 0.02, 0.09, 0.02), WOOL)
    for x in (-0.22, 0.22):
        m.cylinder((x - 0.008, 0, r), (x + 0.008, 0, r), r + 0.006, ROPE, sides=sides)
    return m


def stone_block():
    m = Mesh()
    rng = random.Random(11)
    # a squared top (worked) on a rough bottom: two boxes of rubble, the lower one jittered
    m.box((0, 0, 0.12), (0.42, 0.34, 0.24), RUBBLE, jitter=0.025, rng=rng)
    m.box((0.01, 0.0, 0.27), (0.38, 0.31, 0.07), RUBBLE, jitter=0.006, rng=rng)
    # chisel lying across the top, iron
    m.cylinder((-0.12, -0.03, 0.315), (0.09, 0.05, 0.315), 0.009, IRON, sides=6, r_b=0.006)
    # chips round the block, on the ground
    for i in range(9):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(0.28, 0.45)
        s = rng.uniform(0.02, 0.045)
        m.box((math.cos(a) * d, math.sin(a) * d, s / 3), (s, s * 0.8, s / 1.5), RUBBLE, jitter=s * 0.2, rng=rng)
    return m


def rag():
    """Folded over a peg at the origin: a short flap behind (+Y side, against the wall), the long side in front."""
    m = Mesh()
    rng = random.Random(3)
    width, front_len, back_len = 0.26, 0.5, 0.18
    cols, rows = 5, 9
    thick = 0.004

    def strip(length, y_side, rows_, sway):
        grid = []
        for r_ in range(rows_ + 1):
            t = r_ / rows_
            z = -t * length
            row = []
            for c in range(cols + 1):
                x = -width / 2 + width * c / cols
                ragged = (rng.uniform(-0.05, 0.0) if r_ == rows_ else 0.0)
                wave = math.sin(c * 1.7 + r_ * 0.9) * 0.008 * t
                row.append((x * (1 - 0.15 * t), y_side * (0.012 + sway * t) + wave, z + ragged))
            grid.append(row)
        return grid

    for grid, flip in ((strip(front_len, -1, rows, 0.02), False), (strip(back_len, 1, 3, 0.01), True)):
        for off in (-thick / 2, thick / 2):
            ids = [[m.v((p[0], p[1] + off, p[2])) for p in row] for row in grid]
            for r_ in range(len(ids) - 1):
                for c in range(cols):
                    quad = [ids[r_][c], ids[r_][c + 1], ids[r_ + 1][c + 1], ids[r_ + 1][c]]
                    if (off > 0) != flip:
                        quad.reverse()
                    m.face(quad, RAG)
    return m


def _split_plank(m, x0, x1, y, z, width, thick, rng):
    """One split plank along X from x0 to x1, centred on (y, z) bottom: ragged ends, a slight twist in its width."""
    w0 = width * rng.uniform(0.9, 1.05)
    w1 = width * rng.uniform(0.85, 1.0)
    e0 = rng.uniform(-0.02, 0.02)
    e1 = rng.uniform(-0.02, 0.02)
    corners = [(x0 + e0, y - w0 / 2), (x1 + e1, y - w1 / 2), (x1 - e1, y + w1 / 2), (x0 - e0, y + w0 / 2)]
    bottom = [m.v((cx, cy, z)) for cx, cy in corners]
    top = [m.v((cx, cy, z + thick)) for cx, cy in corners]
    m.face(list(reversed(bottom)), WOOD)
    m.face(top, WOOD)
    for i in range(4):
        j = (i + 1) % 4
        m.face([bottom[i], bottom[j], top[j], top[i]], WOOD)


def plank():
    """A loose split plank, 1.2 m along X, 0.18 m wide, 3.5 cm thick. Origin at its bottom middle."""
    m = Mesh()
    _split_plank(m, -0.6, 0.6, 0.0, 0.0, 0.18, 0.035, random.Random(21))
    return m


def boards():
    """Boards laid over mud: four split planks 1.1 m along X on two battens. Origin at the ground, middle."""
    m = Mesh()
    rng = random.Random(5)
    for x in (-0.38, 0.38):
        m.box((x, 0.0, 0.02), (0.07, 0.68, 0.04), WOOD, jitter=0.005, rng=rng)
    for i, y in enumerate((-0.24, -0.08, 0.08, 0.24)):
        _split_plank(m, -0.55 + rng.uniform(-0.04, 0.04), 0.55 + rng.uniform(-0.04, 0.04), y, 0.04, 0.15, 0.03, rng)
    return m


def peg_rail():
    """A board on a wall with three round pegs. Origin at the wall face, board's middle; pegs out along -Y (Blender)."""
    m = Mesh()
    m.box((0.0, -0.012, 0.0), (0.7, 0.024, 0.09), WOOD, jitter=0.003, rng=random.Random(9))
    for x in (-0.24, 0.0, 0.24):
        m.cylinder((x, -0.024, 0.0), (x, -0.1, 0.012), 0.011, WOOD, sides=6)
    return m


BUILDERS = {"Flail": flail, "TallyBoard": tally_board, "RolledBlanket": rolled_blanket,
            "StoneBlock": stone_block, "Rag": rag, "Plank": plank, "Boards": boards, "PegRail": peg_rail}


# ---------------------------------------------------------------- materials, export


def materials():
    tints = {"Belong_Wood": (0.42, 0.33, 0.24, 1), "Belong_Rope": (0.45, 0.38, 0.27, 1),
             "Belong_Wool": (0.36, 0.38, 0.36, 1), "Belong_Rubble": (0.48, 0.47, 0.45, 1),
             "Belong_Iron": (0.2, 0.2, 0.21, 1), "Belong_Rag": (0.55, 0.5, 0.43, 1)}
    out = []
    for name in MATERIALS:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.diffuse_color = tints[name]
        out.append(mat)
    return out


def export(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, use_tspace=False)


def main():
    source = os.path.dirname(os.path.abspath(__file__))
    out = os.path.dirname(source)
    name = "Belongings"
    old = bpy.data.collections.get(name)
    kept = set(old.objects) if old else set()
    for piece in PIECES:  # checked before anything is deleted
        clash = bpy.data.objects.get(piece)
        if clash and clash not in kept:
            raise RuntimeError("An object called '%s' already exists in this Blender session; rename it first" % piece)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    mats = materials()
    report = {}
    for piece in PIECES:
        mesh = BUILDERS[piece]()
        obj = mesh.to_object(piece, mats, col)
        export([obj], os.path.join(out, piece + ".fbx"))
        report[piece] = mesh.triangles()
    # result only: no scene, the objects and materials (append them to look)
    bpy.data.libraries.write(os.path.join(source, name + ".blend"), set(col.objects) | set(mats), fake_user=True)
    print("BELONGINGS", report)
    return report


if __name__ == "__main__":
    main()
