"""Worker hut shell, built board by board.

Run in Blender (5.x):  blender -b -P build_worker_hut.py
or from a session:      runpy.run_path(r"...\\build_worker_hut.py", run_name="__main__")
(with any other run_name nothing is built: call the returned dict's ["main"]() yourself)

Writes one FBX per entry of VARIANTS beside the Source~ folder, and WorkerHut.blend in it.

Everything below is written in the Unity frame of the prefab (see Prefabs/World/Huts/CLAUDE.md):
x right, y up, +z the front doorway, origin at the floor centre. `to_blender` converts each vertex, so the
FBX (exported -Z forward, Y up, Apply Transform) arrives in Unity in that same frame.

UVs: T_ModularWood holds ten planks per tile, stacked in V. Every board is mapped onto ONE of them (grain
along the board, a random shift along it), at 2 m per tile on both axes, so all boards share one grain
scale and each carries a plank's own dark edges. One plank is TILE / ROWS = 0.2 m: no board is wider.
"""
import math
import os
import random

import bpy

# --- Shell contract (keep in step with WorkerHutKitBuilder) ---------------------------------------
HALF_WIDTH = 1.5          # wall centre line
EAVE = 2.5
PITCH = math.radians(28.0)
OVERHANG = 0.3
INSET = 0.22              # centre line to the inner wall face: the room is 2.56 m across
DOOR = (0.0, 1.05, 2.14)  # min x, max x, height (front wall)
DOOR_MIRRORED = (-DOOR[1], -DOOR[0], DOOR[2])  # the same doorway on the other half of the front wall
TIGHT_OVERHANG = 0.09     # packed rows: the roof ends no further out than the graybox roof did (1.70 m)
WINDOW = (-0.45, 0.4, 1.05, 1.95)  # min x, max x, sill, top (rear wall)
ROOF_LIFT = 0.02          # roof underside above the eave line at the wall centre line

# --- Construction ---------------------------------------------------------------------------------
BOARD = 0.035             # wall board thickness
POST = 0.14
ROOF_BOARD = 0.03
GROUND = -0.1             # boards start below the floor line: the hut stands on uneven terrain
POST_FOOT = -0.3
TILE = 2.0                # metres per texture tile
ROWS = 10                 # planks per tile
MAX_BOARD = TILE / ROWS    # a wider board would show the next plank's seam

WOOD, FRAME = 0, 1
TAN = math.tan(PITCH)
COS = math.cos(PITCH)
SIN = math.sin(PITCH)
INNER = HALF_WIDTH - INSET            # 1.28
POST_IN = INNER + BOARD               # posts stand outside the boards
POST_OUT = POST_IN + POST


def to_blender(p):
    return (-p[0], -p[2], p[1])


def roof_under(x):
    """Height of the roof boards' underside above local x."""
    return EAVE + (HALF_WIDTH - abs(x)) * TAN + ROOF_LIFT


