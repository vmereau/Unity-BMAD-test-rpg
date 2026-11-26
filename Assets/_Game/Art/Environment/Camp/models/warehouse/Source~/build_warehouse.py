"""Area 1 warehouse, built piece by piece and fitted to the ground it stands on.

Run in Blender (5.x):  blender -b -P build_warehouse.py
or from a session:      runpy.run_path(r"...\\build_warehouse.py", run_name="__main__")

Reads   warehouse_ground.json  (Tools/World/Capture Warehouse Ground: terrain + rocks, relative to the floor)
Writes  ../Warehouse.fbx, ../Warehouse.json (colliders, walk surfaces, lantern points for WarehouseBuilder)
        and Warehouse.blend (the built objects only: this script is the source).

Everything is written in the prefab's Unity frame: x east, y up, z north (the river), origin on the floor's
top at the hall's centre. `to_blender` converts each vertex, so the FBX (-Z forward, Y up, Apply Transform)
arrives in Unity in that same frame.

Wood UVs are the huts' (Camp/CLAUDE.md -> One wood, by construction): T_ModularWood holds ten planks per tile;
every piece is mapped onto ONE plank, grain along it, 2 m per tile. A timber wider than a plank (0.2 m) is
squeezed across the grain to stay inside its plank. Stone is box-projected at 1 m per tile.

Sizes that the game depends on (WAREHOUSE_CONCEPTS.md -> Build brief) are the constants of the first block.
"""
import json
import math
import os
import random

import bpy

# --- Build brief ----------------------------------------------------------------------------------
HW, HD = 6.0, 4.0                    # half floor: 12 x 8 m
WALL = 4.0                           # eave height above the floor
PITCH = math.radians(30.0)
EAVE_OVERHANG, GABLE_OVERHANG = 0.08, 0.5   # a tight eave: the flag pole stands 0.3 m off the south-west corner
DOOR_X, DOOR_W, DOOR_H = 2.0, 2.4, 2.8
RAMP_W = 3.5
RAMP_T = 0.2                         # the walk boxes' thickness (Ramp_Camp's scale y)
CAMP_RUN = 13.2                      # plan length of the camp ramp, south from the door
RIVER_RUN = 10.0                     # plan length of the river ramp, west from the landing
LANDING = (-1.0, 5.0, 3.8)           # min x, max x, depth north of the wall
HOIST_REACH = 2.6
HOIST_ROPE = 5.0
WALL_BOX = 0.4                       # collider thickness, as the placeholder's walls

# --- Construction ---------------------------------------------------------------------------------
POST = 0.26                          # squared posts
FACE = 0.04                          # posts' outer face, inside the footprint line
BOARD = 0.04
INNER = POST + FACE                  # footprint line to the posts' inner face (where the boards stand)
SILL = 0.3
DECK = 0.05
STRINGER = 0.24
LEG = 0.2
RAIL_POST = 0.1
RAIL_H = 1.0
RAIL_T = 0.045
PAD = 0.14
TILE = 2.0
ROWS = 10
MAX_BOARD = TILE / ROWS
STONE_TILE = 1.0
# The plain patch of the kit's iron atlas (T_Iron): every iron face is mapped inside it.
IRON_UV = (0.04, 0.17, 0.42, 0.055)

WOOD, FRAME, SHINGLE, PALE, RUBBLE, IRON, ROPE, TAR = range(8)
MATERIALS = ("Wh_Wood", "Wh_Frame", "Wh_Shingle", "Wh_ShinglePale", "Wh_Rubble", "Wh_Iron", "Wh_Rope", "Wh_Tar")
TAN, COS, SIN = math.tan(PITCH), math.cos(PITCH), math.sin(PITCH)
DOOR_A, DOOR_B = DOOR_X - DOOR_W * 0.5, DOOR_X + DOOR_W * 0.5
POST_X = (-(HW - FACE - POST * 0.5), -2.9, DOOR_A - POST * 0.5, DOOR_B + POST * 0.5, HW - FACE - POST * 0.5)
POST_Z = (-(HD - FACE - POST * 0.5), -2.0, 2.0, HD - FACE - POST * 0.5)   # gable ends; the middle is the round log
BENTS_CAMP = (2.75, 6.5, 9.75, 12.3)   # along the slope; the bed pocket beside hut-05 lies between the first two
BENTS_RIVER = (2.6, 5.2, 7.8)


def to_blender(p):
    return (-p[0], -p[2], p[1])


def roof_plane(z):
    """Top of the roof boards above local z."""
    return WALL + (HD - abs(z)) * TAN


def add(a, b, s=1.0):
    return [a[i] + b[i] * s for i in range(3)]


def norm(v):
    l = math.sqrt(sum(c * c for c in v))
    return [c / l for c in v]


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


class Ground:
    def __init__(self, path):
        with open(path) as f:
            data = json.load(f)
        self.h = data["heights"]
        self.min_x, self.min_z, self.step = data["minX"], data["minZ"], data["step"]
        self.columns, self.rows = data["columns"], data["rows"]
        self.floor_world_y = data["floorWorldY"]
        self.centre = data["centre"]

    def at(self, x, z):
        # a query outside the capture would silently read the edge row: widen the grid in WarehouseBuilder instead
        if not (self.min_x - self.step <= x <= self.min_x + (self.columns - 1) * self.step + self.step
                and self.min_z - self.step <= z <= self.min_z + (self.rows - 1) * self.step + self.step):
            raise ValueError("ground asked at (%.2f, %.2f), outside the capture" % (x, z))
        fx = min(max((x - self.min_x) / self.step, 0.0), self.columns - 1.001)
        fz = min(max((z - self.min_z) / self.step, 0.0), self.rows - 1.001)
        ix, iz = int(fx), int(fz)
        tx, tz = fx - ix, fz - iz
        a = self.h[iz][ix] * (1 - tx) + self.h[iz][ix + 1] * tx
        b = self.h[iz + 1][ix] * (1 - tx) + self.h[iz + 1][ix + 1] * tx
        return a * (1 - tz) + b * tz

    def low(self, x, z, r=0.15):
        return min(self.at(x + dx, z + dz) for dx in (-r, 0, r) for dz in (-r, 0, r))

    def high(self, x, z, r=0.15):
        return max(self.at(x + dx, z + dz) for dx in (-r, 0, r) for dz in (-r, 0, r))


