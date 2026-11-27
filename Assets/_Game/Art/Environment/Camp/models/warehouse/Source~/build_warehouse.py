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

The cellar (WAREHOUSE_CELLAR_CONCEPTS.md) is a room under the landing and most of the hall, rubble-walled. The terrain is
cut away there by a hole (WarehouseBuilder.PlaceCellar reads `cellar.hole` from the parts file), so the ground
capture still holds the old bank: `Ground.at` answers with the cellar's floor inside the room, and every post
that stands there comes down to it on its own.

The quay (QUAY_CONCEPTS.md) is everything added on the river side after the cellar: a plank quay on piles along the
dock shelf with a moored barge, a lean-to tally room in the palisade corner east of the hall (entered through a
door in the east gable), the dry-stone retaining wall under it and a side room of the cellar behind that wall,
under a second terrain hole (`cellar.sideHole`). These parts draw from their own random stream (`rng2`), and what
they take out of the hall and the cellar is drawn into a scratch mesh, so the hall and the cellar keep their look.
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

# --- Cellar (WAREHOUSE_CELLAR_CONCEPTS.md -> Build brief), in the prefab's frame --------------------
CELLAR_X = (-1.2, 4.9)               # inside faces, west and east
CELLAR_Z = (-1.9, 7.0)               # inside faces, back (0.2 m clear of the buried rocks) and front (the dock wall)
CELLAR_FLOOR_WORLD_Y = 0.1           # set against the dock shelf (y -0.2), not against the hall
CELLAR_TOP = -0.08                   # walls stop under the sealing boards
CELLAR_WALL = 1.2                    # thickness where a wall hides the terrain hole's edge
CELLAR_FRONT = 0.35                  # the front wall, behind the landing's front posts
CELLAR_DOOR = (2.88, 4.02, 2.14)     # opening: min x, max x, height (the door prefab's leaf is 1.1 m wide)
CELLAR_DOOR_SCALE_Y = 1.06           # Poor small door is 2.01 m tall: stretched to the camp's 2.1 m headroom
CELLAR_JAMB = 0.14
CELLAR_BAY_X = 1.9                   # west of this the front wall is thick: the ground the hole leaves stands in it
CELLAR_BAY_IN = 0.55                 # ... this far into the room
CELLAR_PIER = (0.5, 2.0)             # south-west corner pier (width, length): a buried rock reaches into the room there
CELLAR_BEAM_Z = 1.0                  # a beam on two posts carries the joists midway between the back wall and the hall's north wall
CELLAR_HATCH = (-0.8, 0.2, 2.3, 3.3) # min x, max x, min z, max z in the hall's floor
CELLAR_HOLE = (-1.40, -2.85, 5.60, 6.95)   # terrain hole cells must lie wholly inside: min x, min z, max x, max z
CELLAR_SHOULDER = (5.3, 6.1)         # east of the landing the wall steps down the bank

# --- Quay, tally room, retaining wall, cellar side room (QUAY_CONCEPTS.md -> Build brief) -------------
WATER_WORLD_Y = -2.0
QUAY_X = (-8.5, 9.2)                 # the deck, west end and the dock spur
QUAY_Z = (9.0, 13.5)                 # the shelf's edge and the river edge
QUAY_DECK_WORLD_Y = 0.0              # the shelf is at y -0.17: no step
QUAY_LANE = 2.0                      # the deck's south strip stays clear for carts
QUAY_PILE_STEP = 2.4
QUAY_MOORINGS = (-5.6, -1.9, 3.2, 7.6)
QUAY_LANTERNS = (-3.7, 5.6)
QUAY_LADDER_X = -0.65
QUAY_PLANK_X = 1.0                   # where the gangplank lands on the deck
BARGE = (-1.4, 5.2, 14.05, 16.25)    # hull: min x, max x, min z, max z
BARGE_RAIL_WORLD_Y = -1.15           # gunwale; the hold's floor is 0.6 m under it
TALLY_X = (6.0, 9.3)                 # the hall's east wall line and the dock spur's logs
TALLY_Z = (0.2, 5.6)                 # the north palisade's logs and the retaining wall's face: the plank wall stands on
                                     # the stone (the user's call at the second pause; first built to the hall's line, z 4.0)
TALLY_DOOR = (2.3, 3.44, 2.14)       # in the hall's east gable: min z, max z, height (the door prefab's leaf is 1.1 m wide)
TALLY_DOOR_SCALE_Y = 1.06
TALLY_HATCH = (7.1, 7.9, 1.0, 1.75)  # in the plank wall: min x, max x, sill, head
TALLY_ROOF = (3.95, 2.5)             # top of the roof boards at the palisade and over the plank wall
TALLY_EAVE = 0.45
RETAIN_X = (6.1, 9.25)               # from the cellar's shoulder to the dock spur
RETAIN_Z = 5.7                       # its face: past the bank's toe
SIDE_X = (6.25, 8.2)                 # cellar side room, inside faces (a buried rock stands at x 9.0)
SIDE_Z = (0.95, 4.4)
SIDE_TOP = -1.7                      # its ceiling: a rock's underside is at -1.5
SIDE_PASSAGE = (2.3, 3.35, 2.1)      # through the cellar's east wall: min z, max z, height
SIDE_HOLE = (5.45, 0.70, 8.50, 4.70) # terrain hole cells must lie wholly inside: min x, min z, max x, max z
SIDE_TUNNEL = (6.7, 7.9, 2.0)        # the smuggling tunnel's way out through the side room's south wall: min x, max x, height.
                                     # The tunnel itself is the gang house's model (models/gang house): its first point
                                     # (TUNNEL_PATH) stands on this wall's outer face, in the middle of this opening

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

WOOD, FRAME, SHINGLE, PALE, RUBBLE, IRON, ROPE, TAR, EARTH, TARP = range(10)
MATERIALS = ("Wh_Wood", "Wh_Frame", "Wh_Shingle", "Wh_ShinglePale", "Wh_Rubble", "Wh_Iron", "Wh_Rope", "Wh_Tar",
             "Wh_Earth", "Wh_Tarp")