class Shell:
    def __init__(self, seed, door=DOOR, overhang=OVERHANG):
        self.rng = random.Random(seed)
        self.door = door
        self.overhang = overhang
        self.verts, self.faces, self.uvs, self.mats, self.smooth = [], [], [], [], []

    # -- primitives --------------------------------------------------------------------------------
    def hexa(self, corners, local, grain, mat):
        """Eight corners ordered by (sx, sy, sz) in (-,-,-), (+,-,-), (-,+,-), (+,+,-), (-,-,+)...;
        `local` gives each corner's coordinates in the board's own frame (metres) for the UVs."""
        rng = self.rng
        row = (rng.randrange(ROWS) + 0.5) / ROWS
        shift = rng.random()
        flip = rng.choice((-1.0, 1.0))
        centre = [sum(c[i] for c in corners) / 8.0 for i in range(3)]
        base = len(self.verts)
        self.verts.extend(to_blender(c) for c in corners)
        centre_b = to_blender(centre)
        for axis in range(3):
            others = [a for a in range(3) if a != axis]
            for sign in (0, 1):
                idx = [i for i in range(8) if ((i >> axis) & 1) == sign]
                # order the four corners round the face
                b, c = others
                idx.sort(key=lambda i: (((i >> b) & 1), ((i >> c) & 1) ^ ((i >> b) & 1)))
                # outward winding, checked in Blender space (the conversion mirrors)
                p = [self.verts[base + i] for i in idx]
                n = _normal(p[0], p[1], p[2])
                mid = [sum(q[k] for q in p) / 4.0 for k in range(3)]
                if sum(n[k] * (mid[k] - centre_b[k]) for k in range(3)) < 0:
                    idx.reverse()
                if grain in others:
                    across = others[0] if others[1] == grain else others[1]
                    uv = [(shift + flip * local[i][grain] / TILE, row + local[i][across] / TILE) for i in idx]
                else:  # end grain: a small patch of the same plank
                    uv = [(shift + local[i][b] / TILE, row + local[i][c] / TILE) for i in idx]
                self.faces.append([base + i for i in idx])
                self.uvs.append(uv)
                self.mats.append(mat)
                self.smooth.append(False)

    def box(self, lo, hi, grain, mat, skew=None):
        """Axis-aligned board from `lo` to `hi`. `skew(corner) -> corner` may move corners afterwards."""
        mid = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
        corners, local = [], []
        for i in range(8):
            c = [hi[a] if (i >> a) & 1 else lo[a] for a in range(3)]
            if skew:
                c = skew(c)
            # taken after the skew, so a board cut to the roof line keeps the shared grain scale
            local.append([c[a] - mid[a] for a in range(3)])
            corners.append(c)
        self.hexa(corners, local, grain, mat)

    def framed(self, origin, ax, ay, az, half, grain, mat, ends=None):
        """Board in its own frame (unit axes ax, ay, az, half sizes). `ends[i]` shifts corner i along ax."""
        corners, local = [], []
        for i in range(8):
            l = [(half[a] if (i >> a) & 1 else -half[a]) for a in range(3)]
            if ends:
                l[0] += ends[i]
            local.append(l)
            corners.append([origin[k] + ax[k] * l[0] + ay[k] * l[1] + az[k] * l[2] for k in range(3)])
        self.hexa(corners, local, grain, mat)

    def rows(self, y0, y1):
        """Splits a span into boards 0.15-0.2 m wide (the last one or two take what is left, never wider)."""
        if y1 - y0 < 0.02:
            return []
        out, y = [], y0
        while y1 - y > 0.29:
            w = self.rng.uniform(0.15, MAX_BOARD)
            out.append((y, y + w))
            y += w
        if y1 - y > MAX_BOARD:
            half = (y1 - y) * 0.5
            out.append((y, y + half))
            y += half
        out.append((y, y1))
        return out

    # -- walls -------------------------------------------------------------------------------------
    def side_wall(self, side, hl):
        rng = self.rng
        run = hl - INSET + BOARD  # the side boards close the corner outside the end boards
        for ya, yb in self.rows(GROUND, roof_under(INNER + BOARD) - 0.005):
            out = rng.uniform(0.0, 0.006)
            # the inner face stays at or inside INNER, where the end boards stop: no slit in the corner
            x0, x1 = INNER - rng.uniform(0.0, 0.004), POST_IN + out
            self.box(_sx(side, x0, x1, ya, -run - rng.uniform(0, 0.06))[0],
                     _sx(side, x0, x1, yb, run + rng.uniform(0, 0.06))[1], 2, WOOD)
        # top plate on the posts, proud of the corners
        self.box(*_sx(side, POST_IN, POST_OUT, (EAVE - POST, EAVE), -hl - 0.08, hl + 0.08), 2, FRAME)
        posts = [0.0] if hl < 1.8 else [-hl / 3.0, hl / 3.0]
        for z in posts:
            z += rng.uniform(-0.08, 0.08)
            self.post(side * (POST_IN + POST * 0.5), z, EAVE - POST)

    def post(self, x, z, top, size=POST, plumb=False):
        h = size * 0.5
        lean = 0.0 if plumb else self.rng.uniform(-0.012, 0.012)  # door posts stay plumb: the opening is exact

        def skew(c):
            return [c[0] + (lean if c[1] > 0 else 0.0), c[1], c[2]]
        self.box((x - h, POST_FOOT, z - h), (x + h, top, z + h), 1, FRAME, skew)

    def end_wall(self, end, hl, holes):
        """Boards of an end wall (end = +1 front, -1 rear). holes: list of (x0, x1, y0, y1)."""
        rng = self.rng
        zi = hl - INSET
        cuts = sorted({GROUND, EAVE} | {min(EAVE, max(GROUND, h[2])) for h in holes} | {min(EAVE, h[3]) for h in holes})
        for ya, yb in zip(cuts, cuts[1:]):
            spans, x = [], -INNER
            for h in sorted(holes):
                if h[2] < yb - 1e-4 and h[3] > ya + 1e-4:
                    spans.append((x, h[0]))
                    x = h[1]
            spans.append((x, INNER))
            for xa, xb in spans:
                if xb - xa < 0.02:
                    continue
                for ra, rb in self.rows(ya, yb):
                    out = rng.uniform(0.0, 0.006)
                    z0, z1 = zi + out - 0.003, zi + BOARD + out
                    self.box(*_ez(end, xa, xb, ra, rb, z0, z1), 0, WOOD)
        # corner posts and the tie beam across them
        zp = zi + BOARD + POST * 0.5
        for side in (-1, 1):
            self.post(side * (POST_IN + POST * 0.5), end * zp, EAVE - POST)
        # between the corner posts, not across them: its ends would lie in the top plates' outer faces
        self.box(*_ez(end, -POST_IN, POST_IN, EAVE - 0.11, EAVE + 0.01, zi + BOARD, zi + BOARD + 0.12), 0, FRAME)
        # gable: vertical boards up to the roof, a joint at the ridge line
        edges = [-INNER] + [b for _, b in self.rows(-INNER, 0.0)]
        edges += [-e for e in reversed(edges[:-1])]
        for xa, xb in zip(edges, edges[1:]):
            out = rng.uniform(0.0, 0.006)
            z0, z1 = zi + out - 0.003, zi + BOARD + out
            lo, hi = _ez(end, xa, xb, EAVE, EAVE + 1.0, z0, z1)

            def skew(c, xa=xa, xb=xb):
                if c[1] > EAVE + 0.5:
                    return [c[0], roof_under(c[0]) - 0.004, c[2]]
                return c
            self.box(lo, hi, 1, WOOD, skew)

    def front(self, hl):
        x0, x1, top = self.door
        self.end_wall(1, hl, [(x0, x1, GROUND - 1.0, top)])
        zi = hl - INSET
        zp = zi + BOARD + POST * 0.5
        self.post(x0 - POST * 0.5, zp, EAVE - 0.11, plumb=True)
        self.post(x1 + POST * 0.5, zp, EAVE - 0.11, plumb=True)
        self.box((x0 - POST - 0.06, top, zi + BOARD), (x1 + POST + 0.06, top + 0.12, zi + BOARD + POST + 0.015), 0, FRAME)
        self.box((x0, GROUND, zi), (x1, 0.1, zi + 0.14), 0, FRAME)  # sill, visual only

    def rear(self, hl):
        x0, x1, sill, top = WINDOW
        self.end_wall(-1, hl, [(x0, x1, sill, top)])
        zi = hl - INSET
        zp = -(zi + BOARD + POST * 0.5)
        self.post(x0 - POST * 0.5, zp, EAVE - 0.11)
        self.post(x1 + POST * 0.5, zp, EAVE - 0.11)
        for ya, yb in ((sill - 0.08, sill), (top, top + 0.08)):
            self.box(*_ez(-1, x0, x1, ya, yb, zi, zi + BOARD + 0.1), 0, FRAME)
        # shutter: four boards and two battens, swung open against the wall toward +x
        hinge = (x1 + POST, 0.0, -(zi + BOARD + POST + 0.01))
        yaw = math.radians(13.0)
        along = (math.cos(yaw), 0.0, -math.sin(yaw))   # away from the hinge
        out = (-math.sin(yaw), 0.0, -math.cos(yaw))    # the panel's outer face
        up = (0.0, 1.0, 0.0)
        height = top - sill - 0.03
        x = 0.02
        for _ in range(4):
            w = self.rng.uniform(0.18, MAX_BOARD)
            o = [hinge[k] + along[k] * (x + w * 0.5) + up[k] * ((sill + top) * 0.5 + self.rng.uniform(-0.012, 0.012))
                 for k in range(3)]
            self.framed(o, up, along, out, (height * 0.5, w * 0.5 - 0.002, 0.012), 0, WOOD)
            x += w
        for y in (sill + 0.16, top - 0.16):
            o = [hinge[k] + along[k] * (x * 0.5 + 0.01) + up[k] * y + out[k] * 0.027 for k in range(3)]
            self.framed(o, along, up, out, (x * 0.5 - 0.02, 0.04, 0.015), 0, FRAME)

    # -- roof --------------------------------------------------------------------------------------
    def roof(self, hl):
        rng = self.rng
        length = (hl + self.overhang) * 2.0
        slope = (HALF_WIDTH + self.overhang) / COS
        # a short eave has no board to lose: one broken off would bare the top plate
        can_break = self.overhang >= 0.2
        peak = (0.0, roof_under(0.0), 0.0)
        for side in (-1, 1):
            down = (side * COS, -SIN, 0.0)
            normal = (side * SIN, COS, 0.0)
            ridge = (0.0, 0.0, 1.0)
            wide_gaps = 3  # at most three per slope
            spans = self.rows(-length * 0.5, length * 0.5)
            for n, (z, z_end) in enumerate(spans):
                # each board gives up its far edge to the gap; the last one runs to the barge board
                gap = rng.uniform(0.002, 0.006)
                if wide_gaps and abs(z_end) < hl - 0.4 and rng.random() < 0.22:
                    gap = rng.uniform(0.022, 0.038)  # sun stripes into the room
                    wide_gaps -= 1
                w = z_end - z - (gap if n < len(spans) - 1 else 0.0)
                reach = slope + rng.uniform(-0.09, 0.1)
                if rng.random() < 0.1:
                    short = rng.uniform(0.14, 0.26)  # a board that broke off short
                    if can_break:
                        reach -= short
                lift = rng.uniform(0.0, 0.007)
                mid = reach * 0.5 - 0.01
                o = [peak[k] + down[k] * mid + normal[k] * (ROOF_BOARD * 0.5 + lift) + ridge[k] * (z + w * 0.5)
                     for k in range(3)]
                # ragged lower end: the two sides of the board end at different lengths
                rag = rng.uniform(-0.035, 0.035)
                ends = [(rag if (i >> 2) & 1 else -rag) if i & 1 else 0.0 for i in range(8)]
                self.framed(o, down, normal, ridge, (reach * 0.5, ROOF_BOARD * 0.5, w * 0.5), 0, WOOD, ends)
            # purlins under the boards, resting on the gables; the lower one clear of the side wall's top boards
            for at in (0.28, 0.58):
                o = [peak[k] + down[k] * at * slope - normal[k] * 0.035 for k in range(3)]
                self.framed(o, ridge, down, normal, (hl - INSET + 0.02, 0.035, 0.035), 0, FRAME)
            # barge boards over the board ends at both gables
            for end in (-1, 1):
                reach = slope + 0.06
                o = [peak[k] + down[k] * (reach * 0.5 - (0.1 if side * end > 0 else 0.0)) + normal[k] * -0.03
                     + ridge[k] * end * (length * 0.5 + 0.012) for k in range(3)]
                self.framed(o, down, normal, ridge, (reach * 0.5 + (0.1 if side * end > 0 else 0.0), 0.075, 0.015),
                            0, FRAME)
        self.box((-0.045, peak[1] - 0.1, -(hl - INSET + 0.02)), (0.045, peak[1] - 0.005, hl - INSET + 0.02), 2, FRAME)
        self.ridge_log(peak[1] + ROOF_BOARD / COS + 0.035, length + 0.16)

    def ridge_log(self, y, length):
        """A bark log along the ridge: eight sides, a slight sag and wobble."""
        rng = self.rng
        sides, rings, radius = 8, 7, 0.07
        base = len(self.verts)
        shift = rng.random()
        for r in range(rings):
            t = r / (rings - 1)
            z = (t - 0.5) * length
            cx = rng.uniform(-0.012, 0.012)
            cy = y - 0.03 * math.sin(math.pi * t) + rng.uniform(-0.006, 0.006)
            rad = radius * rng.uniform(0.9, 1.08) * (0.92 + 0.08 * t)
            for s in range(sides):
                a = 2.0 * math.pi * s / sides
                self.verts.append(to_blender((cx + math.cos(a) * rad, cy + math.sin(a) * rad, z)))
        for r in range(rings - 1):
            for s in range(sides):
                n = (s + 1) % sides
                quad = [base + r * sides + s, base + r * sides + n, base + (r + 1) * sides + n, base + (r + 1) * sides + s]
                u0, u1 = shift + (r / (rings - 1)) * length / TILE, shift + ((r + 1) / (rings - 1)) * length / TILE
                v0, v1 = 0.3 + 0.2 * s / sides, 0.3 + 0.2 * (s + 1) / sides  # two planks round the log
                uv = [(u0, v0), (u0, v1), (u1, v1), (u1, v0)]
                p = [self.verts[i] for i in quad]
                nrm = _normal(p[0], p[1], p[2])
                axis = to_blender((0.0, y, 0.0))
                if nrm[0] * (p[0][0] - axis[0]) + nrm[2] * (p[0][2] - axis[2]) < 0:
                    quad.reverse()
                    uv.reverse()
                self.faces.append(quad)
                self.uvs.append(uv)
                self.mats.append(FRAME)
                self.smooth.append(True)
        for r, order in ((0, 1), (rings - 1, -1)):
            cap = [base + r * sides + s for s in range(sides)][::order]
            p = [self.verts[i] for i in cap]
            nrm = _normal(p[0], p[1], p[2])
            if nrm[1] * (p[0][1]) < 0:  # caps face away from the hut's middle (Blender y = -z)
                cap.reverse()
            self.faces.append(cap)
            self.uvs.append([(0.5 + 0.03 * math.cos(2 * math.pi * k / sides), 0.35 + 0.03 * math.sin(2 * math.pi * k / sides))
                             for k in range(sides)])
            self.mats.append(FRAME)
            self.smooth.append(False)

    # -- output ------------------------------------------------------------------------------------
    def to_object(self, name, materials, collection):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.verts, [], self.faces)
        uv = mesh.uv_layers.new(name="UVMap")
        k = 0
        for f, face_uv in enumerate(self.uvs):
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