class Mesh:
    """Collects faces for one object. `rng` is shared so one seed gives one building."""

    def __init__(self, rng):
        self.rng = rng
        self.verts, self.faces, self.uvs, self.mats, self.smooth = [], [], [], [], []

    # -- boxes -------------------------------------------------------------------------------------
    def hexa(self, corners, local, grain, mat, skip=(), mode="plank"):
        """Eight corners indexed by bits (x, y, z of the piece's own frame); `local` are the same corners in
        that frame, in metres, for the UVs. `skip` drops faces: (axis, 0 | 1) pairs. Modes: plank (one plank
        of the wood texture), stone (box projection), iron (inside the plain patch of the iron atlas)."""
        rng = self.rng
        row = (rng.randrange(ROWS) + 0.5) / ROWS
        shift = rng.random()
        flip = rng.choice((-1.0, 1.0))
        off = (rng.random(), rng.random())
        centre = [sum(c[i] for c in corners) / 8.0 for i in range(3)]
        base = len(self.verts)
        self.verts.extend(to_blender(c) for c in corners)
        centre_b = to_blender(centre)
        span = [max(l[a] for l in local) - min(l[a] for l in local) for a in range(3)]
        for axis in range(3):
            others = [a for a in range(3) if a != axis]
            for sign in (0, 1):
                if (axis, sign) in skip:
                    continue
                idx = [i for i in range(8) if ((i >> axis) & 1) == sign]
                b, c = others
                idx.sort(key=lambda i: (((i >> b) & 1), ((i >> c) & 1) ^ ((i >> b) & 1)))
                p = [self.verts[base + i] for i in idx]
                n = _normal(p[0], p[1], p[2])
                mid = [sum(q[k] for q in p) / 4.0 for k in range(3)]
                if sum(n[k] * (mid[k] - centre_b[k]) for k in range(3)) < 0:
                    idx.reverse()
                if mode == "stone":
                    uv = [(off[0] + local[i][b] / STONE_TILE, off[1] + local[i][c] / STONE_TILE) for i in idx]
                elif mode == "iron":
                    u0, v0, du, dv = IRON_UV
                    uv = [(u0 + du * (0.5 + 0.9 * local[i][b] / max(span[b], 1e-4) * min(1.0, span[b] / 0.4)),
                           v0 + dv * (0.5 + 0.9 * local[i][c] / max(span[c], 1e-4))) for i in idx]
                elif grain in others:
                    across = others[0] if others[1] == grain else others[1]
                    # a timber wider than one plank is squeezed across the grain to stay inside it
                    squeeze = min(1.0, MAX_BOARD * 0.92 / max(span[across], 1e-4))
                    uv = [(shift + flip * local[i][grain] / TILE, row + local[i][across] * squeeze / TILE) for i in idx]
                else:  # end grain: a small patch of the same plank
                    squeeze = min(1.0, MAX_BOARD * 0.92 / max(span[b], span[c], 1e-4))
                    uv = [(shift + local[i][b] * squeeze / TILE, row + local[i][c] * squeeze / TILE) for i in idx]
                self.faces.append([base + i for i in idx])
                self.uvs.append(uv)
                self.mats.append(mat)
                self.smooth.append(False)

    def box(self, lo, hi, grain, mat, skew=None, skip=(), mode="plank"):
        lo, hi = [min(lo[i], hi[i]) for i in range(3)], [max(lo[i], hi[i]) for i in range(3)]
        mid = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
        corners, local = [], []
        for i in range(8):
            c = [hi[a] if (i >> a) & 1 else lo[a] for a in range(3)]
            if skew:
                c = skew(c)
            local.append([c[a] - mid[a] for a in range(3)])
            corners.append(c)
        self.hexa(corners, local, grain, mat, skip, mode)

    def framed(self, origin, ax, ay, az, half, grain, mat, ends=None, skip=(), mode="plank", jitter=0.0):
        """A piece in its own frame (unit axes, half sizes). `ends[i]` shifts corner i along ax."""
        corners, local = [], []
        for i in range(8):
            l = [(half[a] if (i >> a) & 1 else -half[a]) for a in range(3)]
            if ends:
                l[0] += ends[i]
            if jitter:
                l = [l[a] * (1.0 + self.rng.uniform(-jitter, jitter)) for a in range(3)]
            local.append(l)
            corners.append([origin[k] + ax[k] * l[0] + ay[k] * l[1] + az[k] * l[2] for k in range(3)])
        self.hexa(corners, local, grain, mat, skip, mode)

    def beam(self, a, b, width, depth, mat, up=(0.0, 1.0, 0.0), extend=0.0):
        """A timber from a to b: `depth` along `up` (made square to the beam), `width` across."""
        ax = norm([b[i] - a[i] for i in range(3)])
        side = cross(up, ax)
        if sum(c * c for c in side) < 1e-6:
            side = cross((1.0, 0.0, 0.0), ax)
        side = norm(side)
        top = norm(cross(ax, side))
        length = math.sqrt(sum((b[i] - a[i]) ** 2 for i in range(3))) + extend * 2.0
        mid = [(a[i] + b[i]) * 0.5 for i in range(3)]
        self.framed(mid, ax, top, side, (length * 0.5, depth * 0.5, width * 0.5), 0, mat)

    def rows(self, y0, y1, lo=0.15):
        if y1 - y0 < 0.02:
            return []
        out, y = [], y0
        while y1 - y > 0.29:
            w = self.rng.uniform(lo, MAX_BOARD)
            out.append((y, y + w))
            y += w
        if y1 - y > MAX_BOARD:
            half = (y1 - y) * 0.5
            out.append((y, y + half))
            y += half
        out.append((y, y1))
        return out

    def pieces(self, a, b, lo=2.4, hi=4.2):
        """Splits a long run into board lengths (butt joints)."""
        out, x = [], a
        while b - x > hi + 0.5:   # what is left after a piece is never a sliver
            n = self.rng.uniform(lo, hi)
            out.append((x, x + n))
            x += n
        out.append((x, b))
        return out

    # -- round timber ------------------------------------------------------------------------------
    def log(self, a, b, radius, mat, sides=8, rings=5, wobble=0.012, smooth=True, v=(0.3, 0.5)):
        rng = self.rng
        ax = norm([b[i] - a[i] for i in range(3)])
        ref = (1.0, 0.0, 0.0) if abs(ax[0]) < 0.9 else (0.0, 0.0, 1.0)
        u = norm(cross(ax, ref))
        w = cross(ax, u)
        length = math.sqrt(sum((b[i] - a[i]) ** 2 for i in range(3)))
        base = len(self.verts)
        shift = rng.random()
        ring_centres = []
        for r in range(rings):
            t = r / (rings - 1)
            c = [a[i] + (b[i] - a[i]) * t + u[i] * rng.uniform(-wobble, wobble) + w[i] * rng.uniform(-wobble, wobble)
                 for i in range(3)]
            ring_centres.append(c)
            rad = radius * rng.uniform(0.94, 1.05)
            for s in range(sides):
                ang = 2.0 * math.pi * s / sides
                self.verts.append(to_blender([c[i] + (u[i] * math.cos(ang) + w[i] * math.sin(ang)) * rad for i in range(3)]))
        for r in range(rings - 1):
            cb = to_blender(ring_centres[r])
            for s in range(sides):
                n = (s + 1) % sides
                quad = [base + r * sides + s, base + r * sides + n, base + (r + 1) * sides + n, base + (r + 1) * sides + s]
                u0, u1 = shift + (r / (rings - 1)) * length / TILE, shift + ((r + 1) / (rings - 1)) * length / TILE
                v0, v1 = v[0] + (v[1] - v[0]) * s / sides, v[0] + (v[1] - v[0]) * (s + 1) / sides
                uv = [(u0, v0), (u0, v1), (u1, v1), (u1, v0)]
                p = [self.verts[i] for i in quad]
                nrm = _normal(p[0], p[1], p[2])
                if sum(nrm[k] * (p[0][k] - cb[k]) for k in range(3)) < 0:
                    quad.reverse()
                    uv.reverse()
                self.faces.append(quad)
                self.uvs.append(uv)
                self.mats.append(mat)
                self.smooth.append(smooth)
        mid_b = to_blender([(a[i] + b[i]) * 0.5 for i in range(3)])
        for r in (0, rings - 1):
            cap = [base + r * sides + s for s in range(sides)]
            p = [self.verts[i] for i in cap]
            nrm = _normal(p[0], p[1], p[2])
            if sum(nrm[k] * (p[0][k] - mid_b[k]) for k in range(3)) < 0:
                cap.reverse()
            self.faces.append(cap)
            self.uvs.append([(0.5 + 0.03 * math.cos(2 * math.pi * k / sides), (v[0] + v[1]) * 0.5 + 0.03 * math.sin(2 * math.pi * k / sides))
                             for k in range(sides)])
            self.mats.append(mat)
            self.smooth.append(False)

    # -- stone -------------------------------------------------------------------------------------
    def stone(self, centre, size, yaw=None):
        rng = self.rng
        yaw = rng.uniform(0, math.pi) if yaw is None else yaw
        tilt = rng.uniform(-0.12, 0.12)
        ax = (math.cos(yaw), math.sin(tilt), math.sin(yaw))
        ax = norm(ax)
        az = norm(cross(ax, (0.0, 1.0, 0.0)))
        ay = cross(az, ax)
        self.framed(centre, ax, ay, az, (size[0] * 0.5, size[1] * 0.5, size[2] * 0.5), 0, RUBBLE, mode="stone", jitter=0.16)

    def pad(self, x, z, top, size=0.56, bottom=None):
        """A flat stone under a post; on a slope it is as deep as the ground falls under it."""
        bottom = top - PAD if bottom is None else min(bottom, top - PAD)
        self.stone((x, (top + bottom) * 0.5, z), (size, top - bottom, size), yaw=self.rng.uniform(-0.3, 0.3))

    # -- output ------------------------------------------------------------------------------------
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