TILE_OF = {EARTH: 3.0}    # box projection, metres per tile (rubble: STONE_TILE)
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
        self.cellar_floor = CELLAR_FLOOR_WORLD_Y - self.floor_world_y

    def at(self, x, z):
        """The ground a post stands on: the cellar's floor inside the room, the captured ground elsewhere."""
        if CELLAR_X[0] < x < CELLAR_X[1] and CELLAR_Z[0] < z < CELLAR_Z[1]:
            return self.cellar_floor
        if SIDE_X[0] < x < SIDE_X[1] and SIDE_Z[0] < z < SIDE_Z[1]:
            return self.cellar_floor
        return self.raw(x, z)

    def raw(self, x, z):
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

    def raw_low(self, x, z, r=0.15):
        return min(self.raw(x + dx, z + dz) for dx in (-r, 0, r) for dz in (-r, 0, r))

    def raw_high(self, x, z, r=0.15):
        return max(self.raw(x + dx, z + dz) for dx in (-r, 0, r) for dz in (-r, 0, r))


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
                    tile = TILE_OF.get(mat, STONE_TILE)
                    uv = [(off[0] + local[i][b] / tile, off[1] + local[i][c] / tile) for i in idx]
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

    def ring(self, centre, radius, tube, mat, segs=10, sides=5):
        """A loop of rope lying flat round `centre` (a torus about the y axis)."""
        base = len(self.verts)
        centres = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            centres.append(to_blender((centre[0] + radius * math.cos(a), centre[1], centre[2] + radius * math.sin(a))))
            for j in range(sides):
                b = 2.0 * math.pi * j / sides
                r = radius + tube * math.cos(b)
                self.verts.append(to_blender((centre[0] + r * math.cos(a), centre[1] + tube * math.sin(b), centre[2] + r * math.sin(a))))
        for i in range(segs):
            ni = (i + 1) % segs
            for j in range(sides):
                nj = (j + 1) % sides
                quad = [base + i * sides + j, base + ni * sides + j, base + ni * sides + nj, base + i * sides + nj]
                p = [self.verts[k] for k in quad]
                n = _normal(p[0], p[1], p[2])
                if sum(n[k] * (p[0][k] - centres[i][k]) for k in range(3)) < 0:
                    quad.reverse()
                self.faces.append(quad)
                self.uvs.append([(0.1 + 0.05 * (k % 2), 0.1 + 0.05 * (k // 2)) for k in range(4)])
                self.mats.append(mat)
                self.smooth.append(True)

    def sheet(self, grid, mat, tile=1.0):
        """A two-sided cloth through rows of points (prefab frame), box-projected from above."""
        rows, cols = len(grid), len(grid[0])
        for flip in (False, True):
            base = len(self.verts)
            for row in grid:
                self.verts.extend(to_blender(p) for p in row)
            for r in range(rows - 1):
                for c in range(cols - 1):
                    cells = ((r, c), (r, c + 1), (r + 1, c + 1), (r + 1, c))
                    quad = [base + i * cols + j for i, j in cells]
                    uv = [(grid[i][j][0] / tile, grid[i][j][2] / tile) for i, j in cells]
                    if flip:
                        quad.reverse()
                        uv.reverse()
                    self.faces.append(quad)
                    self.uvs.append(uv)
                    self.mats.append(mat)
                    self.smooth.append(True)

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
                      ("Frame", "Walls", "Floor", "Roof", "Doors", "Ramps", "Landing", "Hoist", "Rubble", "Iron",
                       "Cellar", "CellarWalls")}
        # The quay's parts have their own random stream; what they take out of the hall and the cellar is drawn
        # into `scratch` (same stream as the hall, never exported), so every other piece keeps its place and look.
        self.rng2 = random.Random(seed * 7 + 3)
        self.parts.update({name: Mesh(self.rng2) for name in
                           ("Quay", "Barge", "Tally", "TallyRoof", "TallyFurniture", "Retaining", "CellarSide")})
        self.scratch = Mesh(self.rng)
        # The tunnel's opening (added after the quay) has a third stream, for the same reason.
        self.rng3 = random.Random(seed * 13 + 5)
        self.parts["CellarTunnel"] = Mesh(self.rng3)
        self.cut_boards, self.burnt = [], None
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
                # the tally room's door stands in the east gable's north bay: no girt across it
                girt = self.scratch if side > 0 and za < TALLY_DOOR[1] and zb > TALLY_DOOR[0] else frame
                girt.box((x + side * 0.03, 1.92, a0), (x + side * 0.13, 2.07, b0), 2, FRAME)
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
                    cut = (side > 0 and ya < TALLY_DOOR[2] + 0.18 and b > TALLY_DOOR[0] - CELLAR_JAMB
                           and a < TALLY_DOOR[1] + CELLAR_JAMB)
                    (self.scratch if cut else walls).box((xi - side * (BOARD + out), ya, a + 0.002), (xi - side * out, yb - 0.003, b - 0.002), 2, WOOD)
                    if cut:   # tally() puts back what stands beside and over the doorway
                        self.cut_boards.append((a, b, ya, yb, out))
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
            if side < 0:
                self.collider("Wall_West", "Walls", (side * (HW - WALL_BOX * 0.5), WALL * 0.5, 0.0), (WALL_BOX, WALL, HD * 2.0))
            else:   # open at the tally room's door
                da, db, dh = TALLY_DOOR
                wx = HW - WALL_BOX * 0.5
                self.collider("Wall_East", "Walls", (wx, WALL * 0.5, (da - HD) * 0.5), (WALL_BOX, WALL, da + HD))
                self.collider("Wall_East_North", "Walls", (wx, WALL * 0.5, (db + HD) * 0.5), (WALL_BOX, WALL, HD - db))
                self.collider("Wall_East_Header", "Walls", (wx, (dh + WALL) * 0.5, (da + db) * 0.5), (WALL_BOX, WALL - dh, db - da))
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
            if CELLAR_X[0] < x < CELLAR_X[1]:           # the cellar's front wall stands between the two rows
                continue
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
            if CELLAR_X[0] - 0.3 < mx < CELLAR_X[1] + 0.3 and CELLAR_Z[0] < mz < CELLAR_Z[1]:   # the cellar is open here
                s += cell
                continue
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
        low = min(self.g.raw_low(x * 0.5, z, 0.2) for x in range(-12, 13) for z in (-HD, 0.0, HD)) - 0.6
        low = min(low, min(self.g.raw_low(sx * HW, z * 0.5, 0.2) for sx in (-1, 1) for z in range(-8, 9)) - 0.6)
        t = 0.5
        # nothing passes under the hall: the perimeter and the palisade line are solid from the ground to the floor
        self.collider("Skirt_Camp", "Rubble", (0.0, low * 0.5, -HD + t * 0.5 + 0.05), (HW * 2.0, -low, t))
        # under the river wall the line is open over the cellar, whose own walls close it on both sides
        cx0, cx1 = CELLAR_X
        self.collider("Skirt_River", "Rubble", ((-HW + cx0) * 0.5, low * 0.5, HD - t * 0.5 - 0.05), (cx0 + HW, -low, t))
        self.collider("Skirt_River_East", "Rubble", ((HW + cx1) * 0.5, low * 0.5, HD - t * 0.5 - 0.05), (HW - cx1, -low, t))
        self.collider("Skirt_West", "Rubble", (-HW + t * 0.5 + 0.05, low * 0.5, 0.0), (t, -low, HD * 2.0))
        # the east line is open at the passage to the cellar's side room
        pa, pb, ph = SIDE_PASSAGE
        head = self.g.cellar_floor + ph
        ex = HW - t * 0.5 - 0.05
        self.collider("Skirt_East", "Rubble", (ex, low * 0.5, (pa - HD) * 0.5), (t, -low, pa + HD))
        self.collider("Skirt_East_North", "Rubble", (ex, low * 0.5, (pb + HD) * 0.5), (t, -low, HD - pb))
        self.collider("Skirt_East_Header", "Rubble", (ex, head * 0.5, (pa + pb) * 0.5), (t, -head, pb - pa))
        # the palisade line: since the cellar reaches south of it, the cellar's side walls close it there
        self.collider("Skirt", "Rubble", ((-HW + cx0) * 0.5, low * 0.5, 0.0), (cx0 + HW, -low, WALL_BOX))
        self.collider("Skirt_Line_East", "Rubble", ((HW + cx1) * 0.5, low * 0.5, 0.0), (HW - cx1, -low, WALL_BOX))
        # The boxes above have no renderer and the rubble is left out of the bake: without a volume the ground
        # under the tall corner could keep NavMesh. Its top stays under the boards, so the floor is not cut.
        under = -0.6
        # ... and it leaves the cellar out: its floor is walked on.
        cz0 = CELLAR_Z[0]
        for name, xa, xb, za, zb in (("UnderFloor", -HW, HW, -HD, cz0), ("UnderFloor_West", -HW, cx0, cz0, HD),
                                     ("UnderFloor_East", cx1, HW, cz0, pa), ("UnderFloor_East_North", cx1, HW, pb, HD)):
            self.collider(name, "NotWalkable", ((xa + xb) * 0.5, (low + under) * 0.5, (za + zb) * 0.5),
                          (xb - xa, under - low, zb - za), into=self.not_walkable)

    # -- cellar ------------------------------------------------------------------------------------
    def face(self, a, b, y0, y1, out, holes=(), top_of=None, bottom_of=None, room=False, walls=None, rng=None, burn=None):
        """Coursed dry-stone on the upright face a -> b (x, z) from y0 to y1; `out` is the way the face looks.
        `holes`: (from, to, y from, y to) along the line. A stone has no back face: a core stands behind it.
        `room`: a face seen only from the cellar's floor, whose low stones need no underside and high ones no top.
        `walls`, `rng`: another mesh and random stream (the quay's parts). `burn`: (from, to, y to) along the line:
        stones reaching into it are drawn into the scratch mesh instead (a doorway cut later), and `self.burnt`
        records how far they reached."""
        walls = self.parts["CellarWalls"] if walls is None else walls
        rng = self.rng if rng is None else rng
        dx, dz = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dz)
        ux, uz = dx / length, dz / length
        az = (out[0], 0.0, out[1])
        y = y0
        while y < y1 - 0.06:
            h = min(rng.uniform(0.15, 0.3), y1 - y)
            if y1 - (y + h) < 0.1:
                h = y1 - y
            spans = [(0.0, length)]
            for s0, s1, ya, yb in holes:
                if y + h > ya + 0.01 and y < yb - 0.01:
                    spans = [p for lo, hi in spans for p in ((lo, min(hi, s0)), (max(lo, s1), hi)) if p[1] - p[0] > 0.05]
            for lo, hi in spans:
                s = lo
                while s < hi - 0.01:
                    w = rng.uniform(0.26, 0.66)
                    if hi - (s + w) < 0.2:
                        w = hi - s
                    mid = s + w * 0.5
                    back = 0.13 + rng.uniform(-0.05, 0.03)     # some stones stand proud, some sit back
                    tilt = rng.uniform(-0.07, 0.07)
                    turn = rng.uniform(-0.09, 0.09)             # and none lies square to the wall
                    if (top_of is None or y + h <= top_of(mid) + 0.04) and (bottom_of is None or y + h > bottom_of(mid) - 0.1):
                        c = (a[0] + ux * mid - out[0] * back, y + h * 0.5, a[1] + uz * mid - out[1] * back)
                        along = norm((ux + out[0] * turn, tilt, uz + out[1] * turn))
                        skip = ((2, 0),)
                        if room:
                            eye = y + h * 0.5 - self.g.cellar_floor
                            skip += ((1, 0),) if eye < 1.2 else ((1, 1),) if eye > 2.1 else ()
                        target = walls
                        if burn and s < burn[1] and s + w > burn[0] and y < burn[2]:
                            target = self.scratch
                            m = self.burnt or [s, s + w, y + h]
                            self.burnt = [min(m[0], s), max(m[1], s + w), max(m[2], y + h)]
                        target.framed(c, along, (0.0, 1.0, 0.0), az, (w * 0.5 + 0.01, h * 0.5 + 0.008, 0.15),
                                      0, RUBBLE, skip=skip, mode="stone", jitter=0.2)
                    s += w
            y += h

    def core(self, x0, x1, y0, y1, z0, z1, inset=0.08, into=None):
        """The body of a rubble wall, a little inside its faces: what shows in the joints between the stones."""
        into = self.parts["CellarWalls"] if into is None else into
        into.box((x0 + inset, y0, z0 + inset), (x1 - inset, y1 - 0.02, z1 - inset), 0, RUBBLE, skip=((1, 0),), mode="stone")

    def cap(self, x0, x1, z0, z1, y, walls=None, rng=None):
        """Flat stones on a wall top that is open to the sky."""
        walls = self.parts["CellarWalls"] if walls is None else walls
        rng = self.rng if rng is None else rng
        z = z0
        while z < z1 - 0.05:
            d = min(rng.uniform(0.4, 0.6), z1 - z)
            x = x0
            while x < x1 - 0.05:
                w = min(rng.uniform(0.38, 0.6), x1 - x)
                h = rng.uniform(0.1, 0.17)
                walls.stone((x + w * 0.5, y + h * 0.5 - 0.04, z + d * 0.5), (w * 1.05, h, d * 1.05), yaw=rng.uniform(-0.12, 0.12))
                x += w
            z += d

    def cellar(self):
        """The room under the landing and most of the hall: dry-stone rubble on every side, a door to the dock
        shelf, a shut hatch and a ladder to the hall."""
        cel, walls, iron, doors = (self.parts[k] for k in ("Cellar", "CellarWalls", "Iron", "Doors"))
        rng, g = self.rng, self.g
        x0, x1 = CELLAR_X
        z0, z1 = CELLAR_Z
        fy, top = g.cellar_floor, CELLAR_TOP
        zf = z1 + CELLAR_FRONT                       # the front wall's outer face
        door_a, door_b, door_h = CELLAR_DOOR
        rough_a, rough_b = door_a - CELLAR_JAMB, door_b + CELLAR_JAMB
        lintel_top = fy + door_h + 0.26
        bay_x, bay_z = CELLAR_BAY_X, z1 - CELLAR_BAY_IN    # the thick west bay of the front wall
        pier_w, pier_l = CELLAR_PIER
        pier_x, pier_z = x0 + pier_w, z0 + pier_l
        beam_z = CELLAR_BEAM_Z
        sx0, sx1 = CELLAR_SHOULDER
        strip_w = x0 - 0.25                          # west of the landing a strip stands full height; the rest stays under the ramp
        west_out, west_top = x0 - 0.8, -0.62
        # West of the landing the river ramp's deck falls away overhead: nothing there may stand higher than this,
        # or it comes up through the deck's boards.
        ramp_x, ramp_top = LANDING[0], -0.2

        # floor: packed earth, a few flat stones trodden in
        cel.box((x0 - 0.1, fy - 0.3, z0 - 0.1), (x1 + 0.1, fy, z1 + 0.1), 0, EARTH, skip=((1, 0),), mode="stone")
        for sx, sz in ((3.45, 6.3), (3.1, 5.3), (3.9, 5.0), (2.6, 4.2), (1.2, 3.0), (0.2, 4.6), (4.2, 2.2),
                       (2.0, 0.3), (3.0, -0.9), (0.9, -0.6)):
            walls.stone((sx + rng.uniform(-0.2, 0.2), fy - 0.015, sz + rng.uniform(-0.2, 0.2)),
                        (rng.uniform(0.4, 0.65), 0.07, rng.uniform(0.32, 0.5)))

        # east wall's shoulder: steps down the bank north of the hall, each step just over the ground it hides
        steps, edges, prev = [], (HD, HD + 1.15, HD + 2.25, zf), top - 0.5
        for za, zb in zip(edges, edges[1:]):
            high = max(g.raw_high(x, z, 0.1) for x in (sx0 + 0.1, (sx0 + sx1) * 0.5, sx1) for z in (za, (za + zb) * 0.5, zb))
            prev = min(prev, high + 0.3)
            steps.append((za, zb, prev))

        def shoulder_top(z):
            for za, zb, t in steps:
                if z <= zb + 1e-4:
                    return t
            return steps[-1][2]

        shelf = min(g.raw_low(x * 0.5, zf + 0.1, 0.1) for x in range(-4, 13)) - 0.15

        # cores
        self.core(x0 - CELLAR_WALL, x1 + 0.7, fy - 0.3, top, z0 - CELLAR_WALL, z0)   # back
        self.core(x0 - 0.08, pier_x, fy - 0.3, top, z0 - 0.08, pier_z)         # the corner pier
        self.core(x0 - CELLAR_WALL, x0, fy - 0.3, top, z0 - 0.3, HD)           # west, under the hall
        self.core(strip_w, x0, fy - 0.3, ramp_top, HD - 0.16, zf)              # west, the strip up to the ramp's head
        self.core(west_out, strip_w + 0.16, shelf, west_top, HD - 0.16, zf)    # west, under the ramp's head
        self.core(x1, x1 + 0.7, fy - 0.3, top, z0 - 0.3, HD, into=self.scratch)   # east, under the hall: side_room() builds it round its passage
        self.core(x1, sx0, fy - 0.3, top, HD - 0.16, zf)                       # east, the full-height strip
        for za, zb, t in steps:
            self.core(sx0 - 0.16, sx1, shelf, t, za - 0.08, zb + 0.08)
            self.cap(sx0, sx1, za, zb, t)
        self.core(bay_x - 0.16, rough_a, fy - 0.3, top, z1, zf)               # front, west of the door
        self.core(rough_b, sx0 + 0.16, fy - 0.3, top, z1, zf)                  # front, east of the door
        self.core(rough_a - 0.16, rough_b + 0.16, lintel_top, top, z1, zf)     # front, over the lintel
        # front, the thick west bay: under the ramp's head, then full height
        walls.box((x0, fy - 0.3, bay_z + 0.08), (ramp_x, ramp_top - 0.02, zf - 0.08), 0, RUBBLE, skip=((1, 0),), mode="stone")
        walls.box((ramp_x, fy - 0.3, bay_z + 0.08), (bay_x - 0.08, top - 0.02, zf - 0.08), 0, RUBBLE, skip=((1, 0),), mode="stone")
        walls.box((west_out + 0.08, shelf, z1), (x0, west_top - 0.02, zf - 0.08), 0, RUBBLE, skip=((1, 0),), mode="stone")

        # rubble faces
        holes = ((rough_a - bay_x, rough_b - bay_x, fy - 1.0, lintel_top),)
        holes_out = ((rough_a - west_out, rough_b - west_out, fy - 1.0, lintel_top),)
        bay_top = lambda s: ramp_top if x0 + s < ramp_x else top
        self.face((pier_x, z0), (x1, z0), fy - 0.05, top, (0.0, 1.0), room=True)                        # back
        self.face((pier_x, z0), (pier_x, pier_z), fy - 0.05, top, (1.0, 0.0), room=True)                # the pier
        self.face((x0, pier_z), (pier_x, pier_z), fy - 0.05, top, (0.0, 1.0), room=True)
        self.face((x0, pier_z), (x0, bay_z), fy - 0.05, top, (1.0, 0.0), room=True,                    # west, inside
                  top_of=lambda s: top if pier_z + s < HD - 0.2 else ramp_top)
        self.face((x1, z1), (x1, z0), fy - 0.05, top, (-1.0, 0.0), room=True,                           # east, inside
                  burn=(z1 - SIDE_PASSAGE[1], z1 - SIDE_PASSAGE[0], fy + SIDE_PASSAGE[2] + 0.2))
        self.face((x0, bay_z), (bay_x, bay_z), fy - 0.05, top, (0.0, -1.0), top_of=bay_top, room=True)   # front bay, inside
        self.face((bay_x, bay_z), (bay_x, z1), fy - 0.05, top, (1.0, 0.0), room=True)                # ... and its return
        self.face((bay_x, z1), (x1, z1), fy - 0.05, top, (0.0, -1.0), holes, room=True)                # front, inside

        def front_top(s):
            x = west_out + s
            return west_top if x < strip_w else ramp_top if x < ramp_x else top if x < sx0 else steps[-1][2]

        self.face((west_out, zf), (sx1, zf), shelf, top, (0.0, 1.0), holes_out,                         # front, outside
                  top_of=front_top, bottom_of=lambda s: g.raw(west_out + s, zf + 0.1))
        self.face((sx1, HD), (sx1, zf), shelf, steps[0][2], (1.0, 0.0),                                  # shoulder, east
                  top_of=lambda s: shoulder_top(HD + s), bottom_of=lambda s: g.raw(sx1 + 0.1, HD + s))
        self.face((sx0, HD), (sx0, zf), steps[-1][2], top, (1.0, 0.0),                                   # the strip above it
                  bottom_of=lambda s: shoulder_top(HD + s))
        for (za, zb, upper), (_, _, lower) in zip(steps, steps[1:]):                                     # the steps' risers
            if upper - lower > 0.12:
                self.face((sx0, zb), (sx1, zb), lower, upper, (0.0, 1.0))
        self.face((west_out, zf), (west_out, HD), shelf, west_top, (-1.0, 0.0),                          # west, under the ramp
                  bottom_of=lambda s: g.raw(west_out - 0.1, zf - s))
        self.face((strip_w, zf), (strip_w, HD), west_top, ramp_top, (-1.0, 0.0))

        X, Y, Z = (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)
        for _ in range(9):                             # loose stones at the west bay's foot, outside ...
            sx = rng.uniform(west_out + 0.3, bay_x - 0.2)
            sz = zf + rng.uniform(0.05, 0.3)
            size = rng.uniform(0.2, 0.36)
            if abs(sx - (LANDING[0] + 0.25)) > 0.4:    # clear of the landing's west post
                walls.stone((sx, g.raw(sx, sz) + size * 0.3, sz), (size * 1.4, size * 0.8, size))
        for _ in range(5):                             # ... and inside
            size = rng.uniform(0.2, 0.34)
            walls.stone((rng.uniform(x0 + 0.3, bay_x - 0.3), fy + size * 0.3, bay_z - rng.uniform(0.3, 0.5)), (size * 1.4, size * 0.8, size))

        # door: jambs, lintel, threshold; the leaf is the game's door prefab (WarehouseBuilder places it)
        for xa, xb in ((rough_a, door_a), (door_b, rough_b)):
            cel.box((xa, fy - 0.05, z1 - 0.02), (xb, fy + door_h, zf + 0.03), 1, FRAME)
        cel.box((rough_a - 0.2, fy + door_h, z1 - 0.05), (rough_b + 0.2, lintel_top, zf + 0.06), 0, FRAME)
        cel.box((door_a, fy - 0.05, z1 + 0.04), (door_b, fy + 0.045, zf - 0.02), 0, FRAME)
        step_top = fy - 0.14
        door_mid = (door_a + door_b) * 0.5
        walls.framed((door_mid, step_top - 0.13, zf + 0.26), X, Y, Z, (0.82, 0.13, 0.25), 0, RUBBLE, mode="stone", jitter=0.04)
        for sx, sz in ((3.3, 8.25), (3.75, 8.95), (3.2, 9.6)):
            walls.stone((sx, g.raw(sx, sz) + 0.01, sz), (rng.uniform(0.5, 0.7), 0.07, rng.uniform(0.38, 0.5)))

        # the hall's floor and the landing's deck are sealed from below: no daylight falls through the boards
        hx0, hx1, hz0, hz1 = CELLAR_HATCH
        lo, hi = x0 - 0.06, x1 + 0.06
        for za, zb in cel.rows(z0 - 0.06, zf, 0.16):
            spans = [(lo if zb < HD else ramp_x, hi)]      # north of the hall the layer starts at the landing's edge
            if zb > hz0 and za < hz1:
                spans = [(lo, hx0), (hx1, hi)]
            for xa, xb in spans:
                for pa, pb in cel.pieces(xa, xb, 2.6, 4.0):
                    cel.box((pa, -0.075, za), (pb, -0.052, zb), 0, WOOD, skip=((1, 1),))
        # ... and under the ramp's head a board closes the strip between the west wall and the landing
        cel.box((x0 - 0.1, ramp_top, HD - 0.1), (ramp_x + 0.02, ramp_top + 0.022, zf), 2, WOOD, skip=((1, 1),))
        # joists under the hall's floor, trimmed round the hatch
        for jx in (hx0 - 0.15, hx1 + 0.15, 1.2, 2.05, 2.9, 3.75, 4.6):
            cel.box((jx - 0.07, -0.26, z0 - 0.1), (jx + 0.07, -0.075, HD - INNER), 2, FRAME)
        for jz in (hz0 - 0.1, hz1 + 0.1):
            cel.box((hx0 - 0.08, -0.24, jz - 0.06), (hx1 + 0.08, -0.075, jz + 0.06), 0, FRAME)

        # a beam on two posts under the joists: the hall's floor spans 5.7 m here
        beam_posts = [px for px in POST_X if x0 < px < x1]
        cel.box((x0 - 0.1, -0.48, beam_z - 0.1), (x1 + 0.1, -0.26, beam_z + 0.1), 0, FRAME)
        for bx in beam_posts:
            walls.stone((bx, fy + 0.03, beam_z), (0.5, 0.12, 0.5), yaw=rng.uniform(-0.3, 0.3))
            cel.box((bx - POST * 0.5, fy + 0.07, beam_z - POST * 0.5), (bx + POST * 0.5, -0.48, beam_z + POST * 0.5), 1, FRAME)

        # hatch: a lid of boards lying in the hall's floor, strap hinges on its north edge, an iron ring; shut
        for xa, xb in doors.rows(hx0, hx1, 0.16):
            doors.box((xa + 0.003, 0.0, hz0 + 0.004), (xb - 0.003, 0.016 + rng.uniform(0.0, 0.004), hz1 - 0.004), 2, WOOD, skip=((1, 0),))
        for hx in (hx0 + 0.22, hx1 - 0.22):
            iron.box((hx - 0.024, 0.016, hz1 - 0.55), (hx + 0.024, 0.026, hz1 + 0.07), 2, IRON, mode="iron")
        iron.box(((hx0 + hx1) * 0.5 - 0.06, 0.018, hz0 + 0.1), ((hx0 + hx1) * 0.5 + 0.06, 0.03, hz0 + 0.13), 0, IRON, mode="iron")

        # ladder under the hatch, leaning north
        lx, foot_z, head_z, head_y = (hx0 + hx1) * 0.5, hz0 - 0.13, hz1 - 0.05, -0.1
        for side in (-0.25, 0.25):
            cel.beam((lx + side, fy, foot_z), (lx + side, head_y, head_z), 0.06, 0.07, FRAME, up=Z)
        rungs = 13
        for i in range(1, rungs + 1):
            t = i / (rungs + 1.0)
            ry, rz = fy + (head_y - fy) * t, foot_z + (head_z - foot_z) * t
            cel.beam((lx - 0.27, ry, rz), (lx + 0.27, ry, rz), 0.045, 0.045, FRAME)
        lean = math.degrees(math.atan2(head_z - foot_z, head_y - fy))
        ladder_len = math.hypot(head_z - foot_z, head_y - fy)

        # lantern brackets: outside at the door's upper east corner, inside on the middle pillar's south face
        hook_y = fy + 2.5
        ox = door_b + 0.42
        iron.box((ox - 0.012, hook_y, zf - 0.03), (ox + 0.012, hook_y + 0.024, zf + 0.34), 2, IRON, mode="iron")
        iron.beam((ox, hook_y - 0.22, zf - 0.02), (ox, hook_y, zf + 0.22), 0.018, 0.018, IRON, up=X)
        self.lanterns.append({"name": "CellarLantern_Door", "position": [round(ox, 3), round(hook_y - 0.34, 3), round(zf + 0.3, 3)], "out": 1})
        px, pz = (LANDING[0] + LANDING[1]) * 0.5, HD + LANDING[2] * 0.5 - 0.12
        iron.box((px - 0.012, hook_y, pz - 0.32), (px + 0.012, hook_y + 0.024, pz), 2, IRON, mode="iron")
        iron.beam((px, hook_y - 0.2, pz), (px, hook_y, pz - 0.2), 0.018, 0.018, IRON, up=X)
        self.lanterns.append({"name": "CellarLantern_Inside", "position": [round(px, 3), round(hook_y - 0.34, 3), round(pz - 0.28, 3)],
                              "out": -1, "always": True})
        qx, qz = beam_posts[0], beam_z - POST * 0.5      # and one for the back half, on the beam's west post
        iron.box((qx - 0.012, hook_y, qz - 0.32), (qx + 0.012, hook_y + 0.024, qz), 2, IRON, mode="iron")
        iron.beam((qx, hook_y - 0.2, qz), (qx, hook_y, qz - 0.2), 0.018, 0.018, IRON, up=X)
        self.lanterns.append({"name": "CellarLantern_Back", "position": [round(qx, 3), round(hook_y - 0.34, 3), round(qz - 0.28, 3)],
                              "out": -1, "always": True})

        # colliders, walk surface, Not Walkable
        def wall(name, xa, xb, za, zb, ya=None, yb=None):
            ya, yb = fy - 0.3 if ya is None else ya, top if yb is None else yb
            self.collider(name, "Cellar", ((xa + xb) * 0.5, (ya + yb) * 0.5, (za + zb) * 0.5), (xb - xa, yb - ya, zb - za))

        wall("Wall_West", x0 - CELLAR_WALL, x0, z0, HD)
        wall("Wall_West_Front", strip_w, x0, HD, zf, None, ramp_top)
        wall("Wall_West_Ramp", west_out, strip_w, HD, zf + 0.05, shelf, west_top)
        wall("Wall_East", x1, x1 + 0.7, z0, SIDE_PASSAGE[0])
        wall("Wall_East_North", x1, x1 + 0.7, SIDE_PASSAGE[1], HD)
        wall("Wall_East_Header", x1, x1 + 0.7, SIDE_PASSAGE[0], SIDE_PASSAGE[1], fy + SIDE_PASSAGE[2])
        self.shoulder_top = shoulder_top
        wall("Wall_East_Front", x1, sx0, HD, zf)
        wall("Wall_East_Shoulder", sx0, sx1, HD, zf, shelf, steps[0][2])
        wall("Wall_Back", x0 - CELLAR_WALL, x1 + 0.7, z0 - 1.0, z0)
        wall("Wall_Pier", x0, pier_x, z0, pier_z)
        wall("Wall_Front_Ramp", strip_w, ramp_x, z1, zf + 0.05, None, ramp_top)
        wall("Wall_Front_West", ramp_x, door_a, z1, zf + 0.05)
        wall("Wall_Front_Bay_Ramp", x0, ramp_x, bay_z - 0.05, z1, None, ramp_top)
        wall("Wall_Front_Bay", ramp_x, bay_x + 0.05, bay_z - 0.05, z1)
        wall("Wall_Front_East", door_b, sx0, z1, zf + 0.05)
        wall("Wall_Front_Header", door_a, door_b, z1, zf + 0.05, fy + door_h, top)
        wall("DoorStep", door_mid - 0.82, door_mid + 0.82, zf, zf + 0.5, step_top - 0.4, step_top)
        pz_wall = HD - FACE - POST * 0.5
        hall_posts = [px_wall for px_wall in POST_X if x0 < px_wall < x1]
        for px_wall in hall_posts:
            self.collider("CellarPost", "Cellar", (px_wall, (fy - SILL) * 0.5, pz_wall), (POST, -SILL - fy, POST))
        for bx in beam_posts:
            self.collider("CellarBeamPost", "Cellar", (bx, (fy - 0.26) * 0.5, beam_z), (POST, -0.26 - fy, POST))
        self.collider("Ladder", "Cellar", (lx, (fy + head_y) * 0.5, (foot_z + head_z) * 0.5), (0.62, ladder_len, 0.1), (lean, 0.0, 0.0))
        self.collider("Cellar", "Walk", ((x0 + x1) * 0.5, fy - 0.15, (z0 + z1) * 0.5), (x1 - x0 + 0.2, 0.3, z1 - z0 + 0.2), into=self.walk)
        self.collider("CellarShoulder", "NotWalkable", ((sx0 + sx1) * 0.5 + 0.05, steps[-1][2] + 1.6, (HD + zf) * 0.5),
                      (sx1 - sx0 + 0.3, steps[0][2] - steps[-1][2] + 3.2, zf - HD + 0.2), into=self.not_walkable)

        def r3(values):
            return [round(v, 3) for v in values]

        landing_posts = [(x, HD + LANDING[2] * 0.5) for x in (LANDING[0] + 0.25, (LANDING[0] + LANDING[1]) * 0.5, LANDING[1] - 0.25)]
        return {"floorY": round(fy, 4), "inside": r3((x0, z0, x1, z1)), "hole": r3(CELLAR_HOLE),
                "door": {"position": r3((door_b, fy, zf - 0.1)), "yaw": 180.0, "scaleY": CELLAR_DOOR_SCALE_Y, "openAngle": -90.0,
                         "opening": r3((door_a, door_b, door_h))},
                "hatch": r3(CELLAR_HATCH),
                "restricted": {"centre": r3(((x0 + x1) * 0.5, fy + 2.15, (z0 + z1) * 0.5)), "size": r3((x1 - x0 - 0.1, 4.3, z1 - z0 - 0.1))},
                "posts": [r3((px_wall, pz_wall)) for px_wall in hall_posts] + [r3((bx, beam_z)) for bx in beam_posts]
                         + [r3(p) for p in landing_posts]}

    # -- quay ----------------------------------------------------------------------------------------
    def round_post(self, mesh, x, z, foot, top, radius, tar_to=None, sides=8):
        """An upright log. Below `tar_to` (just over the water) it is tarred."""
        if tar_to is not None and foot < tar_to < top:
            mesh.log((x, foot, z), (x, tar_to, z), radius * 1.02, TAR, sides=sides, rings=3)
            mesh.log((x, tar_to - 0.02, z), (x, top, z), radius, FRAME, sides=sides, rings=3)
        else:
            mesh.log((x, foot, z), (x, top, z), radius, TAR if tar_to is not None and top <= tar_to else FRAME, sides=sides, rings=3)

    def rope(self, mesh, a, b, sag, radius=0.018, parts=4):
        """A rope hanging between two points."""
        prev = a
        for i in range(1, parts + 1):
            t = i / float(parts)
            p = [a[k] + (b[k] - a[k]) * t for k in range(3)]
            p[1] -= sag * 4.0 * t * (1.0 - t)
            mesh.log(prev, p, radius, ROPE, sides=5, rings=2, wobble=0.0, v=(0.0, 1.0))
            prev = p

    def quay(self):
        """The plank quay along the dock shelf: deck on bearers and tarred piles, kerb, mooring posts, ladder,
        two lantern posts, a net rack, sacks' tarp. Returns the facts Sites places the goods from."""
        q, rng, g = self.parts["Quay"], self.rng2, self.g
        x0, x1 = QUAY_X
        z0, z1 = QUAY_Z
        qy = QUAY_DECK_WORLD_Y - g.floor_world_y
        water = WATER_WORLD_Y - g.floor_world_y
        under = qy - DECK
        X = (1.0, 0.0, 0.0)

        # deck boards across the quay; a few paler ones were replaced
        for xa, xb in q.rows(x0, x1, 0.15):
            q.box((xa + 0.003, under - rng.uniform(0.0, 0.004), z0 + rng.uniform(-0.03, 0.02)),
                  (xb - 0.003, qy - rng.uniform(0.0, 0.005), z1 + rng.uniform(-0.02, 0.04)), 2,
                  PALE if rng.random() < 0.06 else WOOD)
        # bearers along it: the first lies in the mud of the shelf, the others stand on piles
        lines = (z0 + 0.14, (z0 + z1) * 0.5, z1 - 0.3)
        for z in lines:
            for a, b in q.pieces(x0 + 0.05, x1 - 0.05, 3.2, 4.8):
                q.box((a + 0.01, under - 0.22, z - 0.1), (b - 0.01, under, z + 0.1), 0, FRAME)
        x = x0 + 0.5
        while x < x1 - 0.2:
            for z in lines[1:]:
                ground = g.raw(x, z)
                if under - 0.22 - ground > 0.25:
                    self.round_post(q, x, z, ground - 0.6, under - 0.2, 0.15, tar_to=water + 0.55)
            outer = g.raw(x, lines[2])
            if under - outer > 1.8:   # a brace from the outer pile's foot up to the inner one's head
                q.beam((x + 0.17, outer + 0.5, lines[2]), (x + 0.17, under - 0.3, lines[1]), 0.12, 0.05, FRAME, up=X)
            x += QUAY_PILE_STEP
        # kerb beam on the river edge, open at the ladder and at the gangplank
        a = x0
        spans = []
        for ga, gb in sorted(((QUAY_LADDER_X - 0.4, QUAY_LADDER_X + 0.4), (QUAY_PLANK_X - 0.4, QUAY_PLANK_X + 0.4))):
            spans.append((a, ga))
            a = gb
        spans.append((a, x1))
        for a, b in spans:
            for pa, pb in q.pieces(a, b, 3.0, 4.5):
                q.box((pa + 0.01, qy - 0.005, z1 - 0.17), (pb - 0.01, qy + 0.15, z1 - 0.01), 0, FRAME)
        # mooring posts outside the kerb, rope wound round them
        mz = z1 + 0.22
        for x in QUAY_MOORINGS:
            top = qy + 0.9 + rng.uniform(-0.05, 0.05)
            self.round_post(q, x, mz, g.raw(x, mz) - 0.7, top, 0.17, tar_to=water + 0.55, sides=10)
            for k in range(3):
                q.ring((x, qy + 0.42 + k * 0.075, mz), 0.2, 0.04, ROPE)
            self.collider("Mooring", "Quay", (x, qy + 0.2, mz), (0.36, 1.6, 0.36))
        # ladder down to the water
        lx, lz = QUAY_LADDER_X, z1 + 0.06
        for side in (-0.24, 0.24):
            q.box((lx + side - 0.035, water - 0.35, lz - 0.03), (lx + side + 0.035, qy + 0.5, lz + 0.04), 1, FRAME)
        y = water - 0.1
        while y < qy + 0.3:
            q.box((lx - 0.24, y, lz - 0.015), (lx + 0.24, y + 0.045, lz + 0.03), 0, FRAME)
            y += 0.33
        # lantern posts: a squared post, a braced arm over the deck, an iron hook
        for name, x in zip(("QuayLantern_West", "QuayLantern_East"), QUAY_LANTERNS):
            z = z1 - 0.33
            top = qy + 2.6
            arm = top - 0.12
            hz = z - 0.52
            q.box((x - 0.07, qy - 0.3, z - 0.07), (x + 0.07, top, z + 0.07), 1, FRAME)
            q.box((x - 0.045, arm - 0.045, z - 0.62), (x + 0.045, arm + 0.045, z + 0.1), 2, FRAME)
            q.beam((x, arm - 0.5, z - 0.07), (x, arm - 0.05, z - 0.45), 0.07, 0.07, FRAME, up=X)
            q.box((x - 0.008, arm - 0.12, hz - 0.008), (x + 0.008, arm - 0.045, hz + 0.008), 1, IRON, mode="iron")
            self.lanterns.append({"name": name, "position": [round(x, 3), round(arm - 0.12 - 0.27, 3), round(hz, 3)], "out": -1})
            self.collider("LanternPost", "Quay", (x, qy + 1.3, z), (0.16, 2.6, 0.16))
        # boards laid from the deck's edge onto the mud of the shelf, here and there
        x = x0 + rng.uniform(0.2, 0.8)
        while x < x1 - 0.8:
            for _ in range(rng.randrange(1, 4)):
                w = rng.uniform(0.17, MAX_BOARD)
                end = z0 - rng.uniform(0.5, 0.95)
                mid = x + w * 0.5
                q.beam((mid, qy - 0.02, z0 + 0.12), (mid + rng.uniform(-0.06, 0.06), g.raw(mid, end) + 0.035, end), w - 0.006, 0.04, WOOD)
                x += w
            x += rng.uniform(0.6, 2.2)
        # coils of rope on the boards
        for cx, cz in ((x0 + 2.1, z1 - 0.8), (QUAY_LANTERNS[1] + 0.35, z1 - 1.35)):
            for r in (0.14, 0.23, 0.32):
                q.ring((cx, qy + 0.035, cz), r, 0.04, ROPE)
            for r in (0.19, 0.28):
                q.ring((cx + 0.02, qy + 0.105, cz - 0.01), r, 0.04, ROPE)
        # net rack at the west end: two pairs of crossed poles, a bar, a net hung over it (under 2 m: the hunter's fire)
        nz = z1 - 0.45
        na, nb = x0 + 0.6, x0 + 2.5
        bar = qy + 1.72
        for x in (na, nb):
            for lean in (-0.3, 0.3):
                q.log((x, qy, nz + lean), (x, bar + 0.08, nz - lean * 0.1), 0.035, FRAME, sides=6, rings=2, wobble=0.0)
        q.log((na - 0.15, bar, nz), (nb + 0.15, bar, nz), 0.035, FRAME, sides=6, rings=3, wobble=0.004)
        for side in (-1, 1):
            for i in range(9):
                x = na + 0.14 + i * (nb - na - 0.28) / 8.0
                low = qy + (0.35 if side > 0 else 0.75) + rng.uniform(0.0, 0.12)
                q.beam((x, bar + 0.03, nz), (x + rng.uniform(-0.03, 0.03), low, nz + side * 0.16), 0.014, 0.014, ROPE, up=X)
            for k in range(4):
                t = (k + 1) / 5.0
                y = bar + 0.03 + (qy + (0.4 if side > 0 else 0.8) - bar) * t
                q.beam((na + 0.12, y, nz + side * 0.16 * t), (nb - 0.12, y, nz + side * 0.16 * t), 0.014, 0.014, ROPE)
        self.collider("NetRack", "Quay", ((na + nb) * 0.5, qy + 0.9, nz), (nb - na + 0.3, 1.8, 0.5))
        self.collider("NetRack", "NotWalkable", ((na + nb) * 0.5, qy + 0.9, nz), (nb - na + 0.6, 2.0, 0.9), into=self.not_walkable)
        # the tarp over the back sacks, its edges on the boards; two more sacks lie in front of it
        ta, tb, tc, td = QUAY_PLANK_X - 2.3, QUAY_PLANK_X - 0.5, z1 - 1.08, z1 - 0.2
        grid = []
        for r in range(6):
            v = r / 5.0
            row = []
            for c in range(7):
                u = c / 6.0
                lift = (math.sin(math.pi * u) ** 0.6) * (math.sin(math.pi * v) ** 0.6)
                row.append((ta + (tb - ta) * u + rng.uniform(-0.03, 0.03), qy + 0.03 + 0.4 * lift + rng.uniform(0.0, 0.035) * lift,
                            tc + (td - tc) * v + rng.uniform(-0.03, 0.03)))
            grid.append(row)
        q.sheet(grid, TARP)

        self.collider("Quay", "Walk", ((x0 + x1) * 0.5, qy - 0.15, (z0 + z1) * 0.5), (x1 - x0, 0.3, z1 - z0), into=self.walk)

        # goods, for Sites: kit piece, position in the prefab's frame, euler, scale, solid. The kit's crate has its
        # pivot at a bottom corner (it reaches -x and +z from it); casks and sacks stand on their middle.
        def r3(values):
            return [round(v, 3) for v in values]

        goods = []

        def put(kit, x, z, up=0.0, yaw=0.0, scale=1.0, solid=True, base=qy, lay=0.0):
            goods.append({"kit": kit, "position": r3((x, base + up, z)), "euler": r3((lay, yaw, 0.0)), "scale": scale, "solid": solid})

        crate, cask, sack, sack2 = "SM_Crate_B_01", "SM_WoodBarrel_01a", "SM_Sack_01", "SM_Sack_02"
        north = z1 - 0.3                              # goods keep to the river side
        # crates and sacks stand where the user moved them by hand at the second pause (prefab frame)
        for x, z, up, yaw, scale in ((7.40, 13.16, 0.0, 4.0, 1.1), (8.15, 13.21, 0.0, -6.0, 1.0), (8.90, 13.11, 0.0, 8.0, 1.1),
                                     (7.80, 13.21, 0.66, 15.0, 0.9), (8.41, 13.14, 0.62, -8.0, 0.85),
                                     (-3.05, 12.88, 0.0, -5.0, 1.1), (-2.30, 12.83, 0.0, 10.0, 1.0), (-2.75, 12.88, 0.66, 22.0, 0.9)):
            put(crate, x, z, up, yaw, scale, solid=up == 0.0)
        for x, z, yaw in ((2.05, -0.35, 10.0), (2.75, -0.3, 130.0), (2.4, -0.95, 250.0), (4.0, -0.35, 60.0), (4.65, -0.45, 200.0), (4.3, -1.05, 300.0)):
            put(cask, x, north + z, 0.0, yaw)
        for kit, x, z, yaw in ((sack, -1.54, 12.59, 20.0), (sack, -1.56, 13.00, 110.0), (sack2, -1.46, 12.11, 60.0), (sack, -1.89, 12.69, 150.0)):
            put(kit, x, z, 0.0, yaw, solid=False)
        put(cask, -4.6, north - 0.85, 0.34, 25.0, lay=90.0)   # one cask on its side
        return {"deckY": round(qy, 4), "deck": r3((x0, z0, x1, z1)), "lane": r3((x0, z0 - 1.2, x1, z0 + QUAY_LANE)), "goods": goods,
                "restricted": {"centre": r3(((x0 + x1) * 0.5, qy + 0.3, (z0 + BARGE[3] + 0.3) * 0.5)),
                               "size": r3((x1 - x0, 6.6, BARGE[3] + 0.3 - z0))}}

    def barge(self, quay):
        """A flat cargo barge tied alongside the quay, with a gangplank up to the deck."""
        b, rng, g = self.parts["Barge"], self.rng2, self.g
        x0, x1, z0, z1 = BARGE
        rail = BARGE_RAIL_WORLD_Y - g.floor_world_y
        floor = rail - 0.6
        keel = floor - 0.12
        qy = quay["deckY"]
        mid = (x0 + x1) * 0.5
        rake = 0.9

        def flare(c):   # the ends lean out as they rise
            return [c[0] + (rake if c[0] > mid else -rake) * (c[1] - keel), c[1], c[2]]

        for za, zb in b.rows(z0 + 0.05, z1 - 0.05, 0.16):   # bottom boards
            b.box((x0, keel, za + 0.002), (x1, floor - rng.uniform(0.0, 0.004), zb - 0.002), 0, FRAME)
        for side, z in ((-1, z0), (1, z1)):
            y = keel
            for k, h in enumerate((0.26, 0.24, 0.22)):          # three strakes, the lowest tarred
                zc = z + side * 0.03 * k
                b.box((x0 - 0.02, y, zc - 0.03), (x1 + 0.02, y + h - 0.006, zc + 0.03), 0, TAR if k == 0 else FRAME, skew=flare)
                y += h
            b.box((x0 - 0.04, rail - 0.02, z + side * 0.07 - 0.07), (x1 + 0.04, rail + 0.04, z + side * 0.07 + 0.07), 0, FRAME, skew=flare)
        for xe in (x0, x1):                                      # raked ends
            b.box((xe - 0.03, keel, z0 - 0.04), (xe + 0.03, rail + 0.02, z1 + 0.04), 2, FRAME, skew=flare)
        x = x0 + 0.55
        while x < x1 - 0.3:                                      # ribs
            b.box((x - 0.04, floor, z0 + 0.03), (x + 0.04, floor + 0.07, z1 - 0.03), 2, FRAME)
            for side, z in ((1, z0), (-1, z1)):
                b.box((x - 0.04, floor, z + side * 0.03), (x + 0.04, rail - 0.03, z + side * 0.1), 1, FRAME)
            x += 1.1
        for x in (x0 + 0.55, x1 - 0.6):                          # thwarts
            b.box((x - 0.13, rail - 0.1, z0 + 0.03), (x + 0.13, rail - 0.055, z1 - 0.03), 2, WOOD)
        # mooring lines to the quay's posts
        post_z, post_y = QUAY_Z[1] + 0.22, qy + 0.55
        self.rope(b, (x0 - 0.2, rail + 0.04, z0 + 0.05), (QUAY_MOORINGS[1], post_y, post_z + 0.15), 0.12)
        self.rope(b, (x1 + 0.35, rail + 0.04, z0 + 0.05), (QUAY_MOORINGS[3], post_y, post_z + 0.15), 0.25)
        # gangplank: two boards and cleats, from the kerb's gap down to a thwart
        top = (QUAY_PLANK_X, qy + 0.035, QUAY_Z[1] - 0.25)
        foot = (QUAY_PLANK_X + 2.3, rail + 0.03, z0 + 0.5)
        run = [foot[k] - top[k] for k in range(3)]
        length = math.sqrt(sum(c * c for c in run))
        along = norm(run)
        side = norm(cross((0.0, 1.0, 0.0), along))
        for off in (-0.135, 0.135):
            b.beam([top[k] + side[k] * off for k in range(3)], [foot[k] + side[k] * off for k in range(3)], 0.26, 0.045, WOOD)
        s = 0.3
        while s < length - 0.15:
            c = [top[k] + along[k] * s for k in range(3)]
            c[1] += 0.03
            b.beam([c[k] - side[k] * 0.27 for k in range(3)], [c[k] + side[k] * 0.27 for k in range(3)], 0.04, 0.02, FRAME)
            s += 0.42
        pitch = math.degrees(math.asin(-along[1]))
        yaw = math.degrees(math.atan2(along[0], along[2]))
        centre = [(top[k] + foot[k]) * 0.5 for k in range(3)]
        self.collider("Gangplank", "Barge", (centre[0], centre[1] - 0.04, centre[2]), (0.56, 0.08, length), (pitch, yaw, 0.0))
        # colliders: the hold's floor and the four sides
        self.collider("Barge_Floor", "Barge", (mid, floor - 0.15, (z0 + z1) * 0.5), (x1 - x0 + 1.0, 0.3, z1 - z0))
        for name, z in (("Barge_Side_Quay", z0), ("Barge_Side_River", z1)):
            self.collider(name, "Barge", (mid, (keel + rail) * 0.5, z), (x1 - x0 + 1.2, rail - keel + 0.1, 0.14))
        for name, xe, lean in (("Barge_Stern", x0, -1.0), ("Barge_Bow", x1, 1.0)):
            self.collider(name, "Barge", (xe + lean * rake * 0.36, (keel + rail) * 0.5, (z0 + z1) * 0.5), (0.14, rail - keel + 0.1, z1 - z0),
                          (0.0, 0.0, -lean * math.degrees(math.atan(rake))))
        # cargo still in the hold
        def r3(values):
            return [round(v, 3) for v in values]

        zc = (z0 + z1) * 0.5
        for kit, x, z, up, yaw, scale in (("SM_Crate_B_01", x0 + 2.1, zc - 0.5, 0.0, 8.0, 1.1), ("SM_Crate_B_01", x0 + 2.85, zc - 0.2, 0.0, -12.0, 1.0),
                                          ("SM_Crate_B_01", x0 + 2.5, zc - 0.4, 0.66, 20.0, 0.9), ("SM_WoodBarrel_01a", x1 - 1.3, zc + 0.45, 0.0, 40.0, 1.0),
                                          ("SM_WoodBarrel_01a", x1 - 0.7, zc - 0.3, 0.0, 170.0, 1.0), ("SM_Sack_01", x0 + 1.0, zc + 0.5, 0.0, 30.0, 1.0)):
            quay["goods"].append({"kit": kit, "position": r3((x, floor + 0.07 + up, z)), "euler": [0.0, yaw, 0.0], "scale": scale, "solid": False})
        quay["barge"] = r3((x0, z0, x1, z1))
        quay["gangplank"] = {"top": r3(top), "foot": r3(foot), "pitch": round(pitch, 2)}

    # -- tally room ----------------------------------------------------------------------------------
    def tally(self):
        """A lean-to in the palisade corner east of the hall: the palisade's logs are its south and east walls,
        the hall's gable its west wall (with the door), one plank wall with a shuttered hatch looks over the quay."""
        t, roof, fur, rng = self.parts["Tally"], self.parts["TallyRoof"], self.parts["TallyFurniture"], self.rng2
        x0, x1 = TALLY_X
        z0, z1 = TALLY_Z
        da, db, dh = TALLY_DOOR
        ja, jb = da - CELLAR_JAMB, db + CELLAR_JAMB
        head = dh + 0.18
        xi = HW - INNER                     # the gable boards' inner line
        X, Y, Z = (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)

        # the gable's boards put back beside and over the doorway
        for a, b, ya, yb, out in self.cut_boards:
            spans = [(a, min(b, ja), ya, yb), (max(a, jb), b, ya, yb)]
            if yb > head:
                spans.append((max(a, ja), min(b, jb), max(ya, head), yb))
            for pa, pb, y0, y1 in spans:
                if pb - pa > 0.03 and y1 - y0 > 0.02:
                    t.box((xi - (BOARD + out), y0, pa + 0.002), (xi - out, y1 - 0.003, pb - 0.002), 2, WOOD)
        # door frame through the wall's thickness, and the threshold boards
        fx0, fx1 = xi - BOARD - 0.02, HW - FACE + 0.02
        for za, zb in ((ja, da), (db, jb)):
            t.box((fx0, -0.02, za), (fx1, dh, zb), 1, FRAME)
        t.box((fx0 - 0.01, dh, ja - 0.1), (fx1 + 0.01, head, jb + 0.1), 2, FRAME)
        for za, zb in t.rows(da, db, 0.16):
            t.box((xi - BOARD - 0.01, -DECK, za + 0.002), (x0 + 0.04, -0.001, zb - 0.002), 0, WOOD)

        # floor: boards over three joists
        for xa, xb in t.rows(x0 + 0.03, x1, 0.16):
            t.box((xa + 0.002, -DECK, z0 - 0.05), (xb - 0.002, -rng.uniform(0.0, 0.004), z1 - 0.02), 2, WOOD)
        for z in (z0 + 0.45, (z0 + z1) * 0.5, z1 - 0.45):
            t.box((x0 + 0.02, -DECK - 0.16, z - 0.06), (x1, -DECK, z + 0.06), 0, FRAME)
        self.collider("Tally", "Walk", ((x0 + x1) * 0.5 + 0.05, -0.15, (z0 + z1) * 0.5 - 0.05), (x1 - x0 + 0.1, 0.3, z1 - z0 + 0.1), into=self.walk)

        # roof plane (top of the boards): from the palisade down to the plank wall and out over it
        tan = (TALLY_ROOF[0] - TALLY_ROOF[1]) / (z1 - (z0 + 0.1))
        pitch = math.atan(tan)
        cos, sin = math.cos(pitch), math.sin(pitch)

        def plane(z):
            return TALLY_ROOF[0] - (z - (z0 + 0.1)) * tan

        # plank wall: sill, two posts, plate, vertical boards cut round the hatch
        wz = z1 - 0.16                                   # the boards' line; posts and plate are 0.16 deep
        plate_top = plane(z1 - 0.08) - 0.025 - 0.14
        ha, hb, hs, hh = TALLY_HATCH
        t.box((x0 + 0.02, -0.16, wz), (x1, 0.0, z1), 0, FRAME)
        for px in (x0 + 0.1, x1 - 0.1):
            t.box((px - 0.08, 0.0, wz), (px + 0.08, plate_top - 0.12, z1), 1, FRAME)
        t.box((x0 + 0.02, plate_top - 0.12, wz), (x1, plate_top, z1), 0, FRAME)
        edges = [x0 + 0.18] + [b for _, b in t.rows(x0 + 0.18, x1 - 0.18, 0.16)]
        edges = sorted(set(edges) | {ha, hb})
        edges = [e for i, e in enumerate(edges) if i == 0 or e - edges[i - 1] > 0.012 or e in (ha, hb)]
        for xa, xb in zip(edges, edges[1:]):
            spans = [(0.0, plate_top - 0.12)]
            if xb > ha + 1e-3 and xa < hb - 1e-3:
                spans = [(0.0, hs), (hh, plate_top - 0.12)]
            for ya, yb in spans:
                out = rng.uniform(0.0, 0.006)
                t.box((xa + 0.002, ya, wz + 0.05 + out), (xb - 0.002, yb - rng.uniform(0.0, 0.01), wz + 0.05 + BOARD + out), 1, WOOD)
        for ya, yb in ((hs - 0.07, hs), (hh, hh + 0.07)):        # hatch frame
            t.box((ha - 0.07, ya, wz + 0.03), (hb + 0.07, yb, wz + 0.13), 0, FRAME)
        for xa, xb in ((ha - 0.07, ha), (hb, hb + 0.07)):
            t.box((xa, hs, wz + 0.03), (xb, hh, wz + 0.13), 1, FRAME)
        # the shutter: hinged on the head, propped open with a stick
        swing = math.radians(52.0)
        down = (0.0, -math.cos(swing), math.sin(swing))
        face = (0.0, math.sin(swing), math.cos(swing))
        hinge = (0.0, hh + 0.05, wz + 0.14)
        leaf = hh - hs + 0.04
        for xa, xb in t.rows(ha - 0.03, hb + 0.03, 0.16):
            o = [(xa + xb) * 0.5, hinge[1] + down[1] * leaf * 0.5, hinge[2] + down[2] * leaf * 0.5]
            t.framed(o, down, face, X, (leaf * 0.5, 0.014, (xb - xa) * 0.5 - 0.002), 0, WOOD)
        for s in (0.12, leaf - 0.12):
            o = [(ha + hb) * 0.5, hinge[1] + down[1] * s - face[1] * 0.03, hinge[2] + down[2] * s - face[2] * 0.03]
            t.framed(o, X, face, down, ((hb - ha) * 0.5, 0.014, 0.035), 0, FRAME)
        tip = (hb - 0.1, hinge[1] + down[1] * (leaf - 0.05), hinge[2] + down[2] * (leaf - 0.05))
        t.beam((hb - 0.1, hs + 0.01, wz + 0.12), tip, 0.03, 0.03, FRAME, up=X)
        self.collider("Tally_Wall", "Tally", ((x0 + x1) * 0.5, plate_top * 0.5, z1 - 0.1), (x1 - x0, plate_top, 0.2))
        if z1 > HD + 0.3:   # past the hall's corner the room needs a west wall of its own: vertical boards up to the roof
            t.box((x0 + 0.02, -0.16, HD - 0.02), (x0 + 0.18, 0.0, wz), 2, FRAME)
            for za, zb in t.rows(HD - 0.03, wz, 0.16):
                out = rng.uniform(0.0, 0.006)
                t.box((x0 + 0.05 + out, 0.0, za + 0.002), (x0 + 0.05 + BOARD + out, plane((za + zb) * 0.5) - 0.17, zb - 0.002), 1, WOOD)
            self.collider("Tally_Wall_West", "Tally", (x0 + 0.1, plate_top * 0.5 + 0.2, (HD + z1) * 0.5), (0.2, plate_top + 0.4, z1 - HD))

        # roof: a ledger on the palisade, rafters, boards, shingles
        slope = (z1 + TALLY_EAVE - (z0 + 0.1)) / cos
        peak = (0.0, TALLY_ROOF[0], z0 + 0.1)
        down = (0.0, -sin, cos)
        normal = (0.0, cos, sin)
        rx0, rx1 = HW - FACE + 0.03, x1 + 0.05          # 5 cm off the hoist's post, which stands just north of the hall's corner
        t.box((x0 + 0.02, plane(z0 + 0.25) - 0.33, z0 + 0.12), (x1, plane(z0 + 0.25) - 0.2, z0 + 0.26), 0, FRAME)
        for i in range(4):
            x = x0 + 0.25 + i * (x1 - x0 - 0.45) / 3.0
            o = [peak[k] + down[k] * (slope * 0.5) - normal[k] * 0.095 + X[k] * x for k in range(3)]
            t.framed(o, down, normal, X, (slope * 0.5 - 0.04, 0.07, 0.05), 0, FRAME)
        for xa, xb in roof.rows(rx0, rx1, 0.17):
            o = [peak[k] + down[k] * slope * 0.5 - normal[k] * 0.0125 + X[k] * (xa + xb) * 0.5 for k in range(3)]
            roof.framed(o, down, normal, X, (slope * 0.5, 0.0125, (xb - xa) * 0.5 - 0.002), 0, WOOD, skip=((1, 1),))
        exposure, shingle_len, thick = 0.31, 0.5, 0.022
        tilt = math.atan2(thick, exposure)
        ax = norm([down[k] * math.cos(tilt) + normal[k] * math.sin(tilt) for k in range(3)])
        ay = norm([normal[k] * math.cos(tilt) - down[k] * math.sin(tilt) for k in range(3)])
        courses = int((slope + 0.03) / exposure) + 1
        for c in range(courses):
            butt = slope + 0.03 - c * exposure
            if butt < 0.12:
                break
            x = rx0 + rng.uniform(0.0, 0.05)
            while x < rx1:
                w = min(rng.uniform(0.15, MAX_BOARD), rx1 - x)
                if w < 0.05:
                    break
                roll = rng.random()
                if roll > 0.01:
                    rag = rng.uniform(-0.03, 0.035) + (rng.uniform(0.03, 0.07) if roll < 0.04 else 0.0)
                    ln = min(shingle_len, butt + rag - 0.02)
                    mid = butt + rag - ln * 0.5
                    o = [peak[k] + down[k] * mid + normal[k] * (0.004 + rng.uniform(0.0, 0.006) + (ln * 0.5) * math.sin(tilt)) + ay[k] * thick * 0.5
                         + X[k] * (x + w * 0.5) for k in range(3)]
                    skew = rng.uniform(-0.012, 0.012)
                    ends = [(skew if (i >> 2) & 1 else -skew) if i & 1 else 0.0 for i in range(8)]
                    roof.framed(o, ax, ay, X, (ln * 0.5, thick * 0.5, (w - rng.uniform(0.003, 0.009)) * 0.5), 0,
                                PALE if rng.random() < 0.035 else SHINGLE, ends, skip=((1, 0), (0, 0)))
                x += w
        # a cover strip where the roof meets the hall's wall
        run = (HD - (z0 + 0.1)) / cos
        o = [peak[k] + down[k] * run * 0.5 + normal[k] * 0.03 + X[k] * (rx0 - 0.01) for k in range(3)]
        roof.framed(o, down, normal, X, (run * 0.5, 0.02, 0.05), 0, FRAME)
        mz = (z0 + 0.1 + z1 + TALLY_EAVE) * 0.5
        self.collider("Tally_Roof", "Tally", ((rx0 + rx1) * 0.5, plane(mz) - 0.05, mz), (rx1 - rx0, 0.16, slope), (math.degrees(pitch), 0.0, 0.0))

        # furniture: desk and shelves on the dock spur's logs, a stool, a strongbox by the palisade
        dx0, dx1, dz0, dz1, dy = x1 - 0.82, x1 - 0.22, 2.5, 3.7, 0.75
        for za, zb in fur.rows(dz0, dz1, 0.17):
            fur.box((dx0, dy - 0.035, za + 0.002), (dx1, dy, zb - 0.002), 0, WOOD)
        for lx in (dx0 + 0.06, dx1 - 0.06):
            for lz in (dz0 + 0.07, dz1 - 0.07):
                fur.box((lx - 0.035, 0.0, lz - 0.035), (lx + 0.035, dy - 0.035, lz + 0.035), 1, FRAME)
        fur.box((dx0 + 0.03, dy - 0.13, dz0 + 0.07), (dx0 + 0.055, dy - 0.035, dz1 - 0.07), 2, FRAME)
        fur.box((dx1 - 0.055, 0.14, dz0 + 0.07), (dx1 - 0.03, 0.2, dz1 - 0.07), 2, FRAME)
        fur.box((dx0 + 0.12, dy, 2.85), (dx0 + 0.42, dy + 0.012, 3.07), 0, PALE)      # the open ledger: two pages
        fur.box((dx0 + 0.12, dy, 3.08), (dx0 + 0.42, dy + 0.014, 3.3), 0, PALE)
        fur.box((dx0 + 0.1, dy, 2.84), (dx0 + 0.44, dy + 0.006, 3.31), 0, TAR)
        fur.log((dx0 + 0.3, dy, 3.52), (dx0 + 0.3, dy + 0.07, 3.52), 0.022, PALE, sides=6, rings=2, wobble=0.0)   # a candle stub in its dish
        fur.log((dx0 + 0.3, dy, 3.52), (dx0 + 0.3, dy + 0.012, 3.52), 0.06, IRON, sides=8, rings=2, wobble=0.0)
        self.collider("Desk", "Tally", ((dx0 + dx1) * 0.5, dy * 0.5, (dz0 + dz1) * 0.5), (dx1 - dx0, dy, dz1 - dz0))
        for sy in (1.45, 1.95):                                                          # shelves on pegs
            fur.box((x1 - 0.3, sy - 0.03, dz0 - 0.05), (x1 - 0.04, sy, dz1 + 0.05), 2, WOOD)
            for pz in (dz0 + 0.15, dz1 - 0.15):
                fur.beam((x1 - 0.28, sy - 0.05, pz), (x1 + 0.05, sy - 0.05, pz), 0.03, 0.03, FRAME)
        bz = dz0 + 0.05
        for i in range(5):                                                               # ledgers standing on the lower shelf
            w = rng.uniform(0.045, 0.075)
            h = rng.uniform(0.24, 0.3)
            lean = rng.uniform(-0.02, 0.02) if i < 4 else 0.07
            fur.box((x1 - 0.27, 1.45, bz), (x1 - 0.07, 1.45 + h, bz + w), 1, TAR if i % 2 else FRAME,
                    skew=lambda c, lean=lean: [c[0], c[1], c[2] + (lean if c[1] > 1.46 else 0.0)])
            bz += w + rng.uniform(0.004, 0.012)
        sx, sz = dx0 - 0.42, 3.1                                                         # three-legged stool
        fur.log((sx, 0.43, sz), (sx, 0.47, sz), 0.17, WOOD, sides=10, rings=2, wobble=0.0)
        for k in range(3):
            a = 2.0 * math.pi * k / 3.0 + 0.4
            fur.log((sx + 0.15 * math.cos(a), 0.0, sz + 0.15 * math.sin(a)), (sx + 0.08 * math.cos(a), 0.43, sz + 0.08 * math.sin(a)), 0.022, FRAME,
                    sides=6, rings=2, wobble=0.0)
        self.collider("Stool", "Tally", (sx, 0.235, sz), (0.3, 0.47, 0.3))
        cx0, cx1, cz0, cz1, ch = x0 + 0.9, x0 + 1.6, z0 + 0.3, z0 + 0.72, 0.42            # strongbox: dark oak, iron straps
        fur.box((cx0, 0.0, cz0), (cx1, ch - 0.1, cz1), 0, FRAME)
        fur.box((cx0 - 0.012, ch - 0.1, cz0 - 0.012), (cx1 + 0.012, ch, cz1 + 0.012), 0, FRAME)
        for bx in (cx0 + 0.1, (cx0 + cx1) * 0.5, cx1 - 0.1):
            fur.box((bx - 0.025, 0.0, cz0 - 0.018), (bx + 0.025, ch + 0.006, cz1 + 0.018), 1, IRON, mode="iron", skip=((1, 0),))
        fur.box(((cx0 + cx1) * 0.5 - 0.04, ch - 0.19, cz1 + 0.012), ((cx0 + cx1) * 0.5 + 0.04, ch - 0.09, cz1 + 0.03), 1, IRON, mode="iron")
        self.collider("Strongbox", "Tally", ((cx0 + cx1) * 0.5, ch * 0.5, (cz0 + cz1) * 0.5), (cx1 - cx0, ch, cz1 - cz0))

        def r3(values):
            return [round(v, 3) for v in values]

        return {"inside": r3((x0, z0, x1, z1)), "wallTop": round(plate_top, 3),
                "door": {"position": r3((HW - FACE - POST * 0.5, 0.0, da)), "yaw": 270.0, "scaleY": TALLY_DOOR_SCALE_Y, "openAngle": 90.0,
                         "opening": r3((da, db, dh))},
                "hatch": r3(TALLY_HATCH),
                "restricted": {"centre": r3(((x0 + x1) * 0.5, 1.3, (z0 + z1) * 0.5)), "size": r3((x1 - x0 - 0.1, 2.9, z1 - z0 - 0.1))}}

    # -- retaining wall and the cellar's side room ---------------------------------------------------
    def retaining(self):
        """One tall face of dry-stone rubble holding the bank under the tally room, from the cellar's shoulder to
        the dock spur; it is also the side room's north wall."""
        ret, rng, g = self.parts["Retaining"], self.rng2, self.g
        rx0, rx1 = RETAIN_X
        rz = RETAIN_Z
        top = -0.06
        low = min(g.raw(rx0 + i * 0.25, rz + 0.1) for i in range(int((rx1 - rx0) / 0.25) + 1)) - 0.25
        self.core(rx0 - 0.1, rx1 + 0.1, low, top, SIDE_Z[1], rz, into=ret)                 # the wall's body
        self.core(rx0 - 0.1, rx1 + 0.1, SIDE_TOP + 0.05, top - 0.2, HD - 0.2, SIDE_Z[1] + 0.2, into=ret)   # ... and over the side room, under the floor's joists
        self.face((rx0, rz), (rx1, rz), low, top, (0.0, 1.0), bottom_of=lambda s: g.raw(rx0 + s, rz + 0.1), walls=ret, rng=rng)
        self.face((rx0, rz), (rx0, HD), self.shoulder_top(rz) - 0.3, top, (-1.0, 0.0),      # its west end, over the shoulder's steps
                  bottom_of=lambda s: self.shoulder_top(rz - s), walls=ret, rng=rng)
        for _ in range(7):                                                                   # stones fallen at its foot
            sx, sz = rng.uniform(rx0 + 0.3, rx1 - 0.4), rz + rng.uniform(0.1, 0.4)
            size = rng.uniform(0.2, 0.36)
            ret.stone((sx, g.raw(sx, sz) + size * 0.3, sz), (size * 1.4, size * 0.8, size))
        self.collider("Retaining", "Retaining", ((rx0 + rx1) * 0.5, (low + top) * 0.5, (SIDE_Z[1] + rz) * 0.5), (rx1 - rx0, top - low, rz - SIDE_Z[1]))
        self.collider("Retaining_Top", "Retaining", ((rx0 + rx1) * 0.5, (SIDE_TOP + top) * 0.5, (HD + SIDE_Z[1]) * 0.5), (rx1 - rx0, top - SIDE_TOP, SIDE_Z[1] - HD))
        # its top carries the tally room's floor and plank wall: no ledge is left outside
        return {"face": round(rz, 3), "x": [round(rx0, 3), round(rx1, 3)], "top": top}

    def side_room(self, cellar):
        """A side room of the cellar under the tally room, behind the retaining wall, reached through the cellar's
        east wall. The terrain has a second hole over it (`cellar.sideHole`)."""
        cs, rng, g = self.parts["CellarSide"], self.rng2, self.g
        x1 = CELLAR_X[1]
        cz0, cz1 = CELLAR_Z
        sx0, sx1 = SIDE_X
        sz0, sz1 = SIDE_Z
        pa, pb, ph = SIDE_PASSAGE
        fy, top = g.cellar_floor, CELLAR_TOP
        head = fy + ph
        lintel = head + 0.2
        mid_x = x1 + 0.6                           # where the cellar's own east wall ends
        deep = -SILL - 0.03                        # under the hall's sill

        # floors: packed earth, as the cellar's
        cs.box((sx0 - 0.1, fy - 0.3, sz0 - 0.1), (sx1 + 0.1, fy, sz1 + 0.1), 0, EARTH, skip=((1, 0),), mode="stone")
        cs.box((x1 + 0.05, fy - 0.3, pa - 0.1), (sx0 + 0.05, fy + 0.002, pb + 0.1), 0, EARTH, skip=((1, 0),), mode="stone")
        for fx, fz in ((5.4, 2.85), (6.9, 2.6), (7.5, 1.7)):
            cs.stone((fx, fy - 0.015, fz), (rng.uniform(0.4, 0.6), 0.07, rng.uniform(0.32, 0.48)))

        # the cellar's east wall, rebuilt round the passage, and the wall between the two rooms
        self.core(x1, mid_x + 0.1, fy - 0.3, top, cz0 - 0.3, pa, into=cs)
        self.core(x1, mid_x + 0.1, fy - 0.3, top, pb, HD, into=cs)
        self.core(x1, mid_x + 0.1, lintel, top, pa - 0.16, pb + 0.16, into=cs)
        self.core(mid_x - 0.1, sx0, fy - 0.3, deep, 0.3, pa, into=cs)
        self.core(mid_x - 0.1, sx0, fy - 0.3, deep, pb, sz1 + 0.3, into=cs)
        self.core(mid_x - 0.1, sx0, lintel, deep, pa - 0.16, pb + 0.16, into=cs)
        # The south wall is pierced by the tunnel's opening and is drawn into `CellarTunnel` further down. What it
        # was before is still drawn here, into a mesh nobody keeps, so this stream stays as it was for every later part.
        ghost = Mesh(self.rng2)
        self.core(mid_x - 0.1, sx1 + 0.55, fy - 0.3, SIDE_TOP + 0.3, 0.3, sz0, into=ghost)    # south
        self.core(sx1, sx1 + 0.55, fy - 0.3, SIDE_TOP + 0.3, 0.3, RETAIN_Z - 0.2, into=cs)    # east

        room = dict(walls=cs, rng=rng, room=True)
        self.face((sx0, sz0), (sx1, sz0), fy - 0.05, SIDE_TOP, (0.0, 1.0), walls=ghost, rng=rng, room=True)   # south (see above)
        self.face((sx1, sz1), (sx1, sz0), fy - 0.05, SIDE_TOP, (-1.0, 0.0), **room)                     # east
        self.face((sx0, sz1), (sx1, sz1), fy - 0.05, SIDE_TOP, (0.0, -1.0), **room)                     # north: the retaining wall's back
        self.face((sx0, sz0), (sx0, sz1), fy - 0.05, SIDE_TOP, (1.0, 0.0), ((pa - sz0, pb - sz0, fy - 1.0, lintel),), **room)   # west
        self.face((x1, pa), (sx0, pa), fy - 0.05, head, (0.0, 1.0), **room)                             # the passage's sides
        self.face((sx0, pb), (x1, pb), fy - 0.05, head, (0.0, -1.0), **room)
        # the stones the cellar's east face lost round the opening
        if self.burnt:
            smin, smax, ymax = self.burnt
            za, zb = cz1 - smin, cz1 - smax            # the burnt stretch, north end and south end
            if za - pb > 0.05:
                self.face((x1, za), (x1, pb), fy - 0.05, lintel, (-1.0, 0.0), **room)
            if pa - zb > 0.05:
                self.face((x1, pa), (x1, zb), fy - 0.05, lintel, (-1.0, 0.0), **room)
            if ymax - lintel > 0.08:
                self.face((x1, za), (x1, zb), lintel, ymax, (-1.0, 0.0), **room)
        # lintel: timbers side by side through the wall
        lx = x1 - 0.06
        while lx < sx0 + 0.02:
            w = min(rng.uniform(0.26, 0.34), sx0 + 0.06 - lx)
            cs.box((lx + 0.004, head, pa - 0.28), (lx + w - 0.004, lintel + rng.uniform(-0.015, 0.01), pb + 0.28), 2, FRAME)
            lx += w
        # ceiling: boards on three joists, under the rock
        for za, zb in cs.rows(sz0 - 0.08, sz1 + 0.08, 0.16):
            cs.box((sx0 - 0.08, SIDE_TOP, za), (sx1 + 0.08, SIDE_TOP + 0.024, zb), 0, WOOD, skip=((1, 1),))
        joists = (sz0 + 0.55, (sz0 + sz1) * 0.5, sz1 - 0.55)
        for jz in joists:
            cs.box((sx0 - 0.08, SIDE_TOP - 0.16, jz - 0.06), (sx1 + 0.08, SIDE_TOP, jz + 0.06), 0, FRAME)
        # a lantern on the north joist: no window here either
        hx, hz = (sx0 + sx1) * 0.5 + 0.3, joists[2]
        hook = SIDE_TOP - 0.16
        cs.box((hx - 0.008, hook - 0.07, hz - 0.008), (hx + 0.008, hook, hz + 0.008), 1, IRON, mode="iron")
        self.lanterns.append({"name": "CellarLantern_Side", "position": [round(hx, 3), round(hook - 0.07 - 0.27, 3), round(hz, 3)],
                              "out": -1, "always": True})

        def wall(name, xa, xb, za, zb, ya=None, yb=None):
            ya, yb = fy - 0.3 if ya is None else ya, top if yb is None else yb
            self.collider(name, "Cellar", ((xa + xb) * 0.5, (ya + yb) * 0.5, (za + zb) * 0.5), (xb - xa, yb - ya, zb - za))

        # the south wall round the tunnel's opening: stones, a core either side and over it, timber jambs and lintel
        ct, rng3 = self.parts["CellarTunnel"], self.rng3
        ta, tb, th = SIDE_TUNNEL
        thead = fy + th
        tlintel = thead + 0.2
        ct.box((mid_x - 0.1 + 0.08, fy - 0.3, 0.38), (ta - 0.02, SIDE_TOP + 0.28, sz0 - 0.08), 0, RUBBLE, skip=((1, 0),), mode="stone")
        ct.box((tb + 0.02, fy - 0.3, 0.38), (sx1 + 0.55 - 0.08, SIDE_TOP + 0.28, sz0 - 0.08), 0, RUBBLE, skip=((1, 0),), mode="stone")
        ct.box((ta - 0.02, tlintel, 0.38), (tb + 0.02, SIDE_TOP + 0.28, sz0 - 0.08), 0, RUBBLE, skip=((1, 0),), mode="stone")
        self.face((sx0, sz0), (sx1, sz0), fy - 0.05, SIDE_TOP, (0.0, 1.0), ((ta - sx0, tb - sx0, fy - 1.0, tlintel),),
                  walls=ct, rng=rng3, room=True)
        for x in (ta - 0.02, tb - 0.1):
            ct.box((x, fy - 0.02, 0.3), (x + 0.12, thead, sz0 + 0.03), 1, FRAME)
        lx = ta - 0.25
        ct.box((lx, thead, 0.3), (tb + 0.25, tlintel + rng3.uniform(-0.01, 0.01), sz0 + 0.06), 0, FRAME)
        ct.box((ta - 0.05, fy - 0.3, 0.25), (tb + 0.05, fy + 0.002, sz0 + 0.1), 0, EARTH, skip=((1, 0),), mode="stone")
        self.collider("CellarTunnelSill", "Walk", ((ta + tb) * 0.5, fy - 0.15, (0.2 + sz0 + 0.1) * 0.5), (tb - ta + 0.2, 0.3, sz0 + 0.1 - 0.2), into=self.walk)
        wall("Side_West", mid_x, sx0, 0.3, pa, None, deep)
        wall("Side_West_North", mid_x, sx0, pb, sz1, None, deep)
        wall("Side_West_Header", mid_x, sx0, pa, pb, head, deep)
        wall("Side_South", mid_x, ta, 0.3, sz0, None, SIDE_TOP + 0.3)
        wall("Side_South_East", tb, sx1 + 0.55, 0.3, sz0, None, SIDE_TOP + 0.3)
        wall("Side_South_Header", ta, tb, 0.3, sz0, thead, SIDE_TOP + 0.3)
        wall("Side_East", sx1, sx1 + 0.55, 0.3, sz1 + 0.3, None, SIDE_TOP + 0.3)
        wall("Side_Ceiling", sx0 - 0.1, sx1 + 0.1, sz0 - 0.1, sz1 + 0.1, SIDE_TOP, SIDE_TOP + 0.25)
        self.collider("CellarSide", "Walk", ((sx0 + sx1) * 0.5, fy - 0.15, (sz0 + sz1) * 0.5), (sx1 - sx0 + 0.2, 0.3, sz1 - sz0 + 0.2), into=self.walk)
        self.collider("CellarPassage", "Walk", ((x1 + sx0) * 0.5, fy - 0.15, (pa + pb) * 0.5), (sx0 - x1 + 0.2, 0.3, pb - pa + 0.2), into=self.walk)

        def r3(values):
            return [round(v, 3) for v in values]

        height = SIDE_TOP - fy
        cellar["sideHole"] = r3(SIDE_HOLE)
        cellar["side"] = {"inside": r3((sx0, sz0, sx1, sz1)), "top": SIDE_TOP, "passage": r3((x1, pa, sx0, pb)), "passageHeight": ph}
        # the tunnel's opening: min x, max x, height, then the wall's outer and inner faces (z)
        cellar["tunnel"] = r3((ta, tb, th, 0.3, sz0))
        cellar["restrictedMore"] = [
            {"name": "SideRoom", "centre": r3(((sx0 + sx1) * 0.5, fy + height * 0.5, (sz0 + sz1) * 0.5)), "size": r3((sx1 - sx0 - 0.1, height, sz1 - sz0 - 0.1))},
            {"name": "Passage", "centre": r3(((x1 + sx0) * 0.5, fy + ph * 0.5, (pa + pb) * 0.5)), "size": r3((sx0 - x1 + 0.2, ph, pb - pa - 0.1))}]

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
        cellar = self.cellar()
        self.side_room(cellar)
        tally = self.tally()
        retaining = self.retaining()
        quay = self.quay()
        self.barge(quay)
        return {"campRamp": [round(c, 3) for c in camp], "riverRamp": [round(c, 3) for c in river],
                "hoistDrop": [round(drop[0], 3), round(drop[1], 3)], "cellar": cellar, "tally": tally,
                "retaining": retaining, "quay": quay}


def materials():
    out = []
    assets = _find_up(os.path.dirname(os.path.abspath(__file__)), "Assets")
    wood = os.path.join(assets, "Hivemind", "HDRP(Default)", "MedievalFantasyVillage", "Art", "Textures", "T_ModularWood_Basecolor.png") if assets else ""
    stone = os.path.join(assets, "Hivemind", "HDRP", "TheMessengerMountain", "Art", "Terrain", "T_RockTile_BaseColor.PNG") if assets else ""
    iron = os.path.join(assets, "Hivemind", "CastleOfEternalMists", "HDRP(Default)", "Art", "Textures", "T_Iron_BaseColor.PNG") if assets else ""
    earth = os.path.join(assets, "Hivemind", "HDRP(Default)", "MedievalFantasyVillage", "Art", "Landscape", "LanscapeTextures", "T_mud_BaseColor02.png") if assets else ""
    look = {"Wh_Wood": (wood, (1.0, 1.0, 1.0, 1.0)), "Wh_Frame": (wood, (0.72, 0.72, 0.72, 1.0)),
            "Wh_Shingle": (wood, (0.62, 0.68, 0.76, 1.0)), "Wh_ShinglePale": (wood, (1.25, 1.2, 1.05, 1.0)),
            "Wh_Rubble": (stone, (1.1, 1.1, 1.1, 1.0)), "Wh_Iron": (iron, (0.6, 0.6, 0.6, 1.0)),
            "Wh_Rope": ("", (0.45, 0.38, 0.27, 1.0)), "Wh_Tar": (wood, (0.3, 0.29, 0.28, 1.0)),
            "Wh_Earth": (earth, (0.8, 0.78, 0.74, 1.0)), "Wh_Tarp": ("", (0.36, 0.37, 0.3, 1.0))}
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