def _normal(a, b, c):
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    return (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])


def _sx(side, x0, x1, y, z0, z1=None):
    """Bounds of a board on a side wall; `y` is a (lo, hi) pair, or one height when z1 is omitted."""
    xa, xb = sorted((side * x0, side * x1))
    if z1 is None:
        return (xa, y, z0), (xb, y, z0)
    return (xa, y[0], z0), (xb, y[1], z1)


def _ez(end, x0, x1, y0, y1, z0, z1):
    za, zb = sorted((end * z0, end * z1))
    return (x0, y0, za), (x1, y1, zb)


def materials():
    out = []
    textures = _find_up(os.path.dirname(os.path.abspath(__file__)), "Assets")
    image_path = os.path.join(textures, "Hivemind", "HDRP(Default)", "MedievalFantasyVillage", "Art", "Textures",
                              "T_ModularWood_Basecolor.png") if textures else None
    if not (image_path and os.path.exists(image_path)):
        print("build_worker_hut: T_ModularWood_Basecolor.png not found; preview materials stay untextured")
    for name, tint in (("Hut_Wood", (1.0, 1.0, 1.0, 1.0)), ("Hut_Frame", (0.72, 0.72, 0.72, 1.0))):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf and image_path and os.path.exists(image_path) and not any(n.type == 'TEX_IMAGE' for n in m.node_tree.nodes):
            tex = m.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(image_path, check_existing=True)
            mix = m.node_tree.nodes.new("ShaderNodeMix")
            mix.data_type = 'RGBA'
            mix.blend_type = 'MULTIPLY'
            mix.inputs[0].default_value = 1.0
            m.node_tree.links.new(tex.outputs["Color"], mix.inputs[6])
            mix.inputs[7].default_value = tint
            m.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.9
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