def _normal(a, b, c):
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    return (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])


class Warehouse:
    def __init__(self, ground, seed=31):
        self.g = ground
        self.rng = random.Random(seed)
        self.parts = {name: Mesh(self.rng) for name in
                      ("Frame", "Walls", "Floor", "Roof", "Doors", "Ramps", "Landing", "Hoist", "Rubble", "Iron")}
        self.colliders, self.walk, self.not_walkable, self.lanterns = [], [], [], []

    # -- parts file --------------------------------------------------------------------------------
    def collider(self, name, group, centre, size, euler=(0.0, 0.0, 0.0), into=None):
        (self.colliders if into is None else into).append({
            "name": name, "group": group, "centre": [round(c, 4) for c in centre],
            "size": [round(c, 4) for c in size], "euler": [round(c, 3) for c in euler]})

    # -- supports ----------------------------------------------------------------------------------
    def support(self, mesh, x, z, top, size=POST, tar=True, name=None, min_height=0.3):
        """A post from the ground (on a padstone) up to `top`. Returns the foot's height, or None when the
        ground is too close for a post (a padstone then packs the gap)."""
        reach = size * 0.5 + 0.15
        under = self.g.low(x, z, reach) - 0.06          # the pad's underside, in the ground on its low side
        foot = max(self.g.at(x, z) + 0.05, under + PAD)  # the pad's top: the post's foot
        rubble = self.parts["Rubble"]
        if top - foot < min_height:
            if top - foot > 0.02:
                rubble.pad(x, z, top, size + 0.25)
            return None
        h = size * 0.5
        lean = self.rng.uniform(-0.006, 0.006)
        mesh.box((x - h, foot, z - h), (x + h, top, z + h), 1, FRAME,
                 skew=lambda c: [c[0] + (lean if c[1] > foot + 0.01 else 0.0), c[1], c[2]])
        rubble.pad(x, z, foot, size + 0.3, under)
        if tar and top - foot > 0.9:
            t = h + 0.006
            mesh.box((x - t, foot - 0.01, z - t), (x + t, foot + self.rng.uniform(0.4, 0.55), z + t), 1, TAR)
        if name:
            self.collider(name, "Structure", (x, (foot + top) * 0.5 - 0.2, z), (size, top - foot + 0.4, size))
        return foot

    def knee(self, mesh, post, foot, top, direction, reach=0.95, size=0.13):
        """A diagonal brace from a post up to the beam it carries; skipped on a short post."""
        if foot is None or top - foot < 1.5:
            return
        a = (post[0], top - reach, post[1])
        b = (post[0] + direction[0] * reach, top - 0.02, post[1] + direction[1] * reach)
        up = (direction[1], 0.0, -direction[0])
        mesh.beam(a, b, size, size * 0.8, FRAME, up=up, extend=0.04)

    # -- hall --------------------------------------------------------------------------------------
    def hall(self):
        frame, walls, rng = self.parts["Frame"], self.parts["Walls"], self.rng
        px, pz = HW - FACE - POST * 0.5, HD - FACE - POST * 0.5
        plate = WALL - POST
        # long walls: posts from the ground to the plate, sills between them, top plate, girt, boards inside
        for side in (-1, 1):
            z = side * pz
            feet = []
            for x in POST_X:
                foot = self.support(frame, x, z, -SILL, name=None)
                feet.append(foot)
                frame.box((x - POST * 0.5, -SILL - 0.001, z - POST * 0.5), (x + POST * 0.5, plate, z + POST * 0.5), 1, FRAME)
            for (xa, fa), (xb, fb) in zip(zip(POST_X, feet), zip(POST_X[1:], feet[1:])):
                frame.box((xa + POST * 0.5, -SILL, z - 0.11), (xb - POST * 0.5, -0.005, z + 0.11), 0, FRAME)
                self.knee(frame, (xa, z), fa, -SILL, (1.0, 0.0))
                self.knee(frame, (xb, z), fb, -SILL, (-1.0, 0.0))
                if not (abs(xa - (DOOR_A - POST * 0.5)) < 0.01):  # no girt across the doorway
                    frame.box((xa + POST * 0.5, 1.92, z + side * 0.03), (xb - POST * 0.5, 2.07, z + side * 0.13), 0, FRAME)
            frame.box((-HW - GABLE_OVERHANG + 0.06, plate, z - POST * 0.5), (HW + GABLE_OVERHANG - 0.06, WALL, z + POST * 0.5), 0, FRAME)
            # lintel between the door posts
            frame.box((DOOR_A - 0.02, DOOR_H, z - POST * 0.5 + 0.02), (DOOR_B + 0.02, DOOR_H + 0.18, z + POST * 0.5 - 0.02), 0, FRAME)
            # knee braces up to the plate at the corners
            for x, d in ((POST_X[0], 1.0), (POST_X[-1], -1.0)):
                frame.beam((x + d * POST * 0.5, plate - 0.85, z + side * 0.06), (x + d * (POST * 0.5 + 0.85), plate, z + side * 0.06),
                           0.1, 0.12, FRAME, up=(0.0, 0.0, 1.0), extend=0.03)
            # boards: horizontal, on the posts' inner face, cut round the doorway
            zi = side * (HD - INNER)
            for y0, y1, spans in ((-0.02, DOOR_H, [(-HW + INNER, DOOR_A), (DOOR_B, HW - INNER)]),
                                  (DOOR_H, WALL, [(-HW + INNER, HW - INNER)])):
                for ya, yb in walls.rows(y0, y1):
                    for xa, xb in spans:
                        for a, b in walls.pieces(xa, xb):
                            out = rng.uniform(0.0, 0.005)
                            walls.box((a + 0.002, ya, zi - side * (BOARD + out)), (b - 0.002, yb - 0.003, zi - side * out), 0, WOOD)
            self.collider("Wall_%s_L" % ("Camp" if side < 0 else "River"), "Walls",
                          ((-HW + DOOR_A) * 0.5, WALL * 0.5, side * (HD - WALL_BOX * 0.5)), (DOOR_A + HW, WALL, WALL_BOX))
            self.collider("Wall_%s_R" % ("Camp" if side < 0 else "River"), "Walls",
                          ((HW + DOOR_B) * 0.5, WALL * 0.5, side * (HD - WALL_BOX * 0.5)), (HW - DOOR_B, WALL, WALL_BOX))
            self.collider("Wall_%s_Header" % ("Camp" if side < 0 else "River"), "Walls",
                          (DOOR_X, (DOOR_H + WALL) * 0.5, side * (HD - WALL_BOX * 0.5)), (DOOR_W, WALL - DOOR_H, WALL_BOX))
        # gable ends
        for side in (-1, 1):
            x = side * px
            feet = {}
            for z in POST_Z[1:-1]:
                feet[z] = self.support(frame, x, z, -SILL)
                frame.box((x - POST * 0.5, -SILL - 0.001, z - POST * 0.5), (x + POST * 0.5, plate, z + POST * 0.5), 1, FRAME)
            zs = (POST_Z[0], POST_Z[1], 0.0, POST_Z[2], POST_Z[3])
            for za, zb in zip(zs, zs[1:]):
                a0 = za + (POST * 0.5 if za != 0.0 else 0.2)
                b0 = zb - (POST * 0.5 if zb != 0.0 else 0.2)
                frame.box((x - 0.11, -SILL, a0), (x + 0.11, -0.005, b0), 2, FRAME)
                frame.box((x + side * 0.03, 1.92, a0), (x + side * 0.13, 2.07, b0), 2, FRAME)
            frame.box((x - POST * 0.5, plate, -pz + POST * 0.5), (x + POST * 0.5, WALL, pz - POST * 0.5), 2, FRAME)
            for z, d in ((POST_Z[0], 1.0), (POST_Z[-1], -1.0)):
                frame.beam((x + side * 0.06, plate - 0.85, z + d * POST * 0.5), (x + side * 0.06, plate, z + d * (POST * 0.5 + 0.85)),
                           0.1, 0.12, FRAME, up=(1.0, 0.0, 0.0), extend=0.03)
            for z in POST_Z[1:-1]:
                self.knee(frame, (x, z), feet[z], -SILL, (0.0, 1.0 if z < 0 else -1.0))
            # the junction log: the palisade butts into the middle of the end wall, under the ridge
            lx = side * (HW - FACE - 0.1)
            log_top = WALL + 0.06 if side < 0 else roof_plane(0.0) - 0.16
            log_foot = self.g.low(lx, 0.0, 0.2) - 0.25
            frame.log((lx, log_foot, 0.0), (lx, log_top, 0.0), 0.2, FRAME, sides=10, rings=7)
            # boards: horizontal to the plate, vertical in the gable
            xi = side * (HW - INNER)
            for ya, yb in walls.rows(-0.02, WALL):
                for a, b in walls.pieces(-HD + INNER + BOARD, HD - INNER - BOARD):
                    out = rng.uniform(0.0, 0.005)
                    walls.box((xi - side * (BOARD + out), ya, a + 0.002), (xi - side * out, yb - 0.003, b - 0.002), 2, WOOD)
            hatch = (-0.45, 0.45, WALL + 0.5, WALL + 1.5) if side < 0 else None
            edges = [-HD + INNER] + [b for _, b in walls.rows(-HD + INNER, 0.0, 0.16)]
            edges += [-e for e in reversed(edges[:-1])]
            if hatch:   # a joint at each side of the hatch: a board straddling one would leave a slot beside the frame
                edges = sorted(set(edges) | {hatch[0], hatch[1]})
                edges = [e for i, e in enumerate(edges) if i == 0 or e - edges[i - 1] > 0.012 or e in (hatch[0], hatch[1])]
            for za, zb in zip(edges, edges[1:]):
                out = rng.uniform(0.0, 0.005)
                x0, x1 = xi - side * (BOARD + out) + side * 0.045, xi - side * out + side * 0.045
                spans = [(WALL - 0.06, None)]
                if hatch and zb > hatch[0] + 1e-3 and za < hatch[1] - 1e-3:
                    spans = [(WALL - 0.06, hatch[2]), (hatch[3], None)]
                for y0, y1 in spans:
                    def skew(c, y1=y1, y0=y0):
                        if c[1] > y0 + 0.01:
                            return [c[0], (roof_plane(c[2]) - 0.03) if y1 is None else y1, c[2]]
                        return c
                    walls.box((x0, y0, za + 0.002), (x1, y0 + 1.0, zb - 0.002), 1, WOOD, skew)
            if hatch:
                self.hatch(xi + side * 0.045, side, hatch)
            self.collider("Wall_%s" % ("West" if side < 0 else "East"), "Walls",
                          (side * (HW - WALL_BOX * 0.5), WALL * 0.5, 0.0), (WALL_BOX, WALL, HD * 2.0))
            self.collider("Gable_%s" % ("West" if side < 0 else "East"), "Walls",
                          (side * (HW - WALL_BOX * 0.5), WALL + HD * TAN * 0.25, 0.0), (WALL_BOX, HD * TAN * 0.5, HD))

    def hatch(self, x, side, rect):
        """A shuttered loading hatch under the ridge: closed plank shutter, frame and two strap hinges."""
        doors, iron, rng = self.parts["Doors"], self.parts["Iron"], self.rng
        z0, z1, y0, y1 = rect
        # `x` is the gable boards' outer face and `side` points outward: shutter, then ledges, then hinges
        for za, zb in doors.rows(z0, z1, 0.16):
            doors.box((x - side * 0.02, y0 + rng.uniform(0, 0.01), za + 0.003), (x + side * 0.015, y1, zb - 0.003), 1, WOOD)
        for ya, yb in ((y0 - 0.09, y0), (y1, y1 + 0.09)):
            doors.box((x - side * 0.03, ya, z0 - 0.09), (x + side * 0.05, yb, z1 + 0.09), 2, FRAME)
        for za, zb in ((z0 - 0.09, z0), (z1, z1 + 0.09)):
            doors.box((x - side * 0.03, y0, za), (x + side * 0.05, y1, zb), 1, FRAME)
        for y in (y0 + 0.2, y1 - 0.2):
            doors.box((x + side * 0.015, y - 0.045, z0 + 0.02), (x + side * 0.04, y + 0.045, z1 - 0.02), 2, FRAME)
            iron.box((x + side * 0.04, y - 0.022, z0 - 0.06), (x + side * 0.048, y + 0.022, z0 + 0.5), 2, IRON, mode="iron")

    def floor(self):
        floor, frame, rng = self.parts["Floor"], self.parts["Frame"], self.rng
        zi = HD - INNER - BOARD
        for za, zb in floor.rows(-zi, zi, 0.16):
            for a, b in floor.pieces(-HW + INNER + BOARD, HW - INNER - BOARD, 3.0, 4.6):
                floor.box((a + 0.002, -DECK, za + 0.002), (b - 0.002, -rng.uniform(0.0, 0.004), zb - 0.002), 0, WOOD)
        # door sills: the boards run out through both doorways to the decks
        for side in (-1, 1):
            for za, zb in floor.rows(zi, HD, 0.16):
                floor.box((DOOR_A + 0.004, -DECK, side * za), (DOOR_B - 0.004, -0.002, side * zb), 0, WOOD)
        # joists where the ground leaves room for them (seen from the low west side)
        x = -HW + 0.75
        while x < HW - 0.5:
            if max(self.g.high(x, z, 0.1) for z in (-3.0, -1.5, 0.0, 1.5, 3.0)) < -0.6:
                frame.box((x - 0.07, -DECK - 0.2, -HD + INNER), (x + 0.07, -DECK, HD - INNER), 2, FRAME)
            x += 0.86
        self.collider("Floor", "Walk", (0.0, -0.15, 0.0), (HW * 2.0, 0.3, HD * 2.0), into=self.walk)

    def roof(self):
        roof, frame, rng = self.parts["Roof"], self.parts["Frame"], self.rng
        length = (HW + GABLE_OVERHANG) * 2.0
        slope = (HD + EAVE_OVERHANG) / COS
        peak = (0.0, roof_plane(0.0), 0.0)
        ridge = (1.0, 0.0, 0.0)
        for side in (-1, 1):
            down = (0.0, -SIN, side * COS)
            normal = (0.0, COS, side * SIN)
            # roof boards down the slope (seen from inside)
            for xa, xb in roof.rows(-length * 0.5, length * 0.5, 0.17):
                o = [peak[k] + down[k] * slope * 0.5 - normal[k] * 0.0125 + ridge[k] * (xa + xb) * 0.5 for k in range(3)]
                roof.framed(o, down, normal, ridge, (slope * 0.5, 0.0125, (xb - xa) * 0.5 - 0.002), 0, WOOD, skip=((1, 1),))
            # rafters under them
            n = 9
            for i in range(n):
                x = -HW + FACE + POST * 0.5 + i * (2.0 * (HW - FACE - POST * 0.5)) / (n - 1)
                o = [peak[k] + down[k] * (slope * 0.5 - 0.05) - normal[k] * 0.105 + ridge[k] * x for k in range(3)]
                frame.framed(o, down, normal, ridge, (slope * 0.5 - 0.05, 0.08, 0.055), 0, FRAME)
            # barge boards at the gables
            for end in (-1, 1):
                o = [peak[k] + down[k] * (slope * 0.5 - 0.02) - normal[k] * 0.06 + ridge[k] * end * (length * 0.5 + 0.016) for k in range(3)]
                roof.framed(o, down, normal, ridge, (slope * 0.5 + 0.02, 0.1, 0.016), 0, FRAME)
            # shingles, course by course from the eave up; each course laps the one below
            exposure, shingle_len, thick = 0.31, 0.5, 0.022
            tilt = math.atan2(thick, exposure)
            ax = norm([down[k] * math.cos(tilt) + normal[k] * math.sin(tilt) for k in range(3)])
            ay = norm([normal[k] * math.cos(tilt) - down[k] * math.sin(tilt) for k in range(3)])
            courses = int((slope + 0.03) / exposure) + 1
            pale = [(rng.randrange(1, courses - 2), rng.uniform(-length * 0.5 + 0.5, length * 0.5 - 1.6), rng.randrange(1, 3), rng.uniform(0.5, 1.2))
                    for _ in range(4)]
            for c in range(courses):
                butt = slope + 0.03 - c * exposure
                if butt < 0.12:
                    break
                x = -length * 0.5 - 0.03 + rng.uniform(0.0, 0.1)
                while x < length * 0.5 + 0.03:
                    w = min(rng.uniform(0.15, MAX_BOARD), length * 0.5 + 0.03 - x)
                    if w < 0.05:
                        break
                    gap = rng.uniform(0.003, 0.009)
                    roll = rng.random()
                    if roll > 0.006:  # a shingle now and then is gone
                        rag = rng.uniform(-0.03, 0.035) + (rng.uniform(0.03, 0.07) if roll < 0.03 else 0.0)  # some have slipped
                        ln = min(shingle_len, butt + rag - 0.02)
                        mid = butt + rag - ln * 0.5
                        lift = rng.uniform(0.0, 0.006)
                        o = [peak[k] + down[k] * mid + normal[k] * (0.004 + lift + (ln * 0.5) * math.sin(tilt)) + ay[k] * thick * 0.5
                             + ridge[k] * (x + w * 0.5) for k in range(3)]
                        mat = SHINGLE
                        if any(pc <= c < pc + ph and px <= x < px + pw for pc, px, ph, pw in pale) or rng.random() < 0.012:
                            mat = PALE
                        skew = rng.uniform(-0.012, 0.012)
                        ends = [(skew if (i >> 2) & 1 else -skew) if i & 1 else 0.0 for i in range(8)]
                        roof.framed(o, ax, ay, ridge, (ln * 0.5, thick * 0.5, (w - gap) * 0.5), 0, mat, ends, skip=((1, 0), (0, 0)))
                    x += w
            self.collider("Roof_%s" % ("Camp" if side < 0 else "River"), "Roof",
                          (0.0, WALL + HD * TAN * 0.5, side * HD * 0.5), (HW * 2.0 + 1.0, 0.2, HD / COS + 0.6),
                          (side * math.degrees(PITCH), 0.0, 0.0))
        # ridge: a beam under it, two cap boards over the top course
        top = peak[1]
        frame.box((-HW - GABLE_OVERHANG + 0.05, top - 0.3, -0.07), (HW + GABLE_OVERHANG - 0.05, top - 0.1, 0.07), 0, FRAME)
        for side in (-1, 1):
            down = (0.0, -SIN, side * COS)
            normal = (0.0, COS, side * SIN)
            for xa, xb in roof.pieces(-length * 0.5 - 0.04, length * 0.5 + 0.04, 2.6, 3.8):
                o = [peak[k] + down[k] * 0.085 + normal[k] * (0.062 + (0.012 if side > 0 else 0.0)) + ridge[k] * (xa + xb) * 0.5 for k in range(3)]
                roof.framed(o, ridge, normal, down, ((xb - xa) * 0.5 - 0.004, 0.012, 0.1), 0, FRAME)
        # tie beams and king posts at three frames
        for x in (POST_X[1], POST_X[2], POST_X[3]):
            frame.box((x - 0.09, WALL - POST, -HD + INNER), (x + 0.09, WALL - 0.04, HD - INNER), 2, FRAME)
            frame.box((x - 0.07, WALL - 0.04, -0.07), (x + 0.07, top - 0.3, 0.07), 1, FRAME)

    def doors(self):
        doors, iron, frame, rng = self.parts["Doors"], self.parts["Iron"], self.parts["Frame"], self.rng
        leaf = DOOR_W * 0.5
        height = DOOR_H - 0.06
        for side in (-1, 1):                       # -1 camp wall, +1 river wall
            zf = side * (HD - FACE + 0.015)        # just outside the posts
            for hinge_x, away in ((DOOR_A, -1.0), (DOOR_B, 1.0)):
                swing = math.radians(rng.uniform(7.0, 12.0))   # standing open against the wall, not quite flat
                along = (away * math.cos(swing), 0.0, side * math.sin(swing))
                out = (-math.sin(swing) * away, 0.0, side * math.cos(swing))   # the face away from the wall
                up = (0.0, 1.0, 0.0)
                hinge = (hinge_x, 0.03, zf)
                x = 0.015
                for _ in range(6):
                    w = (leaf - 0.03) / 6.0
                    o = [hinge[k] + along[k] * (x + w * 0.5) + up[k] * (height * 0.5 + rng.uniform(-0.008, 0.008)) + out[k] * 0.02
                         for k in range(3)]
                    doors.framed(o, up, along, out, (height * 0.5, w * 0.5 - 0.002, 0.02), 0, WOOD)
                    x += w
                for y in (0.35, height * 0.5, height - 0.35):
                    o = [hinge[k] + along[k] * leaf * 0.5 + up[k] * y + out[k] * 0.06 for k in range(3)]
                    doors.framed(o, along, up, out, (leaf * 0.5 - 0.03, 0.07, 0.02), 0, FRAME)
                    # strap hinge over the ledge, from a pintle on the door post
                    o = [hinge[k] + along[k] * 0.36 + up[k] * y + out[k] * 0.084 for k in range(3)]
                    iron.framed(o, along, up, out, (0.38, 0.028, 0.005), 0, IRON, mode="iron")
                    o = [hinge[k] + along[k] * -0.01 + up[k] * y + out[k] * 0.05 for k in range(3)]
                    iron.framed(o, up, along, out, (0.06, 0.025, 0.04), 0, IRON, mode="iron")
                a = [hinge[k] + along[k] * 0.08 + up[k] * 0.42 + out[k] * 0.06 for k in range(3)]
                b = [hinge[k] + along[k] * (leaf - 0.08) + up[k] * (height * 0.5 - 0.07) + out[k] * 0.06 for k in range(3)]
                doors.beam(a, b, 0.13, 0.04, FRAME, up=out)
                c = [hinge[k] + along[k] * (leaf * 0.5 + 0.05) + up[k] * height * 0.5 + out[k] * 0.03 for k in range(3)]
                yaw = math.degrees(math.atan2(along[0], along[2]))
                self.collider("Leaf", "Structure", c, (0.1, height, leaf - 0.1), (0.0, yaw, 0.0))
            # lantern bracket east of the open leaf: a backing block, an iron arm and a hook
            bx, by = DOOR_B + leaf + 0.42, 2.5
            zw = side * (HD - INNER + 0.0)
            frame.box((bx - 0.07, by - 0.3, zw), (bx + 0.07, by + 0.12, zw + side * 0.1), 1, FRAME)
            iron.box((bx - 0.012, by - 0.012, zw + side * 0.1), (bx + 0.012, by + 0.012, zw + side * 0.52), 2, IRON, mode="iron")
            iron.beam((bx, by - 0.24, zw + side * 0.1), (bx, by - 0.01, zw + side * 0.36), 0.018, 0.018, IRON, up=(1.0, 0.0, 0.0))
            iron.box((bx - 0.008, by - 0.07, zw + side * 0.47), (bx + 0.008, by, zw + side * 0.486), 1, IRON, mode="iron")
            self.lanterns.append({"name": "DoorLantern_%s" % ("Camp" if side < 0 else "River"),
                                  "position": [round(bx, 3), round(by - 0.07 - 0.27, 3), round(zw + side * 0.478, 3)],
                                  "out": side})

    # -- decks -------------------------------------------------------------------------------------
    def ramp(self, name, top, bottom, bents, rails):
        """A plank deck on stringers and trestle bents from `top` to `bottom` (points on the deck's top, on its
        centre line). `rails`: side (-1 | +1, to the left | right looking downhill) -> (from, to) along the
        slope. Legs stand under the deck, so a side can run along the hall's wall."""
        ramps, rng = self.parts["Ramps"], self.rng
        run = [bottom[i] - top[i] for i in range(3)]
        length = math.sqrt(sum(c * c for c in run))
        along = norm(run)
        flat = norm([along[0], 0.0, along[2]])
        right = cross((0.0, 1.0, 0.0), flat)          # to the right, looking downhill
        up = cross(along, right)
        half = RAMP_W * 0.5

        def at(s, side=0.0, lift=0.0):
            return [top[k] + along[k] * s + right[k] * side + up[k] * lift for k in range(3)]

        # deck boards across, with small gaps
        s = 0.0
        while s < length - 0.02:
            w = min(rng.uniform(0.16, MAX_BOARD), length - s)
            o = at(s + w * 0.5, rng.uniform(-0.008, 0.008), -DECK * 0.5 - rng.uniform(0.0, 0.004))
            ramps.framed(o, right, up, along, (half - rng.uniform(0.0, 0.012), DECK * 0.5, w * 0.5 - 0.0025), 0, WOOD)
            s += w
        # grip battens, left open where the wheels run
        s = 0.5
        while s < length - 0.3:
            for a, b in ((-1.62, -1.0), (-0.62, 0.62), (1.0, 1.62)):
                o = at(s + rng.uniform(-0.01, 0.01), (a + b) * 0.5, 0.01)
                ramps.framed(o, right, up, along, ((b - a) * 0.5, 0.01, 0.02), 0, FRAME, skip=((1, 0),))
            s += 0.92
        # stringers
        for side in (-1.42, 0.0, 1.42):
            ramps.framed(at(length * 0.5, side, -DECK - STRINGER * 0.5), along, up, right, (length * 0.5, STRINGER * 0.5, 0.09), 0, FRAME)
        # bents: a cap beam across, legs to the ground, cross-braced when tall
        for s in bents:
            cap_y = -DECK - STRINGER - 0.1
            c = at(s, 0.0, cap_y)
            ramps.framed(c, right, (0.0, 1.0, 0.0), flat, (half + 0.16, 0.1, 0.1), 0, FRAME)
            feet = []
            for side in (-1, 1):
                off = side * (half - 0.32)
                p = at(s, off, cap_y)
                foot = self.support(ramps, p[0], p[2], p[1] - 0.1, LEG, name="Leg")
                feet.append((p, foot))
            (pa, fa), (pb, fb) = feet
            if fa is not None and fb is not None and min(pa[1] - fa, pb[1] - fb) > 1.7:
                d = flat
                ramps.beam((pa[0] + d[0] * 0.13, fa + 0.35, pa[2] + d[2] * 0.13), (pb[0] + d[0] * 0.13, pb[1] - 0.35, pb[2] + d[2] * 0.13),
                           0.14, 0.035, WOOD, up=d)
                ramps.beam((pb[0] - d[0] * 0.13, fb + 0.35, pb[2] - d[2] * 0.13), (pa[0] - d[0] * 0.13, pa[1] - 0.35, pa[2] - d[2] * 0.13),
                           0.14, 0.035, WOOD, up=d)
        # handrails: posts outside the deck's edge, rails on their outer side
        for side, (s0, s1) in rails.items():
            off = side * (half + RAIL_POST * 0.5)
            count = max(2, int(round((s1 - s0) / 1.75)) + 1)
            posts = []
            for i in range(count):
                s = s0 + (s1 - s0) * i / (count - 1)
                p = at(s, off)
                x, z = p[0], p[2]
                ramps.box((x - RAIL_POST * 0.5, p[1] - DECK - STRINGER, z - RAIL_POST * 0.5),
                          (x + RAIL_POST * 0.5, p[1] + RAIL_H + rng.uniform(0.0, 0.04), z + RAIL_POST * 0.5), 1, FRAME)
                posts.append(p)
            ro = side * (RAIL_POST * 0.5 + RAIL_T * 0.5)
            for h, depth in ((RAIL_H - 0.06, 0.09), (RAIL_H * 0.5, 0.07)):
                a = [posts[0][k] + right[k] * ro - along[k] * 0.12 for k in range(3)]
                b = [posts[-1][k] + right[k] * ro + along[k] * 0.12 for k in range(3)]
                a[1] += h
                b[1] += h
                ramps.beam(a, b, RAIL_T, depth, WOOD, up=(0.0, 1.0, 0.0))
            mid = at((s0 + s1) * 0.5, side * (half + 0.085))   # 0.16 wide: its inner face is just off the deck
            pitch = math.degrees(math.asin(-along[1]))
            yaw = math.degrees(math.atan2(along[0], along[2]))
            self.collider("Rail", "Rails", (mid[0], mid[1] + RAIL_H * 0.5, mid[2]), (0.16, RAIL_H + 0.1, s1 - s0 + 0.3), (pitch, yaw, 0.0))
            edge = at((s0 + s1) * 0.5, side * (half + 0.3))
            self.collider("RailEdge", "NotWalkable", (edge[0], edge[1], edge[2]), (0.5, 2.4, s1 - s0 + 0.3), (pitch, yaw, 0.0),
                          into=self.not_walkable)
        pitch = math.degrees(math.asin(-along[1]))
        yaw = math.degrees(math.atan2(along[0], along[2]))
        centre = at(length * 0.5, 0.0, -RAMP_T * 0.5)
        self.collider(name, "Walk", centre, (RAMP_W, RAMP_T, length), (pitch, yaw, 0.0), into=self.walk)
        return length, pitch

    def camp_ramp(self):
        top = (DOOR_X, 0.0, -HD)
        foot_z = -HD - CAMP_RUN
        bottom = (DOOR_X, self.g.at(DOOR_X, foot_z), foot_z)
        length = math.sqrt(CAMP_RUN ** 2 + bottom[1] ** 2)
        # looking downhill (south) the right side is west (the huts), the left east (the rocks)
        return self.ramp("Ramp_Camp", top, bottom, BENTS_CAMP, {1: (0.75, length - 0.45), -1: (0.75, length - 0.45)})

    def river_ramp(self):
        z = HD + LANDING[2] * 0.5
        top = (LANDING[0], 0.0, z)
        bottom = (LANDING[0] - RIVER_RUN, self.g.at(LANDING[0] - RIVER_RUN, z), z)
        length = math.sqrt(RIVER_RUN ** 2 + bottom[1] ** 2)
        # looking downhill (west) the right side is north (the bank), the left south (the hall's wall)
        clear = (HW + LANDING[0] + 0.5) / RIVER_RUN * length   # where the deck passes the hall's west end
        return self.ramp("Ramp_River", top, bottom, BENTS_RIVER, {1: (0.2, length - 0.45), -1: (clear, length - 0.45)})

    def landing(self):
        landing, rng = self.parts["Landing"], self.rng
        x0, x1, depth = LANDING
        z0, z1 = HD, HD + depth
        for xa, xb in landing.rows(x0, x1, 0.16):
            landing.box((xa + 0.0025, -DECK - rng.uniform(0.0, 0.004), z0 - FACE + 0.01), (xb - 0.0025, 0.0, z1 + rng.uniform(-0.01, 0.015)), 2, WOOD)
        beam_y = (-DECK - 0.24, -DECK)
        rows_z = (z0 + 0.2, z0 + depth * 0.5, z1 - 0.2)
        for z in rows_z:
            landing.box((x0 + 0.02, beam_y[0], z - 0.1), (x1 - 0.02, beam_y[1], z + 0.1), 0, FRAME)
        posts_x = (x0 + 0.25, (x0 + x1) * 0.5, x1 - 0.25)
        for z in rows_z[1:]:
            for i, x in enumerate(posts_x):
                foot = self.support(landing, x, z, beam_y[0], 0.24, name="LandingPost")
                for d in (-1.0, 1.0):
                    if 0 <= i + int(d) < len(posts_x):
                        self.knee(landing, (x, z), foot, beam_y[0], (d, 0.0), reach=1.1)
        for x in posts_x:   # one diagonal per line of posts, from the inner foot up to the outer head
            fa = self.g.low(x, rows_z[1], 0.12) + PAD
            if beam_y[0] - 0.25 - (fa + 0.5) < 0.5:   # no room for a diagonal over high ground
                continue
            landing.beam((x + 0.14, fa + 0.5, rows_z[1]), (x + 0.14, beam_y[0] - 0.25, rows_z[2]), 0.14, 0.035, WOOD, up=(1.0, 0.0, 0.0))
        # rails on the river side and the east end; the west end is the ramp
        runs = (((x0, z1 + RAIL_POST * 0.5), (x1 + RAIL_POST * 0.5, z1 + RAIL_POST * 0.5), (0.0, 1.0)),
                ((x1 + RAIL_POST * 0.5, z1 + RAIL_POST * 0.5), (x1 + RAIL_POST * 0.5, z0 + 0.55), (1.0, 0.0)))
        for a, b, out in runs:
            n = max(2, int(round(math.hypot(b[0] - a[0], b[1] - a[1]) / 1.6)) + 1)
            for i in range(n):
                x, z = a[0] + (b[0] - a[0]) * i / (n - 1), a[1] + (b[1] - a[1]) * i / (n - 1)
                landing.box((x - RAIL_POST * 0.5, -DECK - 0.24, z - RAIL_POST * 0.5), (x + RAIL_POST * 0.5, RAIL_H + rng.uniform(0.0, 0.04), z + RAIL_POST * 0.5), 1, FRAME)
            ro = RAIL_POST * 0.5 + RAIL_T * 0.5
            for h, d in ((RAIL_H - 0.06, 0.09), (RAIL_H * 0.5, 0.07)):
                landing.beam((a[0] + out[0] * ro, h, a[1] + out[1] * ro), (b[0] + out[0] * ro, h, b[1] + out[1] * ro), RAIL_T, d, WOOD, extend=0.1)
            mx, mz = (a[0] + b[0]) * 0.5, (a[1] + b[1]) * 0.5
            lx, lz = abs(b[0] - a[0]) + 0.16, abs(b[1] - a[1]) + 0.16
            self.collider("Rail", "Rails", (mx + out[0] * 0.035, RAIL_H * 0.5, mz + out[1] * 0.035), (lx, RAIL_H + 0.1, lz))
            self.collider("RailEdge", "NotWalkable", (mx + out[0] * 0.25, 0.0, mz + out[1] * 0.25), (lx + 0.3, 2.4, lz + 0.3), into=self.not_walkable)
        self.collider("Landing", "Walk", ((x0 + x1) * 0.5, -0.15, (z0 + z1) * 0.5), (x1 - x0, 0.3, depth), into=self.walk)

    def hoist(self):
        hoist, iron = self.parts["Hoist"], self.parts["Iron"]
        x, z = POST_X[-1], HD + 0.5        # in line with the corner post, clear of the eave, east of the landing
        top = WALL + 1.5
        foot = self.support(hoist, x, z, -0.2, 0.22, name=None)
        hoist.box((x - 0.11, -0.2, z - 0.11), (x + 0.11, top, z + 0.11), 1, FRAME)
        for y in (0.9, 3.1):               # blocks bolting it to the corner post's outer face
            hoist.box((x - 0.08, y - 0.08, HD - FACE), (x + 0.08, y + 0.08, z - 0.11), 2, FRAME)
        arm_y = top - 0.25
        tip = HD + HOIST_REACH             # the arm ends 2.6 m out from the wall
        hoist.box((x - 0.09, arm_y - 0.09, z - 0.45), (x + 0.09, arm_y + 0.09, tip), 2, FRAME)
        hoist.beam((x, arm_y - 1.25, z + 0.11), (x, arm_y - 0.09, z + 1.3), 0.12, 0.12, FRAME, up=(1.0, 0.0, 0.0), extend=0.03)
        # pulley block under the arm's end, rope down to a hook and back to a cleat on the post
        pz = tip - 0.25
        iron.box((x - 0.012, arm_y - 0.24, pz - 0.012), (x + 0.012, arm_y - 0.09, pz + 0.012), 1, IRON, mode="iron")
        hoist.box((x - 0.045, arm_y - 0.5, pz - 0.085), (x + 0.045, arm_y - 0.24, pz + 0.085), 1, FRAME)
        hook_y = arm_y - HOIST_ROPE        # level with the landing, beside its east rail
        hoist.log((x, arm_y - 0.42, pz + 0.05), (x + 0.02, hook_y, pz + 0.05), 0.016, ROPE, sides=6, rings=4, wobble=0.004, v=(0.0, 1.0))
        iron.box((x + 0.008, hook_y - 0.16, pz + 0.035), (x + 0.032, hook_y, pz + 0.065), 1, IRON, mode="iron")
        iron.box((x + 0.008, hook_y - 0.16, pz + 0.035), (x + 0.032, hook_y - 0.135, pz + 0.14), 2, IRON, mode="iron")
        hoist.log((x, arm_y - 0.42, pz - 0.05), (x + 0.115, 1.25, z + 0.02), 0.016, ROPE, sides=6, rings=5, wobble=0.01, v=(0.0, 1.0))
        hoist.box((x + 0.11, 1.15, z - 0.1), (x + 0.15, 1.21, z + 0.1), 2, FRAME)
        base = foot if foot is not None else -0.4
        self.collider("Hoist_Post", "Structure", (x, (base + top) * 0.5, z), (0.24, top - base, 0.24))
        return (x, pz)

    # -- rubble ------------------------------------------------------------------------------------
    def bank(self, a, b, inward, sill_y, full=False, face=0.42):
        """Dry-stone rubble along the line a -> b (x, z), standing `face` inside it; `inward` is the unit
        vector toward the hall's middle. It rises 1-2 m from the ground, up to `sill_y` (a top stone's jitter can
        pass it by a few centimetres, into the sill); `full` packs it to that height everywhere (the palisade line)."""
        rubble, rng = self.parts["Rubble"], self.rng
        dx, dz = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dz)
        ux, uz = dx / length, dz / length
        yaw = math.atan2(uz, ux)
        s = 0.0
        height = rng.uniform(1.3, 1.9)
        while s < length:
            cell = min(rng.uniform(0.9, 1.3), length - s)
            if cell < 0.25:
                break
            mx, mz = a[0] + ux * (s + cell * 0.5) + inward[0] * face, a[1] + uz * (s + cell * 0.5) + inward[1] * face
            g = min(self.g.low(mx - inward[0] * 0.3, mz - inward[1] * 0.3, 0.25), self.g.low(mx, mz, 0.25))
            height = min(2.0, max(1.1, height + rng.uniform(-0.25, 0.25)))
            top = sill_y if full else min(sill_y, g + height)
            if top - g > 0.14:
                # a solid core behind the face stones, so no daylight shows between them
                cx, cz = mx + inward[0] * 0.2, mz + inward[1] * 0.2
                rubble.framed((cx, (g + top) * 0.5 - 0.15, cz), (ux, 0.0, uz), (0.0, 1.0, 0.0), (inward[0], 0.0, inward[1]),
                              (cell * 0.5 + 0.02, (top - g) * 0.5 + 0.13, 0.16), 0, RUBBLE, mode="stone", skip=((1, 0),))
                y = g - 0.12
                while y < top - 0.05:
                    h = min(rng.uniform(0.15, 0.3), top - y)
                    if h < 0.07:
                        break
                    batter = (top - y) * 0.13 + rng.uniform(0.0, 0.05)
                    t = rng.uniform(-0.15, 0.1)
                    while t < cell:
                        w = rng.uniform(0.24, 0.5)
                        px = a[0] + ux * (s + t + w * 0.5) + inward[0] * (face - batter)
                        pz = a[1] + uz * (s + t + w * 0.5) + inward[1] * (face - batter)
                        rubble.stone((px, y + h * 0.5, pz), (w * 1.04, h * 1.08, rng.uniform(0.26, 0.4)), yaw + rng.uniform(-0.14, 0.14))
                        t += w
                    y += h * 0.93
                if rng.random() < 0.55:   # a stone or two fallen at the foot
                    for _ in range(rng.randrange(1, 3)):
                        o = rng.uniform(0.25, 0.7)
                        fx = a[0] + ux * (s + rng.uniform(0, cell)) + inward[0] * (face - o - (top - g) * 0.13)
                        fz = a[1] + uz * (s + rng.uniform(0, cell)) + inward[1] * (face - o - (top - g) * 0.13)
                        sz = rng.uniform(0.18, 0.32)
                        rubble.stone((fx, self.g.at(fx, fz) + sz * 0.3, fz), (sz * 1.3, sz * 0.8, sz))
            s += cell

    def rubble(self):
        sill = -SILL + 0.02
        # the perimeter, behind the posts
        self.bank((-HW, -HD), (HW, -HD), (0.0, 1.0), sill)
        self.bank((HW, HD), (-HW, HD), (0.0, -1.0), sill)
        self.bank((-HW, HD), (-HW, -HD), (1.0, 0.0), sill)
        self.bank((HW, -HD), (HW, HD), (-1.0, 0.0), sill)
        # the palisade line under the floor, packed to the joists: the fence stays closed here
        self.bank((-HW + 0.5, -0.2), (HW - 0.5, -0.2), (0.0, 1.0), -DECK - 0.02, full=True, face=0.2)
        low = min(self.g.low(x * 0.5, z, 0.2) for x in range(-12, 13) for z in (-HD, 0.0, HD)) - 0.6
        low = min(low, min(self.g.low(sx * HW, z * 0.5, 0.2) for sx in (-1, 1) for z in range(-8, 9)) - 0.6)
        t = 0.5
        # nothing passes under the hall: the perimeter and the palisade line are solid from the ground to the floor
        self.collider("Skirt_Camp", "Rubble", (0.0, low * 0.5, -HD + t * 0.5 + 0.05), (HW * 2.0, -low, t))
        self.collider("Skirt_River", "Rubble", (0.0, low * 0.5, HD - t * 0.5 - 0.05), (HW * 2.0, -low, t))
        self.collider("Skirt_West", "Rubble", (-HW + t * 0.5 + 0.05, low * 0.5, 0.0), (t, -low, HD * 2.0))
        self.collider("Skirt_East", "Rubble", (HW - t * 0.5 - 0.05, low * 0.5, 0.0), (t, -low, HD * 2.0))
        self.collider("Skirt", "Rubble", (0.0, low * 0.5, 0.0), (HW * 2.0, -low, WALL_BOX))
        # The boxes above have no renderer and the rubble is left out of the bake: without a volume the ground
        # under the tall corner could keep NavMesh. Its top stays under the boards, so the floor is not cut.
        under = -0.6
        self.collider("UnderFloor", "NotWalkable", (0.0, (low + under) * 0.5, 0.0), (HW * 2.0, under - low, HD * 2.0),
                      into=self.not_walkable)

    def build(self):
        self.hall()
        self.floor()
        self.roof()
        self.doors()
        camp = self.camp_ramp()
        river = self.river_ramp()
        self.landing()
        drop = self.hoist()
        self.rubble()
        return {"campRamp": [round(c, 3) for c in camp], "riverRamp": [round(c, 3) for c in river],
                "hoistDrop": [round(drop[0], 3), round(drop[1], 3)]}


def materials():
    out = []
    assets = _find_up(os.path.dirname(os.path.abspath(__file__)), "Assets")
    wood = os.path.join(assets, "Hivemind", "HDRP(Default)", "MedievalFantasyVillage", "Art", "Textures", "T_ModularWood_Basecolor.png") if assets else ""
    stone = os.path.join(assets, "Hivemind", "HDRP", "TheMessengerMountain", "Art", "Terrain", "T_RockTile_BaseColor.PNG") if assets else ""
    iron = os.path.join(assets, "Hivemind", "CastleOfEternalMists", "HDRP(Default)", "Art", "Textures", "T_Iron_BaseColor.PNG") if assets else ""
    look = {"Wh_Wood": (wood, (1.0, 1.0, 1.0, 1.0)), "Wh_Frame": (wood, (0.72, 0.72, 0.72, 1.0)),
            "Wh_Shingle": (wood, (0.62, 0.68, 0.76, 1.0)), "Wh_ShinglePale": (wood, (1.25, 1.2, 1.05, 1.0)),
            "Wh_Rubble": (stone, (1.1, 1.1, 1.1, 1.0)), "Wh_Iron": (iron, (0.6, 0.6, 0.6, 1.0)),
            "Wh_Rope": ("", (0.45, 0.38, 0.27, 1.0)), "Wh_Tar": (wood, (0.3, 0.29, 0.28, 1.0))}
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
    ground = Ground(os.path.join(source, "warehouse_ground.json"))
    house = Warehouse(ground)
    facts = house.build()

    name = "Warehouse"
    old = bpy.data.collections.get(name)
    kept = set(old.objects) if old else set()
    for part in house.parts:   # checked before anything is deleted
        clash = bpy.data.objects.get(part)
        if clash and clash not in kept:
            raise RuntimeError("An object called '%s' already exists in this Blender session; rename it first" % part)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    mats = materials()
    objects = [mesh.to_object(part, mats, col) for part, mesh in house.parts.items()]
    export(objects, os.path.join(out, name + ".fbx"))
    triangles = {part: mesh.triangles() for part, mesh in house.parts.items()}
    # the parts file goes out with the FBX, before anything else can fail: the two must never be a version apart
    parts = {"floorWorldY": ground.floor_world_y, "centre": ground.centre, "triangles": triangles,
             "total": sum(triangles.values()), "objects": list(house.parts), "walk": house.walk,
             "colliders": house.colliders, "notWalkable": house.not_walkable, "lanterns": house.lanterns}
    parts.update(facts)
    with open(os.path.join(out, name + ".json"), "w") as f:
        json.dump(parts, f, indent=1)
    for o in objects:
        o.name = name + "_" + o.name
    bpy.data.libraries.write(os.path.join(source, name + ".blend"), set(objects) | set(mats), fake_user=True)
    return {"triangles": triangles, "total": parts["total"], "facts": facts}


if __name__ == "__main__":
    result = main()
    print("warehouse:", result)