def build(name, half_length, seed, mats, door=DOOR, overhang=OVERHANG):
    """Builds one hut as the objects Walls and Roof in a collection called <name> (main() renames them)."""
    old = bpy.data.collections.get(name)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)

    walls = Shell(seed, door, overhang)
    walls.side_wall(-1, half_length)
    walls.side_wall(1, half_length)
    walls.front(half_length)
    walls.rear(half_length)
    roof = Shell(seed + 100, door, overhang)
    roof.roof(half_length)
    return [walls.to_object("Walls", mats, col), roof.to_object("Roof", mats, col)]


def export(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='OFF',
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, use_tspace=False)


# name, half length, seed, doorway, eave overhang. WorkerHutKitBuilder.Variants lists the same shells.
VARIANTS = (
    ("WorkerHut_Home", 1.5, 7, DOOR, OVERHANG),
    ("WorkerHut_Home_B", 1.5, 23, DOOR, OVERHANG),
    ("WorkerHut_Home_C", 1.5, 41, DOOR_MIRRORED, OVERHANG),
    ("WorkerHut_Tight", 1.5, 57, DOOR, TIGHT_OVERHANG),
    ("WorkerHut_Tight_B", 1.5, 73, DOOR_MIRRORED, TIGHT_OVERHANG),
    ("WorkerHut_Tight_C", 1.5, 89, DOOR, TIGHT_OVERHANG),
    ("WorkerHut_Tight_D", 1.5, 105, DOOR_MIRRORED, TIGHT_OVERHANG),
    ("WorkerHut_Long", 2.0, 11, DOOR, OVERHANG),
)


def main():
    source = os.path.dirname(os.path.abspath(__file__))
    out = os.path.dirname(source)
    mats = materials()
    report = {}
    kept = set()
    for name, half_length, seed, door, overhang in VARIANTS:
        objects = build(name, half_length, seed, mats, door, overhang)
        # the exporter writes object names: both files hold "Walls" and "Roof"
        for o, plain in zip(objects, ("Walls", "Roof")):
            o.name = plain
            if o.name != plain:  # another object of the session holds the name: the builder would not find ours
                raise RuntimeError("An object called '%s' already exists in this Blender session; rename it first" % plain)
        export(objects, os.path.join(out, name + ".fbx"))
        for o, plain in zip(objects, ("Walls", "Roof")):
            o.name = name + "_" + plain
            kept.add(o)
        report[name] = sum(len(p.vertices) - 2 for o in objects for p in o.data.polygons)
        # widest reach of the roof from the middle, in x and along the ridge
        report[name + " roof half x/z"] = (round(max(abs(v.co.x) for v in objects[1].data.vertices), 3),
                                           round(max(abs(v.co.y) for v in objects[1].data.vertices), 3))
    bpy.data.libraries.write(os.path.join(source, "WorkerHut.blend"), kept | set(mats), fake_user=True)
    return report


if __name__ == "__main__":
    result = main()
    print("triangles:", result)
